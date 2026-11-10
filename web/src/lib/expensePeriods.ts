import { addDays } from './dates'

export interface ExpensePeriod { key: string; label: string; from: string; to: string }

/**
 * The Expenses page periods: Total (since the purchase date, else the first record, else the last 12 months), the last 12 months (365 days up to
 * today), then every calendar year from this one back to the year of the first record. `to` is exclusive.
 */
export function expensePeriods(purchaseDate: string | null, today: string, firstRecord: string | null): ExpensePeriod[] {
  const end = addDays(today, 1)
  const last12 = addDays(end, -365)
  // Total is always offered (it is the default and keeps the switch the same on every profile); with nothing recorded yet
  // it spans the last 12 months.
  const start = purchaseDate ?? firstRecord ?? last12
  const out: ExpensePeriod[] = []
  out.push({ key: 'total', label: 'Total', from: start, to: end })
  out.push({ key: 'last12', label: 'Last 12 months', from: last12, to: end })
  if (firstRecord) {
    for (let y = Number(today.slice(0, 4)); y >= Number(firstRecord.slice(0, 4)); y--) {
      out.push({ key: String(y), label: String(y), from: `${y}-01-01`, to: `${y + 1}-01-01` })
    }
  }
  return out
}
