import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import { api } from '../api/client'
import type { Expense, LatestOdometer, TyreSeason, TyreSet, TyreSummaryResponse } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { ExpenseForm } from '../components/ExpenseForm'
import { Modal } from '../components/Modal'
import { errorLines, fmtKm, fmtMoney, fmtNum, invalidateVehicleData } from '../lib/format'
import { maintenanceHistory, type ServiceRow, type WorkItem } from '../lib/maintenance'
import { partLine, seasonLabel, tyreRows } from '../lib/tyreSummary'
import { useParams } from '../state/ParamsContext'
import { useTyreSets, useVehicle } from '../state/useVehicle'

const cost = (e: Expense) => fmtMoney(e.amount, e.currency)
const SEASONS: TyreSeason[] = ['Summer', 'Winter', 'AllSeason']

/** One lane per maintenance type, a marker each time it was done. */
function timelineOption(services: ServiceRow<Expense>[], lanes: string[]): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const pad = 20 * 86_400_000
    return {
      legend: { show: false },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const s = (p as { data: { service: ServiceRow<Expense> } }).data.service
          const odo = s.odometer != null ? `<br/>${fmtNum(s.odometer, 0)} km` : ''
          return `${s.date}${odo}<br/>${s.work.map((w) => w.label).join(', ')}<br/>${cost(s.expense)}`
        },
      },
      grid: { left: 8, right: 24, top: 16, bottom: 8, containLabel: true },
      xAxis: { type: 'time', min: (v: { min: number }) => v.min - pad, max: (v: { max: number }) => v.max + pad, ...axisStyle() },
      yAxis: { type: 'category', data: lanes, inverse: true, ...axisStyle(), splitLine: { show: true, lineStyle: { color: t.grid } } },
      series: lanes.map((lane, i) => ({
        name: lane, type: 'scatter' as const, symbol: 'diamond', symbolSize: 14,
        itemStyle: { color: t.series[i % t.series.length], borderColor: t.surface, borderWidth: 1.5 },
        data: services.filter((s) => s.work.some((w) => w.label.toLowerCase() === lane.toLowerCase()))
          .map((s) => ({ value: [s.date, lane], service: s })),
      })),
    } as EChartsOption
  }
}

function Since({ w }: { w: WorkItem }) {
  if (w.sinceDays == null) return <span className="muted">first recorded</span>
  return <span>{fmtNum(w.sinceDays, 0)} days{w.sinceKm != null ? ` · ${fmtNum(w.sinceKm, 0)} km` : ''}</span>
}

function TyreSetEditor({ vehicleId, set, onDone }: { vehicleId: number; set: TyreSet; onDone(): void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    brand: set.brand ?? '', model: set.model ?? '', size: set.size ?? '', season: set.season, retired: set.retired,
  })
  const save = useMutation({
    mutationFn: () => api.put<TyreSet>(`/vehicles/${vehicleId}/tyres/${set.id}`, {
      ...set, brand: f.brand.trim() || null, model: f.model.trim() || null, size: f.size.trim() || null,
      season: f.season, retired: f.retired,
    }),
    onSuccess: async () => { await invalidateVehicleData(qc, vehicleId); onDone() },
  })
  return (
    <form className="form-grid" onSubmit={(e) => { e.preventDefault(); save.mutate() }}>
      <label>Brand<input value={f.brand} onChange={(e) => setF({ ...f, brand: e.target.value })} /></label>
      <label>Model<input value={f.model} onChange={(e) => setF({ ...f, model: e.target.value })} /></label>
      <label>Size<input value={f.size} onChange={(e) => setF({ ...f, size: e.target.value })} /></label>
      <label>Season
        <select value={f.season} onChange={(e) => setF({ ...f, season: e.target.value as TyreSeason })}>
          {SEASONS.map((s) => <option key={s} value={s}>{seasonLabel(s)}</option>)}
        </select>
      </label>
      <label className="check wide"><input type="checkbox" checked={f.retired} onChange={(e) => setF({ ...f, retired: e.target.checked })} /> Retired</label>
      {save.isError && <ul className="errors wide">{errorLines(save.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      <div className="wide row">
        <button className="btn primary" type="submit" disabled={save.isPending}>Save</button>
        <button className="btn" type="button" onClick={onDone}>Cancel</button>
      </div>
    </form>
  )
}

export default function MaintenancePage() {
  const { vehicleId } = useParams()
  const vehicle = useVehicle()
  const qc = useQueryClient()
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Expense | null>(null)
  const [editingSet, setEditingSet] = useState<TyreSet | null>(null)

  const { data: expenses, isLoading, isError } = useQuery({
    queryKey: ['expenses', vehicleId, 'maintenance'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?category=Maintenance`),
  })
  const { data: latest } = useQuery({
    queryKey: ['expenses', vehicleId, 'latest-odometer'],
    queryFn: async () => (await api.get<LatestOdometer | undefined>(`/vehicles/${vehicleId}/odometer/latest`)) ?? null,
  })
  const { data: tyreSets = [] } = useTyreSets()
  const { data: tyreSummary } = useQuery({
    queryKey: ['tyres', vehicleId, 'summary'],
    queryFn: () => api.get<TyreSummaryResponse>(`/vehicles/${vehicleId}/tyres/summary`),
  })
  const remove = useMutation({
    mutationFn: (e: Expense) => api.del<void>(`/vehicles/${vehicleId}/expenses/${e.id}`),
    onSuccess: async () => { await invalidateVehicleData(qc, vehicleId); setEditing(null) },
  })

  const { services, lanes } = useMemo(() => maintenanceHistory(expenses ?? []), [expenses])
  const opt = useMemo(() => (services.length ? timelineOption(services, lanes) : undefined), [services, lanes])
  const rows = useMemo(() => tyreRows(tyreSummary?.summaries ?? []), [tyreSummary])
  const empty = !!expenses && services.length === 0
  const emptyText = 'No maintenance recorded yet. Use Add maintenance to log a service.'

  return (
    <section>
      <h1>Maintenance</h1>
      <p className="muted">Every service visit. Oil changes also have their own page, Oil intervals, which compares engine stats between changes.</p>
      <div className="toolbar">
        <span className="spacer" />
        <button className="btn primary" onClick={() => setAdding(true)}>Add maintenance</button>
      </div>
      <ChartCard title="Maintenance timeline" loading={isLoading} error={isError} empty={empty} emptyText={emptyText}
        hint="One lane per kind of work; hover a marker for the date, odometer and cost.">
        {opt && <Chart option={opt} height={Math.max(140, 48 + lanes.length * 44)} />}
      </ChartCard>

      {services.length > 0 && (
        <section className="card">
          <h2>Services</h2>
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr><th>Date</th><th className="num">Odometer</th><th>Work done</th><th>Since last time</th><th className="num">Cost</th><th>Note</th></tr>
              </thead>
              <tbody>
                {services.map((s) => {
                  const open = () => { remove.reset(); setEditing(s.expense) }
                  const legacy = s.expense.work.length === 0 ? s.expense.maintenanceDescription : null
                  return (
                    <tr key={s.id} className="clickable" tabIndex={0} onClick={open}
                      onKeyDown={(e) => { if (e.key === 'Enter') open() }}>
                      <td>{s.date}</td>
                      <td className="num">{s.odometer != null ? `${fmtNum(s.odometer, 0)} km` : '—'}</td>
                      <td>{s.work.map((w, i) => {
                        const part = partLine(w)
                        return (
                          <div key={`${w.key}-${i}`}>
                            {w.label}
                            {part && <span className="muted"> · {part}</span>}
                            {w.details && <span className="muted"> · {w.details}</span>}
                          </div>
                        )
                      })}</td>
                      <td>{s.work.map((w, i) => <div key={`${w.key}-${i}`}><Since w={w} /></div>)}</td>
                      <td className="num">{cost(s.expense)}</td>
                      <td>{s.expense.note ?? ''}{legacy && <span className="muted">{s.expense.note ? ' · ' : ''}{legacy}</span>}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <p className="muted">The cost is the whole visit.</p>
        </section>
      )}

      <section className="card">
          <h2>Tyres</h2>
          {rows.length === 0 && !tyreSummary?.unassignedSpans.length && (
            <p className="muted">No tyre sets yet. Tick Tyre swap in a maintenance record to record which tyres were fitted.</p>
          )}
          {rows.length > 0 && <div className="table-wrap">
            <table className="data">
              <thead>
                <tr><th>Tyres</th><th>Size</th><th>Season</th><th>Status</th><th className="num">Km</th><th>Fitted since</th></tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.set.id} className="clickable" tabIndex={0} onClick={() => setEditingSet(r.set)}
                    onKeyDown={(e) => { if (e.key === 'Enter') setEditingSet(r.set) }}>
                    <td>{r.name}</td>
                    <td>{r.size ?? '—'}</td>
                    <td>{seasonLabel(r.season)}</td>
                    <td>{r.status}</td>
                    <td className="num">{fmtKm(r.km)}</td>
                    <td>{r.fittedSince ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>}
          {tyreSummary?.unassignedSpans.map((sp, i) => (
            <p key={`${sp.from}-${sp.to}-${i}`} className="muted">
              Unrecorded tyres: {sp.from} → {sp.to ?? 'now'}{sp.km != null ? `, ${fmtNum(sp.km, 0)} km` : ''}
            </p>
          ))}
        </section>

      {adding && (
        <Modal title="Add maintenance" onClose={() => setAdding(false)}>
          <ExpenseForm vehicle={vehicle} tyreSets={tyreSets} lockCategory="Maintenance" defaultOdometer={latest?.odometer}
            onDone={() => setAdding(false)} />
        </Modal>
      )}
      {editing && (
        <Modal title="Edit maintenance" onClose={() => setEditing(null)}>
          <ExpenseForm key={editing.id} vehicle={vehicle} tyreSets={tyreSets} initial={editing} lockCategory="Maintenance"
            onDone={() => setEditing(null)} deleting={remove.isPending}
            onDelete={() => { if (window.confirm(`Delete this maintenance record from ${editing.date}?`)) remove.mutate(editing) }} />
          {remove.isError && <ul className="errors">{errorLines(remove.error).map((m) => <li key={m}>{m}</li>)}</ul>}
        </Modal>
      )}
      {editingSet && (
        <Modal title="Edit tyre set" onClose={() => setEditingSet(null)}>
          <TyreSetEditor vehicleId={vehicleId} set={editingSet} onDone={() => setEditingSet(null)} />
        </Modal>
      )}
    </section>
  )
}
