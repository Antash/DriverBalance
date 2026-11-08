using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public sealed record TokenRequest(string? Name);

public sealed record TokenCreated(long Id, string Name, string Token);

public sealed record TokenInfo(long Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

public static class TokenEndpoints
{
    private const int MaxNameLength = 100;

    public static void Map(RouteGroupBuilder api)
    {
        var t = api.MapGroup("/tokens");
        t.MapGet("", List);
        t.MapPost("", Create);
        t.MapDelete("/{id:long}", Delete);
    }

    private static async Task<IResult> List(DriverBalanceDbContext db, CancellationToken ct)
    {
        var rows = await db.ApiTokens.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(r => new TokenInfo(r.Id, r.Name, Utc(r.CreatedAt), r.LastUsedAt is { } used ? Utc(used) : null)).ToList());
    }

    private static async Task<IResult> Create(TokenRequest body, DriverBalanceDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var name = body.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ApiValidationException("Name is required.");
        if (name.Length > MaxNameLength) throw new ApiValidationException($"Name must be at most {MaxNameLength} characters.");

        var token = ApiTokens.Generate();
        var row = new ApiTokenRow { Name = name, TokenHash = ApiTokens.Hash(token), CreatedAt = clock.GetUtcNow().UtcDateTime };
        db.ApiTokens.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/tokens/{row.Id}", new TokenCreated(row.Id, row.Name, token)); // the only time the secret is shown
    }

    private static async Task<IResult> Delete(long id, DriverBalanceDbContext db, CancellationToken ct)
    {
        var row = await db.ApiTokens.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return Results.NotFound();
        db.ApiTokens.Remove(row);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
}
