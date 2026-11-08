namespace DriverBalance.Core.Tests;

public class FuelEconomyTests
{
    private static OdometerTimeline Timeline(params (DateOnly d, int km)[] r) =>
        OdometerTimeline.Build(r.Select(x => new OdometerReading(x.d.ToDateTime(new TimeOnly(12, 0)), x.km, OdometerSource.Snapshot)));

    private static Expense Fill(DateOnly d, int? odo, double litres, bool full) =>
        new() { Date = d, Category = ExpenseCategory.Fuel, Odometer = odo, Litres = litres, FullTank = full };

    [Fact]
    public void FuelEconomy_FullToFull()
    {
        var fills = new[]
        {
            Fill(new DateOnly(2026, 1, 1), 1000, 40, true),
            Fill(new DateOnly(2026, 1, 10), 1300, 20, false),
            Fill(new DateOnly(2026, 1, 20), 1600, 25, true),
        };
        var seg = Assert.Single(FuelEconomy.Segments(fills, Timeline()));
        Assert.Equal(45, seg.Litres);
        Assert.Equal(600, seg.EndOdometer - seg.StartOdometer);
        Assert.Equal(7.5, seg.Litres / (seg.EndOdometer - seg.StartOdometer) * 100);
        Assert.False(seg.Inferred);
        Assert.Equal(new DateOnly(2026, 1, 20), seg.EndDate);
    }

    [Fact]
    public void FuelEconomy_InferredOdometer()
    {
        var fills = new[]
        {
            Fill(new DateOnly(2026, 1, 1), 1000, 40, true),
            Fill(new DateOnly(2026, 1, 20), null, 30, true),
        };
        var t = Timeline((new DateOnly(2026, 1, 20), 1500));
        var s = Assert.Single(FuelEconomy.Segments(fills, t));
        Assert.True(s.Inferred);
        Assert.Equal(1500, s.EndOdometer);
        Assert.Equal(30, s.Litres);
    }

    [Fact]
    public void FuelEconomy_NoOdometerAnywhere_Skipped()
    {
        var fills = new[]
        {
            Fill(new DateOnly(2026, 1, 1), 1000, 40, true),
            Fill(new DateOnly(2026, 1, 20), null, 30, true),
        };
        Assert.Empty(FuelEconomy.Segments(fills, Timeline()));
    }

    [Fact]
    public void FuelEconomy_FlowsIntoPeriodStats()
    {
        var expenses = new[]
        {
            Fill(new DateOnly(2026, 1, 1), 1000, 40, true),
            Fill(new DateOnly(2026, 1, 20), 1600, 45, true),
        };
        var data = new VehicleData(Array.Empty<Trip>(), expenses, Array.Empty<TyreSet>(), "CZK", new DateOnly(2026, 6, 1));
        var ctx = AnalysisContext.Create(data, new AnalysisParameters());
        Assert.Equal(7.5, ctx.Stats(CalendarPeriods.Month(2026, 1)).Fuel.FullTankLPer100);
        Assert.Equal(600, ctx.Stats(CalendarPeriods.Month(2026, 1)).Fuel.FullTankKm);
        Assert.Null(ctx.Stats(CalendarPeriods.Month(2026, 2)).Fuel.FullTankLPer100);
    }
}
