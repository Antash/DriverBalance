import { describe, expect, it } from 'vitest'
import type { TyreSetSummary } from '../api/types'
import { derivedTyreName, partLine, seasonLabel, tyreDisplayName, tyreRows } from './tyreSummary'

const sum = (id: number, name: string, over: Partial<TyreSetSummary['set']> = {}, mounted = false, spans: TyreSetSummary['spans'] = [], totalKm = 0): TyreSetSummary => ({
  set: { id, name, season: 'Summer', description: null, purchaseDate: null, retired: false, brand: null, model: null, size: null, ...over },
  totalKm, seasons: 1, purchaseCost: null, costPer1000Km: null, mounted, spans,
})
const span = (from: string, to: string | null) => ({ tyreSetId: 1, from, to, fromOdometer: null, toOdometer: null, km: null })

describe('tyreRows', () => {
  it('sorts Fitted, Stored, Retired then by name', () => {
    const rows = tyreRows([
      sum(1, 'Old', { retired: true }),
      sum(2, 'Zeta winter'),
      sum(3, 'Alpha summer'),
      sum(4, 'On car', {}, true, [span('2025-01-01', '2025-03-01'), span('2025-04-01', null)], 900),
    ])
    expect(rows.map((r) => [r.name, r.status])).toEqual([
      ['On car', 'Fitted'], ['Alpha summer', 'Stored'], ['Zeta winter', 'Stored'], ['Old', 'Retired'],
    ])
    expect(rows[0]).toMatchObject({ km: 900, fittedSince: '2025-04-01' })
    expect(rows[1].fittedSince).toBeNull()
  })
})

describe('labels', () => {
  it('names seasons', () => {
    expect(['Summer', 'Winter', 'AllSeason'].map((s) => seasonLabel(s as never))).toEqual(['Summer', 'Winter', 'All-season'])
  })
  it('formats a part line', () => {
    expect(partLine({ part: 'None', brand: null, model: null })).toBeNull()
    expect(partLine({ part: 'Original', brand: null, model: null })).toBe('Original')
    expect(partLine({ part: 'Aftermarket', brand: 'Brembo', model: 'P 86' })).toBe('Brembo P 86')
    expect(partLine({ part: 'Aftermarket', brand: 'Brembo', model: null })).toBe('Brembo')
  })
})

describe('display name', () => {
  it('uses brand and model when present, else the name, and sorts by it', () => {
    const rows = tyreRows([
      sum(1, 'Summer tyres', { brand: 'Michelin', model: 'Pilot' }),
      sum(2, 'Winter tyres', { brand: 'Continental' }),
      sum(3, 'Alpha set'),
      sum(4, 'Zed', { model: 'X1' }),
    ])
    expect(rows.map((r) => r.name)).toEqual(['Alpha set', 'Continental', 'Michelin Pilot', 'X1'])
  })
})

describe('tyreDisplayName', () => {
  const set = (o: Partial<TyreSetSummary['set']>) => sum(1, 'Stored name', o).set
  it('joins brand and model, else falls back to the name', () => {
    expect(tyreDisplayName(set({ brand: 'Nokian', model: 'WR D4' }))).toBe('Nokian WR D4')
    expect(tyreDisplayName(set({ brand: 'Nokian' }))).toBe('Nokian')
    expect(tyreDisplayName(set({ brand: ' ', model: null }))).toBe('Stored name')
  })
})

describe('derivedTyreName', () => {
  it('mirrors the server', () => {
    expect(derivedTyreName({ brand: 'A', model: 'B', season: 'Winter' })).toBe('A B')
    expect(derivedTyreName({ brand: null, model: ' ', season: 'AllSeason' })).toBe('All-season tyres')
    expect(derivedTyreName({ brand: 'x'.repeat(100), model: 'y'.repeat(100), season: 'Winter' })).toHaveLength(100)
  })
})
