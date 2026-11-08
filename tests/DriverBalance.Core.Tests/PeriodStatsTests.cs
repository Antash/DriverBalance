using System.Text.Json;

namespace DriverBalance.Core.Tests;

public class PeriodStatsTests
{
    private static readonly AnalysisParameters P = new();
    private static readonly DateOnly Today = new(2025, 6, 1);

    private static Trip T(DateTime start, int minutes, double km, double fuel = 0) =>
        new("Private", start, 0, "", start.AddMinutes(minutes), 0, "", TimeSpan.FromMinutes(minutes), km, fuel, "");

    private static AnalysisContext Ctx(params Trip[] trips) =>
        AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", Today), P);

    private static Period Year => new(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), "2025");

    [Fact]
    public void Stats_EmptyVehicle_NoNaN()
    {
        var stats = Ctx().Stats(Year);
        var json = JsonSerializer.Serialize(stats); // throws on NaN/Infinity
        Assert.NotEmpty(json);
        Assert.Null(stats.Usage.OdometerKm);
        Assert.Null(stats.Usage.TripCoverage);
        Assert.Null(stats.Usage.AverageSpeedKmh);
        Assert.Null(stats.Usage.ColdStartsPer100Km);
        Assert.Null(stats.Usage.ShortShareByCount);
        Assert.Null(stats.Usage.ShortShareByKm);
        Assert.Null(stats.Scores.DpfIndex);
        Assert.Null(stats.Fuel.TripLPer100);
        Assert.Null(stats.Fuel.FullTankLPer100);
        Assert.Null(stats.Money.CostPerKm);
        Assert.Equal(0, stats.Money.Total);
        Assert.Empty(stats.Money.ByCategory);
        Assert.Equal(0, stats.Money.NeedsConversionCount);
        Assert.Equal(4, stats.Scores.Buckets.Count);
    }

    [Fact]
    public void Stats_ZeroDayPeriod_KmPerDayNull()
    {
        var d = new DateOnly(2025, 1, 1);
        var stats = Ctx().Stats(new Period(d, d, "empty"));
        Assert.Equal(0, stats.Usage.Days);
        Assert.Null(stats.Usage.KmPerDay);
        JsonSerializer.Serialize(stats);
    }

    [Fact]
    public void Short_IsGroupDurationBelowShortLimit()
    {
        var d = new DateTime(2025, 2, 1, 8, 0, 0);
        var ctx = Ctx(
            T(d, 19, 5),                       // short group (19 < 20)
            T(d.AddHours(5), 20, 10),          // exactly 20 min: not short
            T(d.AddHours(10), 10, 3),          // two trips, 10+10 = 20 min total: not short
            T(d.AddHours(10).AddMinutes(20), 10, 3));
        var s = ctx.Stats(Year);
        Assert.Equal(3, s.Usage.ColdStarts);
        Assert.Equal(1, s.Usage.ShortGroups);
        Assert.Equal(1.0 / 3, s.Usage.ShortShareByCount!.Value, 9);
        Assert.Equal(5.0 / 21, s.Usage.ShortShareByKm!.Value, 9);
        Assert.Equal(4, s.Usage.TripCount);
        Assert.Equal(21, s.Usage.TripKm);
    }

    [Fact]
    public void Mix_ClassifiesGroupsByAverageSpeed()
    {
        var d = new DateTime(2025, 2, 1, 8, 0, 0);
        var ctx = Ctx(
            T(d, 60, 34.9),               // city (< 35)
            T(d.AddHours(5), 60, 35),     // mixed (35 <= v < 50)
            T(d.AddHours(10), 60, 49.9),  // mixed
            T(d.AddHours(15), 60, 50));   // highway
        var mix = ctx.Stats(Year).Mix;
        Assert.Equal(34.9, mix.CityKm, 9);
        Assert.Equal(84.9, mix.MixedKm, 9);
        Assert.Equal(50, mix.HighwayKm, 9);
    }

    [Fact]
    public void GroupsIn_UsesGroupStartDate_HalfOpen()
    {
        // Group starts 23:30 on Mar 31, ends after midnight: belongs to March.
        var late = new DateTime(2025, 3, 31, 23, 30, 0);
        var ctx = Ctx(T(late, 40, 50), T(new DateTime(2025, 4, 1, 12, 0, 0), 30, 20));
        var march = new Period(new DateOnly(2025, 3, 1), new DateOnly(2025, 4, 1), "Mar");
        var april = new Period(new DateOnly(2025, 4, 1), new DateOnly(2025, 5, 1), "Apr");
        Assert.Single(ctx.GroupsIn(march));
        Assert.Equal(50, ctx.Stats(march).Usage.TripKm);
        Assert.Equal(20, ctx.Stats(april).Usage.TripKm);
        Assert.Equal(31, march.Days);
        Assert.Equal(50.0 / 31, ctx.Stats(march).Usage.KmPerDay!.Value, 9);
    }

    [Fact]
    public void KmPerDay_UsesElapsedDaysOnly()
    {
        // Today is mid-June; the June period has 30 days but only 10 have elapsed (Jun 1..Jun 10 inclusive).
        var today = new DateOnly(2025, 6, 10);
        var trips = new[] { T(new DateTime(2025, 6, 2, 8, 0, 0), 30, 100) };
        var ctx = AnalysisContext.Create(new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", today), P);
        var june = new Period(new DateOnly(2025, 6, 1), new DateOnly(2025, 7, 1), "Jun");
        var u = ctx.Stats(june).Usage;
        Assert.Equal(30, u.Days);
        Assert.Equal(10, u.ElapsedDays);
        Assert.Equal(10.0, u.KmPerDay!.Value, 9);

        var future = new Period(new DateOnly(2025, 7, 1), new DateOnly(2025, 8, 1), "Jul");
        Assert.Null(ctx.Stats(future).Usage.KmPerDay);
        Assert.Equal(0, ctx.Stats(future).Usage.ElapsedDays);
    }

    [Fact]
    public void Dpf_IsGoodPlusExcellentKmOverAllKm()
    {
        var d = new DateTime(2025, 2, 1, 8, 0, 0);
        var ctx = Ctx(
            T(d, 60, 90),             // 90 km/h, Excellent
            T(d.AddHours(5), 10, 2)); // Bad
        var s = ctx.Stats(Year);
        Assert.Equal(90.0 / 92, s.Scores.DpfIndex!.Value, 9);
        Assert.Equal(90, s.Scores.Buckets[DrivingScore.Excellent].Km);
        Assert.Equal(1, s.Scores.Buckets[DrivingScore.Bad].Groups);
    }
}
