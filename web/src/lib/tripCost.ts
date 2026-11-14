import type { TripCost } from '../api/types'
import { fmtApprox } from './format'

/** Hover text with the split: "Fuel ≈ 80 CZK · Time ≈ 40 CZK · Wear ≈ 22 CZK". */
export const costTitle = (c: TripCost) =>
  `Fuel ${fmtApprox(c.fuel, c.currency)} · Time ${fmtApprox(c.time, c.currency)} · Wear ${fmtApprox(c.wear, c.currency)}`

/** "≈ 142 CZK", or a dash when nothing at all is known (no fuel price and no other costs). */
export const costText = (c: TripCost) =>
  c.fuel == null && c.time + c.wear === 0 ? '—' : fmtApprox(c.total, c.currency)
