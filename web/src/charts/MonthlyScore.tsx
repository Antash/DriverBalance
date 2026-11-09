import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { MonthlyScoreChart } from '../api/types'
import { axisStyle, chartTheme, scoreColor, timeZoom, ZOOM_GRID_BOTTOM } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum, fmtPct } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, SCORES, type RangeProps } from './shared'
import { timeAxisTooltip } from './dateTooltip'

const KEYS = ['excellentKm', 'goodKm', 'fairKm', 'badKm'] as const

function barsOption(d: MonthlyScoreChart): () => EChartsOption {
  return () => ({
    legend: { top: 0 },
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${fmtNum(Number(v), 0)} km` } as EChartsOption['tooltip'],
    grid: { left: 8, right: 16, top: 40, bottom: ZOOM_GRID_BOTTOM, containLabel: true },
    dataZoom: timeZoom(),
    xAxis: { type: 'category', data: d.months.map((m) => m.month), ...axisStyle() },
    yAxis: { type: 'value', name: 'km', nameTextStyle: { color: chartTheme().muted }, ...axisStyle() },
    series: SCORES.map((s, i) => ({
      name: s, type: 'bar', stack: 'km', barMaxWidth: 28,
      itemStyle: { color: scoreColor(s), borderColor: chartTheme().surface, borderWidth: 2 },
      data: d.months.map((m) => m[KEYS[i]]),
    })),
  })
}

function dpfOption(d: MonthlyScoreChart): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { top: 0 },
      tooltip: { trigger: 'axis', formatter: timeAxisTooltip((v) => fmtPct(v)) } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 40, bottom: ZOOM_GRID_BOTTOM, containLabel: true },
      dataZoom: timeZoom(),
      xAxis: { type: 'time', ...axisStyle() },
      yAxis: {
        type: 'value', min: 0, max: 1, name: 'DPF index', nameTextStyle: { color: t.muted },
        axisLabel: { color: t.muted, formatter: (v: number) => fmtPct(v) }, splitLine: axisStyle().splitLine,
      },
      series: [
        { name: 'Weekly', type: 'line', showSymbol: d.weeks.length < 60, symbolSize: 8, lineStyle: { width: 1.5, opacity: 0.6 },
          itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2 }, data: d.weeks.map((w) => [w.weekStart, w.dpf]) },
        { name: 'Moving average', type: 'line', showSymbol: false, lineStyle: { width: 2 },
          itemStyle: { color: t.series[1] }, data: d.weeks.map((w) => [w.weekStart, w.dpfMovingAverage]) },
      ],
    }
  }
}

/** Monthly km by driving score, and (as a second chart, never a second axis) the weekly DPF index with its moving average. */
export function MonthlyScore(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<MonthlyScoreChart>('charts/monthly-score', rangeBody(range))
  const bars = useMemo(() => (data ? barsOption(data) : undefined), [data])
  const dpf = useMemo(() => (data ? dpfOption(data) : undefined), [data])
  return (
    <ChartCard title="Monthly score and DPF" loading={isLoading && !data} error={isError} empty={!!data && data.months.length === 0}
      hint="Km per month by group score, then the share of good-score km per week (higher is kinder to the DPF).">
      {bars && dpf && <>
        <Chart option={bars} height={260} />
        <Chart option={dpf} height={220} />
      </>}
    </ChartCard>
  )
}
