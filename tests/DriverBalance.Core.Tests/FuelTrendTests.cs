namespace DriverBalance.Core.Tests;

public class FuelTrendTests
{
    private static Trip T(int id, DateTime start, int minutes, double km, double fuel) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, fuel, "") { Id = id };

    private static AnalysisContext Ctx(params Trip[] trips) =>
        AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2025, 12, 31)), new AnalysisParameters());

    [Fact]
    public void FuelTrend_TripLevel_FiltersOutliers()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        var ctx = Ctx(
            T(1, d, 30, 20, 1.4),                 // 7 L/100 ok
            T(2, d.AddDays(1), 30, 20, 0),        // 0 L/100 dropped
            T(3, d.AddDays(2), 30, 10, 4.0),      // 40 L/100 dropped
            T(4, d.AddDays(3), 30, 0, 0.5),       // 0 km dropped
            T(5, d.AddDays(4), 45, 40, 3.2),      // 8 ok
            T(6, d.AddDays(5), 45, 40, 3.6));     // 9 ok
        var chart = FuelCharts.Trend(ctx, CalendarPeriods.Year(2025), TrendLevel.Trip);
        Assert.Equal(new long[] { 1, 5, 6 }, chart.Points.Select(p => p.FirstTripId));
        Assert.All(chart.Points, p => Assert.Equal(1, p.TripCount));
        Assert.Equal(DrivingScore.Good, chart.Points[0].Score);
        Assert.Equal(3, chart.Smooth.Count);
        Assert.All(chart.Smooth, s => Assert.True(double.IsFinite(s.LPer100)));
    }

    [Fact]
    public void FuelTrend_GroupLevel_OnePointPerGroup()
    {
        var d = new DateTime(2025, 5, 1, 8, 0, 0);
        var ctx = Ctx(T(1, d, 20, 10, 0.7), T(2, d.AddMinutes(30), 20, 10, 0.9), T(3, d.AddDays(1), 30, 20, 1.6));
        var chart = FuelCharts.Trend(ctx, CalendarPeriods.Year(2025), TrendLevel.Group);
        Assert.Equal(2, chart.Points.Count);
        Assert.Equal(2, chart.Points[0].TripCount);
        Assert.Equal(1, chart.Points[0].FirstTripId);
        Assert.Equal(8.0, chart.Points[0].LPer100, 9);
        Assert.Equal(20, chart.Points[0].Km, 9);
    }
}
