import type { PeriodWithStats } from '../api/types'
import { compareMany } from '../lib/compare'
import { addDays } from '../lib/dates'
import { fmtNum } from '../lib/format'
import { GROUPS, formatMetric } from '../lib/metricFormat'
import { GroupRows } from './StatsTable'

function Heading({ p, inProgress }: { p: PeriodWithStats; inProgress: boolean }) {
  const { period, stats } = p
  const km = stats.usage.odometerKm ?? stats.usage.tripKm
  return (
    <th className="num" scope="col">
      <div>{period.from} → {addDays(period.to, -1)}</div>
      {period.startOdometer != null && <div className="th-sub">from {fmtNum(period.startOdometer, 0)} km</div>}
      <div className="th-sub">{stats.usage.days} days · {fmtNum(km, 0)} km</div>
      {inProgress && <div className="th-sub in-progress">in progress</div>}
    </th>
  )
}

/**
 * Every interval as its own column, oldest on the left. `intervals` arrive newest first and the first one is the
 * current, still-running interval. On metrics with a good direction the best cell is green and the worst red.
 */
export function MultiCompareTable({ intervals, currency }: { intervals: PeriodWithStats[]; currency: string }) {
  const ordered = [...intervals].reverse()
  const currentIndex = ordered.length - 1
  const rows = compareMany(ordered.map((p) => p.stats))
  const span = ordered.length + 1
  return (
    <div className="table-wrap">
      <p className="muted">Oldest on the left. Green is the best interval on a metric, red the worst; totals of the interval in progress are still growing.</p>
      <table className="data sticky-first">
        <thead>
          <tr>
            <th scope="col">Metric</th>
            {ordered.map((p, i) => <Heading key={p.period.from} p={p} inProgress={i === currentIndex} />)}
          </tr>
        </thead>
        <tbody>
          {GROUPS.map((g) => (
            <GroupRows key={g} group={g} span={span}>
              {rows.filter((r) => r.group === g).map((r) => (
                <tr key={r.key}>
                  <td>{r.label}</td>
                  {r.values.map((v, i) => {
                    const cls = r.best.includes(i) ? ' cell-best' : r.worst.includes(i) ? ' cell-worst' : ''
                    const note = cls === ' cell-best' ? 'best' : cls === ' cell-worst' ? 'worst' : null
                    return (
                      <td key={i} className={`num${cls}`}>
                        {formatMetric(r.key, v, currency)}
                        {note && <span className="sr-only"> ({note})</span>}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </GroupRows>
          ))}
        </tbody>
      </table>
    </div>
  )
}
