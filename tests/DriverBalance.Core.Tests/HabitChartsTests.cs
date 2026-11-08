namespace DriverBalance.Core.Tests;

public class HabitChartsTests
{
    private static readonly Period All = new(new DateOnly(1990, 1, 1), new DateOnly(2100, 1, 1), "all");

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static AnalysisContext FixtureCtx(AnalysisParameters? p = null)
    {
        using var reader = new StreamReader(FixturePath("synthetic-trips.csv"));
        var trips = VolvoCsvParser.Parse(reader).Trips;
        return AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2030, 1, 1)), p ?? new AnalysisParameters());
    }

    private static Trip T(DateTime start, int minutes, double km) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, 1, "");

    [Fact]
    public void Gaps_ThresholdFromParameters()
    {
        var ctx = FixtureCtx(new AnalysisParameters { ColdStartGapMinutes = 33 });
        Assert.Equal(33, HabitCharts.Gaps(ctx, All).ThresholdMinutes);
    }

    [Fact]
    public void Gaps_BinsCoverAllGaps()
    {
        var ctx = FixtureCtx();
        var h = HabitCharts.Gaps(ctx, All);
        Assert.Equal(ctx.Data.Trips.Count - 1, h.Bins.Sum(b => b.Count));
        Assert.Equal(28, h.Bins.Count);
        Assert.Null(h.Bins[^1].To);
        Assert.Equal(1440, h.Bins[^1].From);
    }

    [Fact]
    public void Gaps_PlacesGapsInExpectedBins()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        var ctx = AnalysisContext.Create(new VehicleData(new[]
        {
            T(d, 10, 5),
            T(d.AddMinutes(10 + 7), 10, 5),          // gap 7  -> 5-10
            T(d.AddMinutes(27 + 120), 10, 5),        // gap 120 -> 120-240
            T(d.AddMinutes(157 + 3000), 10, 5),      // gap 3000 -> open
        }, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 12, 31)), new AnalysisParameters());
        var bins = HabitCharts.Gaps(ctx, All).Bins;
        Assert.Equal(1, bins.Single(b => b.From == 5).Count);
        Assert.Equal(1, bins.Single(b => b.From == 120).Count);
        Assert.Equal(1, bins.Single(b => b.To is null).Count);
    }

    [Fact]
    public void WhenYouDrive_KmSumsToPeriodKm()
    {
        var ctx = FixtureCtx();
        var cells = HabitCharts.WhenYouDrive(ctx, All);
        Assert.Equal(ctx.Stats(All).Usage.TripKm, cells.Sum(x => x.Km), 6);
        Assert.Equal(ctx.Data.Trips.Count, cells.Sum(x => x.Trips));
        Assert.All(cells, x => Assert.InRange(x.DayOfWeek, 0, 6));
    }

    [Fact]
    public void WhenYouDrive_AttributesToStartHourAndMondayFirst()
    {
        // 2025-06-02 is a Monday; trip crosses the hour but belongs to its start hour.
        var ctx = AnalysisContext.Create(new VehicleData(new[] { T(new DateTime(2025, 6, 2, 8, 50, 0), 30, 10) },
            Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 12, 31)), new AnalysisParameters());
        var cell = Assert.Single(HabitCharts.WhenYouDrive(ctx, All));
        Assert.Equal(0, cell.DayOfWeek);
        Assert.Equal(8, cell.Hour);
        Assert.Equal(1.0, cell.BadShare); // 30 min at 20 km/h scores Bad
    }
}
