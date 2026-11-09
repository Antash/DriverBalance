const YEAR_MS = 365.25 * 86_400_000
/** At most this many years are shown across the card; longer histories scroll sideways. */
export const VISIBLE_YEARS = 10

/** Chart width as a percentage of its card: 100 for up to 10 years of history, proportionally wider beyond. */
export function timelineWidthPercent(first: string | null, last: string | null): number {
  if (!first || !last) return 100
  const years = (Date.parse(`${last}T00:00:00Z`) - Date.parse(`${first}T00:00:00Z`)) / YEAR_MS
  return years > VISIBLE_YEARS ? (years / VISIBLE_YEARS) * 100 : 100
}
