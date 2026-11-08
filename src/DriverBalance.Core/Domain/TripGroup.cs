namespace DriverBalance.Core;

/// <summary>Trips chained by gaps shorter than the cold-start gap (one cold start).</summary>
public sealed record TripGroup(IReadOnlyList<Trip> Trips, DrivingScore Score)
{
    public DateTime Start => Trips[0].Start;
    public DateTime End => Trips[^1].End;
    public double DistanceKm => Trips.Sum(t => t.DistanceKm);
    /// <summary>Sum of trip durations (driving time), not the wall-clock span.</summary>
    public TimeSpan Duration => TimeSpan.FromTicks(Trips.Sum(t => t.Duration.Ticks));
    public double FuelLiters => Trips.Sum(t => t.FuelLiters);
    public double AverageSpeedKmh => Duration.TotalHours > 0 ? DistanceKm / Duration.TotalHours : 0;
    public double? LPer100 => DistanceKm > 0 ? FuelLiters / DistanceKm * 100.0 : null;
    public DateOnly StartDate => DateOnly.FromDateTime(Start);
}
