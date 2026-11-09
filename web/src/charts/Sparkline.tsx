import { fmtNum } from '../lib/format'


/** Inline SVG trend line; gaps (null) are skipped. */
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

