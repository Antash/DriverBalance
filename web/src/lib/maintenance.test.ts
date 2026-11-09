import { describe, expect, it } from 'vitest'
import type { MaintenanceType } from '../api/types'
import { maintenanceHistory } from './maintenance'

const e = (id: number, date: string, odometer: number | null, maintenance: MaintenanceType[]) =>
  ({ id, date, odometer, maintenance, amount: 100, currency: 'CZK', homeAmount: null, note: null })

describe('maintenanceHistory', () => {
  it('drops oil changes and services with nothing else, newest first', () => {
    const h = maintenanceHistory([
      e(1, '2025-02-06', 7350, ['OilChange', 'BrakeFluid']),
      e(2, '2025-06-01', 12000, ['OilChange']),
      e(3, '2025-10-22', 19200, ['TyreSwap']),
    ])
    expect(h.services.map((s) => s.id)).toEqual([3, 1])
    expect(h.services[1].work.map((w) => w.type)).toEqual(['BrakeFluid'])
    expect(h.types).toEqual(['BrakeFluid', 'TyreSwap'])
  })

  it('gives days and km since the same work was last done', () => {
    const h = maintenanceHistory([
      e(1, '2025-10-22', 19200, ['TyreSwap']),
      e(2, '2026-03-18', 27400, ['TyreSwap', 'CabinFilter']),
    ])
    const latest = h.services[0]
    expect(latest.work).toEqual([
      { type: 'TyreSwap', sinceDays: 147, sinceKm: 8200 },
      { type: 'CabinFilter', sinceDays: null, sinceKm: null },
    ])
    expect(h.services[1].work[0]).toEqual({ type: 'TyreSwap', sinceDays: null, sinceKm: null })
  })

  it('leaves km empty when either odometer is missing', () => {
    const h = maintenanceHistory([e(1, '2025-01-01', null, ['Coolant']), e(2, '2025-01-31', 5000, ['Coolant'])])
    expect(h.services[0].work[0]).toEqual({ type: 'Coolant', sinceDays: 30, sinceKm: null })
  })

  it('orders types in catalogue order', () => {
    const h = maintenanceHistory([e(1, '2025-01-01', 1, ['NewTyres', 'BrakeFluid'])])
    expect(h.types).toEqual(['BrakeFluid', 'NewTyres'])
  })
})
