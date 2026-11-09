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

/** "2026-11-09" as "09/11/2026"; '' for anything that is not an ISO date. */
export const toDmy = (s: string | null | undefined): string => {
  const m = s ? /^(\d{4})-(\d{2})-(\d{2})/.exec(s) : null
  return m ? `${m[3]}/${m[2]}/${m[1]}` : ''
}

/** Parses d/m/yyyy (also . or - separators) into ISO; null when malformed or not a real calendar day. */
export function parseDmy(text: string): string | null {
  const m = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(text.trim())
  if (!m) return null
  const [d, mo, y] = [Number(m[1]), Number(m[2]), Number(m[3])]
  const dt = new Date(y, mo - 1, d)
  if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return null
  return `${m[3]}-${String(mo).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}
