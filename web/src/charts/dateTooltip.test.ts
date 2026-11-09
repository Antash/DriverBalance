import { describe, expect, it } from 'vitest'
import { timeAxisTooltip, tooltipDate } from './dateTooltip'

describe('time axis tooltip', () => {
  it('prints dd/mm/yyyy for a timestamp', () => expect(tooltipDate(new Date(2026, 10, 9).getTime())).toBe('09/11/2026'))
  it('prints dd/mm/yyyy for an ISO string', () => expect(tooltipDate('2026-11-09')).toBe('09/11/2026'))
  it('builds header and rows', () => {
    const html = timeAxisTooltip((v) => `${v} km`)([{ axisValue: new Date(2026, 10, 9).getTime(), marker: '*', seriesName: 'Odo', value: [0, 5] }])
    expect(html).toBe('09/11/2026<br/>* Odo: <b>5 km</b>')
  })
  it('dashes for null', () => {
    expect(timeAxisTooltip(String)([{ axisValue: '2026-01-02', seriesName: 'A', value: ['x', null] }])).toContain('<b>—</b>')
  })
})
