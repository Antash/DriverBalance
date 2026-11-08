namespace DriverBalance.Core.Tests;

public class ChartsIITests
{
    private static readonly Period All = new(new DateOnly(1990, 1, 1), new DateOnly(2100, 1, 1), "all");

    private static AnalysisContext FixtureCtx()
    {
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic-trips.csv"));
        var trips = VolvoCsvParser.Parse(reader).Trips;
        return AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2030, 1, 1)), new AnalysisParameters());
    }

    private static Trip T(long id, DateTime start, int minutes, double km, double fuel) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, fuel, "") { Id = id };

    private static AnalysisContext Ctx(params Trip[] trips) =>
        AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 12, 31)), new AnalysisParameters());

    [Fact]
    public void ScoreMap_PointsMatchGroups()
    {
        var ctx = FixtureCtx();
        var points = ScoreCharts.ScoreMap(ctx, All);
        Assert.Equal(ctx.Groups.Count, points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var g = ctx.Groups[i];
            Assert.Equal(g.Duration.TotalMinutes, points[i].DurationMinutes, 6);
            Assert.Equal(g.AverageSpeedKmh, points[i].AverageSpeedKmh, 6);
            Assert.Equal(g.DistanceKm, points[i].Km, 6);
            Assert.Equal(g.Score, points[i].Score);
            Assert.Equal(g.Trips[0].Id, points[i].FirstTripId);
        }
    }

    [Fact]
    public void ScoreZones_FromParameters()
    {
        var z = ScoreZones.From(new AnalysisParameters());
        Assert.Equal(new ScoreZones(20, 30, 40, 25, 35, 50), z);
    }

    [Fact]
    public void PenaltyCurve_EmptyBinsHaveNullMedian()
    {
        // One 12-minute group (5-min bins: 10-15) and one 100-minute group (open bin).
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        var bins = FuelCharts.PenaltyCurve(Ctx(T(1, d, 12, 5, 0.4), T(2, d.AddDays(1), 100, 80, 5.6)), All);
        Assert.Equal(14, bins.Count);
        Assert.Null(bins[0].MedianLPer100);
        Assert.Equal(0, bins[0].Count);
        var b = bins.Single(x => x.FromMinutes == 10);
        Assert.Equal(8.0, b.MedianLPer100!.Value, 6);
        Assert.Equal(1, b.Count);
        var open = bins[^1];
        Assert.Equal(90, open.FromMinutes);
        Assert.Null(open.ToMinutes);
        Assert.Equal(7.0, open.MedianLPer100!.Value, 6);
        Assert.Equal(2, bins.Sum(x => x.Count));
    }

    [Fact]
    public void PenaltyCurve_QuartilesInterpolate()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        // Four 10-minute groups with 5,6,7,9 L/100.
        var bins = FuelCharts.PenaltyCurve(Ctx(
            T(1, d, 10, 100, 5), T(2, d.AddDays(1), 10, 100, 6), T(3, d.AddDays(2), 10, 100, 7), T(4, d.AddDays(3), 10, 100, 9)), All);
        var b = bins.Single(x => x.FromMinutes == 10);
        Assert.Equal(6.5, b.MedianLPer100!.Value, 6);
        Assert.Equal(5.75, b.P25!.Value, 6);
        Assert.Equal(7.5, b.P75!.Value, 6);
    }

    [Fact]
    public void Seasonality_AggregatesByMonth()
    {
        var ctx = Ctx(T(1, new DateTime(2024, 12, 20, 8, 0, 0), 30, 50, 3), T(2, new DateTime(2025, 1, 5, 8, 0, 0), 30, 50, 4),
            T(3, new DateTime(2025, 1, 9, 8, 0, 0), 30, 50, 5));
        var s = FuelCharts.Seasonality(ctx);
        Assert.Equal(new[] { (2024, 12), (2025, 1) }, s.Select(x => (x.Year, x.Month)));
        Assert.Equal(100, s[1].Km, 6);
        Assert.Equal(9.0, s[1].LPer100!.Value, 6);
    }

    private static Trip A(long id, DateTime start, string from, string to, double km = 40, double fuel = 3) =>
        new("Private", start, 0, from, start.AddMinutes(50), 0, to, TimeSpan.FromMinutes(50), km, fuel, "") { Id = id };

    [Fact]
    public void Routes_NormalizesAddresses()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        var ctx = Ctx(
            A(1, d, "Home St 1", "Work Ave 5"),
            A(2, d.AddDays(1), "  home   st 1 ", "WORK AVE 5"),
            A(3, d.AddDays(2), "Home St 1", "work ave 5"),
            A(4, d.AddDays(3), "Work Ave 5", "Home St 1"));
        var routes = PlacesAnalysis.Routes(ctx, All);
        Assert.Equal(2, routes.Count);
        var r = routes[0];
        Assert.Equal(("Home St 1", "Work Ave 5"), (r.From, r.To)); // most common spelling
        Assert.Equal(3, r.Count);
        Assert.Equal(120, r.Km, 6);
        Assert.Equal(3, r.Trend.Count);
        Assert.Equal(DrivingScore.Good, r.TypicalScore); // 50 min at 48 km/h

        var places = PlacesAnalysis.Places(ctx, All);
        var home = places.Single(x => x.Address == "Home St 1");
        Assert.Equal((3, 1), (home.Starts, home.Ends));
    }

    [Fact]
    public void Routes_UseGroupFirstStartAndLastEnd()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        // Two trips 10 minutes apart form one group: A -> B -> C is the route A -> C.
        var ctx = Ctx(
            new Trip("Private", d, 0, "A", d.AddMinutes(30), 0, "B", TimeSpan.FromMinutes(30), 20, 1, "") { Id = 1 },
            new Trip("Private", d.AddMinutes(40), 0, "B", d.AddMinutes(70), 0, "C", TimeSpan.FromMinutes(30), 20, 1, "") { Id = 2 });
        var r = Assert.Single(PlacesAnalysis.Routes(ctx, All));
        Assert.Equal(("A", "C"), (r.From, r.To));
    }

    [Fact]
    public void DpfHealth_NeedsLongDriveAfterHintDays()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        Trip longTrip = A(1, d, "a", "b", 60, 4);                      // 50 min at 72 km/h: Excellent
        Trip shortTrip = new("Private", d.AddDays(20), 0, "a", d.AddDays(20).AddMinutes(10), 0, "b", TimeSpan.FromMinutes(10), 3, 0.5, "") { Id = 2 };

        AnalysisContext At(DateOnly today) => AnalysisContext.Create(
            new VehicleData(new[] { longTrip, shortTrip }, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", today), new AnalysisParameters());

        var fresh = DpfHealth.Compute(At(new DateOnly(2025, 5, 14))); // 13 days
        Assert.Equal(new DateOnly(2025, 5, 1), fresh.LastLongDrive);
        Assert.Equal(13, fresh.DaysSince);
        Assert.False(fresh.NeedsLongDrive);

        var due = DpfHealth.Compute(At(new DateOnly(2025, 5, 15))); // 14 days
        Assert.Equal(14, due.DaysSince);
        Assert.True(due.NeedsLongDrive);
        Assert.Equal(3.0, due.KmSince!.Value, 6);
        Assert.Equal(new DateOnly(2025, 5, 15), due.Series[^1].Date);
        Assert.Equal(14, due.Series[^1].DaysSinceLongDrive);
        var stretch = Assert.Single(due.WorstStretches);
        Assert.Equal(new Period(new DateOnly(2025, 5, 1), new DateOnly(2025, 5, 15), "14 days"), stretch);
    }

    [Fact]
    public void DpfHealth_WorstStretchesAreTopThreeLongestGaps()
    {
        var d = new DateTime(2025, 1, 1, 8, 0, 0);
        int[] offsets = { 0, 5, 20, 22, 40, 41 }; // gaps 5, 15, 2, 18, 1, then to today
        var trips = offsets.Select((o, i) => A(i + 1, d.AddDays(o), "a", "b", 60, 4)).ToArray();
        var ctx = AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 2, 20)), new AnalysisParameters());
        var s = DpfHealth.Compute(ctx);
        Assert.Equal(new[] { 18, 15, 9 }, s.WorstStretches.Select(x => x.Days).ToArray()); // today gap is 9 days (41 -> 50)
    }

    [Fact]
    public void DpfHealth_NoTrips_Nulls()
    {
        var s = DpfHealth.Compute(Ctx());
        Assert.Null(s.LastLongDrive);
        Assert.Null(s.DaysSince);
        Assert.Null(s.KmSince);
        Assert.False(s.NeedsLongDrive);
        Assert.Empty(s.Series);
        Assert.Empty(s.WorstStretches);
    }
}
