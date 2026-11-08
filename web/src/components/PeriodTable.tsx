import type { PeriodWithStats } from '../api/types'
import { fmtMoney, fmtNum, fmtPct } from '../lib/format'

/** One row per period: km, cold starts, DPF, L/100 and cost. Rows are clickable when onSelect is given. */
export function PeriodTable({ rows, currency, onSelect, selected }: {
  rows: PeriodWithStats[]
  currency: string
  onSelect?(p: PeriodWithStats): void
  selected?: string | null
}) {
  return (
    <div className="table-wrap">
      <table className="data">
        <thead>
          <tr>
            <th>Period</th><th className="num">Km</th><th className="num">Cold starts</th><th className="num">DPF</th>
            <th className="num">L/100</th><th className="num">Cost</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => {
            const s = r.stats
            const open = onSelect ? () => onSelect(r) : undefined
            return (
              <tr key={r.period.from} className={onSelect ? 'clickable' : undefined} aria-selected={selected === r.period.from}
                onClick={open}>
                <td>
                  {onSelect
                    ? <button className="link" onClick={(e) => { e.stopPropagation(); open?.() }}>{r.period.label}</button>
                    : r.period.label}
                </td>
                <td className="num">{fmtNum(s.usage.tripKm, 0)}</td>
                <td className="num">{s.usage.coldStarts}</td>
                <td className="num">{fmtPct(s.scores.dpfIndex)}</td>
                <td className="num">{fmtNum(s.fuel.tripLPer100, 1)}</td>
                <td className="num">{s.money.total ? fmtMoney(s.money.total, currency) : '—'}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
