import type { EChartsOption } from 'echarts'
import type { GapHistogram } from '../api/types'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { thresholdPosition } from '../lib/chartGeometry'

const label = (b: GapHistogram['bins'][number]) => (b.to == null ? `${b.from}+` : `${b.from}–${b.to}`)

/**
 * Bars sit on a category axis (ECharts rounds fractional category coordinates), so the threshold line lives on a
 * hidden linear value axis spanning exactly the same width (-0.5 .. n-0.5) and lands precisely at thresholdMinutes.
 */
export function gapOption(d: GapHistogram): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const n = d.bins.length
    return {
      legend: { show: false },
      tooltip: { trigger: 'axis', valueFormatter: (v) => `${v} gaps` } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
      xAxis: [
        { type: 'category', data: d.bins.map(label), name: 'Gap between trips (min)', nameLocation: 'middle', nameGap: 28, nameTextStyle: { color: t.muted }, ...axisStyle() },
        { type: 'value', min: -0.5, max: n - 0.5, show: false },
      ],
      yAxis: { type: 'value', name: 'Gaps', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [
        {
          name: 'Gaps', type: 'bar', barMaxWidth: 22,
          itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2, borderRadius: [4, 4, 0, 0] },
          data: d.bins.map((b) => b.count),
        },
        {
          name: 'Cold-start gap', type: 'scatter', xAxisIndex: 1, yAxisIndex: 0, data: [], silent: true,
          markLine: {
            silent: true, symbol: 'none', animation: false,
            lineStyle: { color: t.series[1], width: 2, type: 'dashed' },
            label: { formatter: `Cold-start gap: ${d.thresholdMinutes} min`, color: t.text, position: 'insideEndTop' },
            data: [{ xAxis: thresholdPosition(d.bins, d.thresholdMinutes) }],
          },
        },
      ],
    }
  }
}
