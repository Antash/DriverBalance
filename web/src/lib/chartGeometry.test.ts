import { describe, expect, it } from 'vitest'
import { thresholdPosition, zoneRects } from './chartGeometry'

const bins = [
  { from: 0, to: 5, count: 1 }, { from: 5, to: 10, count: 1 }, { from: 10, to: 20, count: 1 }, { from: 20, to: null, count: 1 },
]
const zones = { shortMin: 20, mediumMin: 30, longMin: 40, heavyKmh: 25, cityKmh: 35, mixedKmh: 50 }

describe('thresholdPosition', () => {
  it('places the line inside the bin that holds the threshold', () => {
    expect(thresholdPosition(bins, 5)).toBeCloseTo(0.5)
    expect(thresholdPosition(bins, 15)).toBeCloseTo(1.5 + 0.5)
    expect(thresholdPosition(bins, 0)).toBeCloseTo(-0.5)
  })
  it('puts a threshold in the open-ended bin on that bin', () => {
    expect(thresholdPosition(bins, 45)).toBe(3)
  })
})

describe('zoneRects', () => {
  it('follows the parameters (moving a limit moves the zone edges)', () => {
    const a = zoneRects(zones, 100, 120)
    const b = zoneRects({ ...zones, longMin: 60 }, 100, 120)
    expect(a.find((r) => r.score === 'Excellent')!.x0).toBe(40)
    expect(b.find((r) => r.score === 'Excellent')!.x0).toBe(60)
  })
  it('covers every score except Bad', () => {
    expect(new Set(zoneRects(zones, 100, 120).map((r) => r.score))).toEqual(new Set(['Fair', 'Good', 'Excellent']))
  })
})
