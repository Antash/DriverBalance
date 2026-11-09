import { useMemo } from 'react'
import type { GapHistogram as GapData } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { useAnalytics } from '../state/ParamsContext'
import { gapOption } from './gapOption'
import { rangeBody, type RangeProps } from './shared'

/** Gaps between consecutive trips; the dashed line is the cold-start gap, so shorter gaps join one drive group. */
export function GapHistogram(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<GapData>('charts/gap-histogram', rangeBody(range))
  const opt = useMemo(() => (data ? gapOption(data) : undefined), [data])
  const empty = !!data && data.bins.every((b) => b.count === 0)
  return (
    <ChartCard title="Gaps between trips" loading={isLoading && !data} error={isError} empty={empty}
      hint="Gaps under the cold-start gap (dashed line) join one drive; longer ones mean a cold start.">
      {opt && <Chart option={opt} height={300} />}
    </ChartCard>
  )
}
