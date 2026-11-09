import { describe, expect, it } from 'vitest'
import type { Expense } from '../api/types'
import { buildExpensePayload, parseDecimal, pricePerLitre, toFormState, validateExpenseForm, type ExpenseFormState } from './expenseForm'

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
    const p = buildExpensePayload({ ...base, category: 'Fuel', amount: '1500.5', litres: '40.2', fullTank: true, station: 'Shell' }, 'CZK')
    expect(p).toMatchObject({ category: 'Fuel', amount: 1500.5, litres: 40.2, fullTank: true, station: 'Shell', maintenance: [] })
  })

  it('Maintenance types become a string array and a tyre set is sent for TyreSwap', () => {
    const p = buildExpensePayload({ ...base, category: 'Maintenance', maintenance: ['TyreSwap', 'OilChange'], maintenanceDescription: 'x', tyreSetId: '3' }, 'CZK')
    expect(p.maintenance).toEqual(['TyreSwap', 'OilChange'])
    expect(p.maintenanceDescription).toBe('x')
    expect(p.tyreSetId).toBe(3)
  })

  it('Maintenance without a mount flag drops the tyre set', () => {
    const p = buildExpensePayload({ ...base, category: 'Maintenance', maintenance: ['OilChange'], tyreSetId: '3' }, 'CZK')
    expect(p.tyreSetId).toBeNull()
  })

  it('other categories drop fuel and maintenance fields', () => {
    const p = buildExpensePayload({ ...base, category: 'Parking', litres: '5', station: 'x', maintenance: ['OilChange'], tyreSetId: '2' }, 'CZK')
    expect(p).toMatchObject({ litres: null, fullTank: null, station: null, maintenance: [], maintenanceDescription: null, tyreSetId: null })
  })

  it('Tyres sends the set; empty optionals become null; currency is upper-cased', () => {
    const p = buildExpensePayload({ ...base, category: 'Tyres', tyreSetId: '2', currency: 'eur', homeAmount: '', odometer: '' }, 'CZK')
    expect(p).toMatchObject({ tyreSetId: 2, currency: 'EUR', homeAmount: null, odometer: null, note: null, coversFrom: null, coversTo: null })
  })

  it('keeps the covers period for any category', () => {
    const p = buildExpensePayload({ ...base, category: 'Insurance', coversFrom: '2026-01-01', coversTo: '2026-12-31' }, 'CZK')
    expect(p).toMatchObject({ coversFrom: '2026-01-01', coversTo: '2026-12-31' })
  })
})

describe('parseDecimal', () => {
  it.each([['1 500', 1500], ['12,50', 12.5], [' 99 ', 99], ['1 500,5', 1500.5], ['7', 7]])('accepts %j', (input, out) =>
    expect(parseDecimal(input)).toBe(out))
  it.each(['abc', '12,5,0', '', '  ', '1.2.3', '-', '1e3'])('rejects %j', (input) => expect(parseDecimal(input)).toBeNull())
})

describe('validateExpenseForm', () => {
  it('accepts Czech-style numbers', () =>
    expect(validateExpenseForm({ ...base, amount: '1 500', odometer: '12 345', homeAmount: '12,50', litres: '40,5', category: 'Fuel', currency: 'EUR' }, 'CZK')).toEqual({}))
  it('blank amount is invalid', () => expect(validateExpenseForm({ ...base, amount: '' }, 'CZK').amount).toBeTruthy())
  it('unparsable amount, litres, odometer and homeAmount are flagged', () => {
    const e = validateExpenseForm({ ...base, category: 'Fuel', currency: 'EUR', amount: 'abc', litres: '12,5,0', odometer: 'x', homeAmount: '??' }, 'CZK')
    expect(Object.keys(e).sort()).toEqual(['amount', 'homeAmount', 'litres', 'odometer'])
  })
  it('blank optional fields are valid; a hidden stale homeAmount or litres is ignored', () =>
    expect(validateExpenseForm({ ...base, homeAmount: 'junk', litres: 'junk' }, 'CZK')).toEqual({}))
})

describe('homeAmount', () => {
  it('is null when currency equals the home currency, even if stale', () =>
    expect(buildExpensePayload({ ...base, currency: 'czk', homeAmount: '99' }, 'CZK').homeAmount).toBeNull())
  it('is sent for a foreign currency', () =>
    expect(buildExpensePayload({ ...base, currency: 'EUR', homeAmount: '2 500,5' }, 'CZK').homeAmount).toBe(2500.5))
})

describe('toFormState -> buildExpensePayload round trip', () => {
  const common: Expense = { id: 1, date: '2026-02-03', amount: 1234.5, currency: 'CZK', homeAmount: null, odometer: 45678, note: 'n',
    coversFrom: null, coversTo: null, litres: null, fullTank: null, station: null, maintenance: [], maintenanceDescription: null,
    tyreSetId: null, source: 'Web', category: 'Other', work: [] }
  const strip = ({ id: _id, source: _s, work: _w, ...rest }: Expense) => rest
  it.each([
    ['Fuel', { ...common, category: 'Fuel', litres: 40.2, fullTank: true, station: 'Shell' }],
    ['Maintenance with tyre set', { ...common, category: 'Maintenance', maintenance: ['TyreSwap', 'OilChange'], maintenanceDescription: 'd', tyreSetId: 3 }],
    ['Insurance with covers', { ...common, category: 'Insurance', coversFrom: '2026-01-01', coversTo: '2026-12-31' }],
    ['foreign currency', { ...common, category: 'Parking', currency: 'EUR', homeAmount: 250.5 }],
  ] as [string, Expense][])('%s', (_n, e) => expect(buildExpensePayload(toFormState(e), 'CZK')).toEqual(strip(e)))
})
