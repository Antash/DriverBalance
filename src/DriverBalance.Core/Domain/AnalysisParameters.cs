namespace DriverBalance.Core;

public sealed record AnalysisParameters
{
    public int ColdStartGapMinutes { get; init; } = 45;
    public int ShortDurationMinutes { get; init; } = 20;
    public int MediumDurationMinutes { get; init; } = 30;
    public int LongDurationMinutes { get; init; } = 40;
    public double HeavyTrafficSpeedKmh { get; init; } = 25;
    public double CitySpeedKmh { get; init; } = 35;
    public double MixedSpeedKmh { get; init; } = 50;
    public int DpfMovingAverageWeeks { get; init; } = 3;
    public double FuelTrendMinLPer100 { get; init; } = 1;
    public double FuelTrendMaxLPer100 { get; init; } = 30;
    public double LowessSpan { get; init; } = 0.25;
    public double HeatmapKmCap { get; init; } = 100;
    public int LongDriveHintDays { get; init; } = 14;
    public int ExportReminderDays { get; init; } = 30;

    /// <summary>Returns human-readable problems; an empty list means the parameters are valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (ShortDurationMinutes <= 0 || ShortDurationMinutes > MediumDurationMinutes || MediumDurationMinutes > LongDurationMinutes)
            errors.Add("Durations must satisfy 0 < short <= medium <= long.");
        if (HeavyTrafficSpeedKmh <= 0 || HeavyTrafficSpeedKmh > CitySpeedKmh || CitySpeedKmh > MixedSpeedKmh)
            errors.Add("Speeds must satisfy 0 < heavy traffic <= city <= mixed.");
        if (ColdStartGapMinutes <= 0)
            errors.Add("Cold-start gap must be greater than 0 minutes.");
        if (DpfMovingAverageWeeks < 1)
            errors.Add("DPF moving average must be at least 1 week.");
        if (FuelTrendMinLPer100 < 0 || FuelTrendMinLPer100 >= FuelTrendMaxLPer100)
            errors.Add("Fuel trend limits must satisfy 0 <= min < max.");
        if (LowessSpan <= 0 || LowessSpan > 1)
            errors.Add("LOWESS span must be in (0, 1].");
        if (HeatmapKmCap <= 0)
            errors.Add("Heatmap km cap must be greater than 0.");
        if (LongDriveHintDays < 1)
            errors.Add("Long-drive hint days must be at least 1.");
        if (ExportReminderDays < 1)
            errors.Add("Export reminder days must be at least 1.");
        return errors;
    }
}
