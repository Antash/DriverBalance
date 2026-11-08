import { describe, expect, it } from 'vitest'
import { buildExpensePayload, pricePerLitre, type ExpenseFormState } from './expenseForm'

const base: ExpenseFormState = {
  date: '2026-03-01', category: 'Other', amount: '120', currency: 'CZK', homeAmount: '', odometer: '', note: '',
  coversFrom: '', coversTo: '', litres: '', fullTank: false, station: '',
  maintenance: [], maintenanceDescription: '', tyreSetId: '',
}

describe('pricePerLitre', () => {
  it('returns null for 0 litres', () => expect(pricePerLitre(1000, 0)).toBeNull())
  it('returns null for non-finite input', () => expect(pricePerLitre(NaN, 10)).toBeNull())
  it('divides total by litres', () => expect(pricePerLitre(1000, 40)).toBe(25))
})

describe('buildExpensePayload', () => {
  it('Fuel includes litres, full tank and station', () => {
    const p = buildExpensePayload({ ...base, category: 'Fuel', amount: '1500.5', litres: '40.2', fullTank: true, station: 'Shell' })
    expect(p).toMatchObject({ category: 'Fuel', amount: 1500.5, litres: 40.2, fullTank: true, station: 'Shell', maintenance: [] })
  })

  it('Maintenance types become a string array and a tyre set is sent for TyreSwap', () => {
    const p = buildExpensePayload({ ...base, category: 'Maintenance', maintenance: ['TyreSwap', 'OilChange'], maintenanceDescription: 'x', tyreSetId: '3' })
    expect(p.maintenance).toEqual(['TyreSwap', 'OilChange'])
    expect(p.maintenanceDescription).toBe('x')
    expect(p.tyreSetId).toBe(3)
  })

  it('Maintenance without a mount flag drops the tyre set', () => {
    const p = buildExpensePayload({ ...base, category: 'Maintenance', maintenance: ['OilChange'], tyreSetId: '3' })
    expect(p.tyreSetId).toBeNull()
  })

  it('other categories drop fuel and maintenance fields', () => {
    const p = buildExpensePayload({ ...base, category: 'Parking', litres: '5', station: 'x', maintenance: ['OilChange'], tyreSetId: '2' })
    expect(p).toMatchObject({ litres: null, fullTank: null, station: null, maintenance: [], maintenanceDescription: null, tyreSetId: null })
  })

  it('Tyres sends the set; empty optionals become null; currency is upper-cased', () => {
    const p = buildExpensePayload({ ...base, category: 'Tyres', tyreSetId: '2', currency: 'eur', homeAmount: '', odometer: '' })
    expect(p).toMatchObject({ tyreSetId: 2, currency: 'EUR', homeAmount: null, odometer: null, note: null, coversFrom: null, coversTo: null })
  })

  it('keeps the covers period for any category', () => {
    const p = buildExpensePayload({ ...base, category: 'Insurance', coversFrom: '2026-01-01', coversTo: '2026-12-31' })
    expect(p).toMatchObject({ coversFrom: '2026-01-01', coversTo: '2026-12-31' })
  })
})
