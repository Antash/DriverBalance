import { describe, expect, it } from 'vitest'
import { carryZoom, structureKey } from './chartZoom'

const opt = (data: unknown[], name = 'A') => ({ series: [{ name, type: 'line', data }] })

describe('structureKey', () => {
  it('is stable when only the values change and changes with the time extent or series', () => {
    const a = structureKey(opt([['2025-01-01', 1], ['2025-03-01', 2]]))
    expect(structureKey(opt([['2025-01-01', 9], ['2025-03-01', 8]]))).toBe(a)
    expect(structureKey(opt([{ value: ['2025-01-01', 9] }, { value: ['2025-03-01', 8] }]))).toBe(a)
    expect(structureKey(opt([['2025-01-01', 1], ['2025-04-01', 2]]))).not.toBe(a)
    expect(structureKey(opt([['2025-01-01', 1], ['2025-03-01', 2]], 'B'))).not.toBe(a)
  })
})

describe('structureKey (category series)', () => {
  it('changes when the number of categories changes', () => {
    expect(structureKey(opt([1, 2, 3]))).toBe(structureKey(opt([4, 5, 6])))
    expect(structureKey(opt([1, 2, 3]))).not.toBe(structureKey(opt([1, 2, 3, 4])))
  })
})

describe('carryZoom', () => {
  const next = { dataZoom: [{ type: 'inside', start: 0, end: 100 }, { type: 'slider', start: 0, end: 100, startValue: 5 }], series: [] }
  it('copies the current window onto each dataZoom component', () => {
    const out = carryZoom(next, [{ start: 20, end: 60 }, { start: 20, end: 60 }]) as { dataZoom: Record<string, unknown>[] }
    expect(out.dataZoom.map((z) => [z.start, z.end])).toEqual([[20, 60], [20, 60]])
    expect(out.dataZoom[1].startValue).toBeUndefined()
    expect(out.dataZoom.map((z) => z.type)).toEqual(['inside', 'slider'])
  })
  it('leaves the option alone without a previous or without any dataZoom', () => {
    expect(carryZoom(next, undefined)).toBe(next)
    const plain = { series: [] }
    expect(carryZoom(plain, [{ start: 1, end: 2 }])).toBe(plain)
  })
})
