namespace DriverBalance.Core;

public static class HabitCharts
{
    // Bin i = [edge i, edge i+1); the last edge starts the open-ended bin.
    private static readonly double[] GapEdges = BuildEdges();

    private static double[] BuildEdges()
    {
        var edges = new List<double>();
        for (int m = 0; m <= 120; m += 5) edges.Add(m);
        edges.AddRange(new double[] { 240, 480, 1440 });
        return edges.ToArray();
    }

    public static GapHistogram Gaps(AnalysisContext c, Period p)
    {
        var trips = c.GroupsIn(p).SelectMany(g => g.Trips).OrderBy(t => t.Start).ToList();
        var counts = new int[GapEdges.Length];
        for (int i = 1; i < trips.Count; i++)
        {
            double gap = Math.Max(0, (trips[i].Start - trips[i - 1].End).TotalMinutes);
            int bin = GapEdges.Length - 1;
            for (int b = 0; b < GapEdges.Length - 1; b++)
            {
                if (gap < GapEdges[b + 1]) { bin = b; break; }
            }
            counts[bin]++;
        }
        var bins = GapEdges.Select((from, i) =>
            new HistogramBin(from, i < GapEdges.Length - 1 ? GapEdges[i + 1] : null, counts[i])).ToList();
        return new GapHistogram(bins, c.Parameters.ColdStartGapMinutes);
    }

    public static IReadOnlyList<HourCell> WhenYouDrive(AnalysisContext c, Period p)
    {
        var cells = new Dictionary<(int Dow, int Hour), (double Km, int Trips, double BadKm)>();
        foreach (var g in c.GroupsIn(p))
        {
            foreach (var t in g.Trips)
            {
                var key = (((int)t.Start.DayOfWeek + 6) % 7, t.Start.Hour);
                cells.TryGetValue(key, out var cur);
                cells[key] = (cur.Km + t.DistanceKm, cur.Trips + 1, cur.BadKm + (g.Score == DrivingScore.Bad ? t.DistanceKm : 0));
            }
        }
        return cells.OrderBy(x => x.Key.Dow).ThenBy(x => x.Key.Hour)
            .Select(x => new HourCell(x.Key.Dow, x.Key.Hour, x.Value.Km, x.Value.Trips,
                x.Value.Km > 0 ? x.Value.BadKm / x.Value.Km : null))
            .ToList();
    }
}
