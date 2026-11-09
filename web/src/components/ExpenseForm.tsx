import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Expense, ExpenseCategory, TyreSet, Vehicle } from '../api/types'
import { buildExpensePayload, parseDecimal, validateExpenseForm, emptyFormState, pricePerLitre, toFormState, type ExpenseFormState } from '../lib/expenseForm'
import { MAINTENANCE_TYPES } from '../lib/maintenanceCatalog'
import { MaintenanceWorkEditor } from './MaintenanceWorkEditor'
import { errorLines, fmtMoney, invalidateVehicleData } from '../lib/format'

export const CATEGORIES: ExpenseCategory[] = [
  'Fuel', 'Parking', 'TollsVignette', 'Wash', 'Maintenance', 'Repair', 'Tyres', 'Accessories', 'Insurance',
  'RoadTax', 'Fines', 'Financing', 'Other',
]

export { MAINTENANCE_TYPES }

export const label = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')

export function TyreSetSelect({ sets, value, onChange }: { sets: TyreSet[]; value: string; onChange(v: string): void }) {
  return (
    <select aria-label="Tyre set" value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">- none yet -</option>
      {sets.filter((s) => !s.retired || String(s.id) === value).map((s) => (
        <option key={s.id} value={s.id}>{s.name}{s.retired ? ' (retired)' : ''}</option>
      ))}
    </select>
  )
}

export function ExpenseForm({ vehicle, tyreSets, initial, onDone, lockCategory, onDelete, deleting, defaultOdometer }: {
  vehicle: Vehicle; tyreSets: TyreSet[]; initial?: Expense; onDone(): void
  lockCategory?: ExpenseCategory; onDelete?: () => void; deleting?: boolean; defaultOdometer?: number
}) {
  const qc = useQueryClient()
  const [submitted, setSubmitted] = useState(false)
  const [f, setF] = useState<ExpenseFormState>(() => {
    if (initial) return toFormState(initial)
    const e = emptyFormState(vehicle.homeCurrency)
    return {
      ...e,
      category: lockCategory ?? e.category,
      odometer: e.odometer.trim() === '' && defaultOdometer != null ? String(defaultOdometer) : e.odometer,
    }
  })
  const set = <K extends keyof ExpenseFormState>(k: K, v: ExpenseFormState[K]) => setF((s) => ({ ...s, [k]: v }))

  const save = useMutation({
    mutationFn: () => {
      const body = buildExpensePayload(f, vehicle.homeCurrency)
      return initial
        ? api.put<Expense>(`/vehicles/${vehicle.id}/expenses/${initial.id}`, body)
        : api.post<Expense>(`/vehicles/${vehicle.id}/expenses`, body)
    },
    onSuccess: async () => { await invalidateVehicleData(qc, vehicle.id); onDone() },
  })

  const errors = validateExpenseForm(f, vehicle.homeCurrency)
  const err = (k: string) => (submitted && errors[k] ? <span className="field-error" role="alert">{errors[k]}</span> : null)
  const ppl = f.category === 'Fuel' ? pricePerLitre(parseDecimal(f.amount) ?? NaN, parseDecimal(f.litres) ?? NaN) : null
  const foreign = f.currency.trim().toUpperCase() !== vehicle.homeCurrency.toUpperCase()

  return (
    <form className="form-grid" onSubmit={(e) => { e.preventDefault(); setSubmitted(true); if (Object.keys(errors).length === 0) save.mutate() }}>
      <label>Date<input type="date" required value={f.date} onChange={(e) => set('date', e.target.value)} /></label>
      {!lockCategory && (
        <label>Category
          <select value={f.category} onChange={(e) => set('category', e.target.value as ExpenseCategory)}>
            {CATEGORIES.map((c) => <option key={c} value={c}>{label(c)}</option>)}
          </select>
        </label>
      )}
      <label>Amount<input inputMode="decimal" value={f.amount} onChange={(e) => set('amount', e.target.value)} />{err('amount')}</label>
      <label>Currency<input maxLength={3} value={f.currency} onChange={(e) => set('currency', e.target.value)} /></label>
      {foreign && (
        <label>Amount in {vehicle.homeCurrency} (optional)
          <input inputMode="decimal" value={f.homeAmount} onChange={(e) => set('homeAmount', e.target.value)} />{err('homeAmount')}
        </label>
      )}
      <label>Odometer (km)<input inputMode="numeric" value={f.odometer} onChange={(e) => set('odometer', e.target.value)} />{err('odometer')}</label>

      {f.category === 'Fuel' && (
        <>
          <label>Litres<input inputMode="decimal" value={f.litres} onChange={(e) => set('litres', e.target.value)} />{err('litres')}</label>
          <label>Station<input value={f.station} onChange={(e) => set('station', e.target.value)} /></label>
          <label className="check">
            <input type="checkbox" checked={f.fullTank} onChange={(e) => set('fullTank', e.target.checked)} /> Full tank
          </label>
          <p className="muted wide">
            Price per litre: {ppl == null ? '-' : fmtMoney(ppl, f.currency.trim().toUpperCase() || vehicle.homeCurrency)}
          </p>
        </>
      )}

      {f.category === 'Maintenance' && (
        <>
          <MaintenanceWorkEditor
            work={f.work} onWork={(w) => set('work', w)}
            tyres={f.tyres} onTyres={(t) => set('tyres', t)}
            tyreSetId={f.tyreSetId} onTyreSetId={(v) => set('tyreSetId', v)}
            tyreSets={tyreSets} tyreLocked={initial?.tyreSetId != null} err={err}
          />
          {f.maintenanceDescription && <p className="muted wide">Earlier description: {f.maintenanceDescription}</p>}
        </>
      )}

      {f.category === 'Tyres' && (
        <label className="wide">Tyre set<TyreSetSelect sets={tyreSets} value={f.tyreSetId} onChange={(v) => set('tyreSetId', v)} /></label>
      )}

      <label className="wide">Note<input value={f.note} onChange={(e) => set('note', e.target.value)} /></label>

      <fieldset className="group wide"><legend>Spread cost over a period</legend>
        <div className="row">
          <label>Covers from<input type="date" value={f.coversFrom} onChange={(e) => set('coversFrom', e.target.value)} /></label>
          <label>Covers to<input type="date" value={f.coversTo} onChange={(e) => set('coversTo', e.target.value)} /></label>
        </div>
      </fieldset>

      {save.isError && <ul className="errors wide">{errorLines(save.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      <div className="wide row">
        <button className="btn primary" type="submit" disabled={save.isPending}>{initial ? 'Save' : 'Add expense'}</button>
        <button className="btn" type="button" onClick={onDone}>Cancel</button>
        {initial && onDelete && <button className="btn danger" type="button" disabled={deleting} onClick={onDelete}>Delete</button>}
      </div>
    </form>
  )
}
