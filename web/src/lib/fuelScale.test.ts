import { describe, expect, it } from 'vitest'
import { fuelScale } from './fuelScale'

describe('fuelScale', () => {
  const typical = [...Array.from({ length: 150 }, (_, i) => 4.5 + (i % 50) / 10), 12, 15, 18, 22, 25, 28, 30, 3.5, 3.8, 2.5]

  it('gives the core band most of the axis', () => {
    const s = fuelScale(typical)
    expect(s.lo).toBeGreaterThanOrEqual(3)
    expect(s.hi).toBeLessThanOrEqual(12)
    const core = s.toAxis(s.hi) - s.toAxis(s.lo)
    expect(core / (s.max - s.min)).toBeGreaterThan(0.6)
  })

  it('is monotonic and continuous at the band edges', () => {
    const s = fuelScale(typical)
    let prev = -Infinity
    for (let v = 0.5; v <= 40; v += 0.25) { const a = s.toAxis(v); expect(a).toBeGreaterThan(prev); prev = a }
    expect(s.toAxis(s.hi + 1e-6)).toBeCloseTo(1, 4)
    expect(s.toAxis(s.lo - 1e-6)).toBeCloseTo(0, 4)
  })

  it('labels whole litres inside the band and round values outside', () => {
    const s = fuelScale(typical)
    const vals = s.ticks.map((t) => t.value)
    for (let v = s.lo; v <= s.hi; v++) expect(vals).toContain(v)
    expect(vals.some((v) => v > s.hi && [15, 20, 30].includes(v))).toBe(true)
    expect(vals.every((v, i) => i === 0 || v > vals[i - 1])).toBe(true)
  })

  it('copes with no or uniform data', () => {
    expect(fuelScale([]).ticks.length).toBeGreaterThan(2)
    const s = fuelScale([6, 6, 6])
    expect(s.hi - s.lo).toBeGreaterThanOrEqual(2)
  })
})
