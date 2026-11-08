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

public sealed record Overview(PeriodStats ThisMonth, PeriodStats ThisYear, DpfHealthSummary DpfHealth,
    IReadOnlyList<Expense> RecentExpenses, IReadOnlyList<Reminder> Reminders);

public sealed record TripPage(int Total, int Page, int PageSize, IReadOnlyList<TripGroup> Groups);

public sealed record TyreSummaryResponse(IReadOnlyList<TyreSetSummary> Summaries, IReadOnlyList<TyreMountSpan> UnassignedSpans);
