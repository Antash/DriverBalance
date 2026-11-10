using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

public sealed record InviteRequest(string? Email);

public sealed record InviteInfo(string Email, DateTime CreatedAt, DateTime? UsedAt, long? UsedByUserId);

public sealed record AdminUserInfo(long Id, string Name, string? Email, DateTime CreatedAt, DateTime? LastSignInAt, int VehicleCount, bool IsAdmin, bool Blocked);

/// <summary>Invites and users. Admin only (claim set at Google sign-in from the configured admin email); API tokens are never admin.</summary>
public static class AdminEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var a = api.MapGroup("/admin").RequireAuthorization(HostedAuth.AdminPolicy);
        a.MapGet("/invites", Invites);
        a.MapPost("/invites", AddInvite);
        a.MapDelete("/invites/{email}", RemoveInvite);
        a.MapGet("/users", Users);
        a.MapDelete("/users/{id:long}", DeleteUser);
    }

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private static async Task<IResult> Invites(DriverBalanceDbContext db, CancellationToken ct)
    {
        var rows = await db.Invites.AsNoTracking().OrderBy(i => i.Email).ToListAsync(ct);
        return Results.Ok(rows.Select(i => new InviteInfo(i.Email, Utc(i.CreatedAt), i.UsedAt is { } u ? Utc(u) : null, i.UsedByUserId)).ToList());
    }

    private static async Task<IResult> AddInvite(InviteRequest body, DriverBalanceDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var email = HostingOptions.Normalise(body.Email);
        if (email is null || email.Length > 254 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@') || email.Any(char.IsWhiteSpace))
            throw new ApiValidationException("Enter a valid email address.");
        if (await db.Invites.AnyAsync(i => i.Email == email, ct)) throw new ApiValidationException("That email is already invited.");
        var row = new InviteRow { Email = email, CreatedAt = clock.GetUtcNow().UtcDateTime };
        db.Invites.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/invites/{Uri.EscapeDataString(email)}", new InviteInfo(row.Email, Utc(row.CreatedAt), null, null));
    }

    /// <summary>An unused invite is deleted. A used one is kept and its user is blocked: signed out, cannot sign in again, data stays.</summary>
    private static async Task<IResult> RemoveInvite(string email, DriverBalanceDbContext db, IOptions<HostingOptions> h, CancellationToken ct)
    {
        var e = HostingOptions.Normalise(email);
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Email == e, ct);
        if (invite is null) return Results.NotFound();
        if (invite.UsedByUserId is { } userId)
        {
            if (h.Value.IsAdminEmail(e)) throw new ApiValidationException("The admin cannot be blocked.");
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Blocked, true), ct);
            return Results.NoContent();
        }
        db.Invites.Remove(invite);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Users(DriverBalanceDbContext db, IOptions<HostingOptions> h, CancellationToken ct)
    {
        var counts = await db.Vehicles.IgnoreQueryFilters().GroupBy(v => v.UserId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var rows = await db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(u => new AdminUserInfo(u.Id, u.DisplayName ?? u.Name, u.Email, Utc(u.CreatedAt),
            u.LastSignInAt is { } s ? Utc(s) : null, counts.GetValueOrDefault(u.Id), h.Value.IsAdminEmail(u.Email), u.Blocked)).ToList());
    }

    private static async Task<IResult> DeleteUser(long id, ICurrentUser current, DriverBalanceDbContext db, DocumentStore documents,
        IOptions<HostingOptions> h, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        if (id == current.UserId || h.Value.IsAdminEmail(user.Email)) throw new ApiValidationException("The admin account cannot be deleted.");
        await UserDeletion.DeleteAsync(db, documents, id, ct);
        return Results.NoContent();
    }
}
