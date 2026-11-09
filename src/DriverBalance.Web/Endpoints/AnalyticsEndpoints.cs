using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class AnalyticsEndpoints
{
    private const int ExportAlarmDays = 300;

    private static readonly HashSet<string> ChartNames =
    [
        "monthly-score", "heatmap", "fuel-trend", "score-map", "gap-histogram", "when-you-drive", "penalty-curve",
        "seasonality", "places", "routes", "dpf-health", "money-monthly", "money-totals", "money-cumulative", "timeline",
    ];

    public static void Map(RouteGroupBuilder api)
    {
        var a = api.MapGroup("/vehicles/{vid:long}/analytics");
        a.MapPost("/stats", Stats);
        a.MapPost("/calendar", Calendar);
        a.MapPost("/intervals", IntervalStats);
        a.MapPost("/charts/{name}", Chart);
        a.MapPost("/overview", GetOverview);
    }

    private static async Task<IResult> Stats(long vid, StatsRequest req, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, req.Parameters, ct);
        var periods = (req.Periods ?? []).Select(p => s.Range(p.From, p.To, p.Label ?? $"{p.From:yyyy-MM-dd}..{p.To:yyyy-MM-dd}"));
        return Results.Ok(periods.Select(p => s.Context.Stats(p)).ToList());
    }

    private static async Task<IResult> Calendar(long vid, CalendarRequest req, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, req.Parameters, ct);
        int year = req.Year ?? s.Today.Year;
        int month = req.Month ?? s.Today.Month;
        if (year is < 1 or > 9998) throw new ApiValidationException("Year is out of range.");
        if (month is < 1 or > 12) throw new ApiValidationException("Month must be 1-12.");

        IReadOnlyList<Period> periods = req.Level?.ToLowerInvariant() switch
        {
            "years" => CalendarPeriods.Years(s.DataStart ?? s.Today, s.Today),
            "months" => CalendarPeriods.Months(year),
            "weeks" => CalendarPeriods.Weeks(year, month),
            _ => throw new ApiValidationException("Level must be one of: years, months, weeks."),
        };
        return Results.Ok(WithStats(s, periods));
    }

    private static async Task<IResult> IntervalStats(long vid, IntervalsRequest req, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, req.Parameters, ct);
        var periods = Intervals.Build(req.Type, s.Data.Expenses, s.DataStart, s.Today, s.DataStartOdometer);
        return Results.Ok(WithStats(s, periods));
    }

    private static List<PeriodWithStats> WithStats(AnalysisSession s, IEnumerable<Period> periods) =>
        periods.Select(p => new PeriodWithStats(p, s.Context.Stats(p))).ToList();

    private static async Task<IResult> Chart(long vid, string name, ChartRequest req, AnalysisSessionFactory sessions,
        DriverBalanceDbContext db, CancellationToken ct)
    {
        if (!ChartNames.Contains(name)) return Results.NotFound();
        var s = await sessions.OpenAsync(vid, req.Parameters, ct);
        var c = s.Context;
        var p = s.Range(req.From, req.To);
        var options = req.Options;

        object chart = name switch
        {
            "monthly-score" => ScoreCharts.Monthly(c, p),
            "heatmap" => ScoreCharts.Heatmap(c, options?.Year ?? p.To.AddDays(-1).Year),
            "fuel-trend" => FuelCharts.Trend(c, p, options?.Level ?? TrendLevel.Group),
            "score-map" => new ScoreMapChart(ScoreCharts.ScoreMap(c, p, options?.Level ?? TrendLevel.Group), ScoreZones.From(s.Parameters)),
            "gap-histogram" => HabitCharts.Gaps(c, p),
            "when-you-drive" => HabitCharts.WhenYouDrive(c, p),
            "penalty-curve" => FuelCharts.PenaltyCurve(c, p),
            "seasonality" => FuelCharts.Seasonality(c),
            "places" => PlacesAnalysis.Places(c, p),
            "routes" => PlacesAnalysis.Routes(c, p),
            "dpf-health" => DpfHealth.Compute(c),
            "money-monthly" => MoneyCharts.Monthly(c, p),
            "money-totals" => MoneyCharts.Totals(c, p),
            "money-cumulative" => MoneyCharts.Cumulative(c, p),
            _ => await TimelineWithImports(s, p, db, ct),
        };
        return Results.Ok(chart);
    }

    /// <summary>Core knows nothing of import batches, so they are appended here as Import events.</summary>
    private static async Task<TimelineChart> TimelineWithImports(AnalysisSession s, Period p, DriverBalanceDbContext db, CancellationToken ct)
    {
        var chart = TimelineBuilder.Build(s.Context, p);
        var tz = VehicleDataLoader.ResolveTimeZone(s.Vehicle.TimeZone);
        var batches = await db.ImportBatches.AsNoTracking().Where(b => b.VehicleId == s.Vehicle.Id && b.Imported > 0).ToListAsync(ct);

        var imports = batches
            .Select(b => (Batch: b, Date: DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(b.ImportedAt, DateTimeKind.Utc), tz))))
            .Where(x => p.Contains(x.Date))
            .Select(x => new TimelineEvent(x.Date, TimelineEventKind.Import, $"Imported {x.Batch.Imported} trips", null, null, x.Batch.Id));

        var events = chart.Events.Concat(imports).OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList();
        return chart with { Events = events };
    }

    private static async Task<IResult> GetOverview(long vid, OverviewRequest req, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, req.Parameters, ct);
        var recent = s.Data.Expenses.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Take(5).ToList();
        return Results.Ok(new Overview(
            s.Context.Stats(CalendarPeriods.Month(s.Today.Year, s.Today.Month)),
            s.Context.Stats(CalendarPeriods.Year(s.Today.Year)),
            FuelTypes.HasDpf(s.Vehicle.FuelType) ? DpfHealth.Compute(s.Context) : null,
            recent,
            Reminders(s)));
    }

    private static List<Reminder> Reminders(AnalysisSession s)
    {
        var reminders = new List<Reminder>();

        if (s.Data.Trips.Count > 0)
        {
            var lastTrip = DateOnly.FromDateTime(s.Data.Trips.Max(t => t.Start));
            int days = s.Today.DayNumber - lastTrip.DayNumber;
            if (days > ExportAlarmDays)
                reminders.Add(new Reminder("export", $"Last trip is {days} days old; Volvo deletes trips after 400 days, so export now.", "alarm"));
            else if (days > s.Parameters.ExportReminderDays)
                reminders.Add(new Reminder("export", $"Last trip is {days} days old; time to export new trips.", "warn"));
        }

        int needsConversion = s.Data.Expenses.Count(e => Money.HomeAmountOf(e, s.Data.HomeCurrency) is null);
        if (needsConversion > 0)
            reminders.Add(new Reminder("needs-conversion", $"{needsConversion} expenses have no {s.Data.HomeCurrency} amount.", "info"));

        int unassigned = s.Data.Expenses.Count(e => TyreMileage.IsMount(e) && e.TyreSetId is null);
        if (unassigned > 0)
            reminders.Add(new Reminder("unassigned-tyres", $"{unassigned} tyre swaps have no tyre set assigned.", "info"));

        return reminders;
    }
}
