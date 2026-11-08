namespace DriverBalance.Core;

public sealed record ScoreBucket(double Km, TimeSpan Duration, int Groups);

/// <summary>Days is the full period length; ElapsedDays is clipped to today (inclusive) and is the denominator of per-day rates.</summary>
public sealed record UsageStats(int TripCount, double TripKm, double? OdometerKm, double? TripCoverage, int Days, double? KmPerDay,
    TimeSpan DrivingTime, double? AverageSpeedKmh, int ColdStarts, double? ColdStartsPer100Km, int ShortGroups, double? ShortShareByCount, double? ShortShareByKm, int ElapsedDays);

public sealed record ScoreBreakdown(IReadOnlyDictionary<DrivingScore, ScoreBucket> Buckets, double? DpfIndex);

public sealed record DrivingMix(double CityKm, double MixedKm, double HighwayKm);

public sealed record FuelStats(double TripLiters, double? TripLPer100, double? FullTankLPer100, double? FullTankKm);

public sealed record MoneyStats(decimal Total, IReadOnlyDictionary<ExpenseCategory, decimal> ByCategory, decimal? CostPerKm, int NeedsConversionCount);

public sealed record PeriodStats(Period Period, UsageStats Usage, ScoreBreakdown Scores, DrivingMix Mix, FuelStats Fuel, MoneyStats Money);
