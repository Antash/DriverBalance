import { describe, expect, it } from 'vitest'
import { yearMarks } from './heatmapOption'

describe('yearMarks', () => {
  it('labels the first year and delimits each new year', () => {
    expect(yearMarks('2025-04-14', '2026-11-10')).toEqual([
      { date: '2025-04-14', year: 2025, delimiter: false },
      { date: '2026-01-01', year: 2026, delimiter: true },
    ])
  })
  it('a calendar year has one label and no delimiter (to is exclusive)', () => {
    expect(yearMarks('2025-01-01', '2026-01-01')).toEqual([{ date: '2025-01-01', year: 2025, delimiter: false }])
  })
})
