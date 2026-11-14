using DriverBalance.Core;

namespace DriverBalance.Web;

public sealed record PeriodInput(DateOnly From, DateOnly To, string? Label);

public sealed record StatsRequest(IReadOnlyList<PeriodInput>? Periods, AnalysisParameters? Parameters);

public sealed record CalendarRequest(string? Level, int? Year, int? Month, AnalysisParameters? Parameters);

public sealed record IntervalsRequest(MaintenanceType Type, AnalysisParameters? Parameters);

public sealed record ChartOptions(TrendLevel? Level, int? Year);

public sealed record ChartRequest(DateOnly? From, DateOnly? To, ChartOptions? Options, AnalysisParameters? Parameters);

public sealed record OverviewRequest(AnalysisParameters? Parameters);

public sealed record PeriodWithStats(Period Period, PeriodStats Stats);

public sealed record ScoreMapChart(IReadOnlyList<ScoreMapPoint> Points, ScoreZones Zones);

public sealed record Reminder(string Kind, string Message, string Severity);

public sealed record Overview(PeriodStats ThisMonth, PeriodStats ThisYear, DpfHealthSummary? DpfHealth,
    IReadOnlyList<Expense> RecentExpenses, IReadOnlyList<Reminder> Reminders);

public sealed record TripView(
    long Id, string Category, DateTime Start, int StartOdometer, string StartAddress, DateTime End, int EndOdometer,
    string EndAddress, TimeSpan Duration, double DistanceKm, double FuelLiters, string Notes, double AverageSpeedKmh,
    TripCost Cost)
{
    public static TripView From(Trip t, TripCosts costs) => new(t.Id, t.Category, t.Start, t.StartOdometer, t.StartAddress,
        t.End, t.EndOdometer, t.EndAddress, t.Duration, t.DistanceKm, t.FuelLiters, t.Notes, t.AverageSpeedKmh, costs.Of(t));
}

public sealed record TripGroupView(
    IReadOnlyList<TripView> Trips, DrivingScore Score, DateTime Start, DateTime End, double DistanceKm, TimeSpan Duration,
    double FuelLiters, double AverageSpeedKmh, double? LPer100, DateOnly StartDate, TripCost Cost)
{
    public static TripGroupView From(TripGroup g, TripCosts costs) => new(
        g.Trips.Select(t => TripView.From(t, costs)).ToList(), g.Score, g.Start, g.End, g.DistanceKm, g.Duration,
        g.FuelLiters, g.AverageSpeedKmh, g.LPer100, g.StartDate, costs.Of(g));
}

public sealed record TripPage(int Total, int Page, int PageSize, IReadOnlyList<TripGroupView> Groups);

public sealed record TyreSummaryResponse(IReadOnlyList<TyreSetSummary> Summaries, IReadOnlyList<TyreMountSpan> UnassignedSpans);
