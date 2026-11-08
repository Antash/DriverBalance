namespace DriverBalance.Core.Tests;

public class MoneyChartsTests
{
    private static AnalysisContext Ctx(params Expense[] e) =>
        AnalysisContext.Create(new VehicleData(Array.Empty<Trip>(), e, Array.Empty<TyreSet>(), "CZK", new DateOnly(2026, 6, 1)), new AnalysisParameters());

    [Fact]
    public void MoneyCharts_MonthlyTotalsCumulative()
    {
        var c = Ctx(
            new Expense { Date = new DateOnly(2026, 1, 5), Category = ExpenseCategory.Fuel, Amount = 100m, Odometer = 1000 },
            new Expense { Date = new DateOnly(2026, 2, 5), Category = ExpenseCategory.Fuel, Amount = 200m, Odometer = 1500 },
            new Expense { Date = new DateOnly(2026, 2, 6), Category = ExpenseCategory.Wash, Amount = 50m });
        var p = new Period(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), "x");

        var monthly = MoneyCharts.Monthly(c, p);
        Assert.Equal(new[] { "2026-01", "2026-02" }, monthly.Select(m => m.Month));
        Assert.Equal(100m, monthly[0].ByCategory[ExpenseCategory.Fuel]);
        Assert.Equal(50m, monthly[1].ByCategory[ExpenseCategory.Wash]);

        var totals = MoneyCharts.Totals(c, p);
        Assert.Equal(300m, totals[ExpenseCategory.Fuel]);

        var cum = MoneyCharts.Cumulative(c);
        Assert.Equal(new[] { 100m, 300m, 350m }, cum.Select(x => x.CumulativeCost));
        Assert.Equal(0, cum[0].CumulativeKm);
        Assert.Null(cum[0].CostPerKm);
        Assert.Equal(500, cum[1].CumulativeKm);
        Assert.Equal(0.6m, cum[1].CostPerKm);
    }
}
