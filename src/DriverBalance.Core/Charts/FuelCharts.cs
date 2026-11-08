namespace DriverBalance.Core;

public static class FuelCharts
{
    public static FuelTrendChart Trend(AnalysisContext c, Period p, TrendLevel level)
    {
        var prm = c.Parameters;
        var groups = c.GroupsIn(p);

        var raw = level == TrendLevel.Group
            ? groups.Select(g => new FuelTrendPoint(g.Start, g.LPer100 ?? 0, g.DistanceKm, g.Score, g.Trips[0].Id, g.Trips.Count))
            : groups.SelectMany(g => g.Trips).Select(t => new FuelTrendPoint(
                t.Start,
                t.DistanceKm > 0 ? t.FuelLiters / t.DistanceKm * 100.0 : 0,
                t.DistanceKm,
                Scoring.Score(t.Duration, t.AverageSpeedKmh, prm),
                t.Id, 1));

        var points = raw
            .Where(x => x.Km > 0 && x.LPer100 >= prm.FuelTrendMinLPer100 && x.LPer100 <= prm.FuelTrendMaxLPer100)
            .OrderBy(x => x.Time)
            .ToList();

        var xs = points.Select(x => x.Time.ToOADate()).ToArray();
        var ys = points.Select(x => x.LPer100).ToArray();
        var ws = points.Select(x => x.Km).ToArray();
        var smooth = Lowess.Smooth(xs, ys, ws, prm.LowessSpan);

        return new FuelTrendChart(points, points.Select((x, i) => new SmoothPoint(x.Time, smooth[i])).ToList());
    }

    // Group-duration bins in minutes: 5-min up to 60, then 60-90, then 90+ (open-ended).
    private static readonly double[] PenaltyEdges =
        Enumerable.Range(0, 13).Select(i => i * 5.0).Concat(new[] { 90.0 }).ToArray();

    public static IReadOnlyList<PenaltyBin> PenaltyCurve(AnalysisContext c, Period p)
    {
        var groups = c.GroupsIn(p).Where(g => g.LPer100.HasValue).ToList();
        var result = new List<PenaltyBin>();
        for (int i = 0; i < PenaltyEdges.Length; i++)
        {
            double from = PenaltyEdges[i];
            double? to = i < PenaltyEdges.Length - 1 ? PenaltyEdges[i + 1] : null;
            var values = groups
                .Where(g => g.Duration.TotalMinutes >= from && (to is null || g.Duration.TotalMinutes < to))
                .Select(g => g.LPer100!.Value).OrderBy(v => v).ToList();
            result.Add(values.Count == 0
                ? new PenaltyBin(from, to, null, null, null, 0)
                : new PenaltyBin(from, to, Percentile(values, 0.5), Percentile(values, 0.25), Percentile(values, 0.75), values.Count));
        }
        return result;
    }

    public static IReadOnlyList<SeasonPoint> Seasonality(AnalysisContext c) =>
        c.Groups.GroupBy(g => (g.Start.Year, g.Start.Month))
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month)
            .Select(x =>
            {
                double km = x.Sum(g => g.DistanceKm);
                return new SeasonPoint(x.Key.Year, x.Key.Month, km, km > 0 ? x.Sum(g => g.FuelLiters) / km * 100.0 : null);
            })
            .ToList();

    /// <summary>Linear-interpolated percentile of an ascending list.</summary>
    private static double Percentile(List<double> sorted, double q)
    {
        double pos = q * (sorted.Count - 1);
        int lo = (int)Math.Floor(pos);
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
