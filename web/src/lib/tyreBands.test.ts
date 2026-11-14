import { describe, expect, it } from 'vitest'
import type { TyreMountSpan } from '../api/types'
import { tyreBandKey, tyreSetIdAt } from './tyreBands'

const band = (tyreSetId: number | null, from: string, to: string | null) =>
  ({ tyreSetId, from, to, fromOdometer: null } as TyreMountSpan)

const bands = [
  band(1, '2023-04-01', '2023-11-01'),
  band(2, '2023-11-01', '2024-04-01'),
  band(1, '2024-04-01', '2024-11-01'),
  band(null, '2024-11-01', '2025-04-01'),
  band(3, '2025-04-01', null),
]

describe('tyreBandKey', () => {
  it('lists each set once, in first-fitted order, with its colour index', () => {
    expect(tyreBandKey(bands)).toEqual([
      { tyreSetId: 1, colorIndex: 0 },
      { tyreSetId: 2, colorIndex: 1 },
      { tyreSetId: null, colorIndex: 2 },
      { tyreSetId: 3, colorIndex: 3 },
    ])
  })
  it('is empty without bands', () => expect(tyreBandKey([])).toEqual([]))
})

describe('tyreSetIdAt', () => {
  it('finds the band covering a date (from inclusive, to exclusive)', () => {
    expect(tyreSetIdAt(bands, '2023-04-01')).toBe(1)
    expect(tyreSetIdAt(bands, '2023-10-31')).toBe(1)
    expect(tyreSetIdAt(bands, '2023-11-01')).toBe(2)
    expect(tyreSetIdAt(bands, '2026-01-01')).toBe(3)
  })
  it('is undefined outside every band and null for unassigned', () => {
    expect(tyreSetIdAt(bands, '2022-01-01')).toBeUndefined()
    expect(tyreSetIdAt(bands, '2024-12-01')).toBeNull()
  })
})
