import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { SeasonPoint } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const MAX_SERIES = 8

/** One line per year over Jan-Dec; the most recent eight years (colour follows the year, in a fixed order). */
function option(points: SeasonPoint[]): () => EChartsOption {
  const years = [...new Set(points.map((p) => p.year))].sort((a, b) => a - b).slice(-MAX_SERIES)
  return () => {
    const t = chartTheme()
    return {
      legend: { top: 0 },
      tooltip: { trigger: 'axis', valueFormatter: (v) => (v == null ? '—' : `${fmtNum(Number(v), 1)} L/100`) } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
      xAxis: { type: 'category', data: MONTHS, ...axisStyle() },
      yAxis: { type: 'value', scale: true, name: 'L/100 km', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: years.map((y, i) => ({
        name: String(y), type: 'line' as const, symbolSize: 8, lineStyle: { width: 2 },
        itemStyle: { color: t.series[i], borderColor: t.surface, borderWidth: 2 },
        data: MONTHS.map((_, m) => points.find((p) => p.year === y && p.month === m + 1)?.lPer100 ?? null),
      })),
    }
  }
}

/** Consumption by calendar month, one line per year. Always over all data (the API ignores the period). */
export function Seasonality() {
  const { data, isLoading, isError } = useAnalytics<SeasonPoint[]>('charts/seasonality', {})
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  return (
    <ChartCard title="Seasonality" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}
      emptyText="No trips yet" hint="Fuel use per calendar month for each year (all data, regardless of the period above).">
      {opt && <Chart option={opt} height={300} />}
    </ChartCard>
  )
}
