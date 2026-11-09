import { describe, expect, it } from 'vitest'
import type { Expense, MaintenanceWork } from '../api/types'
import { previousParts, workKey } from './previousParts'

const exp = (id: number, date: string, work: Partial<MaintenanceWork>[]) =>
  ({ id, date, category: 'Maintenance', maintenance: [], work: work.map((w) => ({ type: null, name: null, part: 'None', brand: null, model: null, details: null, ...w })) }) as unknown as Expense

const history = [
  exp(1, '2024-05-01', [{ type: 'OilChange', part: 'Aftermarket', brand: 'Castrol', model: 'Edge 5W-30' }]),
  exp(2, '2025-05-01', [{ type: 'OilChange', part: 'Aftermarket', brand: 'Mobil 1', model: 'ESP 5W-30' }, { type: 'OilFilter', part: 'Aftermarket', brand: 'Mann', model: 'HU 819/1 x' }]),
  exp(3, '2026-01-13', [{ type: 'OilChange', part: 'Aftermarket', brand: 'castrol', model: 'EDGE 5W-30 ' }, { type: 'OilFilter', part: 'Original' }]),
  exp(4, '2026-02-01', [{ type: null, name: 'Headlight polish', part: 'Aftermarket', brand: '3M', model: 'Restorer' }]),
]

describe('previousParts', () => {
  it('lists other-brand products used for the same item, most recent first, without duplicates', () => {
    expect(previousParts(history, workKey('OilChange', ''))).toEqual([
      { brand: 'castrol', model: 'EDGE 5W-30' },
      { brand: 'Mobil 1', model: 'ESP 5W-30' },
    ])
    expect(previousParts(history, workKey('OilFilter', ''))).toEqual([{ brand: 'Mann', model: 'HU 819/1 x' }])
  })
  it('matches custom works by name, ignoring case', () => {
    expect(previousParts(history, workKey(null, ' headlight POLISH'))).toEqual([{ brand: '3M', model: 'Restorer' }])
  })
  it('skips the record being edited and items never bought from another brand', () => {
    expect(previousParts(history, workKey('OilFilter', ''), 2)).toEqual([])
    expect(previousParts(history, workKey('Coolant', ''))).toEqual([])
  })
})
