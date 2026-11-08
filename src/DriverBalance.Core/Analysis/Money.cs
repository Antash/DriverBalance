namespace DriverBalance.Core;

public static class Money
{
    /// <summary>Amount in the home currency; null when the expense is in another currency without a home amount.</summary>
    public static decimal? HomeAmountOf(Expense e, string homeCurrency) =>
        string.Equals(e.Currency, homeCurrency, StringComparison.OrdinalIgnoreCase) ? e.Amount : e.HomeAmount;

    /// <summary>Fraction (days in period, days covered) of the expense that falls in the period.</summary>
    private static (int Part, int Whole) Share(Expense e, Period p)
    {
        if (e.CoversFrom is { } from && e.CoversTo is { } to && to >= from)
        {
            int total = to.DayNumber - from.DayNumber + 1;
            int overlap = Math.Min(to.DayNumber + 1, p.To.DayNumber) - Math.Max(from.DayNumber, p.From.DayNumber);
            return (Math.Max(overlap, 0), total);
        }
        return (p.Contains(e.Date) ? 1 : 0, 1);
    }

    public static MoneyStats For(Period p, IEnumerable<Expense> expenses, string homeCurrency, double? km)
    {
        var byCategory = new Dictionary<ExpenseCategory, decimal>();
        decimal total = 0m;
        int needsConversion = 0;
        foreach (var e in expenses)
        {
            var (part, whole) = Share(e, p);
            if (part == 0) continue;
            if (HomeAmountOf(e, homeCurrency) is not { } amount) { needsConversion++; continue; }
            var portion = amount * part / whole;
            total += portion;
            byCategory[e.Category] = byCategory.GetValueOrDefault(e.Category) + portion;
        }
        decimal? perKm = km is > 0 ? total / (decimal)km.Value : null;
        return new MoneyStats(total, byCategory, perKm, needsConversion);
    }
}
