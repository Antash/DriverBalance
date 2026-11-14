import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { ScoreMapChart } from '../api/types'
import { axisStyle, chartTheme, scoreColor } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { zoneRects } from '../lib/chartGeometry'
import { fmtDateTime, fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, SCORES, type RangeProps, type TripClickProps } from './shared'

interface PointData { value: [number, number]; km: number; start: string; firstTripId: number; score: string }

function option(d: ScoreMapChart): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const z = d.zones
    const xMax = Math.ceil(Math.max(z.longMin * 1.6, ...d.points.map((p) => p.durationMinutes)) / 10) * 10
    const yMax = Math.ceil(Math.max(z.mixedKmh * 1.4, ...d.points.map((p) => p.averageSpeedKmh)) / 10) * 10
    const areas = zoneRects(z, xMax, yMax).map((r) => [
      {
        name: r.label ? r.score : '', xAxis: r.x0, yAxis: r.y0,
        itemStyle: { color: scoreColor(r.score), opacity: 0.14 },
        // Bottom right of each strip, on a backing so dots behind don't swallow it.
        label: {
          show: !!r.label, color: t.textSecondary, position: 'insideBottomRight', fontSize: 11,
          backgroundColor: t.surface, padding: [1, 4], borderRadius: 3,
        },
      },
      { xAxis: r.x1, yAxis: r.y1 },
    ])
    return {
      legend: { top: 0, data: SCORES },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const pd = (p as { data: PointData }).data
          return `${fmtDateTime(pd.start)}<br/>${fmtNum(pd.value[0], 0)} min at ${fmtNum(pd.value[1], 0)} km/h, ${fmtNum(pd.km, 1)} km<br/>Score ${pd.score}<br/><i>Click for details</i>`
        },
      },
      grid: { left: 8, right: 16, top: 48, bottom: 28, containLabel: true },
      xAxis: { type: 'value', min: 0, max: xMax, name: 'Driving time (min)', nameLocation: 'middle', nameGap: 26, nameTextStyle: { color: t.muted }, ...axisStyle() },
      // The name starts at the axis and runs right; centred on the axis it was cut off at the card edge.
      yAxis: { type: 'value', min: 0, max: yMax, name: 'Average speed (km/h)', nameTextStyle: { color: t.muted, align: 'left' }, ...axisStyle(),
        // The rounded-up top is rarely on a tick, so its label would crowd the one below.
        axisLabel: { ...axisStyle().axisLabel, showMaxLabel: false } },
      series: [
        { name: 'Zones', type: 'scatter', data: [], silent: true, markArea: { silent: true, data: areas as never } },
        ...SCORES.map((s) => ({
          name: s, type: 'scatter' as const, cursor: 'pointer', z: 3,
          symbolSize: (_v: unknown, p: unknown) => Math.min(18, 6 + Math.sqrt((p as { data: PointData }).data.km) / 2),
          itemStyle: { color: scoreColor(s), borderColor: t.surface, borderWidth: 1.5, opacity: 0.95 },
          data: d.points.filter((p) => p.score === s).map((p): PointData => ({
            value: [p.durationMinutes, p.averageSpeedKmh], km: p.km, start: p.start, firstTripId: p.firstTripId, score: p.score,
          })),
        })),
      ],
    }
  }
}

/** Duration vs speed per drive group, over the score zones of the current (possibly unsaved) parameters. */
export function ScoreMap({ level, onTrip, ...range }: RangeProps & TripClickProps & { level: 'Group' | 'Trip' }) {
  const { data, isLoading, isError } = useAnalytics<ScoreMapChart>('charts/score-map', { ...rangeBody(range), options: { level } })
  const opt = useMemo(() => (data ? option(data) : undefined), [data])
  return (
    <ChartCard title="Score map" loading={isLoading && !data} error={isError} empty={!!data && data.points.length === 0}
      hint={`Each dot is a ${level === 'Group' ? 'drive group' : 'single trip'}. Shaded zones show where a drive scores Fair, Good or Excellent; everything else is Bad. Click a dot for the trip.`}>
      {opt && <Chart option={opt} height={380} onPointClick={(d) => { const id = (d as PointData | undefined)?.firstTripId; if (id != null) onTrip?.(id) }} />}
    </ChartCard>
  )
}
