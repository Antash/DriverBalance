import type { Expense, MaintenanceType, PartSource, WorkPosition } from '../api/types'
import { MAINTENANCE_TYPES, maintenanceLabel } from './maintenanceCatalog'

type ServiceExpense = Pick<Expense, 'id' | 'date' | 'odometer' | 'maintenance' | 'amount' | 'currency' | 'homeAmount' | 'note'> & { work?: Expense['work'] }

export interface WorkItem {
  key: string; label: string; type: MaintenanceType | null
  part: PartSource; brand: string | null; model: string | null; details: string | null
  /** Brake pads, discs and suspension: which axle. Wiper blades: rubber refill only. */
  position?: WorkPosition | null; rubberOnly?: boolean
  sinceDays: number | null; sinceKm: number | null
}
export interface ServiceRow<E extends ServiceExpense = ServiceExpense> {
  id: number; date: string; odometer: number | null; work: WorkItem[]; expense: E
}

const daysBetween = (a: string, b: string) =>
  Math.round((Date.parse(`${b}T00:00:00Z`) - Date.parse(`${a}T00:00:00Z`)) / 86_400_000)

const ALL_TYPES: MaintenanceType[] = [...MAINTENANCE_TYPES, 'NewTyres']

export type Raw = Omit<WorkItem, 'sinceDays' | 'sinceKm'>

/** A record's work items: its `work` list when present, else one item per flag. */
export function rawItems(e: ServiceExpense): Raw[] {
  if (e.work && e.work.length > 0) {
    return e.work.flatMap((w): Raw[] => {
      if (w.type) return [{ key: w.type, label: maintenanceLabel(w.type), type: w.type, part: w.part, brand: w.brand, model: w.model, details: w.details, position: w.position ?? null, rubberOnly: !!w.rubberOnly }]
      const name = w.name?.trim()
      if (!name) return []
      return [{ key: `custom:${name.toLowerCase()}`, label: name, type: null, part: w.part, brand: w.brand, model: w.model, details: w.details }]
    })
  }
  return ALL_TYPES.filter((t) => e.maintenance.includes(t))
    .map((t) => ({ key: t, label: maintenanceLabel(t), type: t, part: 'None' as PartSource, brand: null, model: null, details: null }))
}

/**
 * All maintenance work: every service with work items, newest first, each item with the days and km since
 * that same work (same key) was last done. Lanes: typed work in catalogue order, then custom names as first seen.
 */
export function maintenanceHistory<E extends ServiceExpense>(expenses: E[]): { services: ServiceRow<E>[]; lanes: string[] } {
  const oldestFirst = expenses
    .map((e) => ({ e, items: rawItems(e) }))
    .filter((x) => x.items.length > 0)
    .sort((a, b) => (a.e.date < b.e.date ? -1 : a.e.date > b.e.date ? 1 : a.e.id - b.e.id))

  const last = new Map<string, E>()
  const firstLabel = new Map<string, string>()
  const services: ServiceRow<E>[] = oldestFirst.map(({ e, items }) => ({
    id: e.id, date: e.date, odometer: e.odometer, expense: e,
    work: items.map((it) => {
      const prev = last.get(it.key)
      last.set(it.key, e)
      if (!firstLabel.has(it.key)) firstLabel.set(it.key, it.label)
      return {
        ...it, label: firstLabel.get(it.key)!,
        sinceDays: prev ? daysBetween(prev.date, e.date) : null,
        sinceKm: prev && prev.odometer != null && e.odometer != null ? e.odometer - prev.odometer : null,
      }
    }),
  }))
  const typed = ALL_TYPES.filter((t) => firstLabel.has(t)).map((t) => maintenanceLabel(t))
  const seen = new Set(typed.map((l) => l.toLowerCase()))
  const custom = [...firstLabel].filter(([k]) => k.startsWith('custom:')).map(([, l]) => l)
    .filter((l) => !seen.has(l.toLowerCase()) && seen.add(l.toLowerCase()))
  return { services: services.reverse(), lanes: [...typed, ...custom] }
}
