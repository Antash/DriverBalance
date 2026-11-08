using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public sealed record ExportToken(long Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

public sealed record ExportDocument(int Version, DateTime ExportedAt, UserRow User, IReadOnlyList<VehicleRow> Vehicles,
    IReadOnlyList<TripRow> Trips, IReadOnlyList<ExpenseRow> Expenses, IReadOnlyList<TyreSetRow> TyreSets,
    IReadOnlyList<ImportBatchRow> ImportBatches, IReadOnlyList<ExportToken> ApiTokens);

public static class ExportEndpoints
{
    public static void Map(RouteGroupBuilder api) => api.MapGet("/export", Export);

    /// <summary>Every row of the current user as one JSON download (token hashes are left out).</summary>
    private static async Task<IResult> Export(HttpContext http, DriverBalanceDbContext db, ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var doc = new ExportDocument(
            1,
            clock.GetUtcNow().UtcDateTime,
            await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.UserId, ct),
            await db.Vehicles.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            await db.Trips.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            await db.Expenses.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            await db.TyreSets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            await db.ImportBatches.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            (await db.ApiTokens.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct))
                .Select(t => new ExportToken(t.Id, t.Name, t.CreatedAt, t.LastUsedAt)).ToList());

        http.Response.Headers.ContentDisposition = $"attachment; filename=\"driverbalance-export-{clock.GetUtcNow():yyyy-MM-dd}.json\"";
        return Results.Ok(doc);
    }
}
