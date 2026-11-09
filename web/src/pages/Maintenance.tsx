import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { VehicleTimeline } from '../charts/VehicleTimeline'
import type { Expense, LatestOdometer, TyreSeason, TyreSet, TyreSummaryResponse } from '../api/types'
import { ChartCard } from '../components/ChartCard'
import { Clip } from '../components/Clip'
import { ExpenseForm } from '../components/ExpenseForm'
import { Modal } from '../components/Modal'
import { addDays, iso } from '../lib/dates'
import { errorLines, fmtKm, fmtMoney, fmtNum, invalidateVehicleData } from '../lib/format'
import { maintenanceHistory, type WorkItem } from '../lib/maintenance'
import { derivedTyreName, partLine, seasonLabel, tyreDisplayName, tyreRows } from '../lib/tyreSummary'
import { useParams } from '../state/ParamsContext'
import { isPreviousOwner } from '../lib/ownership'
import { useTyreSets, useVehicle } from '../state/useVehicle'

const cost = (e: Expense) => fmtMoney(e.amount, e.currency)
const SEASONS: TyreSeason[] = ['Summer', 'Winter', 'AllSeason']
/** Earlier than any data, so the milestone chart covers the whole history. */
const ALL_TIME_FROM = '2000-01-01'

function Since({ w }: { w: WorkItem }) {
  if (w.sinceDays == null) return <span className="muted">first recorded</span>
  return <span>{fmtNum(w.sinceDays, 0)} days{w.sinceKm != null ? ` · ${fmtNum(w.sinceKm, 0)} km` : ''}</span>
}

/** Edits a tyre set, or adds one when `set` is null (so it can be picked as the fitted tyres on a tyre swap). */
function TyreSetEditor({ vehicleId, set, onDone }: { vehicleId: number; set: TyreSet | null; onDone(): void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    brand: set?.brand ?? '', model: set?.model ?? '', size: set?.size ?? '', season: set?.season ?? ('Summer' as TyreSeason), retired: set?.retired ?? false,
  })
  // A name that was derived from the previous brand/model/season is re-derived by the server (name: '').
  const nameWasDerived = !set || set.name === derivedTyreName(set)
  const save = useMutation({
    mutationFn: () => {
      const fields = { brand: f.brand.trim() || null, model: f.model.trim() || null, size: f.size.trim() || null, season: f.season, retired: f.retired }
      return set
        ? api.put<TyreSet>(`/vehicles/${vehicleId}/tyres/${set.id}`, { ...set, ...fields, name: nameWasDerived ? '' : set.name })
        : api.post<TyreSet>(`/vehicles/${vehicleId}/tyres`, { ...fields, name: '', description: null, purchaseDate: null })
    },
    onSuccess: async () => { await invalidateVehicleData(qc, vehicleId); onDone() },
  })
  const remove = useMutation({
    mutationFn: () => api.del<void>(`/vehicles/${vehicleId}/tyres/${set!.id}`),
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
      {set && <label className="check wide"><input type="checkbox" checked={f.retired} onChange={(e) => setF({ ...f, retired: e.target.checked })} /> Retired</label>}
      {save.isError && <ul className="errors wide">{errorLines(save.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      {remove.isError && <ul className="errors wide">{errorLines(remove.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      <div className="wide row">
        <button className="btn primary" type="submit" disabled={save.isPending}>{set ? 'Save' : 'Add tyre set'}</button>
        <button className="btn" type="button" onClick={onDone}>Cancel</button>
        {set && <button className="btn danger" type="button" disabled={remove.isPending}
          onClick={() => { if (window.confirm(`Delete tyre set ${tyreDisplayName(set)}?`)) remove.mutate() }}>Delete</button>}
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
  const [addingSet, setAddingSet] = useState(false)

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

  const { services } = useMemo(() => maintenanceHistory(expenses ?? []), [expenses])
  const [today] = useState(() => iso(new Date()))
  const rows = useMemo(() => tyreRows(tyreSummary?.summaries ?? []), [tyreSummary])
  const fittedName = (id: number | null) => {
    const t = id == null ? undefined : tyreSets.find((x) => x.id === id)
    return t ? tyreDisplayName(t) : 'set not recorded'
  }
  const empty = !!expenses && services.length === 0
  const emptyText = 'No maintenance recorded yet. Use Add maintenance to log a service.'

  return (
    <section>
      <h1>Maintenance</h1>
      <div className="toolbar">
        <span className="spacer" />
        <button className="btn primary" onClick={() => setAdding(true)}>Add maintenance</button>
      </div>
      {services.length > 0
        ? <VehicleTimeline from={ALL_TIME_FROM} to={addDays(today, 1)} title="Service milestones" />
        : <ChartCard title="Service milestones" loading={isLoading} error={isError} empty={empty} emptyText={emptyText}>{null}</ChartCard>}

      {services.length > 0 && (
        <section className="card">
          <h2>Services</h2>
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr><th>Date</th><th className="num">Odometer</th><th>Work done</th><th>Since last time</th><th className="num">Cost</th><th>Where</th><th>Note</th></tr>
              </thead>
              <tbody>
                {services.map((s) => {
                  const open = () => { remove.reset(); setEditing(s.expense) }
                  const legacy = s.expense.work.length === 0 ? s.expense.maintenanceDescription : null
                  return (
                    <tr key={s.id} className="clickable" tabIndex={0} onClick={open}
                      onKeyDown={(e) => { if (e.key === 'Enter') open() }}>
                      <td>{s.date}{isPreviousOwner(s.date, vehicle.purchaseDate) && <span className="muted"> previous owner</span>}<Clip n={s.expense.documents.length} /></td>
                      <td className="num">{s.odometer != null ? `${fmtNum(s.odometer, 0)} km` : '—'}</td>
                      <td>{s.work.map((w, i) => {
                        const part = partLine(w)
                        return (
                          <div key={`${w.key}-${i}`}>
                            {w.label}
                            {w.position && <span className="muted"> · {w.position === 'Both' ? 'front and rear' : w.position.toLowerCase()}</span>}
                            {w.rubberOnly && <span className="muted"> · rubber only</span>}
                            {(w.type === 'TyreSwap' || w.type === 'NewTyres') && (
                              <span className="muted"> · {fittedName(s.expense.tyreSetId)}</span>
                            )}
                            {part && <span className="muted"> · {part}</span>}
                            {w.details && <span className="muted"> · {w.details}</span>}
                          </div>
                        )
                      })}</td>
                      <td>{s.work.map((w, i) => <div key={`${w.key}-${i}`}><Since w={w} /></div>)}</td>
                      <td className="num">{cost(s.expense)}</td>
                      <td>{s.expense.place ?? ''}</td>
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
          <div className="card-head">
            <h2>Tyres</h2>
            <button className="btn" onClick={() => setAddingSet(true)}>Add tyre set</button>
          </div>
          <p className="muted hint">Add the tyre sets you own, then pick them under Tyre swap in a maintenance record.</p>
          {rows.length === 0 && !tyreSummary?.unassignedSpans.length && (
            <p className="muted">No tyre sets yet.</p>
          )}
          {rows.length > 0 && <div className="table-wrap">
            <table className="data">
              <thead>
                <tr><th>Tyres</th><th>Size</th><th>Season</th><th>Status</th><th className="num">Km</th><th>First fitted</th></tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.set.id} className="clickable" tabIndex={0} onClick={() => setEditingSet(r.set)}
                    onKeyDown={(e) => { if (e.key === 'Enter') setEditingSet(r.set) }}>
                    <td>{r.name}</td>
                    <td>{r.size ?? '—'}</td>
                    <td>{seasonLabel(r.season)}</td>
                    <td>{r.status}{r.fittedSince ? ` since ${r.fittedSince}` : ''}</td>
                    <td className="num">{fmtKm(r.km)}</td>
                    <td>{r.firstFitted ?? '—'}</td>
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
            onDelete={() => { if (window.confirm(`Delete this maintenance record from ${editing.date}${editing.documents.length > 0 ? ` and its ${editing.documents.length} document(s)` : ''}?`)) remove.mutate(editing) }} />
          {remove.isError && <ul className="errors">{errorLines(remove.error).map((m) => <li key={m}>{m}</li>)}</ul>}
        </Modal>
      )}
      {addingSet && (
        <Modal title="Add tyre set" onClose={() => setAddingSet(false)}>
          <TyreSetEditor vehicleId={vehicleId} set={null} onDone={() => setAddingSet(false)} />
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
