import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import { FUEL_TYPES } from '../lib/fuelTypes'

/** A new profile has no car yet: ask for the minimum, the rest can be edited later in Settings. */
export default function FirstCar({ onDone, onCancel, allowDemo = true }: { onDone?(): void; onCancel?(): void; allowDemo?: boolean }) {
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [fuelType, setFuelType] = useState('Diesel')
  const [homeCurrency, setHomeCurrency] = useState('EUR')
  const [timeZone, setTimeZone] = useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC')
  const create = useMutation({
    mutationFn: () => api.post('/vehicles', { name: name.trim(), fuelType: fuelType || null, homeCurrency: homeCurrency.trim().toUpperCase(), timeZone: timeZone.trim() }),
    onSuccess: async () => { await qc.invalidateQueries({ queryKey: ['vehicles'] }); onDone?.() },
  })
  const demo = useMutation({
    mutationFn: () => api.post('/vehicles/demo', {}),
    onSuccess: async () => { await qc.invalidateQueries({ queryKey: ['vehicles'] }); onDone?.() },
  })
  const failure = create.error ?? demo.error
  const error = failure instanceof ApiError ? failure.message : failure ? 'Could not add the car.' : null
  const busy = create.isPending || demo.isPending

  const body = (
    <>
      <form className="card" onSubmit={(e) => { e.preventDefault(); if (name.trim()) create.mutate() }}>
        {allowDemo ? <h2>Add your first car</h2> : <h2 className="sr-only">Add your real car</h2>}
        <p className="muted">You can change all of this later in Settings, then import your trip export on the Import page.</p>
        <div className="form-grid">
          <label>Name<input value={name} onChange={(e) => setName(e.target.value)} placeholder="My Volvo" autoFocus /></label>
          <label>Fuel
            <select value={fuelType} onChange={(e) => setFuelType(e.target.value)}>
              {FUEL_TYPES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </label>
          <label>Currency<input value={homeCurrency} onChange={(e) => setHomeCurrency(e.target.value)} maxLength={3} /></label>
          <label>Time zone<input value={timeZone} onChange={(e) => setTimeZone(e.target.value)} /></label>
        </div>
        {error && <p role="alert" className="field-error">{error}</p>}
        <p className="row-actions">
          <button className="btn" disabled={!name.trim() || busy}>Add car</button>
          {onCancel && <button type="button" className="btn" onClick={onCancel}>Cancel</button>}
        </p>
      </form>
      {allowDemo && (
        <div className="card demo-offer">
          <p className="muted divider">or</p>
          <p><button type="button" className="btn" disabled={busy} onClick={() => demo.mutate()}>Try with a demo car</button></p>
          <p className="muted">A made-up BMW with 3½ years of trips, services and costs. Delete it any time; adding your real car removes it.</p>
        </div>
      )}
    </>
  )
  return allowDemo ? <main className="main">{body}</main> : body
}
