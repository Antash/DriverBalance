import type { ExpenseCategory, PeriodStats } from '../api/types'
import { timeSpanHours } from './format'

export type BaseMetricKey =
  | 'tripKm' | 'odometerKm' | 'tripCoverage' | 'days' | 'kmPerDay' | 'averageSpeedKmh' | 'drivingHoursPerDay'
  | 'daysUsed' | 'kmPerDayUsed' | 'drivingHours'
  | 'cityShare' | 'mixedShare' | 'highwayShare'
  | 'coldStarts' | 'coldStartsPer100Km' | 'shortShareByCount' | 'shortShareByKm' | 'dpfIndex'
  | 'tripLPer100' | 'fullTankLPer100'
  | 'total' | 'costPerKm' | 'needsConversionCount'
export type MetricKey = BaseMetricKey | `cat:${ExpenseCategory}`

export type MetricGroup = 'Usage' | 'Engine wear & DPF' | 'Fuel' | 'Money'

export interface MetricRow {
  key: MetricKey; label: string; group: MetricGroup
  a: number | null; b: number | null; change: number | null; better: 'up' | 'down' | null; isRate: boolean
}

interface Def {
  key: BaseMetricKey
  label: string
  group: MetricGroup
  /** Direction that is good for the car/wallet; null for pure volume metrics and neutral rates. */
  better: 'up' | 'down' | null
  /** Rates stay comparable between periods of different length; volumes do not. */
  isRate: boolean
  read(s: PeriodStats): number | null
}

const mixTotal = (s: PeriodStats) => s.mix.cityKm + s.mix.mixedKm + s.mix.highwayKm
const share = (pick: (s: PeriodStats) => number) => (s: PeriodStats) => {
  const total = mixTotal(s)
  return total > 0 ? pick(s) / total : null
}

const drivingHoursPerDay = (s: PeriodStats) => {
  const hours = timeSpanHours(s.usage.drivingTime)
  return hours != null && s.usage.elapsedDays > 0 ? hours / s.usage.elapsedDays : null
}

export const METRICS: Def[] = [
  { key: 'tripKm', label: 'Distance (km)', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.tripKm },
  { key: 'odometerKm', label: 'Odometer distance (km)', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.odometerKm },
  { key: 'tripCoverage', label: 'Trip coverage of odometer', group: 'Usage', better: null, isRate: true, read: (s) => s.usage.tripCoverage },
  { key: 'days', label: 'Days', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.days },
  { key: 'daysUsed', label: 'Days used', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.daysUsed },
  { key: 'kmPerDayUsed', label: 'Km per day used', group: 'Usage', better: null, isRate: true, read: (s) => s.usage.kmPerDayUsed },
  { key: 'drivingHours', label: 'Driving hours', group: 'Usage', better: null, isRate: false, read: (s) => s.usage.drivingHours },
  { key: 'kmPerDay', label: 'Km per day', group: 'Usage', better: null, isRate: true, read: (s) => s.usage.kmPerDay },
  { key: 'drivingHoursPerDay', label: 'Driving time per day (h)', group: 'Usage', better: null, isRate: true, read: drivingHoursPerDay },
  { key: 'averageSpeedKmh', label: 'Average speed (km/h)', group: 'Usage', better: 'up', isRate: true, read: (s) => s.usage.averageSpeedKmh },
  { key: 'cityShare', label: 'City driving share', group: 'Usage', better: 'down', isRate: true, read: share((s) => s.mix.cityKm) },
  { key: 'mixedShare', label: 'Mixed driving share', group: 'Usage', better: null, isRate: true, read: share((s) => s.mix.mixedKm) },
  { key: 'highwayShare', label: 'Highway driving share', group: 'Usage', better: 'up', isRate: true, read: share((s) => s.mix.highwayKm) },
  { key: 'coldStarts', label: 'Cold starts', group: 'Engine wear & DPF', better: null, isRate: false, read: (s) => s.usage.coldStarts },
  { key: 'coldStartsPer100Km', label: 'Cold starts per 100 km', group: 'Engine wear & DPF', better: 'down', isRate: true, read: (s) => s.usage.coldStartsPer100Km },
  { key: 'shortShareByCount', label: 'Short drives (share of drives)', group: 'Engine wear & DPF', better: 'down', isRate: true, read: (s) => s.usage.shortShareByCount },
  { key: 'shortShareByKm', label: 'Short drives (share of km)', group: 'Engine wear & DPF', better: 'down', isRate: true, read: (s) => s.usage.shortShareByKm },
  { key: 'dpfIndex', label: 'DPF index', group: 'Engine wear & DPF', better: 'up', isRate: true, read: (s) => s.scores.dpfIndex },
  { key: 'tripLPer100', label: 'Trip L/100 km', group: 'Fuel', better: 'down', isRate: true, read: (s) => s.fuel.tripLPer100 },
  { key: 'fullTankLPer100', label: 'Full-tank L/100 km', group: 'Fuel', better: 'down', isRate: true, read: (s) => s.fuel.fullTankLPer100 },
  { key: 'total', label: 'Total cost', group: 'Money', better: null, isRate: false, read: (s) => s.money.total },
  { key: 'costPerKm', label: 'Cost per km', group: 'Money', better: 'down', isRate: true, read: (s) => s.money.costPerKm },
  { key: 'needsConversionCount', label: 'Expenses needing conversion', group: 'Money', better: null, isRate: false, read: (s) => s.money.needsConversionCount },
]

export function relativeChange(a: number | null, b: number | null): number | null {
  if (a == null || b == null || a === 0) return null
  return (b - a) / a
}

const CATEGORY_LABEL: Record<string, string> = { TollsVignette: 'Road tolls' }
const categoryLabel = (c: string) => CATEGORY_LABEL[c] ?? c.replace(/([a-z])([A-Z])/g, '$1 $2')

/**
 * Compares two periods; the change is (B - A) / A, so A must be the baseline (older) and B the newer period.
 * Volume rows lose their change when the periods differ in length (the numbers are not comparable).
 */
export function compareStats(a: PeriodStats, b: PeriodStats): MetricRow[] {
  const sameLength = a.usage.days === b.usage.days
  const make = (key: MetricKey, label: string, group: MetricGroup, better: 'up' | 'down' | null, isRate: boolean,
    av: number | null, bv: number | null): MetricRow => ({
    key, label, group, a: av, b: bv, better, isRate,
    change: isRate || sameLength ? relativeChange(av, bv) : null,
  })

  const rows = METRICS.map((m) => make(m.key, m.label, m.group, m.better, m.isRate, m.read(a), m.read(b)))
  const categories = new Set([...Object.keys(a.money.byCategory), ...Object.keys(b.money.byCategory)] as ExpenseCategory[])
  for (const c of [...categories].sort()) {
    rows.push(make(`cat:${c}`, categoryLabel(c), 'Money', null, false, a.money.byCategory[c] ?? null, b.money.byCategory[c] ?? null))
  }
  return rows
}

interface Range { from: string; to: string }

/**
 * Request periods for a Statistics comparison, always baseline first: with a comparison range the result is
 * [comparison (A, baseline), main range (B)]; without one just [main range].
 */
export function comparisonRequest(range: Range, comparison: Range | null): { periods: (Range & { label: string })[] } {
  const periods = [{ ...range, label: 'Range' }]
  if (comparison) periods.unshift({ ...comparison, label: 'Compare' })
  return { periods }
}

export interface MultiMetricRow {
  key: MetricKey; label: string; group: MetricGroup; better: 'up' | 'down' | null
  /** One value per period, in the order given. */
  values: (number | null)[]
  /** Indices of the best and worst periods (all ties); empty without a good direction or with fewer than two distinct values. */
  best: number[]; worst: number[]
}

function extremes(values: (number | null)[], better: 'up' | 'down' | null, ranked: boolean[]): { best: number[]; worst: number[] } {
  values = values.map((v, i) => (ranked[i] ? v : null))
  const present = values.filter((v): v is number => v != null)
  if (better == null || present.length < 2) return { best: [], worst: [] }
  const hi = Math.max(...present), lo = Math.min(...present)
  if (hi === lo) return { best: [], worst: [] }
  const at = (target: number) => values.flatMap((v, i) => (v === target ? [i] : []))
  return better === 'up' ? { best: at(hi), worst: at(lo) } : { best: at(lo), worst: at(hi) }
}

/** Volumes that only trips can measure: with no trips in a period they are unknown, not zero. */
const TRIP_VOLUMES: MetricKey[] = ['tripKm', 'coldStarts', 'drivingHoursPerDay', 'daysUsed', 'kmPerDayUsed', 'drivingHours']

/**
 * Any number of periods side by side, with the best and worst marked on metrics that have a good direction.
 * Periods without trips (e.g. before the trip data starts) show their odometer and money figures but are not ranked.
 */
export function compareMany(list: PeriodStats[]): MultiMetricRow[] {
  const ranked = list.map((s) => s.usage.tripCount > 0)
  const make = (key: MetricKey, label: string, group: MetricGroup, better: 'up' | 'down' | null, values: (number | null)[]): MultiMetricRow =>
    ({ key, label, group, better, values, ...extremes(values, better, ranked) })
  const read = (m: Def, s: PeriodStats) => (s.usage.tripCount === 0 && TRIP_VOLUMES.includes(m.key) ? null : m.read(s))

  const rows = METRICS.map((m) => make(m.key, m.label, m.group, m.better, list.map((s) => read(m, s))))
  const categories = new Set(list.flatMap((s) => Object.keys(s.money.byCategory)) as ExpenseCategory[])
  for (const c of [...categories].sort()) {
    rows.push(make(`cat:${c}`, categoryLabel(c), 'Money', null, list.map((s) => s.money.byCategory[c] ?? null)))
  }
  return rows
}

/** Hours as a .NET TimeSpan string ("d.hh:mm:ss" when a day or more), as the API sends driving time. */
function toTimeSpan(hours: number): string {
  const totalSeconds = Math.round(hours * 3600)
  const d = Math.floor(totalSeconds / 86400), h = Math.floor((totalSeconds % 86400) / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60), s = totalSeconds % 60
  const hms = [h, m, s].map((x) => String(x).padStart(2, '0')).join(':')
  return d > 0 ? `${d}.${hms}` : hms
}

/** Mean of the numbers present; null when none are. */
const mean = (xs: unknown[]): number | null => {
  const nums = xs.filter((x): x is number => typeof x === 'number')
  return nums.length > 0 ? nums.reduce((a, b) => a + b, 0) / nums.length : null
}

function averageNode(nodes: unknown[]): unknown {
  const present = nodes.filter((n) => n != null)
  if (present.length === 0) return null
  if (present.every((n) => typeof n === 'number')) return mean(present)
  if (typeof present[0] === 'object' && !Array.isArray(present[0])) {
    const keys = new Set(present.flatMap((n) => Object.keys(n as object)))
    return Object.fromEntries([...keys].map((k) => [k, averageNode(present.map((n) => (n as Record<string, unknown>)[k]))]))
  }
  return present[0]
}

/**
 * An "average period" from several periods, for comparing the current interval with the typical one: every figure is
 * the mean of the periods that have it; driving time is averaged in hours; a category missing from a period counts as 0.
 */
export function averageStats(list: PeriodStats[]): PeriodStats {
  const avg = averageNode(list) as PeriodStats
  const hours = mean(list.map((s) => timeSpanHours(s.usage.drivingTime)))
  const categories = new Set(list.flatMap((s) => Object.keys(s.money.byCategory))) as Set<ExpenseCategory>
  return {
    ...avg,
    usage: { ...avg.usage, drivingTime: hours == null ? avg.usage.drivingTime : toTimeSpan(hours) },
    money: { ...avg.money, byCategory: Object.fromEntries([...categories].map((c) => [c, mean(list.map((s) => s.money.byCategory[c] ?? 0))])) },
  }
}

/** Rows for a car: the DPF index is dropped when the car has no DPF (petrol, hybrid, electric). */
export function visibleMetrics<T extends { key: MetricKey }>(rows: T[], hasDpf: boolean): T[] {
  return hasDpf ? rows : rows.filter((r) => r.key !== 'dpfIndex')
}

/** The Engine wear group is named after the DPF only on cars that have one. */
export function groupTitle(group: MetricGroup, hasDpf: boolean): string {
  return !hasDpf && group === 'Engine wear & DPF' ? 'Engine wear' : group
}
