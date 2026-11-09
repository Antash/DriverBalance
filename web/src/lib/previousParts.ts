import type { Expense, MaintenanceType } from '../api/types'

export interface PreviousPart { brand: string; model: string | null }

/** Identity of a work item across records: its type, or a custom work's name ignoring case. */
export const workKey = (type: MaintenanceType | null, name: string): string =>
  type ?? `custom:${name.trim().toLowerCase()}`

/**
 * Other-brand products used before for the same work item, most recent first, without duplicates (brand and model
 * compared ignoring case and surrounding spaces; the latest spelling wins). `excludeId` leaves out the record being edited.
 */
export function previousParts(expenses: Expense[], key: string, excludeId?: number | null): PreviousPart[] {
  const newestFirst = [...expenses].sort((a, b) => (a.date < b.date ? 1 : a.date > b.date ? -1 : b.id - a.id))
  const seen = new Set<string>()
  const result: PreviousPart[] = []
  for (const e of newestFirst) {
    if (e.id === excludeId) continue
    for (const w of e.work ?? []) {
      if (w.part !== 'Aftermarket' || !w.brand?.trim() || workKey(w.type, w.name ?? '') !== key) continue
      const brand = w.brand.trim(), model = w.model?.trim() || null
      const id = `${brand.toLowerCase()}|${(model ?? '').toLowerCase()}`
      if (seen.has(id)) continue
      seen.add(id)
      result.push({ brand, model })
    }
  }
  return result
}
