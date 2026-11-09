import { addDays } from './dates'

export interface ExpensePeriod { key: string; label: string; from: string; to: string }

/**
 * The Expenses page periods: Total (since the purchase date, else the first data), Year to date, then each earlier
 * calendar year back to the start year, newest first. `to` is exclusive.
 */
export function expensePeriods(purchaseDate: string | null, today: string, firstData: string | null = null): ExpensePeriod[] {
  const start = purchaseDate ?? firstData
  const end = addDays(today, 1)
  const year = Number(today.slice(0, 4))
  const out: ExpensePeriod[] = []
  if (start) out.push({ key: 'total', label: 'Total', from: start, to: end })
  out.push({ key: 'ytd', label: 'Year to date', from: `${year}-01-01`, to: end })
  if (start) for (let y = year - 1; y >= Number(start.slice(0, 4)); y--) out.push({ key: String(y), label: String(y), from: `${y}-01-01`, to: `${y + 1}-01-01` })
  return out
}
