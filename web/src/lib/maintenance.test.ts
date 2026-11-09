import { describe, expect, it } from 'vitest'
import type { MaintenanceType, MaintenanceWork } from '../api/types'
import { maintenanceHistory } from './maintenance'

const e = (id: number, date: string, odometer: number | null, maintenance: MaintenanceType[], work?: MaintenanceWork[]) =>
  ({ id, date, odometer, maintenance, work, amount: 100, currency: 'CZK', homeAmount: null, note: null })
const w = (type: MaintenanceType | null, name: string | null = null, extra: Partial<MaintenanceWork> = {}): MaintenanceWork =>
  ({ type, name, part: 'None', brand: null, model: null, details: null, ...extra })

describe('maintenanceHistory', () => {
  it('includes oil changes, newest first', () => {
    const h = maintenanceHistory([
      e(1, '2025-02-06', 7350, ['OilChange', 'BrakeFluid']),
      e(2, '2025-06-01', 12000, ['OilChange']),
      e(3, '2025-10-22', 19200, ['TyreSwap']),
    ])
    expect(h.services.map((s) => s.id)).toEqual([3, 2, 1])
    expect(h.services[2].work.map((x) => x.type)).toEqual(['OilChange', 'BrakeFluid'])
    expect(h.services[0].work[0]).toMatchObject({ key: 'TyreSwap', label: 'Tyre swap', part: 'None', details: null })
    expect(h.services[1].work[0].sinceDays).toBe(115)
    expect(h.lanes).toEqual(['Engine oil', 'Brake fluid', 'Tyre swap'])
  })

  it('gives days and km since the same work was last done', () => {
    const h = maintenanceHistory([
      e(1, '2025-10-22', 19200, ['TyreSwap']),
      e(2, '2026-03-18', 27400, ['TyreSwap', 'CabinFilter']),
    ])
    expect(h.services[0].work.map((x) => [x.type, x.sinceDays, x.sinceKm])).toEqual([
      ['CabinFilter', null, null], ['TyreSwap', 147, 8200],
    ])
    expect(h.services[1].work[0]).toMatchObject({ sinceDays: null, sinceKm: null })
  })

  it('leaves km empty when either odometer is missing', () => {
    const h = maintenanceHistory([e(1, '2025-01-01', null, ['Coolant']), e(2, '2025-01-31', 5000, ['Coolant'])])
    expect(h.services[0].work[0]).toMatchObject({ sinceDays: 30, sinceKm: null })
  })

  it('orders lanes in catalogue order', () => {
    const h = maintenanceHistory([e(1, '2025-01-01', 1, ['NewTyres', 'BrakeFluid'])])
    expect(h.lanes).toEqual(['Brake fluid', 'New tyres'])
  })

  it('uses work items with part details when present', () => {
    const h = maintenanceHistory([e(1, '2025-01-01', 1, ['BrakePads'], [
      w('BrakePads', null, { part: 'Aftermarket', brand: 'Brembo', model: 'P 86 024', details: 'front' }),
    ])])
    expect(h.services[0].work[0]).toMatchObject({
      key: 'BrakePads', label: 'Brake pads', type: 'BrakePads', part: 'Aftermarket', brand: 'Brembo', model: 'P 86 024', details: 'front',
    })
  })

  it('tracks custom work by case-insensitive name, after typed lanes', () => {
    const h = maintenanceHistory([
      e(1, '2025-01-01', 1, [], [w(null, 'Headlight polish')]),
      e(2, '2025-01-31', 2, ['Coolant'], [w(null, 'headlight POLISH '), w('Coolant')]),
    ])
    const latest = h.services[0].work
    expect(latest[0]).toMatchObject({ key: 'custom:headlight polish', label: 'Headlight polish', type: null, sinceDays: 30 })
    expect(h.services[1].work[0]).toMatchObject({ key: 'custom:headlight polish', label: 'Headlight polish' })
    expect(h.lanes).toEqual(['Coolant', 'Headlight polish'])
  })
})
