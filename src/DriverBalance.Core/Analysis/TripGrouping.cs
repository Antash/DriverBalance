namespace DriverBalance.Core;

public static class TripGrouping
{
    public static IReadOnlyList<TripGroup> Build(IEnumerable<Trip> trips, AnalysisParameters p)
    {
        var gap = TimeSpan.FromMinutes(p.ColdStartGapMinutes);
        var result = new List<TripGroup>();
        var current = new List<Trip>();
        foreach (var trip in trips.OrderBy(t => t.Start))
        {
            if (current.Count > 0 && trip.Start - current[^1].End >= gap)
            {
                result.Add(new TripGroup(current, Scoring.BestForGroup(current, p)));
                current = new List<Trip>();
            }
            current.Add(trip);
        }
        if (current.Count > 0)
            result.Add(new TripGroup(current, Scoring.BestForGroup(current, p)));
        return result;
    }
}
