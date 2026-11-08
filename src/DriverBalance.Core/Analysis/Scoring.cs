namespace DriverBalance.Core;

public static class Scoring
{
    public static DrivingScore Score(TimeSpan duration, double avgSpeedKmh, AnalysisParameters p)
    {
        if (duration >= TimeSpan.FromMinutes(p.LongDurationMinutes) && avgSpeedKmh >= p.MixedSpeedKmh)
            return DrivingScore.Excellent;
        if (duration >= TimeSpan.FromMinutes(p.MediumDurationMinutes) && avgSpeedKmh >= p.CitySpeedKmh)
            return DrivingScore.Good;
        if (duration >= TimeSpan.FromMinutes(p.ShortDurationMinutes) && avgSpeedKmh >= p.HeavyTrafficSpeedKmh)
            return DrivingScore.Fair;
        return DrivingScore.Bad;
    }

    /// <summary>Best score over every single trip and every run of consecutive trips in the group.</summary>
    public static DrivingScore BestForGroup(IReadOnlyList<Trip> trips, AnalysisParameters p)
    {
        var best = DrivingScore.Bad;
        for (int i = 0; i < trips.Count; i++)
        {
            long ticks = 0;
            double km = 0;
            for (int j = i; j < trips.Count; j++)
            {
                ticks += trips[j].Duration.Ticks;
                km += trips[j].DistanceKm;
                var duration = TimeSpan.FromTicks(ticks);
                var speed = duration.TotalHours > 0 ? km / duration.TotalHours : 0;
                var s = Score(duration, speed, p);
                if (s < best) best = s;
            }
        }
        return best;
    }
}
