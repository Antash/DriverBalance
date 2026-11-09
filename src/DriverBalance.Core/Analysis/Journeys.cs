namespace DriverBalance.Core;

/// <summary>A run of trips with no stop longer than <see cref="Journeys.MaxStop"/> between them, regardless of midnight
/// or the cold-start gap: drive 5 h, rest 1 h, drive 5 h into the next day, rest 2 h, drive on — one journey.</summary>
public sealed record Journey(DateTime Start, DateTime End, double Km, int TripCount, TimeSpan DrivingTime);

public static class Journeys
{
    public static readonly TimeSpan MaxStop = TimeSpan.FromHours(4);

    public static Journey? Longest(IEnumerable<Trip> trips)
    {
        Journey? best = null;
        Journey? cur = null;
        foreach (var t in trips.OrderBy(t => t.Start))
        {
            if (cur is not null && t.Start - cur.End <= MaxStop)
                cur = cur with { End = t.End > cur.End ? t.End : cur.End, Km = cur.Km + t.DistanceKm, TripCount = cur.TripCount + 1, DrivingTime = cur.DrivingTime + t.Duration };
            else
                cur = new Journey(t.Start, t.End, t.DistanceKm, 1, t.Duration);
            if (best is null || cur.Km > best.Km) best = cur;
        }
        return best;
    }
}
