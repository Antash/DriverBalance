import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { FuelTrendChart, TrendLevel } from '../api/types'
import { axisStyle, chartTheme, scoreColor, timeZoom, ZOOM_GRID_BOTTOM } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtDateTime, fmtNum } from '../lib/format'
import { fuelScale } from '../lib/fuelScale'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, SCORES, type RangeProps, type TripClickProps } from './shared'

interface PointData { value: [string, number]; lPer100: number; km: number; tripCount: number; firstTripId: number; score: string }

function option(d: FuelTrendChart): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const sc = fuelScale(d.points.map((p) => p.lPer100))
    const tickAt = sc.ticks.map((k) => k.at)
    const label = (at: number) => { const k = sc.ticks.find((x) => Math.abs(x.at - at) < 1e-9); return k ? fmtNum(k.value, 1) : '' }
    // The compressed tails get a faint band so the change of scale is visible.
    const tails = [
      ...(sc.min < 0 ? [[{ yAxis: sc.min }, { yAxis: 0 }]] : []),
      ...(sc.max > 1 ? [[{ yAxis: 1 }, { yAxis: sc.max }]] : []),
    ]
    return {
      legend: { top: 0 },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const x = p as { seriesType: string; data: PointData | [string, number]; value: [string, number] }
          if (x.seriesType === 'line') return `Smoothed trend<br/>${fmtDateTime(x.value[0])}: ${fmtNum((x.data as unknown as { l: number }).l, 1)} L/100`
          const pd = x.data as PointData
          return `${fmtDateTime(pd.value[0])}<br/>${fmtNum(pd.lPer100, 1)} L/100 over ${fmtNum(pd.km, 1)} km<br/>${pd.tripCount} trip(s), score ${pd.score}<br/><i>Click for details</i>`
        },
      },
      grid: { left: 8, right: 16, top: 40, bottom: ZOOM_GRID_BOTTOM, containLabel: true },
      dataZoom: timeZoom(),
      xAxis: { type: 'time', ...axisStyle() },
      yAxis: {
        type: 'value', min: sc.min, max: sc.max, name: 'L/100 km', ...axisStyle(), nameTextStyle: { color: t.muted, align: 'left' },
        // Split lines follow the ticks, so custom tick values put grid lines on the labelled litres.
        axisTick: { show: false, customValues: tickAt },
        axisLabel: { color: t.muted, customValues: tickAt, formatter: label },
      },
      series: [
        ...SCORES.map((s) => ({
          name: s, type: 'scatter' as const, cursor: 'pointer',
          symbolSize: (_v: unknown, p: unknown) => Math.min(16, 6 + Math.sqrt((p as { data: PointData }).data.km) / 2),
          itemStyle: { color: scoreColor(s), borderColor: t.surface, borderWidth: 1.5, opacity: 0.9 },
          data: d.points.filter((p) => p.score === s).map((p): PointData => ({
            value: [p.time, sc.toAxis(p.lPer100)], lPer100: p.lPer100, km: p.km, tripCount: p.tripCount, firstTripId: p.firstTripId, score: p.score,
          })),
        })),
        { name: 'Smoothed trend', type: 'line' as const, showSymbol: false, smooth: true, z: 3,
          lineStyle: { width: 2.5, color: t.text }, itemStyle: { color: t.text },
          data: d.smooth.map((s) => ({ value: [s.time, sc.toAxis(s.lPer100)], l: s.lPer100 })),
          markArea: { silent: true, itemStyle: { color: t.grid, opacity: 0.35 }, data: tails as never } },
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
      hint={`One dot per ${level === 'Group' ? 'drive group' : 'trip'}; the line is the smoothed trend. The usual range is drawn larger; the shaded outer bands are compressed. Click a dot for the trip. Scroll or use the slider to zoom.`}>
      {opt && <Chart option={opt} height={420} onPointClick={(d) => { const id = (d as PointData | undefined)?.firstTripId; if (id != null) onTrip?.(id) }} />}
    </ChartCard>
  )
}
