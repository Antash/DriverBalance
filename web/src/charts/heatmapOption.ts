import type { EChartsOption } from 'echarts'
import type { HeatmapDay } from '../api/types'
import { chartTheme, scoreColor } from '../components/chartTheme'
import { fmtNum, fmtDate } from '../lib/format'
import { SCORES } from './shared'

/** Colour = best score of the day (fixed score palette); opacity = intensity (km, capped by the heatmap cap). */
export function heatmapOption(days: HeatmapDay[], year: number): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { top: 0, data: SCORES },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const v = (p as { value: [string, number, number, string] }).value
          return `${fmtDate(v[0])}<br/>${fmtNum(v[2], 1)} km, best score: ${v[3]}`
        },
      },
      calendar: {
        range: String(year), top: 64, left: 44, right: 12, bottom: 8, cellSize: ['auto', 14],
        itemStyle: { color: t.grid, borderColor: t.surface, borderWidth: 2 },
        splitLine: { show: false },
        yearLabel: { show: false },
        dayLabel: { firstDay: 1, color: t.muted, nameMap: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] },
        monthLabel: { color: t.muted },
      },
      series: SCORES.map((s) => ({
        name: s, type: 'heatmap' as const, coordinateSystem: 'calendar',
        itemStyle: { color: scoreColor(s) },
        data: days.filter((d) => d.bestScore === s).map((d) => ({
          value: [d.date, d.intensity, d.km, d.bestScore],
          itemStyle: { color: scoreColor(s), opacity: 0.25 + 0.75 * Math.min(1, Math.max(0, d.intensity)) },
        })),
      })),
    }
  }
}
