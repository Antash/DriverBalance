import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import { FUEL_TYPES } from '../lib/fuelTypes'

/** A new profile has no car yet: ask for the minimum, the rest can be edited later in Settings. */
export default function FirstCar() {
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [fuelType, setFuelType] = useState('Diesel')
  const [homeCurrency, setHomeCurrency] = useState('EUR')
  const [timeZone, setTimeZone] = useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC')
  const create = useMutation({
    mutationFn: () => api.post('/vehicles', { name: name.trim(), fuelType: fuelType || null, homeCurrency: homeCurrency.trim().toUpperCase(), timeZone: timeZone.trim() }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['vehicles'] }),
  })
  const error = create.error instanceof ApiError ? create.error.message : create.error ? 'Could not add the car.' : null

  return (
    <main className="main">
      <form className="card" onSubmit={(e) => { e.preventDefault(); if (name.trim()) create.mutate() }}>
        <h2>Add your first car</h2>
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
        <p><button className="btn" disabled={!name.trim() || create.isPending}>Add car</button></p>
      </form>
    </main>
  )
}
