import type { QueryClient } from '@tanstack/react-query'

export const fmtMoney = (n: number | null | undefined, currency: string) =>
  n == null ? '—' : new Intl.NumberFormat('en', { style: 'currency', currency, maximumFractionDigits: 2 }).format(n)

export const fmtKm = (n: number | null | undefined) =>
  n == null ? '—' : `${new Intl.NumberFormat('en', { maximumFractionDigits: 0 }).format(n)} km`

export const fmtNum = (n: number | null | undefined, digits = 1) =>
  n == null || !Number.isFinite(n) ? '—' : new Intl.NumberFormat('en', { maximumFractionDigits: digits, minimumFractionDigits: 0 }).format(n)

export const fmtPct = (n: number | null | undefined, digits = 0) =>
  n == null || !Number.isFinite(n) ? '—' : `${new Intl.NumberFormat('en', { maximumFractionDigits: digits }).format(n * 100)}%`

export const fmtLPer100 = (n: number | null | undefined) => (n == null || !Number.isFinite(n) ? '—' : `${fmtNum(n, 1)} L/100`)

/** .NET TimeSpan ("[d.]hh:mm:ss[.fff]") as "2 h 05 min". */
export function fmtDuration(ts: string | null | undefined): string {
  const m = ts ? /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(ts) : null
  if (!m) return '—'
  const hours = Number(m[1] ?? 0) * 24 + Number(m[2])
  const min = Number(m[3])
  return hours > 0 ? `${hours} h ${String(min).padStart(2, '0')} min` : `${min} min`
}

/** .NET TimeSpan ("[d.]hh:mm:ss[.fff]") as fractional hours; null when unparsable. */
export function timeSpanHours(ts: string | null | undefined): number | null {
  const m = ts ? /^(?:(\d+)\.)?(\d+):(\d+):(\d+(?:\.\d+)?)/.exec(ts) : null
  if (!m) return null
  return Number(m[1] ?? 0) * 24 + Number(m[2]) + Number(m[3]) / 60 + Number(m[4]) / 3600
}

/** "2025-03-04T08:05:00" as "2025-03-04 08:05". */
export const fmtDateTime = (s: string) => s.slice(0, 16).replace('T', ' ')

export function daysAgo(date: string, today = new Date()): number {
  const d = new Date(`${date}T00:00:00`)
  const t = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  return Math.round((t.getTime() - d.getTime()) / 86_400_000)
}

/** Expenses, tyres and every chart for one vehicle may change after a mutation. */
export function invalidateVehicleData(qc: QueryClient, vehicleId: number) {
  return Promise.all([
    qc.invalidateQueries({ queryKey: ['expenses', vehicleId] }),
    qc.invalidateQueries({ queryKey: ['tyres', vehicleId] }),
    qc.invalidateQueries({ queryKey: ['analytics', vehicleId] }),
    qc.invalidateQueries({ queryKey: ['import-batches', vehicleId] }),
  ])
}

export function errorLines(e: unknown): string[] {
  const errors = (e as { errors?: string[] } | null)?.errors
  if (Array.isArray(errors) && errors.length) return errors
  return [e instanceof Error ? e.message : 'Something went wrong.']
}

/** Fixed colour per entity (never by rank); everything else folds into "Other". */
export const CATEGORY_COLOR_INDEX: Record<string, number> = {
  Fuel: 0, Maintenance: 1, Tyres: 2, Insurance: 3, Repair: 4, Parking: 5, TollsVignette: 6,
}
