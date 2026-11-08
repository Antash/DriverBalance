namespace DriverBalance.Core;

public sealed record Trip(
    string Category,
    DateTime Start,
    int StartOdometer,
    string StartAddress,
    DateTime End,
    int EndOdometer,
    string EndAddress,
    TimeSpan Duration,
    double DistanceKm,
    double FuelLiters,
    string Notes)
{
    public long Id { get; init; }

    public double AverageSpeedKmh => Duration.TotalHours > 0 ? DistanceKm / Duration.TotalHours : 0;
}
