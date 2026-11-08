import type { PeriodStats } from '../api/types'
import { compareStats } from '../lib/compare'
import { GROUPS, formatMetric } from '../lib/metricFormat'

/** One period's metrics, grouped under Usage / Engine wear & DPF / Fuel / Money. */
export function StatsTable({ stats, currency }: { stats: PeriodStats; currency: string }) {
  const rows = compareStats(stats, stats)
  return (
    <div className="table-wrap">
      <table className="data">
        <tbody>
          {GROUPS.map((g) => (
            <GroupRows key={g} group={g}>
              {rows.filter((r) => r.group === g).map((r) => (
                <tr key={r.key}><td>{r.label}</td><td className="num">{formatMetric(r.key, r.a, currency)}</td></tr>
              ))}
            </GroupRows>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function GroupRows({ group, span = 2, children }: { group: string; span?: number; children: React.ReactNode }) {
  return (
    <>
      <tr className="group-row"><th colSpan={span}>{group}</th></tr>
      {children}
    </>
  )
}
