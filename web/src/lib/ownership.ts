import type { DateOnly, Ownership } from '../api/types'
import { fmtKm, fmtMoney, fmtDate } from './format'

/** A record dated before the purchase date belongs to the previous owner. */
export const isPreviousOwner = (date: DateOnly, purchaseDate: DateOnly | null | undefined): boolean =>
  !!purchaseDate && date < purchaseDate

/** An interval's `to` is exclusive, so it belongs to the previous owner when to <= purchaseDate. */
export const isPreviousOwnerInterval = (to: DateOnly, purchaseDate: DateOnly | null | undefined): boolean =>
  !!purchaseDate && to <= purchaseDate

export interface OwnershipRow { label: string; value: string; note?: string; strong?: boolean }

/** Rows of the "Cost of ownership" card; null when no purchase date is set. */
export function ownershipRows(o: Ownership, currency: string): OwnershipRow[] | null {
  if (!o.purchaseDate) return null
  const hasResale = o.estimatedResaleValue != null
  return [
    { label: 'Bought on', value: fmtDate(o.purchaseDate) },
    { label: 'Purchase costs', value: fmtMoney(o.purchaseTotal, currency) },
    { label: 'Running costs since purchase', value: fmtMoney(o.runningCosts, currency) },
    { label: 'Km driven since purchase', value: fmtKm(o.kmDriven) },
    { label: 'Estimated resale value', value: hasResale ? fmtMoney(o.estimatedResaleValue, currency) : 'not set',
      note: hasResale && o.resaleEstimateDate ? `on ${fmtDate(o.resaleEstimateDate)}` : undefined },
    { label: 'Net cost of ownership', value: fmtMoney(o.netCost, currency), strong: true },
    { label: 'Net cost per km', value: fmtMoney(o.netCostPerKm, currency) },
    { label: 'Net cost per month', value: fmtMoney(o.netCostPerMonth, currency) },
  ]
}
