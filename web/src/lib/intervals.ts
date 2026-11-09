import type { FuelTrendChart, Period } from '../api/types'
import { addDays } from './dates'

/** Date range covering both intervals (to exclusive) and the service date between them. */
export function intervalWindow(a: Period, b: Period | null): { from: string; to: string; serviceDate: string | null } {
  if (!b || b.from === a.from) return { from: a.from, to: a.to, serviceDate: null }
  const [older, newer] = a.from < b.from ? [a, b] : [b, a]
  return { from: older.from, to: newer.to > older.to ? newer.to : older.to, serviceDate: newer.from }
}

/** Date range covering every interval (to exclusive) and each service date inside it, oldest first. */
export function allIntervalsWindow(periods: Period[]): { from: string; to: string; serviceDates: string[] } | null {
  if (periods.length === 0) return null
  const starts = periods.map((p) => p.from).sort()
  const to = periods.map((p) => p.to).sort().at(-1)!
  return { from: starts[0], to, serviceDates: starts.slice(1) }
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
