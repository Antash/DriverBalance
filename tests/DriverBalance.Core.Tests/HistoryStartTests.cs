namespace DriverBalance.Core.Tests;

public class HistoryStartTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateOnly HistoryStart = new(2023, 5, 1);
    private static readonly DateOnly FirstOil = new(2025, 2, 6);

    private static Trip T(DateTime start, int startOdo, int endOdo) =>
        new("Private", start, startOdo, "", start.AddMinutes(30), endOdo, "", TimeSpan.FromMinutes(30), 10, 1, "");

    private static Expense Oil() => new()
    {
        Date = FirstOil, Category = ExpenseCategory.Maintenance, Maintenance = MaintenanceType.OilChange, Odometer = 7350,
    };

    private static VehicleData Data(DateOnly? historyStart, int? historyOdo = 0) =>
        new([T(new DateTime(2025, 3, 10, 8, 0, 0), 9000, 9010)], [Oil()], [], "CZK", Today,
            HistoryStart: historyStart, HistoryStartOdometer: historyOdo);

    [Fact]
    public void Intervals_BeforeFirstOil_CountsFromZeroKm()
    {
        var data = Data(HistoryStart);
        var iv = Intervals.Build(MaintenanceType.OilChange, data.Expenses, HistoryStart, Today, dataStartOdometer: 0);
        Assert.Equal(2, iv.Count);
        var before = iv[1];
        Assert.Equal("Before first OilChange", before.Label);
        Assert.Equal(HistoryStart, before.From);
        Assert.Equal(FirstOil, before.To);
        Assert.Equal(0, before.StartOdometer);
        Assert.Equal(7350, before.EndOdometer);

        var stats = AnalysisContext.Create(data, new AnalysisParameters()).Stats(before);
        Assert.Equal(7350, stats.Usage.OdometerKm);
        Assert.Equal(7350.0 / (FirstOil.DayNumber - HistoryStart.DayNumber), stats.Usage.KmPerDay);
        Assert.Equal(0, stats.Usage.TripCount);
        Assert.Null(stats.Scores.DpfIndex);
        Assert.Null(stats.Usage.AverageSpeedKmh);
        Assert.Null(stats.Fuel.TripLPer100);
    }

    [Fact]
    public void Intervals_NoOilYet_StartOdometerFromHistoryStart()
    {
        var iv = Intervals.Build(MaintenanceType.OilChange, [], HistoryStart, Today, dataStartOdometer: 0);
        Assert.Single(iv);
        Assert.Equal(0, iv[0].StartOdometer);
        Assert.Equal(HistoryStart, iv[0].From);
    }

    [Fact]
    public void Timeline_HistoryStartBeforeEverything_IsFirstReading()
    {
        var ctx = AnalysisContext.Create(Data(HistoryStart, 0), new AnalysisParameters());
        var first = ctx.Timeline.Readings[0];
        Assert.Equal(OdometerSource.HistoryStart, first.Source);
        Assert.Equal(new DateTime(2023, 5, 1), first.At);
        Assert.Equal(0, first.Km);
    }

    [Fact]
    public void Timeline_NullOdometer_TreatedAsZero()
    {
        var ctx = AnalysisContext.Create(Data(HistoryStart, null), new AnalysisParameters());
        Assert.Equal(0, ctx.Timeline.Readings[0].Km);
    }

    [Fact]
    public void Timeline_HistoryStartAfterFirstReading_Ignored()
    {
        var ctx = AnalysisContext.Create(Data(new DateOnly(2025, 3, 20), 0), new AnalysisParameters());
        Assert.DoesNotContain(ctx.Timeline.Readings, r => r.Source == OdometerSource.HistoryStart);
        Assert.Empty(ctx.Timeline.Dropped);
    }

    [Fact]
    public void Timeline_NoHistoryStart_Unchanged()
    {
        var ctx = AnalysisContext.Create(Data(null), new AnalysisParameters());
        Assert.DoesNotContain(ctx.Timeline.Readings, r => r.Source == OdometerSource.HistoryStart);
    }
}
