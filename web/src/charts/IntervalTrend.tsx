import { useMemo } from 'react'
import type { EChartsOption } from 'echarts'
import type { FuelTrendChart, MonthlyScoreChart } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { fmtNum, fmtPct } from '../lib/format'
import { weeklyLPer100 } from '../lib/intervals'
import { useAnalytics } from '../state/ParamsContext'

function lineOption(opts: {
  name: string; data: [string, number | null][]; color: number; unit: string; min?: number; max?: number
  fmt(v: number): string; serviceDate: string | null
}): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { show: false },
      tooltip: { trigger: 'axis', valueFormatter: (v) => (v == null ? '—' : opts.fmt(Number(v))) } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 28, bottom: 8, containLabel: true },
      xAxis: { type: 'time', ...axisStyle() },
      yAxis: {
        type: 'value', scale: opts.min == null, min: opts.min, max: opts.max, name: opts.unit, nameTextStyle: { color: t.muted },
        axisLabel: { color: t.muted, formatter: (v: number) => opts.fmt(v) }, splitLine: axisStyle().splitLine,
      },
      series: [{
        name: opts.name, type: 'line', showSymbol: opts.data.length < 60, symbolSize: 8, connectNulls: false,
        lineStyle: { width: 2 }, itemStyle: { color: t.series[opts.color], borderColor: t.surface, borderWidth: 2 },
        data: opts.data,
        markLine: opts.serviceDate ? {
          symbol: 'none', silent: true,
          lineStyle: { color: t.text, type: 'dashed', width: 1.5 },
          label: { formatter: `Service ${opts.serviceDate}`, color: t.textSecondary, position: 'insideEndTop' },
          data: [{ xAxis: opts.serviceDate }],
        } : undefined,
      }],
    }
  }
}

/** Weekly L/100 km and weekly DPF index across both intervals, as two charts (never a second axis), with the service date marked. */
export function IntervalTrend({ from, to, serviceDate }: { from: string; to: string; serviceDate: string | null }) {
  const fuel = useAnalytics<FuelTrendChart>('charts/fuel-trend', { from, to, options: { level: 'Group' } })
  const score = useAnalytics<MonthlyScoreChart>('charts/monthly-score', { from, to })
  const weekly = useMemo(() => (fuel.data ? weeklyLPer100(fuel.data.points) : null), [fuel.data])
  const fuelOpt = useMemo(() => weekly && lineOption({
    name: 'Weekly L/100 km', data: weekly.map((w) => [w.weekStart, w.lPer100]), color: 0, unit: 'L/100 km',
    fmt: (v) => fmtNum(v, 1), serviceDate,
  }), [weekly, serviceDate])
  const dpfOpt = useMemo(() => score.data && lineOption({
    name: 'Weekly DPF index', data: score.data.weeks.map((w) => [w.weekStart, w.dpf]), color: 1, unit: 'DPF index', min: 0, max: 1,
    fmt: (v) => fmtPct(v), serviceDate,
  }), [score.data, serviceDate])
  const empty = !!weekly && weekly.length === 0 && !!score.data && score.data.weeks.length === 0
  return (
    <ChartCard title="Consumption and DPF over the intervals" loading={(fuel.isLoading && !fuel.data) || (score.isLoading && !score.data)}
      error={fuel.isError || score.isError} empty={empty}
      hint={serviceDate ? `Weekly values; the dashed line marks the service on ${serviceDate}.` : 'Weekly values.'}>
      {fuelOpt && <Chart option={fuelOpt} height={220} />}
      {dpfOpt && <Chart option={dpfOpt} height={200} />}
    </ChartCard>
  )
}
