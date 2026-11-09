namespace DriverBalance.Core;

public sealed class OdometerTimeline
{
    private OdometerTimeline(IReadOnlyList<OdometerReading> readings, IReadOnlyList<OdometerReading> dropped)
    {
        Readings = readings;
        Dropped = dropped;
    }

    public IReadOnlyList<OdometerReading> Readings { get; }
    public IReadOnlyList<OdometerReading> Dropped { get; }
    public int? Latest => Readings.Count > 0 ? Readings[^1].Km : null;

    /// <summary>Drops readings lower than the running maximum (odometer cannot go backwards).</summary>
    public static OdometerTimeline Build(IEnumerable<OdometerReading> readings)
    {
        var kept = new List<OdometerReading>();
        var dropped = new List<OdometerReading>();
        int max = int.MinValue;
        foreach (var r in readings.OrderBy(r => r.At))
        {
            if (r.Km < max) dropped.Add(r);
            else { kept.Add(r); max = r.Km; }
        }
        return new OdometerTimeline(kept, dropped);
    }

    /// <summary>Last known odometer by the end of <paramref name="d"/>. Inference for undated-odometer records should pass
    /// <paramref name="ignoreHistoryStart"/> so a car-history start does not invent an anchor before the first real reading.</summary>
    public int? AtEndOfDay(DateOnly d, bool ignoreHistoryStart = false) => LastBefore(d.AddDays(1).ToDateTime(TimeOnly.MinValue), ignoreHistoryStart);

    private int? LastBefore(DateTime at, bool ignoreHistoryStart = false)
    {
        int? km = null;
        foreach (var r in Readings)
        {
            if (r.At >= at) break;
            if (ignoreHistoryStart && r.Source == OdometerSource.HistoryStart) continue;
            km = r.Km;
        }
        return km;
    }

    /// <summary>Odometer at <paramref name="at"/>: linear in time inside the gap between a car-history start reading and the
    /// first real reading (the car was driven steadily, we only lack the data); a step function everywhere else.</summary>
    private double? OdometerAt(DateTime at)
    {
        if (Readings.Count >= 2 && Readings[0].Source == OdometerSource.HistoryStart)
        {
            var a = Readings[0];
            var b = Readings[1];
            if (at >= a.At && at <= b.At && b.At > a.At)
                return a.Km + (b.Km - a.Km) * (at - a.At).TotalDays / (b.At - a.At).TotalDays;
        }
        return LastBefore(at);
    }

    public double? KmIn(Period p)
    {
        var from = p.From.ToDateTime(TimeOnly.MinValue);
        var to = p.To.ToDateTime(TimeOnly.MinValue);
        double? start = p.StartOdometer ?? OdometerAt(from);
        if (start is null)
        {
            var first = Readings.FirstOrDefault(r => r.At >= from && r.At < to);
            start = first?.Km;
        }
        double? end = p.EndOdometer ?? OdometerAt(to);
        if (start is null || end is null || end < start) return null;
        return end.Value - start.Value;
    }
}
