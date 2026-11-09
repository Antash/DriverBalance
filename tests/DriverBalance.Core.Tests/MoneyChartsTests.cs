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

    }

    [Fact]
    public void Cumulative_IsWeeklyWithinThePeriod_SpreadingCoveredCosts()
    {
        var c = Ctx(
            new Expense { Date = new DateOnly(2025, 12, 31), Category = ExpenseCategory.Wash, Amount = 50m, Odometer = 10000 },
            new Expense { Date = new DateOnly(2026, 1, 1), Category = ExpenseCategory.Insurance, Amount = 3650m,
                CoversFrom = new DateOnly(2026, 1, 1), CoversTo = new DateOnly(2026, 12, 31) },
            new Expense { Date = new DateOnly(2026, 1, 29), Category = ExpenseCategory.Fuel, Amount = 100m, Odometer = 11000 });
        var p = new Period(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), "x");

        var cum = MoneyCharts.Cumulative(c, p);

        // One point per week from the period start, plus the last day; the wash before the period is not counted.
        Assert.Equal(new DateOnly(2026, 1, 7), cum[0].Date);
        Assert.Equal(70m, cum[0].CumulativeCost);
        Assert.Equal(new DateOnly(2026, 2, 28), cum[^1].Date);
        var feb4 = cum.Single(x => x.Date == new DateOnly(2026, 2, 4));
        Assert.Equal(450m, feb4.CumulativeCost);
        Assert.Equal(1000, feb4.CumulativeKm);
        Assert.Equal(0.45m, feb4.CostPerKm);
        Assert.Equal(690m, cum[^1].CumulativeCost);
        Assert.Equal(0.69m, cum[^1].CostPerKm);
        // Before enough km are driven the ratio is meaningless: no cost per km.
        Assert.Null(cum.Single(x => x.Date == new DateOnly(2026, 1, 28)).CostPerKm);
    }
}
