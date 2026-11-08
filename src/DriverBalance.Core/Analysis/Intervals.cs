namespace DriverBalance.Core;

public static class Intervals
{
    /// <summary>Service intervals for one maintenance type, newest first: Current, ..., "Before first &lt;type&gt;".</summary>
    public static IReadOnlyList<Period> Build(MaintenanceType type, IEnumerable<Expense> expenses, DateOnly? dataStart, DateOnly today)
    {
        var events = expenses
            .Where(e => e.Category == ExpenseCategory.Maintenance && e.Maintenance.HasFlag(type))
            .GroupBy(e => e.Date)
            .Select(g => (Date: g.Key, Odo: g.Where(e => e.Odometer is > 0).Select(e => (int?)e.Odometer).Max()))
            .OrderByDescending(e => e.Date)
            .ToList();

        var result = new List<Period>();
        var end = today.AddDays(1);
        if (events.Count == 0)
        {
            result.Add(new Period(dataStart ?? today, end, $"Before first {type}"));
            return result;
        }

        result.Add(new Period(events[0].Date, end, $"Current (since {events[0].Date:yyyy-MM-dd})", events[0].Odo));
        for (int i = 1; i < events.Count; i++)
        {
            var older = events[i];
            var newer = events[i - 1];
            result.Add(new Period(older.Date, newer.Date, $"{older.Date:yyyy-MM-dd} → {newer.Date:yyyy-MM-dd}", older.Odo, newer.Odo));
        }
        var firstEvent = events[^1];
        var from = dataStart is { } ds && ds < firstEvent.Date ? ds : firstEvent.Date;
        // Data starting on or after the first service leaves no "before" period to show.
        if (from < firstEvent.Date)
            result.Add(new Period(from, firstEvent.Date, $"Before first {type}", null, firstEvent.Odo));
        return result;
    }
}
