import { useState } from 'react'
import { DpfHealthStrip } from '../charts/DpfHealthStrip'
import { VehicleTimeline } from '../charts/VehicleTimeline'
import { ChartCard } from '../components/ChartCard'
import { ExpenseForm, label } from '../components/ExpenseForm'
import { Modal } from '../components/Modal'
import type { OverviewData, PeriodStats } from '../api/types'
import { addDays, addYears, iso } from '../lib/dates'
import { fmtLPer100, fmtMoney, fmtNum, fmtPct } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import { useTyreSets, useVehicle } from '../state/useVehicle'

function Tile({ name, value }: { name: string; value: string }) {
  return (
    <div className="kpi">
      <div className="kpi-name">{name}</div>
      <div className="kpi-value">{value}</div>
    </div>
  )
}

function Kpis({ title, stats, currency }: { title: string; stats: PeriodStats; currency: string }) {
  return (
    <section className="card">
      <h2>{title}</h2>
      <div className="kpis">
        <Tile name="Distance" value={`${fmtNum(stats.usage.tripKm, 0)} km`} />
        <Tile name="Cost" value={fmtMoney(stats.money.total, currency)} />
        <Tile name="Cost per km" value={fmtMoney(stats.money.costPerKm, currency)} />
        <Tile name="DPF index" value={fmtPct(stats.scores.dpfIndex)} />
        <Tile name="Fuel" value={fmtLPer100(stats.fuel.tripLPer100)} />
      </div>
    </section>
  )
}

const SEVERITY_ICON: Record<string, string> = { alarm: '⚠', warn: '▲', info: 'ℹ' }

export default function OverviewPage() {
  const vehicle = useVehicle()
  const { data: tyreSets = [] } = useTyreSets()
  const { data, isLoading, isError } = useAnalytics<OverviewData>('overview')
  const [adding, setAdding] = useState(false)
  const [today] = useState(() => iso(new Date()))
  const cur = vehicle.homeCurrency

  return (
    <section>
      <div className="card-head">
        <h1>Overview</h1>
        <button className="btn primary" onClick={() => setAdding(true)}>Add expense</button>
      </div>
      {isError && <p className="field-error">Could not load the overview.</p>}
      {isLoading && !data && <p className="muted">Loading…</p>}
      {data && (
        <>
          <div className="chart-grid">
            <Kpis title="This month" stats={data.thisMonth} currency={cur} />
            <Kpis title="This year" stats={data.thisYear} currency={cur} />
          </div>

          <DpfHealthStrip health={data.dpfHealth} />

          <ChartCard title="Reminders" empty={data.reminders.length === 0} emptyText="Nothing needs attention.">
            <ul className="plain">
              {data.reminders.map((r, i) => (
                <li key={`${r.kind}-${i}`}><span aria-hidden>{SEVERITY_ICON[r.severity] ?? '•'}</span> <span className="sr-only">{r.severity}: </span>{r.message}</li>
              ))}
            </ul>
          </ChartCard>

          <ChartCard title="Recent expenses" empty={data.recentExpenses.length === 0} emptyText="No expenses yet.">
            <div className="table-wrap">
              <table className="data">
                <thead><tr><th>Date</th><th>Category</th><th className="num">Amount</th><th>Note</th></tr></thead>
                <tbody>
                  {data.recentExpenses.map((e) => (
                    <tr key={e.id}>
                      <td>{e.date}</td><td>{label(e.category)}</td>
                      <td className="num">{fmtMoney(e.amount, e.currency)}</td><td>{e.note ?? ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </ChartCard>
        </>
      )}

      <VehicleTimeline from={addYears(today, -1)} to={addDays(today, 1)} title="Last 12 months" />

      {adding && (
        <Modal title="Add expense" onClose={() => setAdding(false)}>
          <ExpenseForm vehicle={vehicle} tyreSets={tyreSets} onDone={() => setAdding(false)} />
        </Modal>
      )}
    </section>
  )
}
