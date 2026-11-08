namespace DriverBalance.Core;

public enum TimelineEventKind { Maintenance, TyreMount, Expense, Import }

public sealed record TimelineEvent(DateOnly Date, TimelineEventKind Kind, string Label, int? Odometer, decimal? Amount, long? RefId);

public sealed record OdometerPoint(DateOnly Date, int Km);

public sealed record TimelineChart(IReadOnlyList<OdometerPoint> Odometer, IReadOnlyList<TimelineEvent> Events, IReadOnlyList<TyreMountSpan> TyreBands);

public static class TimelineBuilder
{
    public static TimelineChart Build(AnalysisContext c, Period p)
    {
        var odometer = c.Timeline.Readings
            .GroupBy(r => DateOnly.FromDateTime(r.At))
            .Where(g => p.Contains(g.Key))
            .OrderBy(g => g.Key)
            .Select(g => new OdometerPoint(g.Key, g.Max(r => r.Km)))
            .ToList();

        var events = new List<TimelineEvent>();
        foreach (var e in c.Data.Expenses.Where(e => p.Contains(e.Date)))
        {
            decimal? amount = Money.HomeAmountOf(e, c.Data.HomeCurrency);
            int? odo = e.Odometer is > 0 ? e.Odometer : null;
            if (TyreMileage.IsMount(e))
            {
                var set = c.Data.TyreSets.FirstOrDefault(s => s.Id == e.TyreSetId);
                events.Add(new TimelineEvent(e.Date, TimelineEventKind.TyreMount, set is null ? "Tyres mounted" : $"Mounted {set.Name}", odo, amount, e.Id));
            }
            else if (e.Category == ExpenseCategory.Maintenance)
            {
                var label = !string.IsNullOrWhiteSpace(e.MaintenanceDescription) ? e.MaintenanceDescription!
                    : e.Maintenance != MaintenanceType.Custom ? e.Maintenance.ToString() : "Maintenance";
                events.Add(new TimelineEvent(e.Date, TimelineEventKind.Maintenance, label, odo, amount, e.Id));
            }
            else
            {
                events.Add(new TimelineEvent(e.Date, TimelineEventKind.Expense, e.Category.ToString(), odo, amount, e.Id));
            }
        }

        var bands = TyreMileage.Spans(c.Data.Expenses, c.Data.InitialTyreSetId, c.Timeline, c.Data.Today)
            .Where(s => s.From < p.To && (s.To is null || s.To > p.From))
            .ToList();

        return new TimelineChart(odometer, events.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList(), bands);
    }
}
