namespace DriverBalance.Core.Tests;

public class ScoreChartsTests
{
    private static readonly AnalysisParameters P = new();
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static Trip T(DateTime start, int minutes, double km, double fuel = 0) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, fuel, "");

    private static AnalysisContext Ctx(params Trip[] trips) =>
        AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 12, 31)), P);

    private static Period Year(int y) => CalendarPeriods.Year(y);

    [Fact]
    public void Monthly_KmSumsMatchStats()
    {
        using var reader = new StreamReader(FixturePath("synthetic-trips.csv"));
        var trips = VolvoCsvParser.Parse(reader).Trips;
        var ctx = AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2030, 1, 1)), P);
        var period = new Period(new DateOnly(1990, 1, 1), new DateOnly(2100, 1, 1), "all");
        var chart = ScoreCharts.Monthly(ctx, period);
        var stats = ctx.Stats(period);
        Assert.NotEmpty(chart.Months);
        Assert.Equal(stats.Usage.TripKm, chart.Months.Sum(m => m.ExcellentKm + m.GoodKm + m.FairKm + m.BadKm), 6);
        Assert.Equal(stats.Scores.Buckets[DrivingScore.Excellent].Km, chart.Months.Sum(m => m.ExcellentKm), 6);
        Assert.Equal(stats.Scores.Buckets[DrivingScore.Bad].Km, chart.Months.Sum(m => m.BadKm), 6);
        Assert.Equal(chart.Months.Select(m => m.Month).OrderBy(m => m), chart.Months.Select(m => m.Month));
    }

    [Fact]
    public void WeeklyDpf_GroupCountedOnce()
    {
        // Sunday 2025-06-01 23:40 running past midnight, then a continuing trip on Monday (same group).
        var ctx = Ctx(T(new DateTime(2025, 6, 1, 23, 40, 0), 30, 20), T(new DateTime(2025, 6, 2, 0, 20, 0), 15, 10));
        Assert.Single(ctx.Groups);
        var weeks = ScoreCharts.Monthly(ctx, Year(2025)).Weeks;
        var w = Assert.Single(weeks);
        Assert.Equal(new DateOnly(2025, 5, 26), w.WeekStart);
        Assert.NotNull(w.Dpf);
    }

    [Fact]
    public void WeeklyDpf_MovingAverageOverWeeksWithData()
    {
        var ctx = Ctx(
            T(new DateTime(2025, 6, 2, 8, 0, 0), 50, 60),   // excellent -> dpf 1
            T(new DateTime(2025, 6, 16, 8, 0, 0), 10, 1),   // bad -> dpf 0 (empty week between is skipped)
            T(new DateTime(2025, 6, 23, 8, 0, 0), 50, 60)); // dpf 1
        var weeks = ScoreCharts.Monthly(ctx, Year(2025)).Weeks;
        Assert.Equal(3, weeks.Count);
        Assert.Equal(new[] { 1.0, 0.0, 1.0 }, weeks.Select(x => x.Dpf!.Value));
        Assert.Equal(1.0, weeks[0].DpfMovingAverage!.Value, 9);
        Assert.Equal(0.5, weeks[1].DpfMovingAverage!.Value, 9);
        Assert.Equal(2.0 / 3, weeks[2].DpfMovingAverage!.Value, 9);
    }

    [Fact]
    public void Heatmap_IntensityCapped()
    {
        var ctx = Ctx(
            T(new DateTime(2025, 3, 3, 8, 0, 0), 120, 150),
            T(new DateTime(2025, 3, 5, 8, 0, 0), 50, 25),
            T(new DateTime(2024, 3, 5, 8, 0, 0), 50, 25));
        var days = ScoreCharts.Heatmap(ctx, 2025);
        Assert.Equal(2, days.Count);
        Assert.Equal(1.0, days[0].Intensity);
        Assert.Equal(150, days[0].Km, 9);
        Assert.Equal(0.25, days[1].Intensity, 9);
        Assert.Equal(new DateOnly(2025, 3, 5), days[1].Date);
        Assert.Equal(DrivingScore.Excellent, days[0].BestScore);
    }
}
