namespace DriverBalance.Core;

public sealed class AnalysisContext
{
    private AnalysisContext(VehicleData data, AnalysisParameters parameters, IReadOnlyList<TripGroup> groups, OdometerTimeline timeline)
    {
        Data = data;
        Parameters = parameters;
        Groups = groups;
        Timeline = timeline;
    }

    public static AnalysisContext Create(VehicleData data, AnalysisParameters p) =>
        new(data, p, TripGrouping.Build(data.Trips, p), BuildTimeline(data));

    private static OdometerTimeline BuildTimeline(VehicleData data)
    {
        var readings = new List<OdometerReading>();
        foreach (var t in data.Trips)
        {
            if (t.StartOdometer > 0) readings.Add(new OdometerReading(t.Start, t.StartOdometer, OdometerSource.TripStart));
            if (t.EndOdometer > 0) readings.Add(new OdometerReading(t.End, t.EndOdometer, OdometerSource.TripEnd));
        }
        foreach (var e in data.Expenses)
        {
            if (e.Odometer is > 0)
                readings.Add(new OdometerReading(e.Date.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1), e.Odometer.Value, OdometerSource.Expense));
        }
        return OdometerTimeline.Build(readings);
    }

    public OdometerTimeline Timeline { get; }

    public IReadOnlyList<TripGroup> Groups { get; }
    public AnalysisParameters Parameters { get; }
    public VehicleData Data { get; }

    public IEnumerable<TripGroup> GroupsIn(Period period) => Groups.Where(g => period.Contains(g.StartDate));

    public PeriodStats Stats(Period period)
    {
        var groups = GroupsIn(period).ToList();
        var p = Parameters;

        double km = groups.Sum(g => g.DistanceKm);
        double liters = groups.Sum(g => g.FuelLiters);
        var time = TimeSpan.FromTicks(groups.Sum(g => g.Duration.Ticks));
        int tripCount = groups.Sum(g => g.Trips.Count);
        var shortLimit = TimeSpan.FromMinutes(p.ShortDurationMinutes);
        var shortGroups = groups.Where(g => g.Duration < shortLimit).ToList();
        double shortKm = shortGroups.Sum(g => g.DistanceKm);

        double? odometerKm = Timeline.KmIn(period);
        double? kmPerDay = period.Days > 0 ? (odometerKm ?? km) / period.Days : null;

        var usage = new UsageStats(
            tripCount, km, odometerKm, odometerKm > 0 ? km / odometerKm : null, period.Days, kmPerDay, time,
            time.TotalHours > 0 ? km / time.TotalHours : null,
            groups.Count,
            km > 0 ? groups.Count / km * 100.0 : null,
            shortGroups.Count,
            groups.Count > 0 ? (double)shortGroups.Count / groups.Count : null,
            km > 0 ? shortKm / km : null);

        var buckets = new Dictionary<DrivingScore, ScoreBucket>();
        foreach (var score in Enum.GetValues<DrivingScore>())
        {
            var gs = groups.Where(g => g.Score == score).ToList();
            buckets[score] = new ScoreBucket(gs.Sum(g => g.DistanceKm), TimeSpan.FromTicks(gs.Sum(g => g.Duration.Ticks)), gs.Count);
        }
        double goodKm = buckets[DrivingScore.Excellent].Km + buckets[DrivingScore.Good].Km;
        var scores = new ScoreBreakdown(buckets, km > 0 ? goodKm / km : null);

        double city = 0, mixed = 0, highway = 0;
        foreach (var g in groups)
        {
            if (g.AverageSpeedKmh < p.CitySpeedKmh) city += g.DistanceKm;
            else if (g.AverageSpeedKmh < p.MixedSpeedKmh) mixed += g.DistanceKm;
            else highway += g.DistanceKm;
        }

        var fuel = new FuelStats(liters, km > 0 ? liters / km * 100.0 : null, null, null);
        var money = new MoneyStats(0m, new Dictionary<ExpenseCategory, decimal>(), null, 0);

        return new PeriodStats(period, usage, scores, new DrivingMix(city, mixed, highway), fuel, money);
    }
}
