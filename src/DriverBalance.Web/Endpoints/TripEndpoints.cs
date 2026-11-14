using DriverBalance.Core;

namespace DriverBalance.Web;

public static class TripEndpoints
{
    private const int MaxPageSize = 500;

    public static void Map(RouteGroupBuilder api)
    {
        var t = api.MapGroup("/vehicles/{vid:long}/trips");
        t.MapGet("", List);
        t.MapGet("/routes", Routes);
        t.MapGet("/{tripId:long}", Get);
    }

    private static TrendLevel ParseLevel(string? level)
    {
        if (level is null) return TrendLevel.Group;
        if (level.Equals("Group", StringComparison.OrdinalIgnoreCase)) return TrendLevel.Group;
        if (level.Equals("Trip", StringComparison.OrdinalIgnoreCase)) return TrendLevel.Trip;
        throw new ApiValidationException("level must be Group or Trip.");
    }

    /// <summary>
    /// Trip groups (cold starts), or single trips with level=Trip, newest first; from inclusive, to exclusive.
    /// minKm keeps items at least that long, route keeps one regular route; the summary covers every matching item.
    /// </summary>
    private static async Task<IResult> List(long vid, DateOnly? from, DateOnly? to, AnalysisSessionFactory sessions,
        int page = 1, int pageSize = 50, string? level = null, double minKm = 0, string? route = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            throw new ApiValidationException($"page must be >= 1 and pageSize between 1 and {MaxPageSize}.");
        var lvl = ParseLevel(level);
        if (!double.IsFinite(minKm) || minKm < 0) throw new ApiValidationException("minKm must be 0 or more.");

        var s = await sessions.OpenAsync(vid, null, ct);
        var costs = s.Context.Costs;
        var items = RegularRoutes.Items(s.Context, s.Range(from, to), lvl).Where(g => g.DistanceKm >= minKm);
        if (!string.IsNullOrEmpty(route)) items = items.Where(g => RegularRoutes.KeyOf(g) == route);
        var ordered = items.OrderByDescending(g => g.Start).ToList();

        var summary = new TripSummary(ordered.Count, ordered.Sum(g => g.DistanceKm), ordered.Sum(g => costs.Of(g).Total), s.Data.HomeCurrency);
        return Results.Ok(new TripPage(ordered.Count, page, pageSize, ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(g => TripGroupView.From(g, costs)).ToList(), summary));
    }

    /// <summary>Regular routes (driven at least 5 times) in the period.</summary>
    private static async Task<IResult> Routes(long vid, DateOnly? from, DateOnly? to, AnalysisSessionFactory sessions,
        string? level = null, CancellationToken ct = default)
    {
        var lvl = ParseLevel(level);
        var s = await sessions.OpenAsync(vid, null, ct);
        return Results.Ok(RegularRoutes.Build(s.Context, s.Range(from, to), lvl));
    }

    private static async Task<IResult> Get(long vid, long tripId, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, null, ct);
        var trip = s.Data.Trips.FirstOrDefault(t => t.Id == tripId);
        return trip is null ? Results.NotFound() : Results.Ok(TripView.From(trip, s.Context.Costs));
    }
}
