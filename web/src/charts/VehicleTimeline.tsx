import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { TimelineChart, TimelineEventKind, TyreSet } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { useTyreSets } from '../state/useVehicle'

const KINDS: { kind: TimelineEventKind; name: string; symbol: string; color: number }[] = [
  { kind: 'Maintenance', name: 'Maintenance', symbol: 'diamond', color: 1 },
  { kind: 'TyreMount', name: 'Tyre swap', symbol: 'triangle', color: 2 },
  { kind: 'Expense', name: 'Expense', symbol: 'circle', color: 3 },
  { kind: 'Import', name: 'Import', symbol: 'rect', color: -1 },
]

/** Odometer at a date: the last odometer point on or before it. */
function odometerAt(line: [string, number][], date: string): number | null {
  let km: number | null = null
  for (const [d, v] of line) { if (d <= date) km = v; else break }
  return km ?? line[0]?.[1] ?? null
}

function timelineOption(d: TimelineChart, sets: TyreSet[], end: string): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const line = d.odometer.map((p): [string, number] => [p.date, p.km])
    const setName = (id: number | null) => sets.find((s) => s.id === id)?.name ?? 'Unassigned tyres'
    const setIds = [...new Set(d.tyreBands.map((b) => b.tyreSetId))]
    return {
      legend: { top: 0, data: ['Odometer', ...KINDS.filter((k) => d.events.some((e) => e.kind === k.kind)).map((k) => k.name)] },
      tooltip: { trigger: 'item' },
      grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
      xAxis: { type: 'time', ...axisStyle() },
      yAxis: { type: 'value', scale: true, name: 'km', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [
        {
          name: 'Odometer', type: 'line', showSymbol: false, lineStyle: { width: 2, color: t.series[0] }, itemStyle: { color: t.series[0] },
          data: line,
          tooltip: { trigger: 'axis', valueFormatter: (v: unknown) => `${fmtNum(Number(v), 0)} km` },
          markArea: {
            silent: true,
            data: d.tyreBands.map((b) => {
              const c = t.series[setIds.indexOf(b.tyreSetId) % t.series.length]
              return [
                { name: setName(b.tyreSetId), xAxis: b.from, itemStyle: { color: c, opacity: 0.12 }, label: { color: t.textSecondary, position: 'insideTopLeft' } },
                { xAxis: b.to ?? end },
              ]
            }),
          },
        },
        ...KINDS.map((k) => ({
          name: k.name, type: 'scatter' as const, symbol: k.symbol, symbolSize: 11, z: 4,
          itemStyle: { color: k.color < 0 ? t.muted : t.series[k.color], borderColor: t.surface, borderWidth: 1.5 },
          data: d.events.filter((e) => e.kind === k.kind).map((e) => ({
            value: [e.date, e.odometer ?? odometerAt(line, e.date)],
            label: e.label,
          })),
          tooltip: {
            formatter: (p: unknown) => {
              const x = p as { data: { value: [string, number | null]; label: string } }
              return `${x.data.value[0]}<br/>${x.data.label}${x.data.value[1] != null ? `<br/>${fmtNum(x.data.value[1], 0)} km` : ''}`
            },
          },
        })),
      ],
    } as EChartsOption
  }
}

/** Odometer line with event markers and tyre-set bands for [from, to). */
export function VehicleTimeline({ from, to, title = 'Timeline' }: { from: string; to: string; title?: string }) {
  const { data, isLoading, isError } = useAnalytics<TimelineChart>('charts/timeline', { from, to })
  const { data: sets = [] } = useTyreSets()
  const opt = useMemo(() => (data ? timelineOption(data, sets, to) : undefined), [data, sets, to])
  return (
    <ChartCard title={title} loading={isLoading && !data} error={isError} empty={!!data && data.odometer.length === 0}
      emptyText="No odometer data in this period"
      hint="Odometer over time; markers are services, tyre swaps, expenses and imports. Shaded bands show which tyre set was fitted.">
      {opt && <Chart option={opt} height={280} />}
    </ChartCard>
  )
}
