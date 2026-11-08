using System.Globalization;

namespace DriverBalance.Core;

public sealed record MonthlyCost(string Month, IReadOnlyDictionary<ExpenseCategory, decimal> ByCategory);

public sealed record CumulativeCostPoint(DateOnly Date, decimal CumulativeCost, double? CumulativeKm, decimal? CostPerKm);

public static class MoneyCharts
{
    /// <summary>One entry per calendar month touched by the period; each month is clipped to the period.</summary>
    public static IReadOnlyList<MonthlyCost> Monthly(AnalysisContext c, Period p)
    {
        var result = new List<MonthlyCost>();
        if (p.To <= p.From) return result;
        var month = new DateOnly(p.From.Year, p.From.Month, 1);
        while (month < p.To)
        {
            var from = month < p.From ? p.From : month;
            var next = month.AddMonths(1);
            var to = next > p.To ? p.To : next;
            var stats = Money.For(new Period(from, to, ""), c.Data.Expenses, c.Data.HomeCurrency, null);
            result.Add(new MonthlyCost(month.ToString("yyyy-MM", CultureInfo.InvariantCulture), stats.ByCategory));
            month = next;
        }
        return result;
    }

    public static IReadOnlyDictionary<ExpenseCategory, decimal> Totals(AnalysisContext c, Period p) =>
        Money.For(p, c.Data.Expenses, c.Data.HomeCurrency, null).ByCategory;

    /// <summary>Running total by expense date (no amortization); expenses without a home amount are left out.</summary>
    public static IReadOnlyList<CumulativeCostPoint> Cumulative(AnalysisContext c)
    {
        var result = new List<CumulativeCostPoint>();
        int? firstKm = c.Timeline.Readings.Count > 0 ? c.Timeline.Readings[0].Km : null;
        decimal running = 0m;
        var byDate = c.Data.Expenses
            .Select(e => (e.Date, Amount: Money.HomeAmountOf(e, c.Data.HomeCurrency)))
            .Where(x => x.Amount is not null)
            .GroupBy(x => x.Date)
            .OrderBy(g => g.Key);
        foreach (var g in byDate)
        {
            running += g.Sum(x => x.Amount!.Value);
            int? odo = c.Timeline.AtEndOfDay(g.Key);
            double? km = odo is { } o && firstKm is { } f ? o - f : null;
            result.Add(new CumulativeCostPoint(g.Key, Money.Round(running), km, km is > 0 ? Money.Round(running / (decimal)km.Value) : null));
        }
        return result;
    }
}
