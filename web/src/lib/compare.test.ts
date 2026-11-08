import { describe, expect, it } from 'vitest'
import type { PeriodStats } from '../api/types'
import { compareStats, type MetricKey } from './compare'

function stats(over: { dpf?: number | null; coldPer100?: number | null; kmPerDay?: number | null; city?: number; mixed?: number; highway?: number } = {}): PeriodStats {
  return {
    period: { from: '2025-01-01', to: '2025-02-01', label: 'x' },
    usage: {
      tripCount: 10, tripKm: 100, odometerKm: 100, tripCoverage: 1, days: 31, kmPerDay: over.kmPerDay === undefined ? 10 : over.kmPerDay,
      drivingTime: '05:00:00', averageSpeedKmh: 30, coldStarts: 5, coldStartsPer100Km: over.coldPer100 === undefined ? 5 : over.coldPer100,
      shortGroups: 1, shortShareByCount: 0.2, shortShareByKm: 0.1,
    },
    scores: { buckets: {}, dpfIndex: over.dpf === undefined ? 0.5 : over.dpf },
    mix: { cityKm: over.city ?? 10, mixedKm: over.mixed ?? 10, highwayKm: over.highway ?? 30 },
    fuel: { tripLiters: 7, tripLPer100: 7, fullTankLPer100: null, fullTankKm: null },
    money: { total: 100, byCategory: {}, costPerKm: 1, needsConversionCount: 0 },
  }
}
const row = (rows: ReturnType<typeof compareStats>, k: MetricKey) => rows.find((r) => r.key === k)!

describe('compareStats', () => {
  it('gives a null change for a null or zero baseline', () => {
    const rows = compareStats(stats({ kmPerDay: null }), stats({ kmPerDay: 12 }))
    expect(row(rows, 'kmPerDay').change).toBeNull()
    const zero = compareStats(stats({ coldPer100: 0 }), stats({ coldPer100: 3 }))
    expect(row(zero, 'coldStartsPer100Km').change).toBeNull()
  })

  it('computes (b-a)/a otherwise', () => {
    const rows = compareStats(stats({ kmPerDay: 10 }), stats({ kmPerDay: 15 }))
    expect(row(rows, 'kmPerDay').change).toBeCloseTo(0.5)
  })

  it('marks a higher DPF index as better up and more cold starts per 100 km as better down', () => {
    const rows = compareStats(stats(), stats({ dpf: 0.7, coldPer100: 9 }))
    expect(row(rows, 'dpfIndex').better).toBe('up')
    expect(row(rows, 'coldStartsPer100Km').better).toBe('down')
    expect(row(rows, 'coldStartsPer100Km').isRate).toBe(true)
  })

  it('derives cityShare from the mix and leaves it null when there is no distance', () => {
    const rows = compareStats(stats({ city: 25, mixed: 25, highway: 50 }), stats({ city: 0, mixed: 0, highway: 0 }))
    expect(row(rows, 'cityShare').a).toBeCloseTo(0.25)
    expect(row(rows, 'cityShare').b).toBeNull()
    expect(row(rows, 'cityShare').change).toBeNull()
  })
})
