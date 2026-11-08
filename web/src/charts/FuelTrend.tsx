import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { FuelTrendChart, TrendLevel } from '../api/types'
import { axisStyle, chartTheme, scoreColor } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtDateTime, fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, SCORES, type RangeProps, type TripClickProps } from './shared'

interface PointData { value: [string, number]; km: number; tripCount: number; firstTripId: number; score: string }

function option(d: FuelTrendChart): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { top: 0 },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const x = p as { seriesType: string; data: PointData | [string, number]; value: [string, number] }
          if (x.seriesType === 'line') return `Smoothed trend<br/>${fmtDateTime(x.value[0])}: ${fmtNum(x.value[1], 1)} L/100`
          const pd = x.data as PointData
          return `${fmtDateTime(pd.value[0])}<br/>${fmtNum(pd.value[1], 1)} L/100 over ${fmtNum(pd.km, 1)} km<br/>${pd.tripCount} trip(s), score ${pd.score}<br/><i>Click for details</i>`
        },
      },
      grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
      xAxis: { type: 'time', ...axisStyle() },
      yAxis: { type: 'value', scale: true, name: 'L/100 km', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [
        ...SCORES.map((s) => ({
          name: s, type: 'scatter' as const, cursor: 'pointer',
          symbolSize: (_v: unknown, p: unknown) => Math.min(16, 6 + Math.sqrt((p as { data: PointData }).data.km) / 2),
          itemStyle: { color: scoreColor(s), borderColor: t.surface, borderWidth: 1.5, opacity: 0.9 },
          data: d.points.filter((p) => p.score === s).map((p): PointData => ({
            value: [p.time, p.lPer100], km: p.km, tripCount: p.tripCount, firstTripId: p.firstTripId, score: p.score,
          })),
        })),
        { name: 'Smoothed trend', type: 'line' as const, showSymbol: false, smooth: true, z: 3,
          lineStyle: { width: 2.5, color: t.text }, itemStyle: { color: t.text }, data: d.smooth.map((s) => [s.time, s.lPer100]) },
      ],
    }
  }
}

/** Fuel use per drive group or trip, coloured by score, with the LOWESS trend. Click a point for the trip. */
export function FuelTrend({ level, onTrip, ...range }: RangeProps & TripClickProps & { level: TrendLevel }) {
  const { data, isLoading, isError } = useAnalytics<FuelTrendChart>('charts/fuel-trend', { ...rangeBody(range), options: { level } })
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  return (
    <ChartCard title="Fuel consumption trend" loading={isLoading && !data} error={isError} empty={!!data && data.points.length === 0}
      hint={`One dot per ${level === 'Group' ? 'drive group' : 'trip'}; the line is the smoothed trend. Click a dot for the trip.`}>
      {opt && <Chart option={opt} onPointClick={(d) => { const id = (d as PointData | undefined)?.firstTripId; if (id != null) onTrip?.(id) }} />}
    </ChartCard>
  )
}
