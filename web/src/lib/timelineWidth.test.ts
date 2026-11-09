import { describe, expect, it } from 'vitest'
import { timelineWidthPercent } from './timelineWidth'

describe('timelineWidthPercent', () => {
  it('fits up to 10 years in the card', () => {
    expect(timelineWidthPercent('2023-05-01', '2026-11-09')).toBe(100)
    expect(timelineWidthPercent('2016-11-09', '2026-11-09')).toBe(100)
    expect(timelineWidthPercent(null, null)).toBe(100)
  })
  it('widens longer histories so 10 years fill the card', () => {
    expect(timelineWidthPercent('2006-11-09', '2026-11-09')).toBeCloseTo(200, 0)
    expect(timelineWidthPercent('2011-11-09', '2026-11-09')).toBeCloseTo(150, 0)
  })
})
