import { describe, expect, it } from 'vitest'
import type { Ownership } from '../api/types'
import { fmtMoney } from './format'
import { financingSchedule, isPreviousOwner, isPreviousOwnerInterval, ownershipRows } from './ownership'

const own = (extra: Partial<Ownership> = {}): Ownership => ({
  purchaseDate: '2025-04-14', purchaseCosts: [{ id: 1, date: '2025-04-14', amount: 1000000, note: 'Car' }, { id: 2, date: '2025-04-10', amount: 30000, note: null }],
  purchaseTotal: 1030000, lumpSumTotal: 1030000, financing: null, runningCosts: 1000, kmDriven: 10000, months: 12,
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

const fin = { start: '2025-05-01', amountPerPayment: 9500, frequency: 'Monthly', payments: 36, balloon: 200000, total: 542000, endDate: '2028-04-01' } as const

describe('financing in the card', () => {
  it('has no Financing row without financing', () => {
    expect(ownershipRows(own(), 'CZK')!.some((r) => r.label === 'Financing')).toBe(false)
  })
  it('adds a Financing row after Purchase costs; Purchase costs shows lump sums only', () => {
    const r = ownershipRows(own({ financing: fin, purchaseTotal: 1572000 }), 'CZK')!
    expect(r.map((x) => x.label).slice(0, 3)).toEqual(['Bought on', 'Purchase costs', 'Financing'])
    expect(r[1].value).toBe(fmtMoney(1030000, 'CZK'))
    expect(r[2].value).toBe(fmtMoney(542000, 'CZK'))
    expect(r[2].note).toBe(financingSchedule(fin, 'CZK'))
  })
})

describe('financingSchedule', () => {
  it('describes payments, frequency, start and balloon', () => {
    expect(financingSchedule(fin, 'CZK')).toBe(`36 × ${fmtMoney(9500, 'CZK')} monthly from 01/05/2025 + balloon ${fmtMoney(200000, 'CZK')}`)
  })
  it('omits a zero balloon and uses the frequency word', () => {
    expect(financingSchedule({ ...fin, frequency: 'Quarterly', balloon: 0 }, 'CZK')).toBe(`36 × ${fmtMoney(9500, 'CZK')} quarterly from 01/05/2025`)
    expect(financingSchedule({ ...fin, frequency: 'Yearly' }, 'CZK')).toContain('yearly')
  })
})
