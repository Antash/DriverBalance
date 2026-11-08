namespace DriverBalance.Core;

public static class DpfHealth
{
    public static DpfHealthSummary Compute(AnalysisContext c)
    {
        var longGroups = c.Groups.Where(g => g.Score is DrivingScore.Excellent or DrivingScore.Good).ToList();
        if (longGroups.Count == 0)
            return new DpfHealthSummary(null, null, null, false, Array.Empty<DpfHealthPoint>(), Array.Empty<Period>());

        var today = c.Data.Today;
        var dates = longGroups.Select(g => g.StartDate).Distinct().OrderBy(d => d).ToList();
        var last = dates[^1];
        int daysSince = Math.Max(0, today.DayNumber - last.DayNumber);
        double kmSince = c.Groups.Where(g => g.Start > longGroups[^1].Start).Sum(g => g.DistanceKm);

        var series = new List<DpfHealthPoint>();
        int idx = 0;
        var end = today > last ? today : last;
        for (var d = dates[0]; d <= end; d = d.AddDays(1))
        {
            while (idx + 1 < dates.Count && dates[idx + 1] <= d) idx++;
            series.Add(new DpfHealthPoint(d, d.DayNumber - dates[idx].DayNumber));
        }

        var stretches = new List<Period>();
        for (int i = 1; i < dates.Count; i++) stretches.Add(Stretch(dates[i - 1], dates[i]));
        if (today > last) stretches.Add(Stretch(last, today));

        return new DpfHealthSummary(last, daysSince, kmSince, daysSince >= c.Parameters.LongDriveHintDays, series,
            stretches.OrderByDescending(s => s.Days).ThenBy(s => s.From).Take(3).ToList());
    }

    private static Period Stretch(DateOnly from, DateOnly to) => new(from, to, $"{to.DayNumber - from.DayNumber} days");
}
