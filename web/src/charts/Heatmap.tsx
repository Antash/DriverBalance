import { useMemo } from 'react'
import type { HeatmapDay } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { useAnalytics } from '../state/ParamsContext'
import { heatmapOption } from './heatmapOption'
import { rangeBody, type RangeProps } from './shared'

/** Calendar heatmap of daily km over the selected period (shade = intensity, capped by the heatmap km cap parameter). */
export function Heatmap({ from, to }: Required<RangeProps>) {
  const { data, isLoading, isError } = useAnalytics<HeatmapDay[]>('charts/heatmap', rangeBody({ from, to }))
  const opt = useMemo(() => (data ? heatmapOption(data, from, to) : undefined), [data, from, to])
  return (
    <ChartCard title="Driving calendar" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}>
      {opt && <Chart option={opt} height={262} />}
    </ChartCard>
  )
}
