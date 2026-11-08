/** Local-calendar date helpers shared by pickers, forms and pages ("yyyy-MM-dd" strings). */
export const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

export function addDays(s: string, n: number): string {
  const d = new Date(`${s}T00:00:00`)
  d.setDate(d.getDate() + n)
  return iso(d)
}

export const isIsoDate = (s: string | null | undefined): s is string => !!s && /^\d{4}-\d{2}-\d{2}$/.test(s)

/** Same calendar day n years away (Feb 29 clamps to Feb 28 in non-leap years). */
export function addYears(s: string, n: number): string {
  const d = new Date(`${s}T00:00:00`)
  const day = d.getDate()
  d.setDate(1)
  d.setFullYear(d.getFullYear() + n)
  const last = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate()
  d.setDate(Math.min(day, last))
  return iso(d)
}
