import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { PenaltyBin } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, type RangeProps } from './shared'

const label = (b: PenaltyBin) => (b.toMinutes == null ? `${b.fromMinutes}+` : `${b.fromMinutes}–${b.toMinutes}`)

function option(bins: PenaltyBin[]): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { top: 0, data: ['Median', 'Middle half (25th–75th percentile)'] },
      tooltip: {
        trigger: 'axis',
        formatter: (ps: unknown) => {
          const i = (ps as { dataIndex: number }[])[0].dataIndex
          const b = bins[i]
          if (b.count === 0) return `${label(b)} min<br/>No drives`
          return `${label(b)} min (${b.count} drives)<br/>Median ${fmtNum(b.medianLPer100, 1)} L/100<br/>Middle half ${fmtNum(b.p25, 1)}–${fmtNum(b.p75, 1)}`
        },
      } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
      xAxis: { type: 'category', data: bins.map(label), name: 'Driving time of the drive (min)', nameLocation: 'middle', nameGap: 28, nameTextStyle: { color: t.muted }, ...axisStyle() },
      yAxis: { type: 'value', scale: true, name: 'L/100 km', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [
        { name: 'p25', type: 'line', stack: 'band', symbol: 'none', lineStyle: { opacity: 0 }, silent: true, data: bins.map((b) => b.p25) },
        { name: 'Middle half (25th–75th percentile)', type: 'line', stack: 'band', symbol: 'none', lineStyle: { opacity: 0 },
          areaStyle: { color: t.series[0], opacity: 0.18 }, itemStyle: { color: t.series[0] },
          data: bins.map((b) => (b.p25 == null || b.p75 == null ? null : b.p75 - b.p25)) },
        { name: 'Median', type: 'line', symbolSize: 8, lineStyle: { width: 2 }, itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2 },
          data: bins.map((b) => b.medianLPer100) },
      ],
    }
  }
}

/** Consumption by drive length: short drives pay the cold-engine penalty. */
export function PenaltyCurve(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<PenaltyBin[]>('charts/penalty-curve', rangeBody(range))
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  return (
    <ChartCard title="Short-drive penalty" loading={isLoading && !data} error={isError} empty={!!data && data.every((b) => b.count === 0)}
      hint="Median fuel use by how long the drive lasted; shaded band is the middle half of drives.">
      {opt && <Chart option={opt} height={300} />}
    </ChartCard>
  )
}
