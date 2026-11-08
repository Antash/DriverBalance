import type { PeriodStats } from '../api/types'
import { compareStats, type MetricRow } from '../lib/compare'
import { GROUPS, formatMetric } from '../lib/metricFormat'
import { GroupRows } from './StatsTable'

function Change({ row }: { row: MetricRow }) {
  if (row.change == null) return <span className="muted">—</span>
  const pct = `${row.change > 0 ? '+' : ''}${(row.change * 100).toFixed(1)}%`
  if (row.change === 0 || row.better == null) return <span>{pct}</span>
  const improved = (row.change > 0) === (row.better === 'up')
  return (
    <span className={improved ? 'delta good' : 'delta bad'}>
      {row.change > 0 ? '▲' : '▼'} {pct} <span className="delta-word">{improved ? 'better' : 'worse'}</span>
    </span>
  )
}

/** Baseline period A vs newer period B. Change is (B - A) / A; "better" follows each metric's good direction. */
export function CompareTable({ a, b, currency, labelA, labelB }: {
  a: PeriodStats; b: PeriodStats; currency: string; labelA: string; labelB: string
}) {
  const rows = compareStats(a, b)
  return (
    <div className="table-wrap">
      {a.usage.days !== b.usage.days && (
        <p className="muted">The periods differ in length ({a.usage.days} vs {b.usage.days} days), so totals show no change; rates do.</p>
      )}
      <table className="data">
        <thead>
          <tr><th>Metric</th><th className="num">{labelA}</th><th className="num">{labelB}</th><th className="num">Change</th></tr>
        </thead>
        <tbody>
          {GROUPS.map((g) => (
            <GroupRows key={g} group={g} span={4}>
              {rows.filter((r) => r.group === g).map((r) => (
                <tr key={r.key}>
                  <td>{r.label}</td>
                  <td className="num">{formatMetric(r.key, r.a, currency)}</td>
                  <td className="num">{formatMetric(r.key, r.b, currency)}</td>
                  <td className="num"><Change row={r} /></td>
                </tr>
              ))}
            </GroupRows>
          ))}
        </tbody>
      </table>
    </div>
  )
}
