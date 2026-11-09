import { describe, expect, it } from 'vitest'
import type { Expense } from '../api/types'
import { buildExpensePayload, emptyFormState, parseDecimal, pricePerLitre, toFormState, usesTyreSet, validateExpenseForm, type ExpenseFormState, type WorkDraft } from './expenseForm'

const base: ExpenseFormState = {
  date: '2026-03-01', category: 'Other', amount: '120', currency: 'CZK', homeAmount: '', odometer: '', note: '',
  coversFrom: '', coversTo: '', litres: '', fullTank: false, station: '',
  work: [], maintenanceDescription: '', tyreSetId: '',
  tyres: { mode: 'existing', brand: '', model: '', size: '', season: 'Summer' },
}
const item = (o: Partial<WorkDraft> = {}): WorkDraft => ({ id: 'a', type: 'OilChange', name: '', part: 'None', brand: '', model: '', details: '', ...o })
const M: ExpenseFormState = { ...base, category: 'Maintenance' }
const newTyres = { mode: 'new' as const, brand: 'Michelin', model: 'Alpin 6', size: '225/45R17', season: 'Winter' as const }

describe('pricePerLitre', () => {
  it('returns null for 0 litres', () => expect(pricePerLitre(1000, 0)).toBeNull())
  it('returns null for non-finite input', () => expect(pricePerLitre(NaN, 10)).toBeNull())
  it('divides total by litres', () => expect(pricePerLitre(1000, 40)).toBe(25))
})

describe('buildExpensePayload', () => {
  it('Fuel includes litres, full tank and station', () => {
    const p = buildExpensePayload({ ...base, category: 'Fuel', amount: '1500.5', litres: '40.2', fullTank: true, station: 'Shell' }, 'CZK')
    expect(p).toMatchObject({ category: 'Fuel', amount: 1500.5, litres: 40.2, fullTank: true, station: 'Shell', maintenance: [], work: [], tyres: null })
  })

  it('Maintenance work and typed item types are sent; description kept', () => {
    const p = buildExpensePayload({ ...M, work: [item(), item({ id: 'b', type: 'TyreSwap' })], maintenanceDescription: 'x' }, 'CZK')
    expect(p.maintenance).toEqual(['OilChange', 'TyreSwap'])
    expect(p.work).toHaveLength(2)
    expect(p.maintenanceDescription).toBe('x')
  })

  it('tyre mode new sends tyres.isNew and no tyreSetId', () => {
    const p = buildExpensePayload({ ...M, work: [item({ type: 'TyreSwap' })], tyreSetId: '3', tyres: newTyres }, 'CZK')
    expect(p.tyreSetId).toBeNull()
    expect(p.tyres).toEqual({ brand: 'Michelin', model: 'Alpin 6', size: '225/45R17', season: 'Winter', isNew: true })
  })

  it('tyre mode owned sends isNew false', () => {
    const p = buildExpensePayload({ ...M, work: [item({ type: 'TyreSwap' })],
      tyres: { mode: 'owned', brand: 'B', model: '', size: '', season: 'Summer' } }, 'CZK')
    expect(p.tyres).toMatchObject({ brand: 'B', model: null, size: null, isNew: false })
    expect(p.tyreSetId).toBeNull()
  })

  it('tyre mode existing sends tyreSetId and no tyres', () => {
    const p = buildExpensePayload({ ...M, work: [item({ type: 'TyreSwap' })], tyreSetId: '3' }, 'CZK')
    expect(p.tyreSetId).toBe(3)
    expect(p.tyres).toBeNull()
  })

  it('Maintenance without a tyre swap drops the tyre set and tyres', () => {
    const p = buildExpensePayload({ ...M, work: [item()], tyreSetId: '3', tyres: newTyres }, 'CZK')
    expect(p.tyreSetId).toBeNull()
    expect(p.tyres).toBeNull()
  })

  it('Original clears brand and model; Aftermarket keeps them', () => {
    const p = buildExpensePayload({ ...M, work: [
      item({ part: 'Original', brand: 'B', model: 'M' }),
      item({ id: 'b', type: 'Battery', part: 'Aftermarket', brand: 'Varta', model: 'E11', details: ' d ' }),
    ] }, 'CZK')
    expect(p.work[0]).toMatchObject({ type: 'OilChange', part: 'Original', brand: null, model: null })
    expect(p.work[1]).toMatchObject({ part: 'Aftermarket', brand: 'Varta', model: 'E11', details: 'd' })
  })

  it('a fully empty custom draft is dropped; a named custom one is sent', () => {
    const p = buildExpensePayload({ ...M, work: [item({ id: 'c', type: null }), item({ id: 'd', type: null, name: 'Mudflaps' })] }, 'CZK')
    expect(p.work).toHaveLength(1)
    expect(p.work[0]).toMatchObject({ type: null, name: 'Mudflaps' })
    expect(p.maintenance).toEqual([])
  })

  it('other categories drop fuel and maintenance fields', () => {
    const p = buildExpensePayload({ ...base, category: 'Parking', litres: '5', station: 'x', work: [item()], tyreSetId: '2', tyres: newTyres }, 'CZK')
    expect(p).toMatchObject({ litres: null, fullTank: null, station: null, maintenance: [], work: [], maintenanceDescription: null, tyreSetId: null, tyres: null })
  })

  it('Tyres sends the set; empty optionals become null; currency is upper-cased', () => {
    const p = buildExpensePayload({ ...base, category: 'Tyres', tyreSetId: '2', currency: 'eur', homeAmount: '', odometer: '' }, 'CZK')
    expect(p).toMatchObject({ tyreSetId: 2, currency: 'EUR', homeAmount: null, odometer: null, note: null, coversFrom: null, coversTo: null, tyres: null })
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

describe('work validation', () => {
  it('custom with details but no name errors; an empty custom does not', () => {
    expect(validateExpenseForm({ ...M, work: [item({ id: 'x', type: null, details: 'hm' })] }, 'CZK')['work.x.name']).toBeTruthy()
    expect(validateExpenseForm({ ...M, work: [item({ id: 'x', type: null })] }, 'CZK')).toEqual({})
  })
  it('Aftermarket without a brand errors', () => {
    expect(validateExpenseForm({ ...M, work: [item({ id: 'y', part: 'Aftermarket' })] }, 'CZK')['work.y.brand']).toBeTruthy()
    expect(validateExpenseForm({ ...M, work: [item({ id: 'y', part: 'Aftermarket', brand: 'Bosch' })] }, 'CZK')).toEqual({})
  })
  it('is ignored outside Maintenance', () =>
    expect(validateExpenseForm({ ...base, work: [item({ part: 'Aftermarket' })] }, 'CZK')).toEqual({}))
})

describe('usesTyreSet / emptyFormState', () => {
  it('Tyres, or Maintenance with a mount type', () => {
    expect(usesTyreSet('Tyres', [])).toBe(true)
    expect(usesTyreSet('Maintenance', ['TyreSwap'])).toBe(true)
    expect(usesTyreSet('Maintenance', ['OilChange'])).toBe(false)
  })
  it('starts with no work', () => expect(emptyFormState('CZK').work).toEqual([]))
})

describe('homeAmount', () => {
  it('is null when currency equals the home currency, even if stale', () =>
    expect(buildExpensePayload({ ...base, currency: 'czk', homeAmount: '99' }, 'CZK').homeAmount).toBeNull())
  it('is sent for a foreign currency', () =>
    expect(buildExpensePayload({ ...base, currency: 'EUR', homeAmount: '2 500,5' }, 'CZK').homeAmount).toBe(2500.5))
})

describe('toFormState', () => {
  const common: Expense = { id: 1, date: '2026-02-03', amount: 1234.5, currency: 'CZK', homeAmount: null, odometer: 45678, note: 'n',
    coversFrom: null, coversTo: null, litres: null, fullTank: null, station: null, maintenance: [], maintenanceDescription: null,
    tyreSetId: null, source: 'Web', category: 'Other', work: [] }

  it('toFormState_legacyFlagsBecomeWorkItems', () => {
    const f = toFormState({ ...common, category: 'Maintenance', maintenance: ['OilChange', 'BrakeFluid'], maintenanceDescription: 'old text' })
    expect(f.work.map((w) => [w.type, w.part])).toEqual([['OilChange', 'None'], ['BrakeFluid', 'None']])
    expect(f.maintenanceDescription).toBe('old text')
  })

  it('uses work when present', () => {
    const f = toFormState({ ...common, category: 'Maintenance', maintenance: ['Battery'],
      work: [{ type: 'Battery', name: null, part: 'Aftermarket', brand: 'Varta', model: null, details: null }] })
    expect(f.work[0]).toMatchObject({ type: 'Battery', part: 'Aftermarket', brand: 'Varta', model: '' })
  })
})

describe('stored TyreSwap with a stray part', () => {
  it('is normalised so validation cannot fail invisibly', () => {
    const e: Expense = { id: 1, date: '2026-02-03', amount: 1, currency: 'CZK', homeAmount: null, odometer: null, note: null,
      coversFrom: null, coversTo: null, litres: null, fullTank: null, station: null, maintenance: ['TyreSwap'],
      maintenanceDescription: null, tyreSetId: 3, source: 'Web', category: 'Maintenance',
      work: [{ type: 'TyreSwap', name: null, part: 'Aftermarket', brand: null, model: 'x', details: null }] }
    const f = toFormState(e)
    expect(validateExpenseForm(f, 'CZK')).toEqual({})
    expect(buildExpensePayload(f, 'CZK').work[0]).toMatchObject({ type: 'TyreSwap', part: 'None', brand: null, model: null })
    const g = { ...f, work: [{ ...f.work[0], part: 'Aftermarket' as const }] }
    expect(validateExpenseForm(g, 'CZK')).toEqual({})
    expect(buildExpensePayload(g, 'CZK').work[0]).toMatchObject({ part: 'None', brand: null, model: null })
  })
})

describe('toFormState -> buildExpensePayload round trip', () => {
  const common: Expense = { id: 1, date: '2026-02-03', amount: 1234.5, currency: 'CZK', homeAmount: null, odometer: 45678, note: 'n',
    coversFrom: null, coversTo: null, litres: null, fullTank: null, station: null, maintenance: [], maintenanceDescription: null,
    tyreSetId: null, source: 'Web', category: 'Other', work: [] }
  const strip = ({ id: _id, source: _s, ...rest }: Expense) => ({ ...rest, tyres: null })
  it.each([
    ['Fuel', { ...common, category: 'Fuel', litres: 40.2, fullTank: true, station: 'Shell' }],
    ['Maintenance with tyre set', { ...common, category: 'Maintenance', maintenance: ['TyreSwap', 'OilChange'], maintenanceDescription: 'd', tyreSetId: 3,
      work: [
        { type: 'TyreSwap', name: null, part: 'None', brand: null, model: null, details: null },
        { type: 'OilChange', name: null, part: 'Aftermarket', brand: 'Castrol', model: '5W30', details: 'x' },
        { type: null, name: 'Mudflaps', part: 'Original', brand: null, model: null, details: null },
      ] }],
    ['Insurance with covers', { ...common, category: 'Insurance', coversFrom: '2026-01-01', coversTo: '2026-12-31' }],
    ['foreign currency', { ...common, category: 'Parking', currency: 'EUR', homeAmount: 250.5 }],
  ] as [string, Expense][])('%s', (_n, e) => expect(buildExpensePayload(toFormState(e), 'CZK')).toEqual(strip(e)))
})
