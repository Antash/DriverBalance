import type { DpfHealthSummary } from '../api/types'
import { fmtNum, fmtDate } from '../lib/format'
import { Sparkline } from './Sparkline'

/** Days since a long drive, a callout when one is due, the series as a sparkline and the worst stretches. */
export function DpfHealthStrip({ health }: { health: DpfHealthSummary }) {
  if (health.daysSince == null) {
    return <section className="card"><h2>DPF health</h2><p className="muted empty">No long drives recorded yet.</p></section>
  }
  return (
    <section className="card">
      <h2>DPF health</h2>
      <div className="dpf-strip">
        <div>
          <div className="kpi-value">{health.daysSince} <span className="kpi-unit">days</span></div>
          <div className="muted">since a long drive{health.lastLongDrive ? ` (${fmtDate(health.lastLongDrive)})` : ''}, {fmtNum(health.kmSince, 0)} km driven since</div>
        </div>
        <Sparkline values={health.series.map((p) => p.daysSinceLongDrive)} width={220} height={40}
          label="Days since a long drive" unit="days" />
      </div>
      {health.needsLongDrive && (
        <p className="callout" role="status">▲ Needs a long drive: it has been {health.daysSince} days. A longer motorway run lets the DPF regenerate.</p>
      )}
      {health.worstStretches.length > 0 && (
        <>
          <h3>Longest stretches without a long drive</h3>
          <ul className="plain">
            {health.worstStretches.map((s) => <li key={s.from}>{s.label}: {fmtDate(s.from)} to {fmtDate(s.to)}</li>)}
          </ul>
        </>
      )}
    </section>
  )
}
