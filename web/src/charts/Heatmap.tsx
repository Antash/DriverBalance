import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { HeatmapDay } from '../api/types'
import { chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'

function option(days: HeatmapDay[], year: number): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()
    return {
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const v = (p as { value: [string, number, number, string] }).value
          return `${v[0]}<br/>${fmtNum(v[2], 1)} km, best score: ${v[3]}`
        },
      },
      visualMap: {
        min: 0, max: 1, calculable: false, orient: 'horizontal', left: 'center', bottom: 0, itemWidth: 12, itemHeight: 120,
        text: ['more km', 'less'], textStyle: { color: t.textSecondary }, inRange: { color: [t.grid, accent] },
      },
      calendar: {
        range: String(year), top: 28, left: 36, right: 12, bottom: 44, cellSize: ['auto', 14],
        itemStyle: { color: t.grid, borderColor: t.surface, borderWidth: 2 },
        splitLine: { show: false },
        yearLabel: { show: false },
        dayLabel: { firstDay: 1, color: t.muted },
        monthLabel: { color: t.muted },
      },
      series: [{
        type: 'heatmap', coordinateSystem: 'calendar',
        data: days.map((d) => [d.date, d.intensity, d.km, d.bestScore]),
      }],
    }
  }
}

/** Calendar heatmap of daily km (shade = intensity, capped by the heatmap km cap parameter). */
export function Heatmap({ year, years, onYear }: { year: number; years: number[]; onYear(y: number): void }) {
  const { data, isLoading, isError } = useAnalytics<HeatmapDay[]>('charts/heatmap', { options: { year } })
  const opt = useMemo(() => (data ? option(data, year) : undefined), [data, year])
  return (
    <ChartCard title="Driving calendar" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}
      emptyText={`No trips in ${year}`}
      actions={
        <select aria-label="Heatmap year" value={year} onChange={(e) => onYear(Number(e.target.value))}>
          {(years.includes(year) ? years : [year, ...years]).map((y) => <option key={y} value={y}>{y}</option>)}
        </select>
      }>
      {opt && <Chart option={opt} height={230} />}
    </ChartCard>
  )
}
