namespace DriverBalance.Core;

public sealed record MonthlyScorePoint(string Month, double ExcellentKm, double GoodKm, double FairKm, double BadKm);
public sealed record WeeklyDpfPoint(DateOnly WeekStart, double? Dpf, double? DpfMovingAverage);
public sealed record MonthlyScoreChart(IReadOnlyList<MonthlyScorePoint> Months, IReadOnlyList<WeeklyDpfPoint> Weeks);
public sealed record HeatmapDay(DateOnly Date, double Km, DrivingScore BestScore, double Intensity);
public sealed record FuelTrendPoint(DateTime Time, double LPer100, double Km, DrivingScore Score, long FirstTripId, int TripCount);
public sealed record SmoothPoint(DateTime Time, double LPer100);
public sealed record FuelTrendChart(IReadOnlyList<FuelTrendPoint> Points, IReadOnlyList<SmoothPoint> Smooth);
public enum TrendLevel { Group, Trip }
