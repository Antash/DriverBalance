import { useEffect, useRef } from 'react'
import * as echarts from 'echarts'
import { echartsBaseOption } from './chartTheme'

/** Thin wrapper over echarts: init on mount, setOption on change, resize on layout change, dispose on unmount. */
export function Chart({ option, height = 320, onPointClick }: {
  /** A function is re-evaluated on every theme change so token colours follow light/dark. */
  option: echarts.EChartsOption | (() => echarts.EChartsOption)
  height?: number
  /** Called with the clicked data item (series data entry) for series that carry per-point objects. */
  onPointClick?(data: unknown): void
}) {
  const clickRef = useRef(onPointClick)
  useEffect(() => { clickRef.current = onPointClick })
  const el = useRef<HTMLDivElement>(null)
  const chart = useRef<echarts.ECharts | null>(null)
  const latest = useRef(option)


  const apply = () => {
    const o = typeof latest.current === 'function' ? latest.current() : latest.current
    chart.current?.setOption({ ...echartsBaseOption(), ...o } as echarts.EChartsOption, true)
  }

  useEffect(() => {
    const c = echarts.init(el.current!)
    chart.current = c
    c.on('click', (p) => clickRef.current?.((p as { data?: unknown }).data))
    apply()
    const ro = new ResizeObserver(() => c.resize())
    ro.observe(el.current!)
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    mq.addEventListener('change', apply)
    return () => {
      mq.removeEventListener('change', apply)
      ro.disconnect()
      c.dispose()
      chart.current = null
    }
  }, [])

  useEffect(() => {
    latest.current = option
    apply()
  }, [option])

  return <div ref={el} className="chart" style={{ height }} />
}
