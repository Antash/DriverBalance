namespace DriverBalance.Core.Tests;

public class TripGroupingTests
{
    private static readonly AnalysisParameters P = new();

    private static Trip T(DateTime start, int minutes, double km = 5, double fuel = 0.4) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, fuel, "");

    private static readonly DateTime D = new(2025, 3, 1, 8, 0, 0);

    [Fact]
    public void Gap44Minutes_Joins()
    {
        var a = T(D, 10);
        var b = T(a.End.AddMinutes(44), 10);
        var groups = TripGrouping.Build(new[] { b, a }, P);
        var g = Assert.Single(groups);
        Assert.Equal(2, g.Trips.Count);
        Assert.Equal(a.Start, g.Start);
        Assert.Equal(b.End, g.End);
    }

    [Fact]
    public void Gap45Minutes_Splits()
    {
        var a = T(D, 10);
        var b = T(a.End.AddMinutes(45), 10);
        var groups = TripGrouping.Build(new[] { a, b }, P);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Group_SumsDurationDistanceFuel()
    {
        var a = T(D, 10, km: 10, fuel: 1);
        var b = T(a.End.AddMinutes(5), 20, km: 30, fuel: 2);
        var g = Assert.Single(TripGrouping.Build(new[] { a, b }, P));
        Assert.Equal(TimeSpan.FromMinutes(30), g.Duration);
        Assert.Equal(40, g.DistanceKm);
        Assert.Equal(3, g.FuelLiters);
        Assert.Equal(80, g.AverageSpeedKmh, 6);
        Assert.Equal(7.5, g.LPer100!.Value, 6);
        Assert.Equal(new DateOnly(2025, 3, 1), g.StartDate);
    }

    [Fact]
    public void Empty_NoGroups() => Assert.Empty(TripGrouping.Build(Array.Empty<Trip>(), P));
}
