namespace DriverBalance.Core;

public sealed record MonthlyScorePoint(string Month, double ExcellentKm, double GoodKm, double FairKm, double BadKm);
public sealed record WeeklyDpfPoint(DateOnly WeekStart, double? Dpf, double? DpfMovingAverage);
public sealed record MonthlyScoreChart(IReadOnlyList<MonthlyScorePoint> Months, IReadOnlyList<WeeklyDpfPoint> Weeks);
public sealed record HeatmapDay(DateOnly Date, double Km, DrivingScore BestScore, double Intensity);
public sealed record FuelTrendPoint(DateTime Time, double LPer100, double Km, DrivingScore Score, long FirstTripId, int TripCount);
public sealed record SmoothPoint(DateTime Time, double LPer100);
public sealed record FuelTrendChart(IReadOnlyList<FuelTrendPoint> Points, IReadOnlyList<SmoothPoint> Smooth);
public enum TrendLevel { Group, Trip }

public sealed record ScoreMapPoint(double DurationMinutes, double AverageSpeedKmh, double Km, DrivingScore Score, DateTime Start, long FirstTripId);
public sealed record ScoreZones(double ShortMin, double MediumMin, double LongMin, double HeavyKmh, double CityKmh, double MixedKmh)
{
    public static ScoreZones From(AnalysisParameters p) =>
        new(p.ShortDurationMinutes, p.MediumDurationMinutes, p.LongDurationMinutes, p.HeavyTrafficSpeedKmh, p.CitySpeedKmh, p.MixedSpeedKmh);
}
/// <summary>[From, To) in minutes; To is null for the open-ended last bin.</summary>
public sealed record HistogramBin(double From, double? To, int Count);
public sealed record GapHistogram(IReadOnlyList<HistogramBin> Bins, double ThresholdMinutes);
public sealed record HourCell(int DayOfWeek, int Hour, double Km, int Trips, double? BadShare);
/// <summary>ToMinutes is null for the open-ended last bin.</summary>
public sealed record PenaltyBin(double FromMinutes, double? ToMinutes, double? MedianLPer100, double? P25, double? P75, int Count);
public sealed record SeasonPoint(int Year, int Month, double Km, double? LPer100);
public sealed record PlaceStat(string Address, int Starts, int Ends);
public sealed record RouteTrendPoint(DateOnly Date, double? LPer100);
public sealed record RouteStat(string From, string To, int Count, double Km, double? AvgLPer100, DrivingScore TypicalScore, IReadOnlyList<RouteTrendPoint> Trend);
public sealed record DpfHealthPoint(DateOnly Date, int DaysSinceLongDrive);
public sealed record DpfHealthSummary(DateOnly? LastLongDrive, int? DaysSince, double? KmSince, bool NeedsLongDrive,
    IReadOnlyList<DpfHealthPoint> Series, IReadOnlyList<Period> WorstStretches);
