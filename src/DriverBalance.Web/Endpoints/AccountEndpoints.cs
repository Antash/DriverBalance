using DriverBalance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

public sealed record MeInfo(long Id, string Name, string? Email, string? PictureUrl, bool IsAdmin, string Mode);

public sealed record DeleteAccountRequest(string? Confirm);

/// <summary>Who is signed in, sign-out, and "delete my account".</summary>
public static class AccountEndpoints
{
    public const string DeleteWord = "DELETE";

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me", Me);
        api.MapPost("/logout", Logout);
        api.MapDelete("/me", DeleteMe);
    }

    private static async Task<IResult> Me(HttpContext ctx, ICurrentUser current, DriverBalanceDbContext db, IOptions<HostingOptions> h, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.UserId, ct);
        if (user is null) return Results.Unauthorized();
        var isAdmin = ctx.User.HasClaim(HostedAuth.AdminClaim, "true");
        return Results.Ok(new MeInfo(user.Id, user.DisplayName ?? user.Name, user.Email, user.PictureUrl, isAdmin, h.Value.Mode.ToString()));
    }

    private static async Task<IResult> Logout(HttpContext ctx, IOptions<HostingOptions> h)
    {
        if (h.Value.IsHosted) await ctx.SignOutAsync(HostedAuth.Cookie);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteMe([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest body, HttpContext ctx, ICurrentUser current, DriverBalanceDbContext db,
        DocumentStore documents, IOptions<HostingOptions> h, CancellationToken ct)
    {
        if (!h.Value.IsHosted) throw new ApiValidationException("There is no account to delete in local mode.");
        if (ctx.User.HasClaim(HostedAuth.AdminClaim, "true")) throw new ApiValidationException("The admin account cannot be deleted. Export your data instead.");
        if (!string.Equals(body.Confirm?.Trim(), DeleteWord, StringComparison.Ordinal)) throw new ApiValidationException($"Type {DeleteWord} to confirm.");
        await UserDeletion.DeleteAsync(db, documents, current.UserId, ct);
        await ctx.SignOutAsync(HostedAuth.Cookie);
        return Results.NoContent();
    }
}

/// <summary>Removes a user with every row and document, and their invite (so they only come back if invited again).</summary>
public static class UserDeletion
{
    public static async Task DeleteAsync(DriverBalanceDbContext db, DocumentStore documents, long userId, CancellationToken ct)
    {
        var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        var vehicleIds = await db.Vehicles.IgnoreQueryFilters().Where(v => v.UserId == userId).Select(v => v.Id).ToListAsync(ct);

        // Expenses first: they restrict deleting the tyre sets they use, and the user delete cascades to both at once.
        await db.Expenses.IgnoreQueryFilters().Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
        if (email is not null) await db.Invites.Where(i => i.Email == email).ExecuteDeleteAsync(ct);

        foreach (var id in vehicleIds) documents.DeleteVehicleFolder(id);
    }
}
