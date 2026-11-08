import type { RouteStat } from '../api/types'
import { ChartCard } from '../components/ChartCard'
import { fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { rangeBody, type RangeProps } from './shared'

/** Inline SVG trend of a route's consumption; gaps (null) are skipped. */
export function Sparkline({ values, width = 90, height = 24, label, unit = 'L/100' }: {
  values: (number | null)[]; width?: number; height?: number; label?: string; unit?: string
}) {
  const pts = values.map((v, i) => ({ v, i })).filter((p): p is { v: number; i: number } => p.v != null)
  if (pts.length < 2) return <span className="muted">—</span>
  const min = Math.min(...pts.map((p) => p.v)), max = Math.max(...pts.map((p) => p.v))
  const span = max - min || 1
  const x = (i: number) => 2 + (i / (values.length - 1)) * (width - 4)
  const y = (v: number) => height - 3 - ((v - min) / span) * (height - 6)
  return (
    <svg width={width} height={height} role="img" aria-label={`${label ?? 'Consumption trend'}, ${fmtNum(min, 1)} to ${fmtNum(max, 1)} ${unit}`}
      className="sparkline">
      <polyline fill="none" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round"
        points={pts.map((p) => `${x(p.i).toFixed(1)},${y(p.v).toFixed(1)}`).join(' ')} />
    </svg>
  )
}

/** Most common routes with a per-route consumption sparkline. */
export function Routes(range: RangeProps) {
  const { data, isLoading, isError } = useAnalytics<RouteStat[]>('charts/routes', rangeBody(range))
  return (
    <ChartCard title="Routes" loading={isLoading && !data} error={isError} empty={!!data && data.length === 0}>
      <div className="table-wrap">
        <table className="data">
          <thead>
            <tr><th>From</th><th>To</th><th className="num">Drives</th><th className="num">Km</th><th className="num">L/100</th><th>Typical score</th><th>Trend</th></tr>
          </thead>
          <tbody>
            {data?.map((r) => (
              <tr key={`${r.from}→${r.to}`}>
                <td>{r.from}</td><td>{r.to}</td>
                <td className="num">{r.count}</td>
                <td className="num">{fmtNum(r.km, 0)}</td>
                <td className="num">{fmtNum(r.avgLPer100, 1)}</td>
                <td><span className={`score score-${r.typicalScore}`}>{r.typicalScore}</span></td>
                <td><Sparkline values={r.trend.map((p) => p.lPer100)} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ChartCard>
  )
}
