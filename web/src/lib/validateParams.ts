import type { AnalysisParameters } from '../api/types'

/** Mirrors AnalysisParameters.Validate in DriverBalance.Core (same rules, same messages). Empty list = valid. */
export function validateParams(p: AnalysisParameters): string[] {
  const errors: string[] = []
  if (p.shortDurationMinutes <= 0 || p.shortDurationMinutes > p.mediumDurationMinutes || p.mediumDurationMinutes > p.longDurationMinutes)
    errors.push('Durations must satisfy 0 < short <= medium <= long.')
  if (p.heavyTrafficSpeedKmh <= 0 || p.heavyTrafficSpeedKmh > p.citySpeedKmh || p.citySpeedKmh > p.mixedSpeedKmh)
    errors.push('Speeds must satisfy 0 < heavy traffic <= city <= mixed.')
  if (p.coldStartGapMinutes <= 0)
    errors.push('Cold-start gap must be greater than 0 minutes.')
  if (p.dpfMovingAverageWeeks < 1)
    errors.push('DPF moving average must be at least 1 week.')
  if (p.fuelTrendMinLPer100 < 0 || p.fuelTrendMinLPer100 >= p.fuelTrendMaxLPer100)
    errors.push('Fuel trend limits must satisfy 0 <= min < max.')
  if (p.lowessSpan <= 0 || p.lowessSpan > 1)
    errors.push('LOWESS span must be in (0, 1].')
  if (p.heatmapKmCap <= 0)
    errors.push('Heatmap km cap must be greater than 0.')
  if (p.longDriveHintDays < 1)
    errors.push('Long-drive hint days must be at least 1.')
  if (p.exportReminderDays < 1)
    errors.push('Export reminder days must be at least 1.')
  return errors
}
