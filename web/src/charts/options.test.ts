import { describe, expect, it } from 'vitest'
import { scoreColor } from '../components/chartTheme'
import { gapOption } from './gapOption'
import { heatmapOption } from './heatmapOption'

describe('heatmapOption', () => {
  const days = [
    { date: '2025-01-06', km: 80, bestScore: 'Excellent' as const, intensity: 0.8 },
    { date: '2025-01-07', km: 10, bestScore: 'Bad' as const, intensity: 0.1 },
  ]
  it('gives every series the score colour so legend swatches match the cells', () => {
    const tokens = { excellent: '#0a0', good: '#fa1', fair: '#e85', bad: '#d33' }
    for (const [k, v] of Object.entries(tokens)) document.documentElement.style.setProperty(`--score-${k}`, v)
    const o = heatmapOption(days, '2025-01-01', '2026-01-01')() as { series: { name: 'Excellent' | 'Good' | 'Fair' | 'Bad'; itemStyle: { color: string } }[] }
    const scored = o.series.filter((s) => s.name !== ('Years' as string))
    expect(scored).toHaveLength(4)
    for (const s of scored) {
      expect(s.itemStyle.color).toBe(scoreColor(s.name))
      expect(s.itemStyle.color).not.toBe('')
    }
  })
  it('encodes best score as colour and intensity as opacity, without a visualMap over the score string', () => {
    const o = heatmapOption(days, '2025-01-01', '2026-01-01')() as { visualMap?: unknown; series: { name: string; data: { value: unknown[]; itemStyle: { opacity: number } }[] }[] }
    expect(o.visualMap).toBeUndefined()
    const ex = o.series.find((s) => s.name === 'Excellent')!
    const bad = o.series.find((s) => s.name === 'Bad')!
    expect(ex.data).toHaveLength(1)
    expect(ex.data[0].itemStyle.opacity).toBeGreaterThan(bad.data[0].itemStyle.opacity)
    expect(ex.data[0].value[0]).toBe('2025-01-06')
  })
})

describe('gapOption', () => {
  const d = { bins: [{ from: 0, to: 5, count: 1 }, { from: 5, to: 10, count: 2 }, { from: 10, to: null, count: 1 }], thresholdMinutes: 7.5 }
  it('draws the threshold on a hidden linear value axis spanning the category axis', () => {
    const o = gapOption(d)() as unknown as {
      xAxis: { type: string; min?: number; max?: number }[]
      series: { xAxisIndex?: number; markLine?: { data: { xAxis: number }[] } }[]
    }
    expect(o.xAxis[1]).toMatchObject({ type: 'value', min: -0.5, max: 2.5 })
    const s = o.series.find((x) => x.markLine)!
    expect(s.xAxisIndex).toBe(1)
    expect(s.markLine!.data[0].xAxis).toBeCloseTo(0.5 + 0.5)
  })
})
