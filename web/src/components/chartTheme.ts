import type { DrivingScore } from '../api/types'

function css(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

/** Resolved from the CSS tokens, so charts follow light/dark. */
export function chartTheme() {
  return {
    text: css('--text'),
    textSecondary: css('--text-2'),
    muted: css('--muted'),
    grid: css('--grid'),
    axis: css('--axis'),
    surface: css('--surface'),
    series: [1, 2, 3, 4, 5, 6, 7, 8].map((i) => css(`--series-${i}`)),
  }
}

export function scoreColor(score: DrivingScore): string {
  return css(`--score-${score.toLowerCase()}`)
}

export function echartsBaseOption() {
  const t = chartTheme()
  return {
    backgroundColor: 'transparent',
    color: t.series,
    textStyle: { color: t.textSecondary, fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif' },
    grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
    tooltip: { trigger: 'axis', backgroundColor: t.surface, borderColor: t.grid, textStyle: { color: t.text } },
    legend: { textStyle: { color: t.textSecondary } },
  }
}

/** Axis defaults: recessive hairline grid, muted labels. */
export function axisStyle() {
  const t = chartTheme()
  return {
    axisLine: { lineStyle: { color: t.axis } },
    axisTick: { show: false },
    axisLabel: { color: t.muted },
    splitLine: { lineStyle: { color: t.grid } },
  }
}
