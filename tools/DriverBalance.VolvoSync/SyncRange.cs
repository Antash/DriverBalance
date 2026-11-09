namespace DriverBalance.VolvoSync;

internal readonly record struct DateRange(DateOnly From, DateOnly To);

internal static class SyncRange
{
    public const int OverlapDays = 2;
    public const int FallbackDays = 90;

    /// <summary>From = explicit, else last trip minus 2 days, else today minus 90 days; never after today. To = today.</summary>
    public static DateRange Compute(DateOnly? explicitFrom, DateOnly? lastTrip, DateOnly today)
    {
        var from = explicitFrom
            ?? (lastTrip is { } l ? l.AddDays(-OverlapDays) : today.AddDays(-FallbackDays));
        if (from > today) from = today;
        return new DateRange(from, today);
    }
}

internal static class ExportNaming
{
    public static string ExportFileName(DateOnly from, DateOnly to) => $"volvo-journal-{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv";
    public static string FailureScreenshotName(DateTime at) => $"failure-{at:yyyyMMdd-HHmmss}.png";
}
