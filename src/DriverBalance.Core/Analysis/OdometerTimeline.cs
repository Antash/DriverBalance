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

    public int? AtEndOfDay(DateOnly d) => LastBefore(d.AddDays(1).ToDateTime(TimeOnly.MinValue));

    private int? LastBefore(DateTime at)
    {
        int? km = null;
        foreach (var r in Readings)
        {
            if (r.At >= at) break;
            km = r.Km;
        }
        return km;
    }

    public double? KmIn(Period p)
    {
        var from = p.From.ToDateTime(TimeOnly.MinValue);
        var to = p.To.ToDateTime(TimeOnly.MinValue);
        int? start = p.StartOdometer ?? LastBefore(from);
        if (start is null)
        {
            var first = Readings.FirstOrDefault(r => r.At >= from && r.At < to);
            start = first?.Km;
        }
        int? end = p.EndOdometer ?? LastBefore(to);
        if (start is null || end is null || end < start) return null;
        return end.Value - start.Value;
    }
}
