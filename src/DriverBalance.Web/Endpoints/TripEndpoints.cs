using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class TripEndpoints
{
    private const int MaxPageSize = 500;

    public static void Map(RouteGroupBuilder api)
    {
        var t = api.MapGroup("/vehicles/{vid:long}/trips");
        t.MapGet("", List);
        t.MapGet("/{tripId:long}", Get);
    }

    /// <summary>Trip groups (cold starts) by the saved parameters, newest first; from inclusive, to exclusive.</summary>
    private static async Task<IResult> List(long vid, DateOnly? from, DateOnly? to, AnalysisSessionFactory sessions,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            throw new ApiValidationException($"page must be >= 1 and pageSize between 1 and {MaxPageSize}.");

        var s = await sessions.OpenAsync(vid, null, ct);
        var groups = s.Context.Groups.AsEnumerable();
        if (from is { } f) groups = groups.Where(g => g.StartDate >= f);
        if (to is { } t) groups = groups.Where(g => g.StartDate < t);
        var ordered = groups.OrderByDescending(g => g.Start).ToList();

        return Results.Ok(new TripPage(ordered.Count, page, pageSize, ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()));
    }

    private static async Task<IResult> Get(long vid, long tripId, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId && t.VehicleId == vid, ct);
        return row is null ? Results.NotFound() : Results.Ok(row.ToDomain());
    }
}
