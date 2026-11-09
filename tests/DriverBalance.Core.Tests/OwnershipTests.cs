namespace DriverBalance.Core.Tests;

public class OwnershipTests
{
    private static readonly DateOnly Purchase = new(2025, 4, 14);
    private static readonly DateOnly Today = new(2025, 12, 31);

    private static Trip T(DateTime start, int startOdo, int endOdo) =>
        new("Private", start, startOdo, "", start.AddMinutes(30), endOdo, "", TimeSpan.FromMinutes(30), 10, 1, "");

    private static AnalysisContext Ctx(DateOnly? purchase, decimal? resale = 600000m, DateOnly? resaleDate = null, Financing? financing = null)
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
            EstimatedResaleValue: resale, ResaleEstimateDate: resaleDate, Financing: financing);
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
        var p = new Period(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), "");
        var points = MoneyCharts.Cumulative(c, p);
        Assert.Equal(Money.For(p, c.Data.Expenses, "CZK", null).Total, points[^1].CumulativeCost);
        Assert.DoesNotContain(points, x => x.CumulativeCost >= 1000000m);
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

    private static readonly Financing Loan = new(new DateOnly(2025, 5, 1), 9500m, FinancingFrequency.Monthly, 36, 200000m);

    [Fact]
    public void Compute_Financing_WholeContractCountsInPurchaseTotalAndNet()
    {
        var o = Ownership.Compute(Ctx(Purchase, 600000m, null, Loan));
        Assert.Equal(36 * 9500m + 200000m, o.Financing!.Total);
        Assert.Equal(1030000m, o.LumpSumTotal);
        Assert.Equal(1030000m + 542000m, o.PurchaseTotal);
        Assert.Equal(1030000m + 542000m + 894.25m - 600000m, o.NetCost);
        Assert.Equal(new DateOnly(2028, 4, 1), o.Financing.EndDate);
        Assert.Equal(36, o.Financing.Payments);
    }

    [Fact]
    public void Compute_Financing_WithoutBalloon()
    {
        var o = Ownership.Compute(Ctx(Purchase, null, null, Loan with { Balloon = 0m }));
        Assert.Equal(342000m, o.Financing!.Total);
        Assert.Equal(1030000m + 342000m, o.PurchaseTotal);
    }

    [Theory]
    [InlineData(FinancingFrequency.Monthly, 11)]
    [InlineData(FinancingFrequency.Quarterly, 33)]
    [InlineData(FinancingFrequency.Yearly, 132)]
    public void Financing_FrequencyChangesEndDateNotTotal(FinancingFrequency f, int monthsToLast)
    {
        var fin = Loan with { Frequency = f, Payments = 12 };
        Assert.Equal(12 * 9500m + 200000m, fin.Total);
        Assert.Equal(Loan.Start.AddMonths(monthsToLast), fin.EndDate);
    }

    [Fact]
    public void Compute_NoFinancing_Null_AndLumpSumEqualsTotal()
    {
        var o = Ownership.Compute(Ctx(Purchase));
        Assert.Null(o.Financing);
        Assert.Equal(o.PurchaseTotal, o.LumpSumTotal);
    }

    [Fact]
    public void Compute_Financing_WithoutPurchaseDate_StillInPurchaseTotal()
    {
        var o = Ownership.Compute(Ctx(null, 600000m, null, Loan));
        Assert.Equal(1030000m + 542000m, o.PurchaseTotal);
        Assert.Null(o.NetCost);
    }

    [Fact]
    public void Compute_PurchaseCostsCarryExpenseId()
    {
        var o = Ownership.Compute(Ctx(Purchase));
        Assert.Equal([1L, 2L], o.PurchaseCosts.Select(x => x.Id).ToArray());
    }
}
