namespace DriverBalance.Core.Tests;

public class TripCostsTests
{
    private static Trip T(DateTime start, double km, int minutes, double litres) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, litres, "");

    private static Expense E(string date, ExpenseCategory cat, decimal amount, double? litres = null,
        string? from = null, string? to = null, string currency = "CZK", decimal? home = null) => new()
    {
        Date = DateOnly.Parse(date), Category = cat, Amount = amount, Litres = litres, Currency = currency, HomeAmount = home,
        CoversFrom = from is null ? null : DateOnly.Parse(from), CoversTo = to is null ? null : DateOnly.Parse(to),
    };

    private static AnalysisContext Ctx(IReadOnlyList<Trip> trips, params Expense[] expenses) =>
        AnalysisContext.Create(new VehicleData(trips, expenses, [], "CZK", new DateOnly(2026, 11, 8)), new AnalysisParameters());

    private static readonly DateTime Jun1 = new(2026, 6, 1, 8, 0, 0);

    [Fact]
    public void WorkedExample_MatchesSpec()
    {
        var a = T(Jun1, 15, 30, 0);
        var b = T(Jun1.AddHours(2), 100, 30, 0);
        var filler = T(Jun1.AddHours(4), 14_885, 299 * 60, 0);
        var ctx = Ctx([a, b, filler],
            E("2026-06-01", ExpenseCategory.Insurance, 20_000m),
            E("2026-06-01", ExpenseCategory.Maintenance, 10_000m));
        var ca = ctx.Costs.Of(a);
        var cb = ctx.Costs.Of(b);
        Assert.Equal(33.33m, ca.Time);
        Assert.Equal(13.33m, ca.Wear);
        Assert.Equal(46.67m, ca.Total);
        Assert.Equal(41.67m, cb.Wear);
        Assert.Equal(75.00m, cb.Total);
        Assert.Null(ca.Fuel);
        Assert.Equal("CZK", ca.Currency);
    }

    [Fact]
    public void FuelPrice_FromLitresBearingExpenses()
    {
        var trip = T(new DateTime(2026, 3, 5, 8, 0, 0), 50, 60, 10);
        var ctx = Ctx([trip],
            E("2026-03-01", ExpenseCategory.Fuel, 1000m, 25),
            E("2026-03-10", ExpenseCategory.Fuel, 600m));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(40.00m, c.PricePerLitre);
        Assert.Equal(400.00m, c.Fuel);
    }

    [Fact]
    public void FuelPrice_FallsBackToSpendOverTripLitres()
    {
        var t1 = T(new DateTime(2026, 3, 5, 8, 0, 0), 50, 60, 20);
        var t2 = T(new DateTime(2026, 3, 6, 8, 0, 0), 30, 40, 10);
        var ctx = Ctx([t1, t2], E("2026-03-01", ExpenseCategory.Fuel, 900m));
        var c = ctx.Costs.Of(t2);
        Assert.Equal(30.00m, c.PricePerLitre);
        Assert.Equal(300.00m, c.Fuel);
    }

    [Fact]
    public void FuelPrice_FallsBackToAllData()
    {
        var trip = T(Jun1, 50, 60, 10);
        var ctx = Ctx([trip], E("2025-01-01", ExpenseCategory.Fuel, 500m, 10));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(50.00m, c.PricePerLitre);
        Assert.Equal(500.00m, c.Fuel);
    }

    [Fact]
    public void NoFuelData_FuelNullAndTotalIsTimePlusWear()
    {
        var trip = T(Jun1, 100, 60, 8);
        var ctx = Ctx([trip],
            E("2026-06-01", ExpenseCategory.Insurance, 1000m),
            E("2026-06-01", ExpenseCategory.Repair, 500m));
        var c = ctx.Costs.Of(trip);
        Assert.Null(c.Fuel);
        Assert.Null(c.PricePerLitre);
        Assert.Equal(1000m, c.Time);
        Assert.Equal(500m, c.Wear);
        Assert.Equal(1500m, c.Total);
    }

    [Fact]
    public void CoveragePeriod_CountedProRata()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx([trip], E("2025-01-01", ExpenseCategory.Insurance, 3650m, from: "2025-01-01", to: "2034-12-31"));
        Assert.InRange(ctx.Costs.Of(trip).Time, 364.79m, 364.81m);
    }

    [Fact]
    public void PurchaseAndFuel_NotInPools()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx([trip],
            E("2026-06-01", ExpenseCategory.Purchase, 500_000m),
            E("2026-06-01", ExpenseCategory.Fuel, 1000m));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(0m, c.Time);
        Assert.Equal(0m, c.Wear);
    }

    [Fact]
    public void ForeignWithoutHomeAmount_Skipped()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx([trip], E("2026-06-01", ExpenseCategory.Insurance, 100m, currency: "EUR"));
        Assert.Equal(0m, ctx.Costs.Of(trip).Time);
    }

    [Fact]
    public void ZeroDurationAndZeroKm_NoDivideByZero()
    {
        var trip = T(Jun1, 0, 0, 0);
        var ctx = Ctx([trip], E("2026-06-01", ExpenseCategory.Insurance, 1000m));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(0m, c.Time);
        Assert.Equal(0m, c.Wear);
        Assert.Equal(0m, c.Total);
    }

    [Fact]
    public void ExpenseOutsideTripRange_NoIndexError()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx([trip],
            E("2024-01-01", ExpenseCategory.Tyres, 2000m),
            E("2026-12-01", ExpenseCategory.Insurance, 1000m, from: "2027-01-01", to: "2027-12-31"));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(0m, c.Wear);
        Assert.Equal(0m, c.Time);
    }

    [Fact]
    public void GroupCost_IsSumOfTrips()
    {
        var t1 = T(Jun1, 10, 20, 1);
        var t2 = T(Jun1.AddMinutes(30), 20, 30, 2);
        var ctx = Ctx([t1, t2],
            E("2026-06-01", ExpenseCategory.Insurance, 1000m),
            E("2026-06-01", ExpenseCategory.Maintenance, 700m),
            E("2026-06-01", ExpenseCategory.Fuel, 90m, 3));
        var g = ctx.Groups.Single();
        Assert.Equal(2, g.Trips.Count);
        var gc = ctx.Costs.Of(g);
        var sum = ctx.Costs.Of(t1).Total + ctx.Costs.Of(t2).Total;
        Assert.InRange(gc.Total, sum - 0.02m, sum + 0.02m);
        Assert.Equal(90m, gc.Fuel);
        Assert.Null(gc.PricePerLitre);
    }
}
