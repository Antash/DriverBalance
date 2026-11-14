namespace DriverBalance.Core.Tests;

public class RegularRoutesTests
{
    private static readonly Period Y2026 = CalendarPeriods.Year(2026);

    /// <summary>One drive: a chain of trips through the given addresses (10 min each, 5 min apart, 5 km each).</summary>
    private static List<Trip> Drive(DateTime start, params string[] addresses)
    {
        var list = new List<Trip>();
        var t = start;
        for (int i = 0; i + 1 < addresses.Length; i++)
        {
            list.Add(new Trip("Private", t, 0, addresses[i], t.AddMinutes(10), 0, addresses[i + 1], TimeSpan.FromMinutes(10), 5, 0.4, ""));
            t = t.AddMinutes(15);
        }
        return list;
    }

    /// <summary>Drives on consecutive days from 2026-03-02 (a Monday) plus an offset, all at the given hour.</summary>
    private static List<Trip> Repeat(int count, int dayOffset, int hour, params string[] addresses)
    {
        var all = new List<Trip>();
        for (int i = 0; i < count; i++)
            all.AddRange(Drive(new DateTime(2026, 3, 2, hour, 10, 0).AddDays(dayOffset + i), addresses));
        return all;
    }

    private static AnalysisContext Ctx(IEnumerable<Trip> trips, params Expense[] expenses) =>
        AnalysisContext.Create(new VehicleData(trips.ToList(), expenses, [], "CZK", new DateOnly(2026, 11, 8)), new AnalysisParameters());

    private static Expense Insurance(decimal amount) =>
        new() { Date = new DateOnly(2025, 7, 1), Category = ExpenseCategory.Insurance, Amount = amount, Currency = "CZK" };

    [Fact]
    public void TripLevel_BothDirectionsAreOneRoute()
    {
        // 4 Home->Work (Mon-Thu mornings) and 2 Work->Home (Mon, Tue evenings).
        var trips = new List<Trip>();
        for (int i = 0; i < 4; i++) trips.AddRange(Drive(new DateTime(2026, 3, 2, 7, 10, 0).AddDays(i), "Home", "Work"));
        for (int i = 0; i < 2; i++) trips.AddRange(Drive(new DateTime(2026, 3, 2, 16, 20, 0).AddDays(i), "Work", "Home"));
        var r = Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Trip));
        Assert.Equal(6, r.Count);
        Assert.Equal("Home", r.From);
        Assert.Equal("Work", r.To);
        Assert.Empty(r.Via);
        Assert.False(r.RoundTrip);
        Assert.Equal("CZK", r.Currency);
    }

    [Fact]
    public void FewerThanFiveDrives_NotARoute()
    {
        var trips = Repeat(2, 0, 7, "A", "B").Concat(Repeat(2, 10, 16, "B", "A"));
        Assert.Empty(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Trip));
    }

    [Fact]
    public void BlankAddress_Skipped()
    {
        var ctx = Ctx(Repeat(6, 0, 7, "Home", ""));
        Assert.Empty(RegularRoutes.Build(ctx, Y2026, TrendLevel.Trip));
        Assert.Empty(RegularRoutes.Build(ctx, Y2026, TrendLevel.Group));
        foreach (var item in RegularRoutes.Items(ctx, Y2026, TrendLevel.Trip)) Assert.Null(RegularRoutes.KeyOf(item));
    }

    [Fact]
    public void GroupLevel_DirectAndViaAreSeparate()
    {
        var trips = Repeat(5, 0, 7, "Home", "School", "Work").Concat(Repeat(5, 10, 7, "Home", "Work")).ToList();
        var routes = RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Group);
        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Via.SequenceEqual(["School"]));
        Assert.Contains(routes, r => r.Via.Count == 0);
    }

    [Fact]
    public void GroupLevel_ReversedStopsMerge()
    {
        var trips = Repeat(3, 0, 7, "Home", "School", "Work").Concat(Repeat(2, 10, 16, "Work", "School", "Home")).ToList();
        var r = Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Group));
        Assert.Equal(5, r.Count);
        Assert.Equal("Home", r.From);
        Assert.Equal("Work", r.To);
        Assert.Equal(["School"], r.Via);
    }

    [Fact]
    public void RoundTrip_StopOrderIgnored()
    {
        var trips = Repeat(3, 0, 7, "Home", "Shop", "Mall", "Home").Concat(Repeat(2, 10, 7, "Home", "Mall", "Shop", "Home")).ToList();
        var r = Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Group));
        Assert.True(r.RoundTrip);
        Assert.Equal(5, r.Count);
        Assert.Equal("Home", r.From);
        Assert.Equal("Home", r.To);
        Assert.Equal(2, r.Via.Count);
    }

    [Fact]
    public void StopEqualToEndpoint_Dropped()
    {
        var ctx = Ctx(Repeat(1, 0, 7, "Home", "Work", "Work").Concat(Repeat(1, 10, 7, "Home", "Work")));
        var items = RegularRoutes.Items(ctx, Y2026, TrendLevel.Group);
        Assert.Equal(2, items.Count);
        var k0 = RegularRoutes.KeyOf(items[0]);
        Assert.NotNull(k0);
        Assert.Equal(k0, RegularRoutes.KeyOf(items[1]));
    }

    [Fact]
    public void KeyOf_IsOpaqueHexAndDirectionIndependent()
    {
        var ctx = Ctx(Repeat(1, 0, 7, "Home", "Work").Concat(Repeat(1, 10, 7, " work ", "HOME")));
        var items = RegularRoutes.Items(ctx, Y2026, TrendLevel.Group);
        var k = RegularRoutes.KeyOf(items[0])!;
        Assert.Equal(16, k.Length);
        Assert.Matches("^[0-9a-f]{16}$", k);
        Assert.Equal(k, RegularRoutes.KeyOf(items[1]));
    }

    private static string TypicalOf(IEnumerable<Trip> trips) =>
        Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Trip)).Typical;

    [Fact]
    public void Typical_Weekdays()
    {
        var trips = new List<Trip>();
        for (int i = 0; i < 5; i++)
        {
            trips.AddRange(Drive(new DateTime(2026, 3, 2, 7, 10, 0).AddDays(i), "Home", "Work"));
            trips.AddRange(Drive(new DateTime(2026, 3, 2, 16, 20, 0).AddDays(i), "Work", "Home"));
        }
        Assert.Equal("Weekdays · 7–8, 16–17", TypicalOf(trips));
    }

    [Fact]
    public void Typical_Weekends()
    {
        var trips = new List<Trip>();
        for (int i = 0; i < 3; i++)
        {
            trips.AddRange(Drive(new DateTime(2026, 3, 7, 10, 5, 0).AddDays(7 * i), "Home", "Lake"));
            trips.AddRange(Drive(new DateTime(2026, 3, 8, 10, 40, 0).AddDays(7 * i), "Home", "Lake"));
        }
        Assert.Equal("Weekends · 10–11", TypicalOf(trips));
    }

    [Fact]
    public void Typical_AnyDay()
    {
        // 10 drives, 5 on Saturdays and 5 on Mondays, each at a different hour.
        var trips = new List<Trip>();
        for (int i = 0; i < 10; i++)
            trips.AddRange(Drive(new DateTime(2026, 3, i % 2 == 0 ? 7 : 2, 8 + i, 5, 0).AddDays(i / 2 * 7), "Home", "Lake"));
        Assert.Equal("Any day", TypicalOf(trips));
    }

    [Fact]
    public void PerMonthAndShare()
    {
        var anchor = Drive(new DateTime(2025, 7, 1, 12, 0, 0), "X", "Y"); // first recorded trip
        var route = new List<Trip>();
        for (int i = 0; i < 6; i++) route.AddRange(Drive(new DateTime(2025, 8, 4, 7, 10, 0).AddDays(i), "Home", "Work"));
        var other = new List<Trip>();
        for (int i = 0; i < 4; i++) other.AddRange(Drive(new DateTime(2025, 9, 1, 9, 0, 0).AddDays(i * 2), "P" + i, "Q" + i));
        var ctx = Ctx(anchor.Concat(route).Concat(other), Insurance(5000m));
        var p = CalendarPeriods.Year(2025);

        var r = Assert.Single(RegularRoutes.Build(ctx, p, TrendLevel.Trip));
        var items = RegularRoutes.Items(ctx, p, TrendLevel.Trip);
        decimal routeTotal = items.Where(i => RegularRoutes.KeyOf(i) == r.Key).Sum(i => ctx.Costs.Of(i).Total);
        decimal all = items.Sum(i => ctx.Costs.Of(i).Total);
        Assert.True(routeTotal > 0);
        Assert.Equal(Money.Round(routeTotal / (184m / 30.44m)), r.PerMonth);
        Assert.InRange(r.Share, (double)(routeTotal / all) - 1e-9, (double)(routeTotal / all) + 1e-9);
        Assert.True(r.Share < 1);
        Assert.Equal(6, r.Count);
        Assert.Equal(5.0, r.AvgKm);
    }

    [Fact]
    public void PerMonth_TodayInsidePeriod_ClipsToToday()
    {
        var trips = Drive(new DateTime(2025, 7, 1, 12, 0, 0), "X", "Y")
            .Concat(Repeat(0, 0, 7, "Home", "Work")).ToList();
        for (int i = 0; i < 6; i++) trips.AddRange(Drive(new DateTime(2025, 8, 4, 7, 10, 0).AddDays(i), "Home", "Work"));
        var data = new VehicleData(trips, [Insurance(5000m)], [], "CZK", new DateOnly(2025, 10, 15));
        var ctx = AnalysisContext.Create(data, new AnalysisParameters());
        var p = CalendarPeriods.Year(2025);
        var r = Assert.Single(RegularRoutes.Build(ctx, p, TrendLevel.Trip));
        var routeTotal = RegularRoutes.Items(ctx, p, TrendLevel.Trip).Where(i => RegularRoutes.KeyOf(i) == r.Key).Sum(i => ctx.Costs.Of(i).Total);
        Assert.True(routeTotal > 0);
        Assert.Equal(Money.Round(routeTotal / (107m / 30.44m)), r.PerMonth);
    }

    [Fact]
    public void DisplayDirection_TieIsAlphabetical()
    {
        // 3 drives each way: equal starts from both ends, so the alphabetically first address is shown first.
        var trips = Repeat(3, 0, 7, "Work", "Home").Concat(Repeat(3, 10, 16, "Home", "Work")).ToList();
        var r = Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Trip));
        Assert.Equal("Home", r.From);
        Assert.Equal("Work", r.To);
    }

    [Fact]
    public void BlankIntermediateAddress_TreatedAsNoStop()
    {
        var withBlank = Repeat(3, 0, 7, "Home", "", "Work");
        var trips = withBlank.Concat(Repeat(2, 10, 7, "Home", "Work")).ToList();
        var r = Assert.Single(RegularRoutes.Build(Ctx(trips), Y2026, TrendLevel.Group));
        Assert.Equal(5, r.Count);
        Assert.Empty(r.Via);
    }

    [Fact]
    public void EmptyPeriod_NoRoutes()
    {
        var ctx = Ctx(Repeat(6, 0, 7, "Home", "Work"));
        var p = CalendarPeriods.Year(2030);
        Assert.Empty(RegularRoutes.Build(ctx, p, TrendLevel.Trip));
        Assert.Empty(RegularRoutes.Build(ctx, p, TrendLevel.Group));
    }

    [Fact]
    public void Items_TripLevelScoresEachTrip()
    {
        var ctx = Ctx(Drive(new DateTime(2026, 3, 2, 7, 10, 0), "Home", "School", "Work"));
        var items = RegularRoutes.Items(ctx, Y2026, TrendLevel.Trip);
        Assert.Equal(2, items.Count);
        foreach (var item in items)
        {
            var t = Assert.Single(item.Trips);
            Assert.Equal(Scoring.Score(t.Duration, t.AverageSpeedKmh, ctx.Parameters), item.Score);
        }
        Assert.Single(RegularRoutes.Items(ctx, Y2026, TrendLevel.Group));
    }
}
