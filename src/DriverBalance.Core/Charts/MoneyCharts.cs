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

    /// <summary>Below this many km driven in the period, cost per km is too noisy to show.</summary>
    public const double MinKmForCostPerKm = 200;

    /// <summary>
    /// Running cost and cost per km within the period, one point per week from its start plus its last day. Costs use the
    /// same spreading as every other money figure (covered periods amortised, purchase costs excluded), so the line is smooth.
    /// </summary>
    public static IReadOnlyList<CumulativeCostPoint> Cumulative(AnalysisContext c, Period p)
    {
        var result = new List<CumulativeCostPoint>();
        var last = (p.To <= c.Data.Today.AddDays(1) ? p.To : c.Data.Today.AddDays(1)).AddDays(-1);
        if (last < p.From) return result;
        var days = new List<DateOnly>();
        for (var d = p.From.AddDays(6); d < last; d = d.AddDays(7)) days.Add(d);
        days.Add(last);
        foreach (var d in days)
        {
            var upTo = new Period(p.From, d.AddDays(1), "");
            var cost = Money.For(upTo, c.Data.Expenses, c.Data.HomeCurrency, null).Total;
            var km = c.Timeline.KmIn(upTo);
            decimal? perKm = km is >= MinKmForCostPerKm ? Money.Round(cost / (decimal)km.Value) : null;
            result.Add(new CumulativeCostPoint(d, cost, km, perKm));
        }
        return result;
    }
}
