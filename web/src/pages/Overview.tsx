import { useState } from 'react'
import { FUEL_TYPES } from '../lib/fuelTypes'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import { DpfHealthStrip } from '../charts/DpfHealthStrip'
import { VehicleTimeline } from '../charts/VehicleTimeline'
import { ChartCard } from '../components/ChartCard'
import { ExpenseForm } from '../components/ExpenseForm'
import { RecentTrips } from '../components/RecentTrips'
import { Modal } from '../components/Modal'
import { Tile } from '../components/Tile'
import type { OverviewData, Ownership, PeriodStats } from '../api/types'
import { addDays, addYears, iso } from '../lib/dates'
import { fmtLPer100, fmtMoney, fmtNum, fmtPct, fmtDate } from '../lib/format'
import { ownershipRows } from '../lib/ownership'
import { useAnalytics } from '../state/ParamsContext'
import { useTyreSets, useVehicle } from '../state/useVehicle'

function Kpis({ title, stats, currency, hasDpf }: { title: string; stats: PeriodStats; currency: string; hasDpf: boolean }) {
  return (
    <section className="card">
      <h2>{title}</h2>
      <div className="kpis">
        <Tile name="Distance" value={`${fmtNum(stats.usage.tripKm, 0)} km`} />
        <Tile name="Cost" value={fmtMoney(stats.money.total, currency)} />
        <Tile name="Cost per km" value={fmtMoney(stats.money.costPerKm, currency)} />
        {hasDpf && <Tile name="DPF index" value={fmtPct(stats.scores.dpfIndex)} />}
        <Tile name="Fuel" value={fmtLPer100(stats.fuel.tripLPer100)} />
      </div>
    </section>
  )
}

function OwnershipCard({ currency }: { currency: string }) {
  const vehicle = useVehicle()
  const { data, isError } = useQuery({
    queryKey: ['analytics', vehicle.id, 'ownership'],
    queryFn: () => api.get<Ownership>(`/vehicles/${vehicle.id}/ownership`),
  })
  const rows = data ? ownershipRows(data, currency) : null
  return (
    <section className="card">
      <h2>Cost of ownership</h2>
      {isError && <p className="field-error">Could not load the cost of ownership.</p>}
      {data && !rows && <p className="muted">Set the purchase date in Settings to see the cost of ownership.</p>}
      {data && rows && (
        <>
          <table className="data">
            <tbody>
              {rows.map((r) => (
                <tr key={r.label}>
                  <td>{r.strong ? <strong>{r.label}</strong> : r.label}</td>
                  <td className="num">
                    {r.strong ? <strong>{r.value}</strong> : r.value}
                    {r.note && r.label === 'Financing' && <div className="th-sub muted">{r.note}</div>}
                    {r.note && r.label !== 'Financing' && <span className="muted"> ({r.note})</span>}
                    {r.label === 'Purchase costs' && data.purchaseCosts.length > 0 && (
                      <div className="th-sub muted">
                        {data.purchaseCosts.map((c, i) => <div key={i}>{fmtDate(c.date)} · {fmtMoney(c.amount, currency)}{c.note ? ` · ${c.note}` : ''}</div>)}
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="muted">Resale value is your estimate; net figures are estimates.</p>
        </>
      )}
    </section>
  )
}

/** What the car is, from Settings → Car details; hidden until something is filled in. */
function CarCard() {
  const v = useVehicle()
  const d = v.details
  if (!d || (!d.model && !d.engine && !d.trim && !d.modelYear && d.equipment.length === 0)) return null
  const title = [d.model ?? v.name, d.engine, d.trim].filter(Boolean).join(' ')
  const fuel = FUEL_TYPES.find(([key]) => key === v.fuelType)?.[1].replace(/ \(.*\)$/, '') ?? null
  const sub = [d.modelYear ? `MY${d.modelYear}` : null, fuel, d.transmission, d.awd ? 'AWD' : null, d.exteriorColour, d.interior]
    .filter(Boolean).join(' · ')
  return (
    <section className="card car-card">
      <h2>{title}</h2>
      {sub && <p className="muted">{sub}</p>}
      {d.equipment.length > 0 && <ul className="tags">{d.equipment.map((e) => <li key={e}>{e}</li>)}</ul>}
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
      <CarCard />
      {isError && <p className="field-error">Could not load the overview.</p>}
      {isLoading && !data && <p className="muted">Loading…</p>}
      {data && (
        <>
          <div className="chart-grid">
            <Kpis title="This month" stats={data.thisMonth} currency={cur} hasDpf={vehicle.hasDpf} />
            <Kpis title="This year" stats={data.thisYear} currency={cur} hasDpf={vehicle.hasDpf} />
          </div>

          <RecentTrips />

          <VehicleTimeline from={addYears(today, -1)} to={addDays(today, 1)} title="Last 12 months" />

          {vehicle.hasDpf && data.dpfHealth && <DpfHealthStrip health={data.dpfHealth} />}

          {data.reminders.length > 0 && (
            <ChartCard title="Reminders">
              <ul className="plain">
                {data.reminders.map((r, i) => (
                  <li key={`${r.kind}-${i}`}><span aria-hidden>{SEVERITY_ICON[r.severity] ?? '•'}</span> <span className="sr-only">{r.severity}: </span>{r.message}</li>
                ))}
              </ul>
            </ChartCard>
          )}

          <OwnershipCard currency={cur} />
        </>
      )}

      {adding && (
        <Modal title="Add expense" onClose={() => setAdding(false)}>
          <ExpenseForm vehicle={vehicle} tyreSets={tyreSets} onDone={() => setAdding(false)} />
        </Modal>
      )}
    </section>
  )
}
