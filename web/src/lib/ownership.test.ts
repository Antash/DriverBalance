import { describe, expect, it } from 'vitest'
import type { Ownership } from '../api/types'
import { isPreviousOwner, isPreviousOwnerInterval, ownershipRows } from './ownership'

const own = (extra: Partial<Ownership> = {}): Ownership => ({
  purchaseDate: '2025-04-14', purchaseCosts: [{ date: '2025-04-14', amount: 1000000, note: 'Car' }, { date: '2025-04-10', amount: 30000, note: null }],
  purchaseTotal: 1030000, runningCosts: 1000, kmDriven: 10000, months: 12,
  estimatedResaleValue: 600000, resaleEstimateDate: '2026-11-01',
  netCost: 431000, netCostPerKm: 43.1, netCostPerMonth: 35916.67, ...extra,
})

describe('isPreviousOwner', () => {
  it('is true only for dates strictly before the purchase date', () => {
    expect(isPreviousOwner('2025-04-13', '2025-04-14')).toBe(true)
    expect(isPreviousOwner('2025-04-14', '2025-04-14')).toBe(false)
    expect(isPreviousOwner('2025-05-01', '2025-04-14')).toBe(false)
  })
  it('is false without a purchase date', () => {
    expect(isPreviousOwner('2020-01-01', null)).toBe(false)
  })
})

describe('isPreviousOwnerInterval', () => {
  it('uses the exclusive end: to <= purchaseDate', () => {
    expect(isPreviousOwnerInterval('2025-04-14', '2025-04-14')).toBe(true)
    expect(isPreviousOwnerInterval('2025-04-13', '2025-04-14')).toBe(true)
    expect(isPreviousOwnerInterval('2025-04-15', '2025-04-14')).toBe(false)
    expect(isPreviousOwnerInterval('2020-01-01', null)).toBe(false)
  })
})

describe('ownershipRows', () => {
  it('returns null without a purchase date', () => {
    expect(ownershipRows(own({ purchaseDate: null }), 'CZK')).toBeNull()
  })
  it('lists the figures in order', () => {
    const r = ownershipRows(own(), 'CZK')!
    expect(r.map((x) => x.label)).toEqual([
      'Bought on', 'Purchase costs', 'Running costs since purchase', 'Km driven since purchase',
      'Estimated resale value', 'Net cost of ownership', 'Net cost per km', 'Net cost per month',
    ])
    expect(r[0].value).toBe('14/04/2025')
    expect(r[3].value).toBe('10,000 km')
    expect(r[4].note).toBe('on 01/11/2026')
    expect(r[5].strong).toBe(true)
  })
  it('shows not set without a resale estimate and dashes for missing figures', () => {
    const r = ownershipRows(own({ estimatedResaleValue: null, resaleEstimateDate: null, netCost: null, netCostPerKm: null, netCostPerMonth: null }), 'CZK')!
    expect(r[4].value).toBe('not set')
    expect(r[5].value).toBe('—')
  })
})
