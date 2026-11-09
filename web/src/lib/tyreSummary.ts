import type { PartSource, TyreSeason, TyreSet, TyreSetSummary } from '../api/types'

export type TyreStatus = 'Fitted' | 'Stored' | 'Retired'
export interface TyreRow { set: TyreSet; name: string; size: string | null; season: TyreSeason; status: TyreStatus; km: number
  /** Start of the current span, for the set on the car. */
  fittedSince: string | null
  /** Start of the earliest span: the first day for the set the car came with. */
  firstFitted: string | null
}

const ORDER: TyreStatus[] = ['Fitted', 'Stored', 'Retired']

export const seasonLabel = (s: TyreSeason) => (s === 'AllSeason' ? 'All-season' : s)

/** "Brand Model" when either is present, else the stored name. */
export function tyreDisplayName(set: TyreSet): string {
  return [set.brand, set.model].filter((x) => x && x.trim()).join(' ') || set.name
}

/** The name the server derives for a set with no explicit name. */
export function derivedTyreName(s: { brand: string | null; model: string | null; season: TyreSeason }): string {
  const joined = `${s.brand?.trim() ?? ''} ${s.model?.trim() ?? ''}`.trim()
  return (joined || `${seasonLabel(s.season)} tyres`).slice(0, 100)
}

export function tyreRows(summaries: TyreSetSummary[]): TyreRow[] {
  return summaries
    .map((s): TyreRow => ({
      set: s.set, name: tyreDisplayName(s.set), size: s.set.size, season: s.set.season, km: s.totalKm,
      status: s.mounted ? 'Fitted' : s.set.retired ? 'Retired' : 'Stored',
      fittedSince: s.mounted ? (s.spans.find((sp) => sp.to == null)?.from ?? null) : null,
      firstFitted: s.spans.map((sp) => sp.from).sort()[0] ?? null,
    }))
    .sort((a, b) => ORDER.indexOf(a.status) - ORDER.indexOf(b.status) || a.name.localeCompare(b.name))
}

/** "Original", "Brand Model", or null when no part was recorded. */
export function partLine(w: { part: PartSource; brand: string | null; model: string | null }): string | null {
  if (w.part === 'Original') return 'Original'
  if (w.part === 'Aftermarket') return [w.brand, w.model].filter((x) => x && x.trim()).join(' ') || null
  return null
}
