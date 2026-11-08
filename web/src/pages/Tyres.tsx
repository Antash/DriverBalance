import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import { api, ApiError } from '../api/client'
import type { Expense, TyreSeason, TyreSet, TyreSummaryResponse, Vehicle } from '../api/types'
import { Chart } from '../components/Chart'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { TyreSetSelect, label } from '../components/ExpenseForm'
import { Modal } from '../components/Modal'
import { buildExpensePayload, toFormState, usesTyreSet } from '../lib/expenseForm'
import { errorLines, fmtKm, fmtMoney, invalidateVehicleData } from '../lib/format'
import { useParams } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

const SEASONS: TyreSeason[] = ['Summer', 'Winter', 'AllSeason']

function kmOption(data: TyreSummaryResponse): EChartsOption {
  const t = chartTheme()
  const spans = data.summaries.flatMap((s) => s.spans.map((sp) => ({ set: s.set, ...sp })))
    .sort((a, b) => a.from.localeCompare(b.from))
  const sets = data.summaries.map((s) => s.set)
  return {
    legend: { top: 0, show: sets.length > 1 },
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v) => fmtKm(Number(v)) } as EChartsOption['tooltip'],
    grid: { left: 8, right: 16, top: 40, bottom: 8, containLabel: true },
    xAxis: { type: 'category', data: spans.map((sp) => sp.from), ...axisStyle() },
    yAxis: { type: 'value', name: 'km', nameTextStyle: { color: t.muted }, ...axisStyle() },
    series: sets.map((s, i) => ({
      name: s.name, type: 'bar', stack: 'km', barMaxWidth: 36,
      itemStyle: { color: t.series[i] ?? t.muted, borderColor: t.surface, borderWidth: 2 },
      data: spans.map((sp) => (sp.set.id === s.id ? sp.km : null)),
    })),
  }
}

function SetForm({ vehicleId, initial, onDone }: { vehicleId: number; initial?: TyreSet; onDone(): void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    name: initial?.name ?? '', season: initial?.season ?? ('Summer' as TyreSeason), description: initial?.description ?? '',
    purchaseDate: initial?.purchaseDate ?? '', retired: initial?.retired ?? false,
  })
  const save = useMutation({
    mutationFn: () => {
      const body = { name: f.name, season: f.season, description: f.description.trim() || null, purchaseDate: f.purchaseDate || null, retired: f.retired }
      return initial ? api.put<TyreSet>(`/vehicles/${vehicleId}/tyres/${initial.id}`, body) : api.post<TyreSet>(`/vehicles/${vehicleId}/tyres`, body)
    },
    onSuccess: async () => { await invalidateVehicleData(qc, vehicleId); onDone() },
  })
  return (
    <form className="form-grid" onSubmit={(e) => { e.preventDefault(); save.mutate() }}>
      <label className="wide">Name<input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></label>
      <label>Season
        <select value={f.season} onChange={(e) => setF({ ...f, season: e.target.value as TyreSeason })}>
          {SEASONS.map((s) => <option key={s} value={s}>{label(s)}</option>)}
        </select>
      </label>
      <label>Purchase date<input type="date" value={f.purchaseDate} onChange={(e) => setF({ ...f, purchaseDate: e.target.value })} /></label>
      <label className="wide">Description<input value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} /></label>
      <label className="check wide"><input type="checkbox" checked={f.retired} onChange={(e) => setF({ ...f, retired: e.target.checked })} /> Retired</label>
      {save.isError && <ul className="errors wide">{errorLines(save.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      <div className="wide row">
        <button className="btn primary" type="submit" disabled={save.isPending}>{initial ? 'Save' : 'Add set'}</button>
        <button className="btn" type="button" onClick={onDone}>Cancel</button>
      </div>
    </form>
  )
}

function AssignRow({ expense, sets, vehicleId }: { expense: Expense; sets: TyreSet[]; vehicleId: number }) {
  const qc = useQueryClient()
  const [setId, setSetId] = useState('')
  const assign = useMutation({
    mutationFn: () => api.put<Expense>(`/vehicles/${vehicleId}/expenses/${expense.id}`, buildExpensePayload({ ...toFormState(expense), tyreSetId: setId })),
    onSuccess: () => invalidateVehicleData(qc, vehicleId),
  })
  return (
    <div className="row">
      <span>{expense.date} {expense.maintenance.map(label).join(', ')}</span>
      <TyreSetSelect sets={sets} value={setId} onChange={setSetId} />
      <button className="btn small" disabled={!setId || assign.isPending} onClick={() => assign.mutate()}>Assign set</button>
      {assign.isError && <span className="errors">{errorLines(assign.error).join('; ')}</span>}
    </div>
  )
}

export default function TyresPage() {
  const { vehicleId } = useParams()
  const vehicle: Vehicle = useVehicle()
  const qc = useQueryClient()
  const [editing, setEditing] = useState<TyreSet | 'new' | null>(null)
  const [blocked, setBlocked] = useState<{ set: TyreSet; errors: string[] } | null>(null)

  const { data } = useQuery({
    queryKey: ['tyres', vehicleId, 'summary'],
    queryFn: () => api.get<TyreSummaryResponse>(`/vehicles/${vehicleId}/tyres/summary`),
  })
  const { data: expenses = [] } = useQuery({
    queryKey: ['expenses', vehicleId, 'all'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses`),
  })
  const sets = useMemo(() => data?.summaries.map((s) => s.set) ?? [], [data])
  const unassigned = expenses.filter((e) => usesTyreSet(e.category, e.maintenance) && e.category === 'Maintenance' && e.tyreSetId == null)
  const kmOpt = useMemo(() => (data ? kmOption(data) : null), [data])

  const remove = useMutation({
    mutationFn: (s: TyreSet) => api.del<void>(`/vehicles/${vehicleId}/tyres/${s.id}`),
    onSuccess: () => invalidateVehicleData(qc, vehicleId),
    onError: (e, s) => { if (e instanceof ApiError && e.status === 409) setBlocked({ set: s, errors: errorLines(e) }) },
  })
  const retire = useMutation({
    mutationFn: (s: TyreSet) => api.put<TyreSet>(`/vehicles/${vehicleId}/tyres/${s.id}`, { ...s, retired: true }),
    onSuccess: async () => { setBlocked(null); await invalidateVehicleData(qc, vehicleId) },
  })

  return (
    <section>
      <h1>Tyres</h1>
      <div className="toolbar"><span className="spacer" /><button className="btn primary" onClick={() => setEditing('new')}>Add set</button></div>

      {blocked && (
        <div className="warn" role="alert">
          <ul>{blocked.errors.map((m) => <li key={m}>{m}</li>)}</ul>
          <button className="btn small" onClick={() => retire.mutate(blocked.set)}>Retire "{blocked.set.name}"</button>{' '}
          <button className="btn small" onClick={() => setBlocked(null)}>Dismiss</button>
        </div>
      )}

      <div className="cards">
        {data?.summaries.map(({ set, totalKm, seasons, costPer1000Km, mounted }) => (
          <article key={set.id} className="card">
            <h2>{set.name} {mounted && <span className="badge">mounted</span>} {set.retired && <span className="badge">retired</span>}</h2>
            <p className="muted">{label(set.season)}{set.description ? ` - ${set.description}` : ''}</p>
            <p><strong>{fmtKm(totalKm)}</strong> over {seasons} season{seasons === 1 ? '' : 's'}</p>
            <p>Cost per 1 000 km: {fmtMoney(costPer1000Km, vehicle.homeCurrency)}</p>
            <div className="row">
              <button className="btn small" onClick={() => setEditing(set)}>Edit</button>
              {!set.retired && <button className="btn small" onClick={() => retire.mutate(set)}>Retire</button>}
              <button className="btn small danger" onClick={() => { if (window.confirm(`Delete tyre set "${set.name}"?`)) remove.mutate(set) }}>Delete</button>
            </div>
          </article>
        ))}
        {data && data.summaries.length === 0 && <p className="muted">No tyre sets yet.</p>}
      </div>

      {unassigned.length > 0 && (
        <section className="card">
          <h2>Swaps without a tyre set</h2>
          {unassigned.map((e) => <AssignRow key={e.id} expense={e} sets={sets} vehicleId={vehicleId} />)}
        </section>
      )}

      <section className="card"><h2>Km per mount</h2>{kmOpt ? <Chart option={kmOpt} /> : <p className="muted">Loading…</p>}</section>

      {editing && (
        <Modal title={editing === 'new' ? 'Add tyre set' : 'Edit tyre set'} onClose={() => setEditing(null)}>
          <SetForm vehicleId={vehicleId} initial={editing === 'new' ? undefined : editing} onDone={() => setEditing(null)} />
        </Modal>
      )}
    </section>
  )
}
