import { iso } from '../lib/dates'
import { fmtDate } from '../lib/format'

interface AxisParam { axisValue?: number | string; marker?: string; seriesName?: string; value?: unknown }

/** Header date for a time-axis tooltip (ECharts hands over a local-midnight timestamp or a yyyy-MM-dd string). */
export function tooltipDate(axisValue: number | string | undefined): string {
  if (axisValue == null) return ''
  return fmtDate(typeof axisValue === 'number' ? iso(new Date(axisValue)) : axisValue)
}

/** trigger:'axis' tooltip formatter for a time axis that prints the date as dd/mm/yyyy. */
export function timeAxisTooltip(fmtValue: (v: number) => string) {
  return (params: unknown): string => {
    const list = (Array.isArray(params) ? params : [params]) as AxisParam[]
    if (list.length === 0) return ''
    const rows = list.map((p) => {
      const y = Array.isArray(p.value) ? p.value[1] : p.value
      return `${p.marker ?? ''} ${p.seriesName ?? ''}: <b>${y == null ? '—' : fmtValue(Number(y))}</b>`
    })
    return `${tooltipDate(list[0].axisValue)}<br/>${rows.join('<br/>')}`
  }
}
