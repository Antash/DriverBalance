import { describe, expect, it } from 'vitest'
import type { Period } from '../api/types'
import { allIntervalsWindow, intervalWindow, weeklyLPer100 } from './intervals'

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

describe('allIntervalsWindow', () => {
  const p = (from: string, to: string): Period => ({ from, to, label: from })
  it('spans every interval and marks each service after the oldest start', () => {
    const newestFirst = [p('2026-03-14', '2026-11-09'), p('2025-08-01', '2026-03-14'), p('2025-02-02', '2025-08-01')]
    expect(allIntervalsWindow(newestFirst)).toEqual({ from: '2025-02-02', to: '2026-11-09', serviceDates: ['2025-08-01', '2026-03-14'] })
  })
  it('has no markers for a single interval and null for none', () => {
    expect(allIntervalsWindow([p('2026-03-14', '2026-11-09')])).toEqual({ from: '2026-03-14', to: '2026-11-09', serviceDates: [] })
    expect(allIntervalsWindow([])).toBeNull()
  })
})
