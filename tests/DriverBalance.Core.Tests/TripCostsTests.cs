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

    // Zero-length trips that only widen the pool clip range [first trip day, max(last trip day, Today)].
    private static Trip[] WithAnchors(Trip trip) =>
        [T(new DateTime(2025, 1, 1, 8, 0, 0), 0, 0, 0), trip, T(new DateTime(2027, 1, 1, 8, 0, 0), 0, 0, 0)];

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
        Assert.Equal(0m, ca.Fuel);
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
        var ctx = Ctx(WithAnchors(trip), E("2025-01-01", ExpenseCategory.Insurance, 3650m, from: "2025-01-01", to: "2034-12-31"));
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

    [Fact]
    public void AbsurdCoverageSpan_ClippedAndProRata()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ins = E("2026-06-01", ExpenseCategory.Insurance, 3_652_059m, from: "0001-01-01", to: "9999-12-31");
        int span = DateOnly.MaxValue.DayNumber - DateOnly.MinValue.DayNumber + 1;
        decimal expected = 3_652_059m / span * 365;
        Assert.InRange(Ctx(WithAnchors(trip), ins).Costs.Of(trip).Time, expected - 0.01m, expected + 0.01m);
    }

    [Fact]
    public void FuelWindow_InclusivePlusMinus45Days()
    {
        var trip = T(Jun1, 10, 60, 10);
        // Fuel at +45 days counts (price 20); at +46 it does not (no price in window, falls back to all data = same 20 here, so tell apart by a decoy).
        var decoy = E("2026-06-01", ExpenseCategory.Fuel, 1000m, 10); // price 100 in window
        var edgeIn = E("2026-07-16", ExpenseCategory.Fuel, 100m, 10); // +45 -> blended (1000+100)/20 = 55
        var edgeOut = E("2026-07-17", ExpenseCategory.Fuel, 100m, 10); // +46 -> ignored -> 100
        Assert.Equal(55m, Ctx([trip], decoy, edgeIn).Costs.Of(trip).PricePerLitre);
        Assert.Equal(100m, Ctx([trip], decoy, edgeOut).Costs.Of(trip).PricePerLitre);
        // minus side
        var inM = E("2026-04-17", ExpenseCategory.Fuel, 100m, 10); // -45
        var outM = E("2026-04-16", ExpenseCategory.Fuel, 100m, 10); // -46
        Assert.Equal(55m, Ctx([trip], decoy, inM).Costs.Of(trip).PricePerLitre);
        Assert.Equal(100m, Ctx([trip], decoy, outM).Costs.Of(trip).PricePerLitre);
    }

    [Theory]
    [InlineData("2026-11-30", 500)]  // +182 counts
    [InlineData("2026-12-01", 0)]    // +183 does not
    [InlineData("2025-12-01", 500)]  // -182 counts
    [InlineData("2025-11-30", 0)]    // -183 does not
    public void PoolWindow_InclusivePlusMinus182Days(string date, int expectedWear)
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx(WithAnchors(trip), E(date, ExpenseCategory.Repair, 500m));
        Assert.Equal((decimal)expectedWear, ctx.Costs.Of(trip).Wear);
    }

    [Fact]
    public void ForeignWithHomeAmount_UsesHomeAmount()
    {
        var trip = T(Jun1, 10, 60, 0);
        var ctx = Ctx([trip], E("2026-06-01", ExpenseCategory.Insurance, 100m, currency: "EUR", home: 2500m));
        Assert.Equal(2500m, ctx.Costs.Of(trip).Time);
    }

    [Fact]
    public void CoverageStartingInsideWindow_CountsOnlyOverlap()
    {
        var trip = T(Jun1, 10, 60, 0);
        // 1000 days at 1/day; window ends 2026-11-30, covered from 2026-11-01 -> 30 days inside.
        var ctx = Ctx(WithAnchors(trip), E("2026-11-01", ExpenseCategory.Insurance, 1000m, from: "2026-11-01", to: "2029-07-27"));
        Assert.InRange(ctx.Costs.Of(trip).Time, 29.99m, 30.01m);
    }

    [Fact]
    public void RecentTrips_NotChargedForPrepaidFuture()
    {
        var start = new DateOnly(2025, 1, 1);
        var today = new DateOnly(2026, 11, 8);
        var trips = new List<Trip>();
        for (var d = start; d <= today; d = d.AddDays(1)) trips.Add(T(d.ToDateTime(new TimeOnly(8, 0)), 30, 60, 0));
        var expenses = new List<Expense>();
        var from = new DateOnly(2024, 7, 1);
        while (from <= today)
        {
            var to = from.AddDays(364);
            expenses.Add(E(from.ToString("yyyy-MM-dd"), ExpenseCategory.Insurance, 3650m, from: from.ToString("yyyy-MM-dd"), to: to.ToString("yyyy-MM-dd")));
            from = to.AddDays(1);
        }
        var last = expenses[^1];
        Assert.Equal(new DateOnly(2027, 6, 30), last.CoversTo);
        var ctx = Ctx(trips, expenses.ToArray());

        decimal spend = 0;
        foreach (var e in expenses)
        {
            var days = e.CoversTo!.Value.DayNumber - e.CoversFrom!.Value.DayNumber + 1;
            var inside = Math.Min(e.CoversTo!.Value.DayNumber, today.DayNumber) - Math.Max(e.CoversFrom!.Value.DayNumber, start.DayNumber) + 1;
            if (inside > 0) spend += e.Amount * inside / days;
        }
        var total = trips.Sum(t => ctx.Costs.Of(t).Time);
        Assert.InRange(total, spend * 0.99m, spend * 1.01m);

        var mid = ctx.Costs.Of(trips.First(t => t.Start.Date == new DateTime(2025, 6, 15))).Time;
        var lastTime = ctx.Costs.Of(trips[^1]).Time;
        Assert.InRange(lastTime, mid * 0.95m, mid * 1.05m);
    }

    [Fact]
    public void ZeroLitreTrip_FuelZeroWithoutPrice()
    {
        var trip = T(Jun1, 40, 60, 0);
        var ctx = Ctx([trip], E("2026-06-01", ExpenseCategory.Insurance, 100m));
        var c = ctx.Costs.Of(trip);
        Assert.Equal(0m, c.Fuel);
        Assert.Null(c.PricePerLitre);
    }

    [Fact]
    public void BogusDate_IgnoredAndNormalTripUnchanged()
    {
        var normal = T(Jun1, 50, 60, 10);
        var bogus = T(new DateTime(1, 1, 1, 8, 0, 0), 50, 60, 10);
        var exp = new[]
        {
            E("2026-06-01", ExpenseCategory.Insurance, 1000m), E("2026-06-01", ExpenseCategory.Repair, 500m),
            E("2026-05-20", ExpenseCategory.Fuel, 400m, 10),
        };
        var alone = Ctx([normal], exp).Costs.Of(normal);
        var ctx = Ctx([bogus, normal], exp);
        Assert.Equal(alone, ctx.Costs.Of(normal));
        var b = ctx.Costs.Of(bogus);
        Assert.Equal(0m, b.Time);
        Assert.Equal(0m, b.Wear);
    }

    [Fact]
    public void HugeLitres_DoesNotThrow()
    {
        var trip = T(Jun1, 50, 60, 1e30);
        var ctx = Ctx([trip], E("2026-06-01", ExpenseCategory.Fuel, 400m, 10));
        var c = ctx.Costs.Of(trip);
        Assert.True(Math.Abs(c.Total) <= 1e16m);
    }
}
