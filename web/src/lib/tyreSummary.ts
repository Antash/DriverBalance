import type { PartSource, TyreSeason, TyreSet, TyreSetSummary } from '../api/types'

export type TyreStatus = 'Fitted' | 'Stored' | 'Retired'
export interface TyreRow { set: TyreSet; name: string; size: string | null; season: TyreSeason; status: TyreStatus; km: number; fittedSince: string | null }

const ORDER: TyreStatus[] = ['Fitted', 'Stored', 'Retired']

export const seasonLabel = (s: TyreSeason) => (s === 'AllSeason' ? 'All-season' : s)

export function tyreRows(summaries: TyreSetSummary[]): TyreRow[] {
  return summaries
    .map((s): TyreRow => ({
      set: s.set, name: s.set.name, size: s.set.size, season: s.set.season, km: s.totalKm,
      status: s.mounted ? 'Fitted' : s.set.retired ? 'Retired' : 'Stored',
      fittedSince: s.mounted ? (s.spans.find((sp) => sp.to == null)?.from ?? null) : null,
    }))
    .sort((a, b) => ORDER.indexOf(a.status) - ORDER.indexOf(b.status) || a.name.localeCompare(b.name))
}

/** "Original", "Brand Model", or null when no part was recorded. */
export function partLine(w: { part: PartSource; brand: string | null; model: string | null }): string | null {
  if (w.part === 'Original') return 'Original'
  if (w.part === 'Aftermarket') return [w.brand, w.model].filter((x) => x && x.trim()).join(' ') || null
  return null
}
