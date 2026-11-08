import { describe, expect, it } from 'vitest'
import type { AnalysisParameters } from '../api/types'
import { validateParams } from './validateParams'

const ok: AnalysisParameters = {
  coldStartGapMinutes: 45, shortDurationMinutes: 20, mediumDurationMinutes: 30, longDurationMinutes: 40,
  heavyTrafficSpeedKmh: 25, citySpeedKmh: 35, mixedSpeedKmh: 50, dpfMovingAverageWeeks: 3,
  fuelTrendMinLPer100: 1, fuelTrendMaxLPer100: 30, lowessSpan: 0.25, heatmapKmCap: 100,
  longDriveHintDays: 14, exportReminderDays: 30,
}

describe('validateParams', () => {
  it('accepts the defaults and equal boundaries', () => {
    expect(validateParams(ok)).toEqual([])
    expect(validateParams({ ...ok, shortDurationMinutes: 30, citySpeedKmh: 50, lowessSpan: 1 })).toEqual([])
  })

  it.each<[Partial<AnalysisParameters>, string]>([
    [{ shortDurationMinutes: 35 }, 'Durations must satisfy 0 < short <= medium <= long.'],
    [{ longDurationMinutes: 25 }, 'Durations must satisfy 0 < short <= medium <= long.'],
    [{ heavyTrafficSpeedKmh: 40 }, 'Speeds must satisfy 0 < heavy traffic <= city <= mixed.'],
    [{ mixedSpeedKmh: 30 }, 'Speeds must satisfy 0 < heavy traffic <= city <= mixed.'],
    [{ coldStartGapMinutes: 0 }, 'Cold-start gap must be greater than 0 minutes.'],
    [{ dpfMovingAverageWeeks: 0 }, 'DPF moving average must be at least 1 week.'],
    [{ fuelTrendMinLPer100: 30 }, 'Fuel trend limits must satisfy 0 <= min < max.'],
    [{ fuelTrendMinLPer100: -1 }, 'Fuel trend limits must satisfy 0 <= min < max.'],
    [{ lowessSpan: 0 }, 'LOWESS span must be in (0, 1].'],
    [{ lowessSpan: 1.5 }, 'LOWESS span must be in (0, 1].'],
    [{ heatmapKmCap: 0 }, 'Heatmap km cap must be greater than 0.'],
    [{ longDriveHintDays: 0 }, 'Long-drive hint days must be at least 1.'],
    [{ exportReminderDays: 0 }, 'Export reminder days must be at least 1.'],
  ])('%j -> %s', (over, message) => {
    expect(validateParams({ ...ok, ...over })).toEqual([message])
  })
})
