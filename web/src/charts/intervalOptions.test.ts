import { describe, expect, it } from 'vitest'
import type { PeriodStats, PeriodWithStats } from '../api/types'
import { smallMultipleOption } from './intervalOptions'

const mk = (label: string, from: string, kmPerDay: number | null): PeriodWithStats => ({
  period: { from, to: from, label },
  stats: {
    period: { from, to: from, label },
    usage: { kmPerDay } as PeriodStats['usage'],
    scores: { buckets: {}, dpfIndex: null },
    mix: { cityKm: 0, mixedKm: 0, highwayKm: 0 },
    fuel: { tripLiters: 0, tripLPer100: null, fullTankLPer100: null, fullTankKm: null },
    money: { total: 0, byCategory: {}, costPerKm: null, needsConversionCount: 0 },
  },
})

describe('smallMultipleOption', () => {
  const newestFirst = [mk('Current', '2026-03-01', 39), mk('Previous', '2025-02-01', null), mk('Before first', '2024-01-01', 30)]
  const opt = smallMultipleOption(newestFirst, 'kmPerDay', 'CZK')() as {
    xAxis: { data: string[] }
    series: { data: ({ value: number | null } | null)[] }[]
  }

  it('orders oldest to newest', () => {
    expect(opt.xAxis.data).toEqual(['Before first', 'Previous', 'Current'])
  })
  it('keeps null as a gap, never 0, and highlights only the current bar', () => {
    const d = opt.series[0].data as { value: number | null; itemStyle: { opacity: number } }[]
    expect(d.map((x) => x.value)).toEqual([30, null, 39])
    expect(d.map((x) => x.itemStyle.opacity)).toEqual([0.45, 0.45, 1])
  })
})
