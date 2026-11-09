namespace DriverBalance.Core.Tests;

public class JourneysTests
{
    private static Trip T(DateTime start, int minutes, double km) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, 0, "");

    [Fact]
    public void Longest_ChainsTripsAcrossLongStopsAndMidnight()
    {
        var d = new DateTime(2025, 3, 1, 14, 0, 0);
        // 5 h, 1 h rest, 5 h (ends 01:00 next day), 2 h rest, 1 h: one journey. Then a 5 h break starts a new one.
        var trips = new[]
        {
            T(d, 300, 400), T(d.AddHours(6), 300, 380), T(d.AddHours(13), 60, 90),
            T(d.AddHours(19), 30, 20),
        };
        var j = Journeys.Longest(trips)!;
        Assert.Equal(870, j.Km);
        Assert.Equal(3, j.TripCount);
        Assert.Equal(d, j.Start);
        Assert.Equal(d.AddHours(14), j.End);
        Assert.Equal(TimeSpan.FromHours(11), j.DrivingTime);
    }

    [Fact]
    public void Longest_GapOfExactlyFourHoursStillJoins_OverItSplits()
    {
        var d = new DateTime(2025, 3, 1, 8, 0, 0);
        Assert.Equal(2, Journeys.Longest([T(d, 60, 10), T(d.AddHours(5), 60, 10)])!.TripCount);
        Assert.Equal(1, Journeys.Longest([T(d, 60, 10), T(d.AddHours(5).AddMinutes(1), 60, 10)])!.TripCount);
    }

    [Fact]
    public void Longest_UnsortedInput_And_Empty()
    {
        var d = new DateTime(2025, 3, 1, 8, 0, 0);
        Assert.Null(Journeys.Longest([]));
        var j = Journeys.Longest([T(d.AddDays(2), 30, 5), T(d.AddHours(2), 30, 30), T(d, 30, 30)])!;
        Assert.Equal(60, j.Km);
    }

    [Fact]
    public void Stats_ExposeLongestJourney()
    {
        var d = new DateTime(2025, 3, 1, 8, 0, 0);
        var ctx = AnalysisContext.Create(new VehicleData([T(d, 60, 50), T(d.AddHours(3), 60, 60)], [], [], "CZK", new DateOnly(2025, 6, 1)), new AnalysisParameters());
        var s = ctx.Stats(new Period(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), "2025"));
        Assert.Equal(110, s.Usage.LongestJourney!.Km);
    }
}
