import type { DateOnly, Ownership, OwnershipFinancing } from '../api/types'
import { fmtKm, fmtMoney, fmtDate } from './format'

/** A record dated before the purchase date belongs to the previous owner. */
export const isPreviousOwner = (date: DateOnly, purchaseDate: DateOnly | null | undefined): boolean =>
  !!purchaseDate && date < purchaseDate

/** An interval's `to` is exclusive, so it belongs to the previous owner when to <= purchaseDate. */
export const isPreviousOwnerInterval = (to: DateOnly, purchaseDate: DateOnly | null | undefined): boolean =>
  !!purchaseDate && to <= purchaseDate

export interface OwnershipRow { label: string; value: string; note?: string; strong?: boolean }

/** "36 × 9,500 monthly from 01/05/2025 + balloon 200,000" */
export function financingSchedule(f: Pick<OwnershipFinancing, 'start' | 'amountPerPayment' | 'frequency' | 'payments' | 'balloon'>, currency: string): string {
  const base = `${f.payments} × ${fmtMoney(f.amountPerPayment, currency)} ${f.frequency.toLowerCase()} from ${fmtDate(f.start)}`
  return f.balloon > 0 ? `${base} + balloon ${fmtMoney(f.balloon, currency)}` : base
}

/** Rows of the "Cost of ownership" card; null when no purchase date is set. */
export function ownershipRows(o: Ownership, currency: string): OwnershipRow[] | null {
  if (!o.purchaseDate) return null
  const hasResale = o.estimatedResaleValue != null
  return [
    { label: 'Bought on', value: fmtDate(o.purchaseDate) },
    { label: 'Purchase costs', value: fmtMoney(o.lumpSumTotal, currency) },
    ...(o.financing ? [{ label: 'Financing', value: fmtMoney(o.financing.total, currency), note: financingSchedule(o.financing, currency) }] : []),
    { label: 'Running costs since purchase', value: fmtMoney(o.runningCosts, currency) },
    { label: 'Km driven since purchase', value: fmtKm(o.kmDriven) },
    { label: 'Estimated resale value', value: hasResale ? fmtMoney(o.estimatedResaleValue, currency) : 'not set',
      note: hasResale && o.resaleEstimateDate ? `on ${fmtDate(o.resaleEstimateDate)}` : undefined },
    { label: 'Net cost of ownership', value: fmtMoney(o.netCost, currency), strong: true },
    { label: 'Net cost per km', value: fmtMoney(o.netCostPerKm, currency) },
    { label: 'Net cost per month', value: fmtMoney(o.netCostPerMonth, currency) },
  ]
}
