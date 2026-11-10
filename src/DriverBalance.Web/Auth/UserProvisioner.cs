using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

/// <summary>What Google told us about the person who just signed in.</summary>
public sealed record GoogleIdentity(string Subject, string? Email, bool EmailVerified, string? Name, string? PictureUrl);

public enum SignInStatus { Ok, InviteOnly, Blocked, EmailNotVerified }

public sealed record SignInResult(SignInStatus Status, long UserId = 0, bool IsAdmin = false);

public sealed record SessionCheck(bool Valid, bool IsAdmin);

/// <summary>
/// Turns a Google sign-in into a profile, or refuses it. Invite-only: someone who is not allowed leaves nothing in the database.
/// The admin email is always allowed; the first admin sign-in takes over user 1 (the owner's data) instead of creating a new user.
/// Works outside any request user, so it uses a context with no current user (users and invites have no per-user filter).
/// </summary>
public sealed class UserProvisioner(DbContextOptions<DriverBalanceDbContext> options, IOptions<HostingOptions> hosting, TimeProvider clock)
{
    private sealed class NoUser : ICurrentUser
    {
        public long UserId => throw new InvalidOperationException("No current user outside a request.");
    }

    private HostingOptions Hosting => hosting.Value;

    private DriverBalanceDbContext Open() => new(options, new NoUser());

    public async Task<SignInResult> SignInAsync(GoogleIdentity g, CancellationToken ct = default)
    {
        var email = HostingOptions.Normalise(g.Email);
        if (string.IsNullOrEmpty(g.Subject) || email is null || !g.EmailVerified) return new SignInResult(SignInStatus.EmailNotVerified);
        var isAdmin = Hosting.IsAdminEmail(email);
        var now = clock.GetUtcNow().UtcDateTime;

        for (var attempt = 0; ; attempt++)
        {
            await using var db = Open();
            var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == g.Subject, ct);
            if (user is not null)
            {
                if (user.Blocked && !isAdmin) return new SignInResult(SignInStatus.Blocked);
                Fill(user, g, email, now);
                await db.SaveChangesAsync(ct);
                return new SignInResult(SignInStatus.Ok, user.Id, isAdmin);
            }

            InviteRow? invite = null;
            if (!isAdmin)
            {
                invite = await db.Invites.FirstOrDefaultAsync(i => i.Email == email, ct);
                if (invite is null || invite.UsedByUserId is not null) return new SignInResult(SignInStatus.InviteOnly);
            }

            if (isAdmin)
            {
                // The owner's existing data lives on user 1: link it to the owner's Google account and take the name from there.
                user = await db.Users.FirstOrDefaultAsync(u => u.Id == LocalUserHandler.LocalUserId && u.GoogleSubject == null, ct);
                if (user is not null) user.Name = "";
            }
            if (user is null)
            {
                user = new UserRow { CreatedAt = now };
                db.Users.Add(user);
            }
            Fill(user, g, email, now);
            user.Blocked = false;

            try
            {
                await db.SaveChangesAsync(ct);
                if (invite is not null)
                {
                    invite.UsedByUserId = user.Id;
                    invite.UsedAt = now;
                    await db.SaveChangesAsync(ct);
                }
                return new SignInResult(SignInStatus.Ok, user.Id, isAdmin);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Two first sign-ins of the same account at once: the second finds the first one's row.
            }
        }
    }

    private static void Fill(UserRow user, GoogleIdentity g, string email, DateTime now)
    {
        user.GoogleSubject = g.Subject;
        user.Email = email;
        user.DisplayName = g.Name?.Trim() is { Length: > 0 } name ? name : null;
        user.PictureUrl = string.IsNullOrWhiteSpace(g.PictureUrl) ? null : g.PictureUrl.Trim();
        user.LastSignInAt = now;
        if (string.IsNullOrWhiteSpace(user.Name) || user.Name == "Local user") user.Name = user.DisplayName ?? email;
    }

    /// <summary>Re-checked while a session is alive: the user must still exist and not be blocked. Admin status follows the configured email.</summary>
    public async Task<SessionCheck> ValidateAsync(long userId, CancellationToken ct = default)
    {
        await using var db = Open();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return new SessionCheck(false, false);
        var isAdmin = Hosting.IsAdminEmail(user.Email);
        return new SessionCheck(!user.Blocked || isAdmin, isAdmin);
    }
}
