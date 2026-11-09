import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { TokenCreated, TokenInfo, TripPage, Vehicle, VehicleInput } from '../api/types'
import { ParamsPanel } from '../components/ParamsPanel'
import { errorLines } from '../lib/format'
import { useTyreSets, useVehicle } from '../state/useVehicle'

function VehicleCard({ vehicle }: { vehicle: Vehicle }) {
  const qc = useQueryClient()
  const { data: sets = [] } = useTyreSets()
  const [f, setF] = useState<VehicleInput>({
    name: vehicle.name, homeCurrency: vehicle.homeCurrency, timeZone: vehicle.timeZone,
    initialTyreSetId: vehicle.initialTyreSetId, isDefault: vehicle.isDefault,
    historyStart: vehicle.historyStart, historyStartOdometer: vehicle.historyStartOdometer,
    purchaseDate: vehicle.purchaseDate, estimatedResaleValue: vehicle.estimatedResaleValue,
    resaleEstimateDate: vehicle.resaleEstimateDate,
    details: vehicle.details,
  })
  const d = f.details ?? { model: null, engine: null, trim: null, modelYear: null, exteriorColour: null, interior: null, equipment: [] }
  const setD = (p: Partial<typeof d>) => setF({ ...f, details: { ...d, ...p } })
  const [equipmentText, setEquipmentText] = useState(d.equipment.join('\n'))
  // The first recorded trip, for the "Use first trip" shortcut: the list is newest first, so ask for the last page of size 1.
  const { data: firstTrip } = useQuery({
    queryKey: ['analytics', vehicle.id, 'first-trip'],
    queryFn: async () => {
      const head = await api.get<TripPage>(`/vehicles/${vehicle.id}/trips?page=1&pageSize=1`)
      if (head.total < 1) return null
      const last = head.total === 1 ? head : await api.get<TripPage>(`/vehicles/${vehicle.id}/trips?page=${head.total}&pageSize=1`)
      return last.groups[0]?.start?.slice(0, 10) ?? null
    },
  })
  const save = useMutation({
    mutationFn: () => api.put<Vehicle>(`/vehicles/${vehicle.id}`, f),
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['vehicles'] }),
      qc.invalidateQueries({ queryKey: ['analytics', vehicle.id] }),
      qc.invalidateQueries({ queryKey: ['expenses', vehicle.id] }),
    ]),
  })
  return (
    <section className="card">
      <h2>Vehicle</h2>
      <form className="stack" onSubmit={(e) => { e.preventDefault(); save.mutate() }}>
        <label>Name<input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></label>
        <label>Home currency<input maxLength={3} value={f.homeCurrency} onChange={(e) => setF({ ...f, homeCurrency: e.target.value.toUpperCase() })} /></label>
        <label>Time zone<input value={f.timeZone} onChange={(e) => setF({ ...f, timeZone: e.target.value })} /></label>
        <label>Initial tyre set
          <select value={f.initialTyreSetId ?? ''} onChange={(e) => setF({ ...f, initialTyreSetId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">- none -</option>
            {sets.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </label>
        <label>Car history start
          <input type="date" value={f.historyStart ?? ''}
            onChange={(e) => setF({ ...f, historyStart: e.target.value || null, historyStartOdometer: e.target.value ? (f.historyStartOdometer ?? 0) : null })} />
        </label>
        <label>Odometer at that date (km)
          <input type="number" min={0} step={1} disabled={!f.historyStart} value={f.historyStartOdometer ?? ''}
            onChange={(e) => setF({ ...f, historyStartOdometer: e.target.value === '' ? 0 : Math.max(0, Math.trunc(Number(e.target.value))) })} />
        </label>
        <p className="muted">When the car was new (or when you bought it). Stats count from here.</p>
        <label>Bought on
          <input type="date" value={f.purchaseDate ?? ''} onChange={(e) => setF({ ...f, purchaseDate: e.target.value || null })} />
        </label>
        {!f.purchaseDate && firstTrip && (
          <div><button className="btn small" type="button" onClick={() => setF({ ...f, purchaseDate: firstTrip })}>Use first trip ({firstTrip})</button></div>
        )}
        <p className="muted">Your first day with the car; earlier records belong to the previous owner.</p>
        <label>Estimated resale value ({f.homeCurrency})
          <input type="number" min={0} step="any" value={f.estimatedResaleValue ?? ''}
            onChange={(e) => setF({ ...f, estimatedResaleValue: e.target.value === '' ? null : Math.max(0, Number(e.target.value)) })} />
        </label>
        <label>Estimated on
          <input type="date" value={f.resaleEstimateDate ?? ''} onChange={(e) => setF({ ...f, resaleEstimateDate: e.target.value || null })} />
        </label>
        <fieldset className="group"><legend>Car details</legend>
          <div className="stack">
            <label>Model<input value={d.model ?? ''} placeholder="e.g. Volvo V90" onChange={(e) => setD({ model: e.target.value || null })} /></label>
            <label>Engine<input value={d.engine ?? ''} placeholder="e.g. D5 AWD" onChange={(e) => setD({ engine: e.target.value || null })} /></label>
            <label>Trim<input value={d.trim ?? ''} onChange={(e) => setD({ trim: e.target.value || null })} /></label>
            <label>Model year<input inputMode="numeric" value={d.modelYear ?? ''}
              onChange={(e) => setD({ modelYear: e.target.value.trim() === '' ? null : Math.trunc(Number(e.target.value)) || null })} /></label>
            <label>Exterior colour<input value={d.exteriorColour ?? ''} onChange={(e) => setD({ exteriorColour: e.target.value || null })} /></label>
            <label>Interior<input value={d.interior ?? ''} onChange={(e) => setD({ interior: e.target.value || null })} /></label>
            <label>Equipment (one per line)
              <textarea rows={6} value={equipmentText} onChange={(e) => {
                setEquipmentText(e.target.value)
                setD({ equipment: e.target.value.split('\n').map((x) => x.trim()).filter(Boolean) })
              }} />
            </label>
          </div>
        </fieldset>
        <label className="check"><input type="checkbox" checked={f.isDefault} onChange={(e) => setF({ ...f, isDefault: e.target.checked })} /> Default vehicle</label>
        {save.isError && <ul className="errors">{errorLines(save.error).map((m) => <li key={m}>{m}</li>)}</ul>}
        <div className="row">
          <button className="btn primary" type="submit" disabled={save.isPending}>Save vehicle</button>
          {save.isSuccess && <span role="status" className="muted">Saved.</span>}
        </div>
      </form>
    </section>
  )
}

function TokensCard() {
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [created, setCreated] = useState<TokenCreated | null>(null)
  const [copied, setCopied] = useState(false)
  const { data: tokens } = useQuery({ queryKey: ['tokens'], queryFn: () => api.get<TokenInfo[]>('/tokens') })
  const create = useMutation({
    mutationFn: () => api.post<TokenCreated>('/tokens', { name }),
    onSuccess: (t) => { setCreated(t); setCopied(false); setName(''); void qc.invalidateQueries({ queryKey: ['tokens'] }) },
  })
  const revoke = useMutation({
    mutationFn: (id: number) => api.del<void>(`/tokens/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tokens'] }),
  })
  const copy = async () => {
    if (!created) return
    try { await navigator.clipboard.writeText(created.token); setCopied(true) } catch { setCopied(false) }
  }
  return (
    <section className="card">
      <h2>API tokens</h2>
      <form className="row" onSubmit={(e) => { e.preventDefault(); if (name.trim()) create.mutate() }}>
        <label className="stack">Token name<input value={name} onChange={(e) => setName(e.target.value)} /></label>
        <button className="btn primary" type="submit" disabled={!name.trim() || create.isPending}>Create token</button>
      </form>
      {create.isError && <ul className="errors">{errorLines(create.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      {created && (
        <div className="warn" role="status">
          <p>Copy this token now. It will not be shown again.</p>
          <div className="token-once">{created.token}</div>
          <div className="row">
            <button className="btn small" onClick={copy}>{copied ? 'Copied' : 'Copy'}</button>
            <button className="btn small" onClick={() => setCreated(null)}>Done</button>
          </div>
        </div>
      )}
      <div className="table-wrap">
        <table className="data">
          <thead><tr><th>Name</th><th>Created</th><th>Last used</th><th /></tr></thead>
          <tbody>
            {(tokens ?? []).map((t) => (
              <tr key={t.id}>
                <td>{t.name}</td><td>{new Date(t.createdAt).toLocaleDateString()}</td>
                <td>{t.lastUsedAt ? new Date(t.lastUsedAt).toLocaleString() : 'never'}</td>
                <td><button className="btn small danger" onClick={() => { if (window.confirm(`Revoke token "${t.name}"?`)) revoke.mutate(t.id) }}>Revoke</button></td>
              </tr>
            ))}
            {tokens?.length === 0 && <tr><td colSpan={4} className="muted">No tokens.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}

async function downloadExport() {
  const res = await fetch('/api/export')
  if (!res.ok) throw new Error(`Export failed (${res.status})`)
  const url = URL.createObjectURL(await res.blob())
  const a = document.createElement('a')
  a.href = url
  a.download = `driverbalance-export-${new Date().toISOString().slice(0, 10)}.json`
  a.click()
  URL.revokeObjectURL(url)
}

export default function SettingsPage() {
  const vehicle = useVehicle()
  const [panel, setPanel] = useState(false)
  const exp = useMutation({ mutationFn: downloadExport })
  return (
    <section>
      <h1>Settings</h1>
      <VehicleCard key={vehicle.id + ':' + vehicle.name + ':' + vehicle.initialTyreSetId} vehicle={vehicle} />
      <section className="card">
        <h2>Analysis parameters</h2>
        <p className="muted">Thresholds and chart settings apply to this vehicle.</p>
        <button className="btn" onClick={() => setPanel(true)}>Open parameters</button>
      </section>
      <TokensCard />
      <section className="card">
        <h2>Export</h2>
        <button className="btn" onClick={() => exp.mutate()} disabled={exp.isPending}>Export all data</button>
        {exp.isError && <ul className="errors">{errorLines(exp.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      </section>
      {panel && <ParamsPanel onClose={() => setPanel(false)} />}
    </section>
  )
}
