using System.Text.Json;

namespace DriverBalance.Core.Tests;

public class TimelineTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);
    private static readonly DateOnly Today = new(2026, 11, 8);

    private static VehicleData FixtureData(bool withExpenses)
    {
        using var reader = new StreamReader(FixturePath("synthetic-trips.csv"));
        var trips = VolvoCsvParser.Parse(reader).Trips;
        var expenses = withExpenses ? LoadMaintenanceExpenses() : new List<Expense>();
        return new VehicleData(trips, expenses, Array.Empty<TyreSet>(), "CZK", Today);
    }

    // Reuses the legacy-JSON shape (see LegacyParityTests.LoadMaintenance), converted to Maintenance expenses.
    private static List<Expense> LoadMaintenanceExpenses()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath("synthetic-maintenance.json")));
        return doc.RootElement.EnumerateArray().Select(e => new Expense
        {
            Date = DateOnly.FromDateTime(e.GetProperty("Date").GetDateTime()),
            Category = ExpenseCategory.Maintenance,
            Maintenance = (MaintenanceType)e.GetProperty("Type").GetInt32(),
            Odometer = e.GetProperty("CurrentOdometer").GetInt32(),
        }).ToList();
    }

    private static OdometerReading R(int day, int km) =>
        new(new DateTime(2025, 1, day, 12, 0, 0), km, OdometerSource.Snapshot);

    [Fact]
    public void Timeline_IgnoresLowerReadings()
    {
        var t = OdometerTimeline.Build(new[] { R(1, 1000), R(2, 1200), R(3, 900), R(4, 1300) });
        Assert.Equal(new[] { 1000, 1200, 1300 }, t.Readings.Select(r => r.Km));
        Assert.Single(t.Dropped);
        Assert.Equal(900, t.Dropped[0].Km);
        Assert.Equal(1300, t.Latest);
        for (int d = 1; d <= 4; d++)
            for (int e = d; e <= 5; e++)
            {
                var km = t.KmIn(new Period(new DateOnly(2025, 1, d), new DateOnly(2025, 1, e + 1), "x"));
                Assert.True(km is null || km >= 0);
            }
        Assert.Equal(1200, t.AtEndOfDay(new DateOnly(2025, 1, 3)) );
    }

    [Fact]
    public void Timeline_UnknownStart_Null()
    {
        var t = OdometerTimeline.Build(Array.Empty<OdometerReading>());
        Assert.Null(t.Latest);
        Assert.Null(t.KmIn(new Period(new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), "x")));
        var t2 = OdometerTimeline.Build(new[] { R(10, 1000) });
        // no reading before or within the period, so the start is unknown
        Assert.Null(t2.KmIn(new Period(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5), "x")));
    }

    [Fact]
    public void Intervals_FixtureOilChange()
    {
        var data = FixtureData(true);
        var first = DateOnly.FromDateTime(data.Trips.Min(t => t.Start));
        Assert.Equal(new DateOnly(2025, 1, 6), first);
        var iv = Intervals.Build(MaintenanceType.OilChange, data.Expenses, first, Today);
        Assert.Equal(3, iv.Count);
        Assert.Equal("Current (since 2025-09-15)", iv[0].Label);
        Assert.Equal(4493, iv[0].StartOdometer);
        Assert.Equal(new DateOnly(2025, 3, 10), iv[1].From);
        Assert.Equal(new DateOnly(2025, 9, 15), iv[1].To);
        Assert.Equal("2025-03-10 → 2025-09-15", iv[1].Label);
        Assert.Equal(1864, iv[1].StartOdometer);
        Assert.Equal(4493, iv[1].EndOdometer);
        Assert.Equal("Before first OilChange", iv[2].Label);
        Assert.Equal(first, iv[2].From);
        Assert.Equal(1864, iv[2].EndOdometer);
        var ctx = AnalysisContext.Create(data, new AnalysisParameters());
        Assert.Equal(4493 - 1864, ctx.Stats(iv[1]).Usage.OdometerKm);
    }

    [Theory]
    [InlineData(2025, 3, 10)] // data starts on the first service day
    [InlineData(2025, 4, 1)]  // data starts after the first service
    public void Intervals_NoZeroDayBeforeFirst(int y, int m, int d)
    {
        var exps = new[]
        {
            new Expense { Date = new DateOnly(2025, 3, 10), Category = ExpenseCategory.Maintenance, Maintenance = MaintenanceType.OilChange, Odometer = 1864 },
        };
        var iv = Intervals.Build(MaintenanceType.OilChange, exps, new DateOnly(y, m, d), new DateOnly(2025, 6, 1));
        Assert.Single(iv);
        Assert.StartsWith("Current", iv[0].Label);
    }

    [Fact]
    public void Intervals_TripOnServiceDay_InNewInterval()
    {
        var day = new DateOnly(2025, 5, 10);
        var exps = new[]
        {
            new Expense { Date = day, Category = ExpenseCategory.Maintenance, Maintenance = MaintenanceType.OilChange, Odometer = 2000 },
            new Expense { Date = day, Category = ExpenseCategory.Maintenance, Maintenance = MaintenanceType.OilChange | MaintenanceType.BrakeFluid, Odometer = 2000 },
            // not a Maintenance expense, so it must not create a boundary even with the flag set
            new Expense { Date = new DateOnly(2025, 3, 1), Category = ExpenseCategory.Fuel, Maintenance = MaintenanceType.OilChange },
        };
        var today = new DateOnly(2025, 6, 1);
        var iv = Intervals.Build(MaintenanceType.OilChange, exps, new DateOnly(2025, 1, 1), today);
        Assert.Equal(2, iv.Count); // two events on one day make one boundary; the Fuel row adds none
        Assert.Equal(day, iv[1].To); // older interval is exclusive on the event date
        Assert.Equal(day, iv[0].From);

        static Trip T(DateTime start) =>
            new("Private", start, 0, "", start.AddMinutes(30), 0, "", TimeSpan.FromMinutes(30), 10, 1, "");
        var onDay = T(new DateTime(2025, 5, 10, 8, 0, 0));
        var dayBefore = T(new DateTime(2025, 5, 9, 18, 0, 0));
        var ctx = AnalysisContext.Create(
            new VehicleData(new[] { dayBefore, onDay }, exps, Array.Empty<TyreSet>(), "CZK", today), new AnalysisParameters());
        Assert.Equal(1, ctx.Stats(iv[0]).Usage.TripCount);
        Assert.Equal(1, ctx.Stats(iv[1]).Usage.TripCount);
        Assert.Equal(10, ctx.Stats(iv[0]).Usage.TripKm);
        Assert.Equal(10, ctx.Stats(iv[1]).Usage.TripKm);
    }

    [Fact]
    public void Calendar_MonthsSumToYear()
    {
        var ctx = AnalysisContext.Create(FixtureData(false), new AnalysisParameters());
        var year = ctx.Stats(CalendarPeriods.Year(2025));
        var months = CalendarPeriods.Months(2025).Select(ctx.Stats).ToList();
        Assert.Equal(12, months.Count);
        Assert.NotNull(year.Usage.OdometerKm);
        Assert.Equal(year.Usage.OdometerKm!.Value, months.Sum(m => m.Usage.OdometerKm ?? 0), 6);
        Assert.Equal(year.Usage.TripKm, months.Sum(m => m.Usage.TripKm), 6);
        Assert.Equal(year.Usage.ColdStarts, months.Sum(m => m.Usage.ColdStarts));
        Assert.InRange(year.Usage.TripCoverage!.Value, 0.9, 1.1);
    }

    [Fact]
    public void Calendar_WeeksClipped()
    {
        var ctx = AnalysisContext.Create(FixtureData(false), new AnalysisParameters());
        var weeks = CalendarPeriods.Weeks(2025, 3);
        Assert.Equal(new DateOnly(2025, 3, 1), weeks[0].From);
        Assert.Equal(new DateOnly(2025, 4, 1), weeks[^1].To);
        Assert.Equal("W09", weeks[0].Label);
        for (int i = 1; i < weeks.Count; i++) Assert.Equal(weeks[i - 1].To, weeks[i].From);
        var month = ctx.Stats(CalendarPeriods.Month(2025, 3));
        Assert.Equal(month.Usage.TripKm, weeks.Sum(w => ctx.Stats(w).Usage.TripKm), 6);
        Assert.Equal(month.Usage.OdometerKm!.Value, weeks.Sum(w => ctx.Stats(w).Usage.OdometerKm ?? 0), 6);
    }
}

public class HistoryStartGapTests
{
    private static OdometerTimeline T() => OdometerTimeline.Build(new[]
    {
        new OdometerReading(new DateTime(2023, 5, 1), 0, OdometerSource.HistoryStart),
        new OdometerReading(new DateTime(2025, 2, 6), 7350, OdometerSource.Expense),
        new OdometerReading(new DateTime(2025, 6, 1), 10000, OdometerSource.Expense),
    });

    private static Period P(DateOnly f, DateOnly t) => new(f, t, "p");

    [Fact]
    public void Gap_IsInterpolatedByDays()
    {
        var t = T();
        var y23 = t.KmIn(P(new(2023, 5, 1), new(2024, 1, 1)))!.Value;
        var y24 = t.KmIn(P(new(2024, 1, 1), new(2025, 1, 1)))!.Value;
        var y25gap = t.KmIn(P(new(2025, 1, 1), new(2025, 2, 6)))!.Value;
        Assert.Equal(7350.0 * 245 / 647, y23, 0);
        Assert.Equal(7350.0 * 366 / 647, y24, 0);
        Assert.Equal(7350.0 * 36 / 647, y25gap, 0);
        Assert.Equal(7350, y23 + y24 + y25gap, 0);
    }

    [Fact]
    public void PeriodSpanningGapEnd_InterpolatesOnlyItsStart()
    {
        var km = T().KmIn(P(new(2025, 1, 1), new(2026, 1, 1)))!.Value;
        Assert.Equal(10000 - 7350.0 * 245 / 647 - 7350.0 * 366 / 647, km, 0);
    }

    [Fact]
    public void PeriodAfterFirstRealReading_IsUnchanged() =>
        Assert.Equal(10000 - 7350, T().KmIn(P(new(2025, 3, 1), new(2026, 1, 1)))!.Value, 6);
}
