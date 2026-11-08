namespace DriverBalance.Core.Tests;

public class MoneyTests
{
    private static readonly Period Jan = CalendarPeriods.Month(2026, 1);

    [Fact]
    public void Money_ForeignWithoutHomeAmount_ExcludedAndCounted()
    {
        var expenses = new[]
        {
            new Expense { Date = new DateOnly(2026, 1, 5), Category = ExpenseCategory.Parking, Amount = 100m },
            new Expense { Date = new DateOnly(2026, 1, 6), Category = ExpenseCategory.Fuel, Amount = 50m, Currency = "EUR" },
            new Expense { Date = new DateOnly(2026, 1, 7), Category = ExpenseCategory.Fuel, Amount = 40m, Currency = "EUR", HomeAmount = 1000m },
        };
        var m = Money.For(Jan, expenses, "CZK", 100);
        Assert.Equal(1100m, m.Total);
        Assert.Equal(1, m.NeedsConversionCount);
        Assert.Equal(1000m, m.ByCategory[ExpenseCategory.Fuel]);
        Assert.Equal(11m, m.CostPerKm);
    }

    [Fact]
    public void Money_YearlyInsuranceAmortized()
    {
        var ins = new Expense
        {
            Date = new DateOnly(2026, 1, 1), Category = ExpenseCategory.Insurance, Amount = 3650m,
            CoversFrom = new DateOnly(2026, 1, 1), CoversTo = new DateOnly(2026, 12, 31),
        };
        Assert.Equal(310m, Money.For(Jan, new[] { ins }, "CZK", null).Total);
        Assert.Equal(3650m, Money.For(CalendarPeriods.Year(2026), new[] { ins }, "CZK", null).Total);
    }

    [Fact]
    public void Money_CostPerKm_NullWhenNoKm()
    {
        var e = new[] { new Expense { Date = new DateOnly(2026, 1, 5), Amount = 100m } };
        Assert.Null(Money.For(Jan, e, "CZK", null).CostPerKm);
        Assert.Null(Money.For(Jan, e, "CZK", 0).CostPerKm);
    }

    [Fact]
    public void Money_FlowsIntoPeriodStats()
    {
        var data = new VehicleData(Array.Empty<Trip>(),
            new[] { new Expense { Date = new DateOnly(2026, 1, 5), Amount = 100m } },
            Array.Empty<TyreSet>(), "CZK", new DateOnly(2026, 6, 1));
        var s = AnalysisContext.Create(data, new AnalysisParameters()).Stats(Jan);
        Assert.Equal(100m, s.Money.Total);
    }
}
