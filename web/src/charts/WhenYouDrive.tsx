import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { HourCell } from '../api/types'
import { chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum, fmtPct } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, type RangeProps } from './shared'

const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
const HOURS = Array.from({ length: 24 }, (_, h) => String(h).padStart(2, '0'))

function option(cells: HourCell[]): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()
    const max = Math.max(1, ...cells.map((c) => c.km))
    return {
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const c = (p as { data: { cell: HourCell } }).data.cell
          return `${DAYS[c.dayOfWeek]} ${HOURS[c.hour]}:00<br/>${fmtNum(c.km, 1)} km in ${c.trips} trip(s)<br/>Bad-score share: ${c.badShare == null ? '—' : fmtPct(c.badShare)}`
        },
      },
      grid: { left: 8, right: 16, top: 40, bottom: 30, containLabel: true },
      xAxis: { type: 'category', data: HOURS, name: 'Trip start hour', nameLocation: 'middle', nameGap: 28, nameTextStyle: { color: t.muted }, splitArea: { show: false }, axisLabel: { color: t.muted }, axisLine: { lineStyle: { color: t.axis } }, axisTick: { show: false } },
      yAxis: { type: 'category', data: DAYS, inverse: true, axisLabel: { color: t.muted }, axisLine: { show: false }, axisTick: { show: false } },
      visualMap: {
        min: 0, max, calculable: false, orient: 'horizontal', left: 'center', top: 0, itemWidth: 12, itemHeight: 120,
        text: ['more km', 'less'], textStyle: { color: t.textSecondary }, inRange: { color: [t.grid, accent] }, dimension: 2,
      },
      series: [{
        type: 'heatmap',
        itemStyle: { borderColor: t.surface, borderWidth: 2, borderRadius: 3 },
        data: cells.map((c) => ({ value: [c.hour, c.dayOfWeek, c.km], cell: c })),
      }],
    }
  }
}

/** Weekday x hour grid of km driven (trip start hour). */
export function WhenYouDrive(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<HourCell[]>('charts/when-you-drive', rangeBody(range))
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  return (
    <ChartCard title="When you drive" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}
      hint="Km driven by weekday and the hour the trip started; darker cells mean more km.">
      {opt && <Chart option={opt} height={300} />}
    </ChartCard>
  )
}
