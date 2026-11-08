using System.Globalization;

namespace DriverBalance.Core;

public static class CalendarPeriods
{
    public static Period Year(int y) => new(new DateOnly(y, 1, 1), new DateOnly(y + 1, 1, 1), y.ToString(CultureInfo.InvariantCulture));

    public static Period Month(int y, int m)
    {
        var from = new DateOnly(y, m, 1);
        return new Period(from, from.AddMonths(1), from.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }

    public static IReadOnlyList<Period> Years(DateOnly first, DateOnly last) =>
        Enumerable.Range(first.Year, Math.Max(0, last.Year - first.Year + 1)).Select(Year).ToList();

    public static IReadOnlyList<Period> Months(int year) => Enumerable.Range(1, 12).Select(m => Month(year, m)).ToList();

    public static IReadOnlyList<Period> Weeks(int year, int month)
    {
        var m = Month(year, month);
        var result = new List<Period>();
        var from = m.From;
        while (from < m.To)
        {
            int daysToNextMonday = 7 - ((int)from.DayOfWeek + 6) % 7;
            var to = from.AddDays(daysToNextMonday);
            if (to > m.To) to = m.To;
            int week = ISOWeek.GetWeekOfYear(from.ToDateTime(TimeOnly.MinValue));
            result.Add(new Period(from, to, $"W{week:00}"));
            from = to;
        }
        return result;
    }
}
