namespace DriverBalance.Core.Tests;

public class OwnershipTests
{
    private static readonly DateOnly Purchase = new(2025, 4, 14);
    private static readonly DateOnly Today = new(2025, 12, 31);

    private static Trip T(DateTime start, int startOdo, int endOdo) =>
        new("Private", start, startOdo, "", start.AddMinutes(30), endOdo, "", TimeSpan.FromMinutes(30), 10, 1, "");

    private static AnalysisContext Ctx(DateOnly? purchase, decimal? resale = 600000m, DateOnly? resaleDate = null)
    {
        var expenses = new[]
        {
            new Expense { Id = 1, Date = Purchase, Category = ExpenseCategory.Purchase, Amount = 1000000m, Note = "Car purchase price" },
            new Expense { Id = 2, Date = Purchase, Category = ExpenseCategory.Purchase, Amount = 30000m, Note = "Warranty" },
            new Expense { Id = 3, Date = Purchase, Category = ExpenseCategory.Insurance, Amount = 1200m,
                CoversFrom = new DateOnly(2025, 1, 1), CoversTo = new DateOnly(2025, 12, 31) },
        };
        var trips = new[]
        {
            T(new DateTime(2025, 4, 3, 9, 0, 0), 9990, 10000),
            T(new DateTime(2025, 12, 30, 9, 0, 0), 19990, 20000),
        };
        var data = new VehicleData(trips, expenses, [], "CZK", Today, PurchaseDate: purchase,
            EstimatedResaleValue: resale, ResaleEstimateDate: resaleDate);
        return AnalysisContext.Create(data, new AnalysisParameters());
    }

    [Fact]
    public void Money_IgnoresPurchaseExpenses()
    {
        var p = new Period(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), "");
        var expenses = new[]
        {
            new Expense { Date = Purchase, Category = ExpenseCategory.Purchase, Amount = 1000000m },
            new Expense { Date = Purchase, Category = ExpenseCategory.Parking, Amount = 100m },
        };
        var m = Money.For(p, expenses, "CZK", 100);
        Assert.Equal(100m, m.Total);
        Assert.False(m.ByCategory.ContainsKey(ExpenseCategory.Purchase));
        Assert.Equal(1m, m.CostPerKm);
    }

    [Fact]
    public void Cumulative_IgnoresPurchaseExpenses()
    {
        var c = Ctx(Purchase);
        var points = MoneyCharts.Cumulative(c);
        Assert.Single(points);
        Assert.Equal(1200m, points[0].CumulativeCost);
    }

    [Fact]
    public void Compute_NetCostFromPurchaseDate()
    {
        var o = Ownership.Compute(Ctx(Purchase, 600000m, new DateOnly(2025, 12, 1)));
        Assert.Equal(Purchase, o.PurchaseDate);
        Assert.Equal(2, o.PurchaseCosts.Count);
        Assert.Equal("Car purchase price", o.PurchaseCosts[0].Note);
        Assert.Equal(1030000m, o.PurchaseTotal);
        Assert.Equal(894.25m, o.RunningCosts); // 1200 * 272 / 365
        Assert.Equal(10000, o.KmDriven);
        Assert.Equal(8.9, o.Months);
        Assert.Equal(600000m, o.EstimatedResaleValue);
        Assert.Equal(new DateOnly(2025, 12, 1), o.ResaleEstimateDate);
        Assert.Equal(1030000m + 894.25m - 600000m, o.NetCost);
        Assert.Equal(Money.Round(o.NetCost!.Value / 10000m), o.NetCostPerKm);
        Assert.Equal(Money.Round(o.NetCost!.Value / 8.9m), o.NetCostPerMonth);
    }

    [Fact]
    public void Compute_WithoutResale_NetCostIsWhatWasSpent()
    {
        var o = Ownership.Compute(Ctx(Purchase, null));
        Assert.Equal(1030000m + 894.25m, o.NetCost);
    }

    [Fact]
    public void Compute_WithoutPurchaseDate_AllDerivedValuesNull()
    {
        var o = Ownership.Compute(Ctx(null));
        Assert.Null(o.PurchaseDate);
        Assert.Null(o.RunningCosts);
        Assert.Null(o.KmDriven);
        Assert.Null(o.Months);
        Assert.Null(o.NetCost);
        Assert.Null(o.NetCostPerKm);
        Assert.Null(o.NetCostPerMonth);
        Assert.Equal(1030000m, o.PurchaseTotal);
    }
}
