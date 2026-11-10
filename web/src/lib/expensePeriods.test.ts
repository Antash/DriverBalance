import { describe, expect, it } from 'vitest'
import { expensePeriods } from './expensePeriods'

describe('expensePeriods', () => {
  it('offers Total (from purchase), the last 12 months and every recorded calendar year, newest first', () => {
    expect(expensePeriods('2025-04-14', '2026-11-09', '2025-02-06')).toEqual([
      { key: 'total', label: 'Total', from: '2025-04-14', to: '2026-11-10' },
      { key: 'last12', label: 'Last 12 months', from: '2025-11-10', to: '2026-11-10' },
      { key: '2026', label: '2026', from: '2026-01-01', to: '2027-01-01' },
      { key: '2025', label: '2025', from: '2025-01-01', to: '2026-01-01' },
    ])
  })
  it('falls back to the first record when there is no purchase date', () => {
    const p = expensePeriods(null, '2026-03-01', '2023-06-10')
    expect(p.map((x) => x.key)).toEqual(['total', 'last12', '2026', '2025', '2024', '2023'])
    expect(p[0].from).toBe('2023-06-10')
  })
  it('without any records still offers Total (as the last 12 months) and the last 12 months', () => {
    const p = expensePeriods(null, '2026-03-01', null)
    expect(p.map((x) => x.key)).toEqual(['total', 'last12'])
    expect(p[0]).toEqual({ key: 'total', label: 'Total', from: p[1].from, to: p[1].to })
  })
})
