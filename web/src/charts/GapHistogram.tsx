import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { GapHistogram as GapData } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { thresholdPosition } from '../lib/chartGeometry'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, type RangeProps } from './shared'

const label = (b: GapData['bins'][number]) => (b.to == null ? `${b.from}+` : `${b.from}–${b.to}`)

function option(d: GapData): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { show: false },
      tooltip: { trigger: 'axis', valueFormatter: (v) => `${v} gaps` } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
      xAxis: { type: 'category', data: d.bins.map(label), name: 'Gap between trips (min)', nameLocation: 'middle', nameGap: 28, nameTextStyle: { color: t.muted }, ...axisStyle() },
      yAxis: { type: 'value', name: 'Gaps', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [{
        name: 'Gaps', type: 'bar', barMaxWidth: 22,
        itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2, borderRadius: [4, 4, 0, 0] },
        data: d.bins.map((b) => b.count),
        markLine: {
          silent: true, symbol: 'none', animation: false,
          lineStyle: { color: t.series[1], width: 2, type: 'dashed' },
          label: { formatter: `Cold-start gap: ${d.thresholdMinutes} min`, color: t.text, position: 'insideEndTop' },
          data: [{ xAxis: thresholdPosition(d.bins, d.thresholdMinutes) }],
        },
      }],
    }
  }
}

/** Gaps between consecutive trips; the dashed line is the cold-start gap, so shorter gaps join one drive group. */
export function GapHistogram(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<GapData>('charts/gap-histogram', rangeBody(range))
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  const empty = !!data && data.bins.every((b) => b.count === 0)
  return (
    <ChartCard title="Gaps between trips" loading={isLoading && !data} error={isError} empty={empty}
      hint="Gaps shorter than the cold-start gap (dashed line) count as the same drive; longer ones start a cold engine.">
      {opt && <Chart option={opt} height={300} />}
    </ChartCard>
  )
}
