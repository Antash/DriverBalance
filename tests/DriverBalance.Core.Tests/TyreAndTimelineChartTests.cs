namespace DriverBalance.Core.Tests;

public class TyreAndTimelineChartTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    private static OdometerTimeline Timeline(params (DateOnly d, int km)[] r) =>
        OdometerTimeline.Build(r.Select(x => new OdometerReading(x.d.ToDateTime(new TimeOnly(12, 0)), x.km, OdometerSource.Snapshot)));

    private static Expense Mount(DateOnly d, int? odo, long? set, MaintenanceType type = MaintenanceType.TyreSwap) =>
        new() { Date = d, Category = ExpenseCategory.Maintenance, Maintenance = type, Odometer = odo, TyreSetId = set, Amount = 0m };

    [Fact]
    public void Tyres_KmSummedAcrossSeasons()
    {
        var expenses = new[]
        {
            Mount(new DateOnly(2025, 3, 1), 1000, 1),
            Mount(new DateOnly(2025, 10, 1), 5000, 2),
            Mount(new DateOnly(2026, 3, 1), 9000, 1),
        };
        var t = Timeline((new DateOnly(2025, 1, 1), 1000), (new DateOnly(2026, 11, 1), 10000));
        var spans = TyreMileage.Spans(expenses, null, t, Today);
        Assert.Equal(3, spans.Count);
        var sets = new[] { new TyreSet { Id = 1, Name = "A" }, new TyreSet { Id = 2, Name = "B" } };
        var buy = new Expense { Date = new DateOnly(2025, 2, 1), Category = ExpenseCategory.Tyres, Amount = 10000m, TyreSetId = 1 };
        var sum = TyreMileage.Summaries(spans, sets, new[] { buy }, "CZK");
        var a = sum.Single(s => s.Set.Id == 1);
        Assert.Equal(5000, a.TotalKm);
        Assert.Equal(2, a.Seasons);
        Assert.True(a.Mounted);
        Assert.Equal(10000m, a.PurchaseCost);
        Assert.Equal(2000m, a.CostPer1000Km);
        var b = sum.Single(s => s.Set.Id == 2);
        Assert.Equal(4000, b.TotalKm);
        Assert.False(b.Mounted);
        Assert.Null(b.PurchaseCost);
    }

    [Fact]
    public void Tyres_InitialSetSpanBeforeFirstMount()
    {
        var t = Timeline((new DateOnly(2025, 1, 1), 100), (new DateOnly(2026, 11, 1), 3000));
        var spans = TyreMileage.Spans(new[] { Mount(new DateOnly(2025, 6, 1), 1100, 2) }, 7, t, Today);
        Assert.Equal(2, spans.Count);
        Assert.Equal(7, spans[0].TyreSetId);
        Assert.Equal(new DateOnly(2025, 1, 1), spans[0].From);
        Assert.Equal(1000, spans[0].Km);
        Assert.Null(spans[1].To);
    }

    [Fact]
    public void Tyres_UnassignedSwapSpanHasNullSet()
    {
        var t = Timeline((new DateOnly(2025, 1, 1), 100), (new DateOnly(2026, 11, 1), 3000));
        var spans = TyreMileage.Spans(new[] { Mount(new DateOnly(2025, 6, 1), 1100, null) }, null, t, Today);
        var s = Assert.Single(spans);
        Assert.Null(s.TyreSetId);
        Assert.Equal(1900, s.Km);
        var sum = TyreMileage.Summaries(spans, new[] { new TyreSet { Id = 1, Name = "A" } }, Array.Empty<Expense>(), "CZK");
        Assert.Equal(0, sum.Single().TotalKm);
    }

    [Fact]
    public void Timeline_EventsSortedAndBandsMatchSpans()
    {
        var expenses = new[]
        {
            new Expense { Date = new DateOnly(2025, 9, 1), Category = ExpenseCategory.Fuel, Amount = 900m, Odometer = 4000 },
            Mount(new DateOnly(2025, 3, 1), 1000, 1),
            new Expense { Date = new DateOnly(2025, 6, 1), Category = ExpenseCategory.Maintenance, Maintenance = MaintenanceType.OilChange, MaintenanceDescription = "Oil", Odometer = 2500, Amount = 2000m },
        };
        var data = new VehicleData(Array.Empty<Trip>(), expenses, new[] { new TyreSet { Id = 1, Name = "Winter A" } }, "CZK", Today);
        var ctx = AnalysisContext.Create(data, new AnalysisParameters());
        var chart = TimelineBuilder.Build(ctx, CalendarPeriods.Year(2025));

        Assert.Equal(3, chart.Events.Count);
        Assert.Equal(chart.Events.OrderBy(e => e.Date).Select(e => e.Date), chart.Events.Select(e => e.Date));
        Assert.Equal(TimelineEventKind.TyreMount, chart.Events[0].Kind);
        Assert.Contains("Winter A", chart.Events[0].Label);
        Assert.Equal(TimelineEventKind.Maintenance, chart.Events[1].Kind);
        Assert.Equal(TimelineEventKind.Expense, chart.Events[2].Kind);
        Assert.Equal(900m, chart.Events[2].Amount);

        var spans = TyreMileage.Spans(expenses, data.InitialTyreSetId, ctx.Timeline, Today);
        Assert.Equal(spans, chart.TyreBands);
        Assert.Equal(new[] { 1000, 2500, 4000 }, chart.Odometer.Select(o => o.Km));
        Assert.Equal(new DateOnly(2025, 3, 1), chart.Odometer[0].Date);
    }
}
