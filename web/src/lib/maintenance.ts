import type { Expense, MaintenanceType } from '../api/types'
import { MAINTENANCE_TYPES } from '../components/ExpenseForm'

type ServiceExpense = Pick<Expense, 'id' | 'date' | 'odometer' | 'maintenance' | 'amount' | 'currency' | 'homeAmount' | 'note'>

export interface WorkItem { type: MaintenanceType; sinceDays: number | null; sinceKm: number | null }
export interface ServiceRow<E extends ServiceExpense = ServiceExpense> {
  id: number; date: string; odometer: number | null; work: WorkItem[]; expense: E
}

const daysBetween = (a: string, b: string) =>
  Math.round((Date.parse(`${b}T00:00:00Z`) - Date.parse(`${a}T00:00:00Z`)) / 86_400_000)

/**
 * Maintenance other than oil changes (those live on the Intervals page): every service that included other work,
 * newest first, each work item with the days and km since that same work was last done.
 */
export function maintenanceHistory<E extends ServiceExpense>(expenses: E[]): { services: ServiceRow<E>[]; types: MaintenanceType[] } {
  const oldestFirst = expenses
    .map((e) => ({ e, work: MAINTENANCE_TYPES.filter((t) => t !== 'OilChange' && e.maintenance.includes(t)) }))
    .filter((x) => x.work.length > 0)
    .sort((a, b) => (a.e.date < b.e.date ? -1 : a.e.date > b.e.date ? 1 : a.e.id - b.e.id))

  const last = new Map<MaintenanceType, E>()
  const services: ServiceRow<E>[] = oldestFirst.map(({ e, work }) => ({
    id: e.id, date: e.date, odometer: e.odometer, expense: e,
    work: work.map((type) => {
      const prev = last.get(type)
      last.set(type, e)
      return {
        type,
        sinceDays: prev ? daysBetween(prev.date, e.date) : null,
        sinceKm: prev && prev.odometer != null && e.odometer != null ? e.odometer - prev.odometer : null,
      }
    }),
  }))
  const seen = new Set(services.flatMap((s) => s.work.map((w) => w.type)))
  return { services: services.reverse(), types: MAINTENANCE_TYPES.filter((t) => seen.has(t)) }
}
