import type { QueryClient } from '@tanstack/react-query'
import { toDmy } from './dates'

export const fmtMoney = (n: number | null | undefined, currency: string) =>
  n == null ? '—' : new Intl.NumberFormat('en', { style: 'currency', currency, maximumFractionDigits: 2 }).format(n)

/** A rounded estimate, e.g. "≈ 142 CZK"; the em dash for null. */
export const fmtApprox = (n: number | null | undefined, currency: string) =>
  n == null || !Number.isFinite(n) ? '—' : `≈ ${fmtNum(n, 0)} ${currency}`

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

/** "2026-11-09" (or a datetime) as "09/11/2026"; the em dash for empty. */
export const fmtDate = (iso: string | null | undefined): string => (iso ? toDmy(iso) || '—' : '—')

/** "2026-11-09T08:05:00" as "09/11/2026 08:05". */
export const fmtDateTime = (s: string) => `${toDmy(s)} ${s.slice(11, 16)}`

export function daysAgo(date: string, today = new Date()): number {
  const d = new Date(`${date}T00:00:00`)
  const t = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  return Math.round((t.getTime() - d.getTime()) / 86_400_000)
}

export function fmtBytes(n: number): string {
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${fmtNum(n / 1024, 1)} KB`
  return `${fmtNum(n / (1024 * 1024), 1)} MB`
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

/** A UTC instant (e.g. "2026-11-09T07:05:00Z") as local "dd/mm/yyyy HH:mm". */
export function fmtInstant(s: string): string {
  const d = new Date(s)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`
}
