import type { EChartsOption } from 'echarts'
import type { PeriodWithStats } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { METRICS, type MetricKey } from '../lib/compare'
import { formatMetric } from '../lib/metricFormat'

/** One metric across all intervals of a type, oldest to newest. Null values stay gaps; the current (newest) bar is full strength. */
export function smallMultipleOption(newestFirst: PeriodWithStats[], key: MetricKey, currency: string): () => EChartsOption {
  const def = METRICS.find((m) => m.key === key)!
  const rows = [...newestFirst].reverse()
  return () => {
    const t = chartTheme()
    return {
      legend: { show: false },
      tooltip: {
        trigger: 'axis',
        valueFormatter: (v) => formatMetric(key, v == null ? null : Number(v), currency),
      } as EChartsOption['tooltip'],
      grid: { left: 8, right: 8, top: 12, bottom: 4, containLabel: true },
      xAxis: { type: 'category', data: rows.map((r) => r.period.label), ...axisStyle(), axisLabel: { show: false } },
      yAxis: {
        type: 'value', ...axisStyle(),
        axisLabel: { color: t.muted, formatter: (v: number) => formatMetric(key, v, currency) },
      },
      series: [{
        name: def.label, type: 'bar', barMaxWidth: 28,
        data: rows.map((r, i) => ({
          value: def.read(r.stats),
          itemStyle: {
            color: t.series[0], opacity: i === rows.length - 1 ? 1 : 0.45,
            borderColor: t.surface, borderWidth: 2, borderRadius: [4, 4, 0, 0],
          },
        })),
      }],
    }
  }
}
