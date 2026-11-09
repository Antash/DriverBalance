import type { EChartsOption } from 'echarts'
import type { HeatmapDay } from '../api/types'
import { chartTheme, scoreColor } from '../components/chartTheme'
import { addDays } from '../lib/dates'
import { fmtNum, fmtDate } from '../lib/format'
import { SCORES } from './shared'

/** Year labels for the calendar: the first year at the start of the range, then every 1 January inside it (with a delimiter). */
export function yearMarks(from: string, toExclusive: string): { date: string; year: number; delimiter: boolean }[] {
  const last = addDays(toExclusive, -1)
  if (last < from) return []
  const marks = [{ date: from, year: Number(from.slice(0, 4)), delimiter: false }]
  for (let y = Number(from.slice(0, 4)) + 1; y <= Number(last.slice(0, 4)); y++) marks.push({ date: `${y}-01-01`, year: y, delimiter: true })
  return marks
}

interface CalendarCoordSys { x: number; y: number; width: number; height: number; cellWidth: number; cellHeight: number }

/**
 * One calendar strip over the whole period (to exclusive). Colour = best score of the day (fixed score palette);
 * opacity = intensity (km, capped by the heatmap cap). A stepped line splits the years, each labelled above its first week.
 */
export function heatmapOption(days: HeatmapDay[], from: string, toExclusive: string): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const marks = yearMarks(from, toExclusive)
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
        range: [from, addDays(toExclusive, -1)], top: 76, left: 44, right: 12, bottom: 8, cellSize: ['auto', 14],
        itemStyle: { color: t.grid, borderColor: t.surface, borderWidth: 2 },
        splitLine: { show: false },
        yearLabel: { show: false },
        dayLabel: { firstDay: 1, color: t.muted, nameMap: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] },
        monthLabel: { color: t.muted },
      },
      series: [
        ...SCORES.map((s) => ({
          name: s, type: 'heatmap' as const, coordinateSystem: 'calendar',
          itemStyle: { color: scoreColor(s) },
          data: days.filter((d) => d.bestScore === s).map((d) => ({
            value: [d.date, d.intensity, d.km, d.bestScore],
            itemStyle: { color: scoreColor(s), opacity: 0.25 + 0.75 * Math.min(1, Math.max(0, d.intensity)) },
          })),
        })),
        {
          name: 'Years', type: 'custom' as const, coordinateSystem: 'calendar', silent: true, z: 10,
          data: marks.map((m) => [m.date, m.year, m.delimiter ? 1 : 0]),
          renderItem: (params: unknown, api: unknown) => {
            const a = api as { value(i: number): number | string; coord(v: unknown[]): number[] }
            const cs = (params as { coordSys: CalendarCoordSys }).coordSys
            const [cx, cy] = a.coord([a.value(0)])
            const cw = cs.cellWidth, ch = cs.cellHeight
            const top = cs.y, bottom = cs.y + 7 * ch
            const row = Math.round((cy - top) / ch - 0.5)
            const xl = cx - cw / 2, xr = cx + cw / 2
            const label = {
              type: 'text' as const,
              style: { text: String(a.value(1)), x: row > 0 ? xr + 2 : xl + 2, y: top - 40, fill: t.text, font: '600 12px system-ui, sans-serif' },
            }
            if (!a.value(2)) return label
            const points = row === 0
              ? [[xl, bottom], [xl, top - 30]]
              : [[xl, bottom], [xl, cy - ch / 2], [xr, cy - ch / 2], [xr, top - 30]]
            return {
              type: 'group' as const,
              children: [
                { type: 'polyline' as const, shape: { points }, style: { stroke: t.text, lineWidth: 1.5, fill: 'none' } },
                label,
              ],
            }
          },
        },
      ] as EChartsOption['series'],
    }
  }
}
