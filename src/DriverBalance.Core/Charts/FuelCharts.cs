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
}
