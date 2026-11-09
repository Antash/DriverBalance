import { describe, expect, it } from 'vitest'
import { expensePeriods } from './expensePeriods'

describe('expensePeriods', () => {
  it('offers Total (from purchase), Year to date and each past calendar year, newest first', () => {
    expect(expensePeriods('2025-04-14', '2026-11-09')).toEqual([
      { key: 'total', label: 'Total', from: '2025-04-14', to: '2026-11-10' },
      { key: 'ytd', label: 'Year to date', from: '2026-01-01', to: '2026-11-10' },
      { key: '2025', label: '2025', from: '2025-01-01', to: '2026-01-01' },
    ])
  })
  it('lists several past years and falls back to the first data when there is no purchase date', () => {
    expect(expensePeriods(null, '2026-03-01', '2023-06-10').map((p) => p.key)).toEqual(['total', 'ytd', '2025', '2024', '2023'])
    expect(expensePeriods(null, '2026-03-01', '2023-06-10')[0].from).toBe('2023-06-10')
  })
  it('without any start date offers only the current year', () => {
    expect(expensePeriods(null, '2026-03-01').map((p) => p.key)).toEqual(['ytd'])
  })
})
