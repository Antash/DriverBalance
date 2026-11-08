import type { FuelTrendChart, MaintenanceType, Period } from '../api/types'
import { MAINTENANCE_TYPES } from '../components/ExpenseForm'
import { addDays } from './dates'

/** Intervals arrive newest first: [current, previous, ..., "before first"]. */
export function pickDefaultIntervals<T>(intervals: T[]): [T | null, T | null] {
  return [intervals[0] ?? null, intervals[1] ?? null]
}

/** Maintenance types that occur in at least one expense, in catalogue order. */
export function presentTypes(expenses: { maintenance: MaintenanceType[] }[]): MaintenanceType[] {
  const seen = new Set(expenses.flatMap((e) => e.maintenance))
  return MAINTENANCE_TYPES.filter((t) => seen.has(t))
}

export function defaultType(types: MaintenanceType[]): MaintenanceType | null {
  return types.includes('OilChange') ? 'OilChange' : (types[0] ?? null)
}

/** Date range covering both intervals (to exclusive) and the service date between them. */
export function intervalWindow(a: Period, b: Period | null): { from: string; to: string; serviceDate: string | null } {
  if (!b || b.from === a.from) return { from: a.from, to: a.to, serviceDate: null }
  const [older, newer] = a.from < b.from ? [a, b] : [b, a]
  return { from: older.from, to: newer.to > older.to ? newer.to : older.to, serviceDate: newer.from }
}

const mondayOf = (date: string) => {
  const d = new Date(`${date}T00:00:00`)
  return addDays(date, -((d.getDay() + 6) % 7))
}

/** Fuel-trend points collapsed to one km-weighted L/100 value per Monday-start week. */
export function weeklyLPer100(points: Pick<FuelTrendChart['points'][number], 'time' | 'lPer100' | 'km'>[]): { weekStart: string; lPer100: number }[] {
  const weeks = new Map<string, { litres: number; km: number }>()
  for (const p of points) {
    const w = mondayOf(p.time.slice(0, 10))
    const acc = weeks.get(w) ?? { litres: 0, km: 0 }
    acc.litres += (p.lPer100 * p.km) / 100
    acc.km += p.km
    weeks.set(w, acc)
  }
  return [...weeks.entries()]
    .filter(([, v]) => v.km > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1))
    .map(([weekStart, v]) => ({ weekStart, lPer100: (v.litres / v.km) * 100 }))
}
