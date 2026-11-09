namespace DriverBalance.Core;

public sealed record TyreMountSpan(long? TyreSetId, DateOnly From, DateOnly? To, int? FromOdometer, int? ToOdometer, double? Km);

public sealed record TyreSetSummary(TyreSet Set, double TotalKm, int Seasons, decimal? PurchaseCost, decimal? CostPer1000Km, bool Mounted, IReadOnlyList<TyreMountSpan> Spans);

public static class TyreMileage
{
    public const MaintenanceType MountFlags = MaintenanceType.TyreSwap | MaintenanceType.NewTyres;

    public static bool IsMount(Expense e) => e.Category == ExpenseCategory.Maintenance && (e.Maintenance & MountFlags) != 0;

    /// <summary>A span per mount, each running to the next mount; the last one is open (To = null) and runs to the latest reading.</summary>
    public static IReadOnlyList<TyreMountSpan> Spans(IEnumerable<Expense> expenses, long? initialSetId, OdometerTimeline t, DateOnly today)
    {
        var mounts = expenses.Where(IsMount).OrderBy(e => e.Date).ThenBy(e => e.Id).ToList();

        // (set, start date, start odometer) for each span, in order.
        var starts = new List<(long? Set, DateOnly Date, int? Odo)>();
        if (initialSetId is not null && t.Readings.Count > 0)
            starts.Add((initialSetId, DateOnly.FromDateTime(t.Readings[0].At), t.Readings[0].Km));
        foreach (var m in mounts)
            starts.Add((m.TyreSetId, m.Date, m.Odometer is > 0 ? m.Odometer : t.AtEndOfDay(m.Date, ignoreHistoryStart: true)));

        var spans = new List<TyreMountSpan>();
        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            bool last = i == starts.Count - 1;
            DateOnly? to = last ? null : starts[i + 1].Date;
            int? toOdo = last ? t.Latest : starts[i + 1].Odo;
            double? km = s.Odo is { } a && toOdo is { } b && b >= a ? b - a : null;
            spans.Add(new TyreMountSpan(s.Set, s.Date, to, s.Odo, toOdo, km));
        }
        return spans;
    }

    public static IReadOnlyList<TyreSetSummary> Summaries(IReadOnlyList<TyreMountSpan> spans, IEnumerable<TyreSet> sets, IEnumerable<Expense> expenses, string homeCurrency)
    {
        var expenseList = expenses.ToList();
        var result = new List<TyreSetSummary>();
        foreach (var set in sets)
        {
            var own = spans.Where(s => s.TyreSetId == set.Id).ToList();
            double totalKm = own.Sum(s => s.Km ?? 0);

            var purchases = expenseList.Where(e => e.Category == ExpenseCategory.Tyres && e.TyreSetId == set.Id).ToList();
            decimal? cost = null;
            if (purchases.Count > 0)
            {
                var amounts = purchases.Select(e => Money.HomeAmountOf(e, homeCurrency)).Where(a => a is not null).ToList();
                if (amounts.Count > 0) cost = amounts.Sum(a => a!.Value);
            }
            decimal? per1000 = cost is { } c && totalKm > 0 ? c / (decimal)totalKm * 1000m : null;

            result.Add(new TyreSetSummary(set, totalKm, own.Count, cost, per1000, own.Any(s => s.To is null), own));
        }
        return result;
    }
}
