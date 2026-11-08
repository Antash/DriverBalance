import { useEffect, useRef } from 'react'
import * as echarts from 'echarts'
import { echartsBaseOption } from './chartTheme'

/** Thin wrapper over echarts: init on mount, setOption on change, resize on layout change, dispose on unmount. */
export function Chart({ option, height = 320 }: { option: echarts.EChartsOption; height?: number }) {
  const el = useRef<HTMLDivElement>(null)
  const chart = useRef<echarts.ECharts | null>(null)
  const latest = useRef(option)


  const apply = () => {
    chart.current?.setOption({ ...echartsBaseOption(), ...latest.current } as echarts.EChartsOption, true)
  }

  useEffect(() => {
    const c = echarts.init(el.current!)
    chart.current = c
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
