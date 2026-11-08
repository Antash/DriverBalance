import { describe, expect, it } from 'vitest'
import type { Period } from '../api/types'
import { defaultType, intervalWindow, pickDefaultIntervals, presentTypes, weeklyLPer100 } from './intervals'

describe('pickDefaultIntervals', () => {
  it('returns [current, previous] from a newest-first list', () => {
    expect(pickDefaultIntervals(['cur', 'prev', 'first'])).toEqual(['cur', 'prev'])
  })
  it('returns [current, null] with a single interval', () => {
    expect(pickDefaultIntervals(['only'])).toEqual(['only', null])
  })
  it('returns [null, null] with none', () => {
    expect(pickDefaultIntervals([])).toEqual([null, null])
  })
})

describe('maintenance types', () => {
  it('offers only types that occur, in catalogue order, and defaults to OilChange', () => {
    const types = presentTypes([{ maintenance: ['BrakePads'] }, { maintenance: ['OilChange', 'AirFilter'] }, { maintenance: [] }])
    expect(types).toEqual(['OilChange', 'BrakePads', 'AirFilter'])
    expect(defaultType(types)).toBe('OilChange')
  })
  it('falls back to the first type when there is no oil change, null when empty', () => {
    expect(defaultType(['Coolant', 'BrakePads'])).toBe('Coolant')
    expect(defaultType([])).toBeNull()
  })
})

describe('intervalWindow', () => {
  const prev: Period = { from: '2025-02-02', to: '2026-03-14', label: 'Previous' }
  const cur: Period = { from: '2026-03-14', to: '2026-11-09', label: 'Current' }
  it('spans both intervals whichever order and marks the service date at the newer start', () => {
    expect(intervalWindow(cur, prev)).toEqual({ from: '2025-02-02', to: '2026-11-09', serviceDate: '2026-03-14' })
    expect(intervalWindow(prev, cur)).toEqual({ from: '2025-02-02', to: '2026-11-09', serviceDate: '2026-03-14' })
  })
  it('has no marker for one interval or the same interval twice', () => {
    expect(intervalWindow(cur, null)).toEqual({ from: '2026-03-14', to: '2026-11-09', serviceDate: null })
    expect(intervalWindow(cur, cur).serviceDate).toBeNull()
  })
})

describe('weeklyLPer100', () => {
  it('weights by km per Monday-start week', () => {
    const pts = [
      { time: '2026-03-02T08:00:00', lPer100: 5, km: 100 }, // Monday
      { time: '2026-03-08T08:00:00', lPer100: 8, km: 100 }, // Sunday, same week
      { time: '2026-03-09T08:00:00', lPer100: 6, km: 50 },
    ]
    expect(weeklyLPer100(pts)).toEqual([
      { weekStart: '2026-03-02', lPer100: 6.5 },
      { weekStart: '2026-03-09', lPer100: 6 },
    ])
  })
})
