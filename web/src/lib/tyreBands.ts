import type { TyreMountSpan } from '../api/types'

/** One key entry per tyre set (null = unassigned), in first-fitted order; colorIndex matches the band colour. */
export function tyreBandKey(bands: TyreMountSpan[]): { tyreSetId: number | null; colorIndex: number }[] {
  const ids = [...new Set(bands.map((b) => b.tyreSetId))]
  return ids.map((tyreSetId, colorIndex) => ({ tyreSetId, colorIndex }))
}

/** The tyre set fitted on a yyyy-MM-dd date: from inclusive, to exclusive (null to = still fitted); undefined when none. */
export function tyreSetIdAt(bands: TyreMountSpan[], date: string): number | null | undefined {
  return bands.find((b) => b.from <= date && (b.to == null || date < b.to))?.tyreSetId
}
