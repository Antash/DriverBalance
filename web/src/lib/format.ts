import type { QueryClient } from '@tanstack/react-query'

export const fmtMoney = (n: number | null | undefined, currency: string) =>
  n == null ? '—' : new Intl.NumberFormat('en', { style: 'currency', currency, maximumFractionDigits: 2 }).format(n)

export const fmtKm = (n: number | null | undefined) =>
  n == null ? '—' : `${new Intl.NumberFormat('en', { maximumFractionDigits: 0 }).format(n)} km`

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
