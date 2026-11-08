import type { PeriodStats } from '../api/types'

export type MetricKey = 'kmPerDay'|'coldStartsPer100Km'|'shortShareByKm'|'dpfIndex'|'cityShare'|'tripLPer100'|'fullTankLPer100'|'costPerKm'|'total'|'tripKm'|'days'|'coldStarts'
export interface MetricRow { key: MetricKey; label: string; a: number|null; b: number|null; change: number|null; better: 'up'|'down'|null; isRate: boolean }

export type MetricGroup = 'Usage' | 'Engine wear & DPF' | 'Fuel' | 'Money'

interface Def {
  key: MetricKey
  label: string
  group: MetricGroup
  /** Direction that is good for the car/wallet; null for pure volume metrics. */
  better: 'up' | 'down' | null
  isRate: boolean
  read(s: PeriodStats): number | null
}

const cityShare = (s: PeriodStats) => {
  const total = s.mix.cityKm + s.mix.mixedKm + s.mix.highwayKm
  return total > 0 ? s.mix.cityKm / total : null
}

export const METRICS: Def[] = [
  { key: 'tripKm', label: 'Distance (km)', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.tripKm },
  { key: 'days', label: 'Days', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.days },
  { key: 'kmPerDay', label: 'Km per day', group: 'Usage', better: null, isRate: true, read: (s) => s.usage.kmPerDay },
  { key: 'cityShare', label: 'City driving share', group: 'Usage', better: 'down', isRate: true, read: cityShare },
  { key: 'coldStarts', label: 'Cold starts', group: 'Engine wear & DPF', better: null, isRate: false, read: (s) => s.usage.coldStarts },
  { key: 'coldStartsPer100Km', label: 'Cold starts per 100 km', group: 'Engine wear & DPF', better: 'down', isRate: true, read: (s) => s.usage.coldStartsPer100Km },
  { key: 'shortShareByKm', label: 'Short drives (share of km)', group: 'Engine wear & DPF', better: 'down', isRate: true, read: (s) => s.usage.shortShareByKm },
  { key: 'dpfIndex', label: 'DPF index', group: 'Engine wear & DPF', better: 'up', isRate: true, read: (s) => s.scores.dpfIndex },
  { key: 'tripLPer100', label: 'Trip L/100 km', group: 'Fuel', better: 'down', isRate: true, read: (s) => s.fuel.tripLPer100 },
  { key: 'fullTankLPer100', label: 'Full-tank L/100 km', group: 'Fuel', better: 'down', isRate: true, read: (s) => s.fuel.fullTankLPer100 },
  { key: 'total', label: 'Total cost', group: 'Money', better: null, isRate: false, read: (s) => s.money.total },
  { key: 'costPerKm', label: 'Cost per km', group: 'Money', better: 'down', isRate: true, read: (s) => s.money.costPerKm },
]

export function relativeChange(a: number | null, b: number | null): number | null {
  if (a == null || b == null || a === 0) return null
  return (b - a) / a
}

export function compareStats(a: PeriodStats, b: PeriodStats): MetricRow[] {
  return METRICS.map((m) => {
    const av = m.read(a), bv = m.read(b)
    return { key: m.key, label: m.label, a: av, b: bv, change: relativeChange(av, bv), better: m.better, isRate: m.isRate }
  })
}
