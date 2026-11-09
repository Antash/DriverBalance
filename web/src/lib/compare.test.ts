import { describe, expect, it } from 'vitest'
import type { PeriodStats } from '../api/types'
import { compareMany, compareStats, comparisonRequest, type MetricKey } from './compare'

interface Over {
  dpf?: number | null; coldPer100?: number | null; kmPerDay?: number | null; city?: number; mixed?: number; highway?: number
  days?: number; tripKm?: number; odometerKm?: number | null; coverage?: number | null; speed?: number | null; driving?: string
  shortCount?: number | null; shortKm?: number | null; tripL?: number | null; tankL?: number | null; total?: number; costPerKm?: number | null
  byCategory?: PeriodStats['money']['byCategory']; needsConv?: number
}
const pick = <T,>(v: T | undefined, d: T): T => (v === undefined ? d : v)

function stats(over: Over = {}): PeriodStats {
  return {
    period: { from: '2025-01-01', to: '2025-02-01', label: 'x' },
    usage: {
      tripCount: 10, tripKm: pick(over.tripKm, 100), odometerKm: pick(over.odometerKm, 100), tripCoverage: pick(over.coverage, 1),
      days: pick(over.days, 31), elapsedDays: pick(over.days, 31), kmPerDay: pick(over.kmPerDay, 10),
      drivingTime: pick(over.driving, '05:00:00'), averageSpeedKmh: pick(over.speed, 30), coldStarts: 5, coldStartsPer100Km: pick(over.coldPer100, 5),
      shortGroups: 1, shortShareByCount: pick(over.shortCount, 0.2), shortShareByKm: pick(over.shortKm, 0.1),
    },
    scores: { buckets: {}, dpfIndex: over.dpf === undefined ? 0.5 : over.dpf },
    mix: { cityKm: over.city ?? 10, mixedKm: over.mixed ?? 10, highwayKm: over.highway ?? 30 },
    fuel: { tripLiters: 7, tripLPer100: pick(over.tripL, 7), fullTankLPer100: pick(over.tankL, null), fullTankKm: null },
    money: { total: pick(over.total, 100), byCategory: over.byCategory ?? {}, costPerKm: pick(over.costPerKm, 1), needsConversionCount: pick(over.needsConv, 0) },
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

  // Rates and their good direction. "bump" moves the metric in the numeric-up direction from a baseline stats().
  const RATES: [MetricKey, Over, 'up' | 'down' | null][] = [
    ['kmPerDay', { kmPerDay: 15 }, null],
    ['tripCoverage', { coverage: 0.5 }, null],
    ['averageSpeedKmh', { speed: 40 }, 'up'],
    ['drivingHoursPerDay', { driving: '10:00:00' }, null],
    ['cityShare', { city: 30, mixed: 10, highway: 10 }, 'down'],
    ['mixedShare', { city: 10, mixed: 30, highway: 10 }, null],
    ['highwayShare', { city: 10, mixed: 10, highway: 50 }, 'up'],
    ['coldStartsPer100Km', { coldPer100: 9 }, 'down'],
    ['shortShareByKm', { shortKm: 0.3 }, 'down'],
    ['shortShareByCount', { shortCount: 0.4 }, 'down'],
    ['dpfIndex', { dpf: 0.7 }, 'up'],
    ['tripLPer100', { tripL: 9 }, 'down'],
    ['fullTankLPer100', { tankL: 9 }, 'down'],
    ['costPerKm', { costPerKm: 2 }, 'down'],
  ]

  it.each(RATES)('rate %s has direction %s and keeps its change when the periods differ in length', (key, over, better) => {
    const base = stats({ tankL: 6 })
    const rows = compareStats(base, stats({ ...over, days: 28 }))
    const r = row(rows, key)
    expect(r.isRate).toBe(true)
    expect(r.better).toBe(better)
    expect(r.change).not.toBeNull()
  })

  it('volume rows have no direction and lose their change when the periods differ in length', () => {
    const volumes: MetricKey[] = ['tripKm', 'odometerKm', 'coldStarts', 'total', 'needsConversionCount', 'cat:Fuel']
    const mk = (days: number) => stats({
      days, tripKm: 200, odometerKm: 210, total: 300, needsConv: 2, byCategory: { Fuel: 120 },
    })
    const same = compareStats(stats({ byCategory: { Fuel: 60 }, needsConv: 1, odometerKm: 90 }), mk(31))
    const diff = compareStats(stats({ byCategory: { Fuel: 60 }, needsConv: 1, odometerKm: 90 }), mk(28))
    for (const k of volumes) {
      expect(row(same, k).better).toBeNull()
      expect(row(same, k).isRate).toBe(false)
      expect(row(same, k).change).not.toBeNull()
      expect(row(diff, k).change).toBeNull()
    }
  })

  it('adds a per-category money row for every category present in either period', () => {
    const rows = compareStats(stats({ byCategory: { Fuel: 100 } }), stats({ byCategory: { Fuel: 150, Parking: 20 } }))
    expect(row(rows, 'cat:Fuel')).toMatchObject({ label: 'Fuel', a: 100, b: 150, group: 'Money' })
    expect(row(rows, 'cat:Parking')).toMatchObject({ a: null, b: 20, change: null })
    expect(rows.some((r) => r.key === 'cat:Tyres')).toBe(false)
  })

  it('derives driving time per elapsed day from the TimeSpan', () => {
    const rows = compareStats(stats({ driving: '1.02:00:00', days: 13 }), stats({ driving: '05:30:00', days: 11 }))
    expect(row(rows, 'drivingHoursPerDay').a).toBeCloseTo(26 / 13)
    expect(row(rows, 'drivingHoursPerDay').b).toBeCloseTo(0.5)
  })

  it('Statistics compare: the comparison range is the baseline (A), the main range is B', () => {
    const range = { from: '2025-01-01', to: '2026-01-01' }, cmp = { from: '2024-01-01', to: '2025-01-01' }
    const req = comparisonRequest(range, cmp)
    expect(req.periods.map((p) => p.from)).toEqual(['2024-01-01', '2025-01-01'])
    expect(comparisonRequest(range, null).periods).toHaveLength(1)
    // baseline DPF 0.5 -> main 0.7 must read as an improvement
    const rows = compareStats(stats({ dpf: 0.5 }), stats({ dpf: 0.7 }))
    const dpf = row(rows, 'dpfIndex')
    expect(dpf.change).toBeGreaterThan(0)
    expect((dpf.change! > 0) === (dpf.better === 'up')).toBe(true)
  })
})

describe('compareMany', () => {
  const many = (rows: ReturnType<typeof compareMany>, k: MetricKey) => rows.find((r) => r.key === k)!

  it('lists one value per period in the given order', () => {
    const rows = compareMany([stats({ kmPerDay: 10 }), stats({ kmPerDay: 20 }), stats({ kmPerDay: 30 })])
    expect(many(rows, 'kmPerDay').values).toEqual([10, 20, 30])
  })

  it('marks best and worst by the metric direction', () => {
    const rows = compareMany([stats({ coldPer100: 4 }), stats({ coldPer100: 2 }), stats({ coldPer100: 6 })])
    const r = many(rows, 'coldStartsPer100Km')
    expect(r.best).toEqual([1])
    expect(r.worst).toEqual([2])
    const dpf = many(compareMany([stats({ dpf: 0.4 }), stats({ dpf: 0.9 })]), 'dpfIndex')
    expect(dpf.best).toEqual([1])
    expect(dpf.worst).toEqual([0])
  })

  it('marks every tied period and ignores nulls', () => {
    const r = many(compareMany([stats({ tripL: 6 }), stats({ tripL: null }), stats({ tripL: 6 }), stats({ tripL: 7 })]), 'tripLPer100')
    expect(r.best).toEqual([0, 2])
    expect(r.worst).toEqual([3])
  })

  it('marks nothing for metrics without a direction, all-equal values or a single value', () => {
    expect(many(compareMany([stats({ tripKm: 1 }), stats({ tripKm: 2 })]), 'tripKm').best).toEqual([])
    expect(many(compareMany([stats({ dpf: 0.5 }), stats({ dpf: 0.5 })]), 'dpfIndex').best).toEqual([])
    const single = many(compareMany([stats({ dpf: 0.5 }), stats({ dpf: null })]), 'dpfIndex')
    expect(single.best).toEqual([])
    expect(single.worst).toEqual([])
  })

  it('adds a row for every expense category seen in any period', () => {
    const rows = compareMany([stats({ byCategory: { Fuel: 10 } }), stats({ byCategory: { Maintenance: 5 } })])
    expect(many(rows, 'cat:Fuel').values).toEqual([10, null])
    expect(many(rows, 'cat:Maintenance').values).toEqual([null, 5])
  })
})

describe('compareMany with a period that has no trips', () => {
  const noTrips = () => { const s = stats({ tripKm: 0, costPerKm: 0, coldPer100: null }); s.usage.tripCount = 0; s.usage.coldStarts = 0; s.usage.drivingTime = '00:00:00'; return s }
  const many = (rows: ReturnType<typeof compareMany>, k: MetricKey) => rows.find((r) => r.key === k)!

  it('shows trip-based volumes as unknown instead of zero', () => {
    const rows = compareMany([noTrips(), stats()])
    expect(many(rows, 'tripKm').values[0]).toBeNull()
    expect(many(rows, 'coldStarts').values[0]).toBeNull()
    expect(many(rows, 'drivingHoursPerDay').values[0]).toBeNull()
    expect(many(rows, 'odometerKm').values[0]).toBe(100)
  })

  it('leaves the period out of best and worst', () => {
    const rows = compareMany([noTrips(), stats({ costPerKm: 1 }), stats({ costPerKm: 2 })])
    expect(many(rows, 'costPerKm').best).toEqual([1])
    expect(many(rows, 'costPerKm').worst).toEqual([2])
  })
})
