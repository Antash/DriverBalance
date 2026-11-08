import { useMemo } from 'react'
import type { HeatmapDay } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { useAnalytics } from '../state/ParamsContext'
import { heatmapOption } from './heatmapOption'

/** Calendar heatmap of daily km (shade = intensity, capped by the heatmap km cap parameter). */
export function Heatmap({ year, years, onYear }: { year: number; years: number[]; onYear(y: number): void }) {
  const { data, isLoading, isError } = useAnalytics<HeatmapDay[]>('charts/heatmap', { options: { year } })
  const opt = useMemo(() => (data ? heatmapOption(data, year) : undefined), [data, year])
  return (
    <ChartCard title="Driving calendar" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}
      emptyText={`No trips in ${year}`}
      actions={
        <select aria-label="Heatmap year" value={year} onChange={(e) => onYear(Number(e.target.value))}>
          {(years.includes(year) ? years : [year, ...years]).map((y) => <option key={y} value={y}>{y}</option>)}
        </select>
      }>
      {opt && <Chart option={opt} height={250} />}
    </ChartCard>
  )
}
