using System.Globalization;

namespace DriverBalance.Core;

public static class ScoreCharts
{
    public static MonthlyScoreChart Monthly(AnalysisContext c, Period p)
    {
        var groups = c.GroupsIn(p).ToList();

        var months = groups
            .GroupBy(g => new DateOnly(g.StartDate.Year, g.StartDate.Month, 1))
            .OrderBy(m => m.Key)
            .Select(m => new MonthlyScorePoint(
                m.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                KmOf(m, DrivingScore.Excellent), KmOf(m, DrivingScore.Good),
                KmOf(m, DrivingScore.Fair), KmOf(m, DrivingScore.Bad)))
            .ToList();

        // A group belongs only to the ISO week of its first trip; only weeks containing data appear.
        var weekly = groups
            .GroupBy(g => CalendarPeriods.IsoWeekStart(g.StartDate))
            .OrderBy(w => w.Key)
            .Select(w =>
            {
                double all = w.Sum(g => g.DistanceKm);
                double good = w.Where(g => g.Score is DrivingScore.Excellent or DrivingScore.Good).Sum(g => g.DistanceKm);
                return (Start: w.Key, Dpf: all > 0 ? good / all : (double?)null);
            })
            .ToList();

        int window = Math.Max(1, c.Parameters.DpfMovingAverageWeeks);
        var weeks = new List<WeeklyDpfPoint>();
        for (int i = 0; i < weekly.Count; i++)
        {
            var values = weekly.Skip(Math.Max(0, i - window + 1)).Take(Math.Min(window, i + 1))
                .Where(x => x.Dpf.HasValue).Select(x => x.Dpf!.Value).ToList();
            weeks.Add(new WeeklyDpfPoint(weekly[i].Start, weekly[i].Dpf, values.Count > 0 ? values.Average() : null));
        }
        return new MonthlyScoreChart(months, weeks);
    }

    public static IReadOnlyList<HeatmapDay> Heatmap(AnalysisContext c, int year)
    {
        var days = new Dictionary<DateOnly, (DrivingScore Best, double Km)>();
        foreach (var g in c.Groups)
        {
            foreach (var day in g.Trips.GroupBy(t => DateOnly.FromDateTime(t.Start)))
            {
                if (day.Key.Year != year) continue;
                double km = day.Sum(t => t.DistanceKm);
                days[day.Key] = days.TryGetValue(day.Key, out var cur)
                    ? (g.Score < cur.Best ? g.Score : cur.Best, cur.Km + km)
                    : (g.Score, km);
            }
        }
        double cap = c.Parameters.HeatmapKmCap;
        return days.OrderBy(d => d.Key)
            .Select(d => new HeatmapDay(d.Key, d.Value.Km, d.Value.Best, Math.Min(1, d.Value.Km / cap)))
            .ToList();
    }

    public static IReadOnlyList<ScoreMapPoint> ScoreMap(AnalysisContext c, Period p, TrendLevel level = TrendLevel.Group)
    {
        var groups = c.GroupsIn(p);
        if (level == TrendLevel.Group)
            return groups.Select(g => new ScoreMapPoint(g.Duration.TotalMinutes, g.AverageSpeedKmh, g.DistanceKm, g.Score, g.Start, g.Trips[0].Id)).ToList();
        return groups.SelectMany(g => g.Trips)
            .Select(t => new ScoreMapPoint(t.Duration.TotalMinutes, t.AverageSpeedKmh, t.DistanceKm,
                Scoring.Score(t.Duration, t.AverageSpeedKmh, c.Parameters), t.Start, t.Id))
            .ToList();
    }

    private static double KmOf(IEnumerable<TripGroup> groups, DrivingScore score) =>
        groups.Where(g => g.Score == score).Sum(g => g.DistanceKm);
}
