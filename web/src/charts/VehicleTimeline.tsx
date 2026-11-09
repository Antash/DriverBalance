import { useEffect, useMemo, useRef } from 'react'
import type { EChartsOption } from 'echarts'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Expense, TimelineChart, TyreSet } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtMoney, fmtNum, fmtDate } from '../lib/format'
import { timelineWidthPercent } from '../lib/timelineWidth'
import { MILESTONE_GROUPS, serviceMilestones, type MilestoneGroupKey, type ServiceMilestone } from '../lib/milestones'
import { partLine, tyreDisplayName } from '../lib/tyreSummary'
import { useAnalytics, useParams } from '../state/ParamsContext'
import { useTyreSets } from '../state/useVehicle'
import { timeAxisTooltip } from './dateTooltip'


/** 24x24 silhouettes for the service icons. */
const ICONS: Record<MilestoneGroupKey, { path: string; color: number }> = {
  oil: { path: 'path://M12 1C12 1 4 10.5 4 15a8 8 0 0 0 16 0C20 10.5 12 1 12 1z', color: 4 },
  tyres: { path: 'path://M12 1a11 11 0 1 1 0 22a11 11 0 1 1 0-22zM12 7a5 5 0 1 0 0 10a5 5 0 1 0 0-10z', color: 2 },
  brakes: { path: 'path://M12 1a11 11 0 1 1 0 22a11 11 0 1 1 0-22zM12 9.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5zM5 11h2v2H5zM17 11h2v2h-2zM11 5h2v2h-2zM11 17h2v2h-2z', color: 1 },
  filters: { path: 'path://M2 3h20l-8 9v8l-4 2V12z', color: 5 },
  other: { path: 'path://M21.7 6.3a6 6 0 0 1-8.1 6.6L5.4 21.1a2.6 2.6 0 0 1-3.6-3.6l8.2-8.2A6 6 0 0 1 16.7 1.2l-3.4 3.4.9 3.6 3.6.9z', color: -1 },
}
const ICON_SIZE = 16
/** Approximate plot height in px for a 280 px chart (legend, axis labels and zoom slider take the rest). */
const PLOT_HEIGHT = 180
type Extent = { min: number; max: number }

function milestoneTooltip(m: ServiceMilestone, sets: TyreSet[]): string {
  const set = m.expense.tyreSetId != null ? sets.find((s) => s.id === m.expense.tyreSetId) : undefined
  const lines = m.items.map((i) => {
    const part = i.type === 'TyreSwap' || i.type === 'NewTyres' ? (set ? tyreDisplayName(set) : null) : partLine(i)
    return `${i.label}${part ? ` · ${part}` : ''}${i.details ? ` · ${i.details}` : ''}`
  })
  const odo = m.odometer != null ? ` · ${fmtNum(m.odometer, 0)} km` : ''
  return `<b>${fmtDate(m.date)}</b>${odo}<br/>${lines.join('<br/>')}<br/>${fmtMoney(m.expense.amount, m.expense.currency)}`
}

function timelineOption(d: TimelineChart, sets: TyreSet[], maintenance: Expense[], from: string, end: string): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const line = d.odometer.map((p): [string, number] => [p.date, p.km])
    const setName = (id: number | null) => sets.find((s) => s.id === id)?.name ?? 'Unassigned tyres'
    const setIds = [...new Set(d.tyreBands.map((b) => b.tyreSetId))]
    const milestones = serviceMilestones(maintenance, from, end, line)
    const groupsShown = MILESTONE_GROUPS.filter((g) => milestones.some((m) => m.groups.includes(g.key)))
    const colorOf = (c: number) => (c < 0 ? t.muted : t.series[c])
    const tallestStack = Math.max(0, ...milestones.map((m) => m.groups.length))
    const headroom = Math.min(0.6, tallestStack === 0 ? 0 : (tallestStack * (ICON_SIZE + 2) + 8) / PLOT_HEIGHT)
    return {
      legend: {
        top: 0,
        data: ['Odometer', ...groupsShown.map((g) => ({ name: g.name, icon: ICONS[g.key].path }))],
      },
      tooltip: { trigger: 'item' },
      grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
      // Pad the time axis so edge icons aren't clipped, and leave headroom above the line for the tallest icon stack.
      xAxis: { type: 'time', min: (v: Extent) => v.min - (v.max - v.min) * 0.02, max: (v: Extent) => v.max + (v.max - v.min) * 0.02, ...axisStyle() },
      yAxis: {
        type: 'value', scale: true, name: 'km', nameTextStyle: { color: t.muted }, ...axisStyle(),
        // The padded top is rarely on a tick, so its label would crowd the one below.
        axisLabel: { ...axisStyle().axisLabel, showMaxLabel: false },
        max: (v: Extent) => {
          const top = v.max + (v.max - v.min) * headroom / (1 - headroom)
          const step = v.max - v.min > 20000 ? 5000 : 1000
          return Math.ceil(top / step) * step
        },
      },
      series: [
        {
          name: 'Odometer', type: 'line', showSymbol: false, lineStyle: { width: 2, color: t.series[0] }, itemStyle: { color: t.series[0] },
          data: line,
          tooltip: { trigger: 'axis', formatter: timeAxisTooltip((v) => `${fmtNum(v, 0)} km`) },
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
        // Service milestones: one icon per group of work, stacked above the odometer point of the visit.
        ...groupsShown.map((g) => ({
          name: g.name, type: 'scatter' as const, symbol: ICONS[g.key].path, symbolSize: ICON_SIZE, z: 5,
          itemStyle: { color: colorOf(ICONS[g.key].color), borderColor: t.surface, borderWidth: 1 },
          data: milestones.filter((m) => m.groups.includes(g.key)).map((m) => ({
            value: [m.date, m.odometer],
            symbolOffset: [0, -(ICON_SIZE / 2 + 4 + m.groups.indexOf(g.key) * (ICON_SIZE + 2))],
            milestone: m,
          })),
          tooltip: { formatter: (p: unknown) => milestoneTooltip((p as { data: { milestone: ServiceMilestone } }).data.milestone, sets) },
        })),
      ],
    } as EChartsOption
  }
}

/** Odometer line with event markers and tyre-set bands for [from, to). */
export function VehicleTimeline({ from, to, title = 'Timeline' }: { from: string; to: string; title?: string }) {
  const { data, isLoading, isError } = useAnalytics<TimelineChart>('charts/timeline', { from, to })
  const { data: sets = [] } = useTyreSets()
  const { vehicleId } = useParams()
  const { data: maintenance = [] } = useQuery({
    queryKey: ['expenses', vehicleId, 'maintenance'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?category=Maintenance`),
  })
  const scroller = useRef<HTMLDivElement>(null)
  const widthPercent = timelineWidthPercent(data?.odometer[0]?.date ?? null, data?.odometer.at(-1)?.date ?? null)
  useEffect(() => { const el = scroller.current; if (el) el.scrollLeft = el.scrollWidth }, [widthPercent])
  const opt = useMemo(() => (data ? timelineOption(data, sets, maintenance, from, to) : undefined), [data, sets, maintenance, from, to])
  return (
    <ChartCard title={title} loading={isLoading && !data} error={isError} empty={!!data && data.odometer.length === 0}
      emptyText="No odometer data in this period"
      hint="Odometer over time with service milestones: oil, tyres, brakes, filters and other service; a visit with several kinds of work stacks its icons. Hover for details. Shaded bands show which tyre set was fitted.">
      {opt && (
        // No zoom: up to 10 years fit the card; a longer history scrolls sideways, opening on the most recent years.
        <div className="chart-scroll" ref={scroller}>
          <div style={{ width: `${widthPercent}%` }}><Chart option={opt} height={260} /></div>
        </div>
      )}
    </ChartCard>
  )
}
