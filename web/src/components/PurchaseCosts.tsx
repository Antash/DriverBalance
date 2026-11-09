import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { PurchaseCostInput, PurchaseCostRow } from '../api/types'
import { errorLines, fmtDate, fmtMoney, invalidateVehicleData } from '../lib/format'
import { DateInput } from './DateInput'

/** currency is set when editing a stored cost (kept as is); a new cost uses the home currency. */
interface Draft { id: number | null; date: string; amount: string; note: string; currency?: string }
const EMPTY: Draft = { id: null, date: '', amount: '', note: '' }

/**
 * The car's lump-sum purchase costs (price, warranty, registration transfer). Each change is saved at once,
 * independent of the vehicle form it sits in, so Enter inside the editor saves the cost and not the vehicle.
 */
export function PurchaseCosts({ vehicleId, currency }: { vehicleId: number; currency: string }) {
  const qc = useQueryClient()
  const [draft, setDraft] = useState<Draft | null>(null)
  const { data: costs = [] } = useQuery({
    queryKey: ['purchase-costs', vehicleId],
    queryFn: () => api.get<PurchaseCostRow[]>(`/vehicles/${vehicleId}/purchase-costs`),
  })
  const refresh = () => Promise.all([qc.invalidateQueries({ queryKey: ['purchase-costs', vehicleId] }), invalidateVehicleData(qc, vehicleId)])
  const save = useMutation({
    mutationFn: (d: Draft) => {
      const body: PurchaseCostInput = { date: d.date, amount: Number(d.amount), note: d.note.trim() || null, ...(d.currency ? { currency: d.currency } : {}) }
      return d.id === null
        ? api.post<PurchaseCostRow>(`/vehicles/${vehicleId}/purchase-costs`, body)
        : api.put<PurchaseCostRow>(`/vehicles/${vehicleId}/purchase-costs/${d.id}`, body)
    },
    onSuccess: async () => { setDraft(null); await refresh() },
  })
  const remove = useMutation({
    mutationFn: (id: number) => api.del<void>(`/vehicles/${vehicleId}/purchase-costs/${id}`),
    onSuccess: refresh,
  })
  const canSave = !!draft && draft.date !== '' && Number(draft.amount) > 0 && !save.isPending
  const submit = () => { if (draft && canSave) save.mutate(draft) }
  const keys = (e: React.KeyboardEvent) => {
    if (!(e.target instanceof HTMLInputElement)) return
    if (e.key === 'Enter') { e.preventDefault(); submit() }
    else if (e.key === 'Escape') setDraft(null)
  }

  return (
    <fieldset className="group"><legend>Purchase costs</legend>
      <p className="muted">Price, warranty, registration transfer and similar lump sums ({currency}).</p>
      {costs.length > 0 && (
        <table className="data">
          <tbody>
            {costs.map((c) => draft?.id === c.id ? null : (
              <tr key={c.id}>
                <td>{fmtDate(c.date)}</td>
                <td className="num">
                  {fmtMoney(c.amount, c.currency)}
                  {c.currency !== currency && (c.homeAmount != null
                    ? <span className="muted"> ({fmtMoney(c.homeAmount, currency)})</span>
                    : <span className="muted"> (no {currency} amount, not counted)</span>)}
                </td>
                <td>{c.note ?? ''}</td>
                <td>
                  <button className="btn small" type="button" onClick={() => { save.reset(); remove.reset(); setDraft({ id: c.id, date: c.date, amount: String(c.amount), note: c.note ?? '', currency: c.currency }) }}>Edit</button>{' '}
                  <button className="btn small danger" type="button" disabled={remove.isPending}
                    onClick={() => { if (window.confirm(`Delete this purchase cost (${fmtMoney(c.amount, c.currency)})?`)) remove.mutate(c.id) }}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {draft && (
        <div className="row" onKeyDown={keys}>
          <label>Date<DateInput aria-label="Cost date" value={draft.date} onChange={(v) => setDraft({ ...draft, date: v })} /></label>
          <label>Amount ({draft.currency ?? currency})
            <input type="number" min={0} step="any" value={draft.amount} onChange={(e) => setDraft({ ...draft, amount: e.target.value })} />
          </label>
          <label>Note
            <input maxLength={200} value={draft.note} placeholder="e.g. Warranty" onChange={(e) => setDraft({ ...draft, note: e.target.value })} />
          </label>
          <button className="btn primary small" type="button" disabled={!canSave} onClick={submit}>{draft.id === null ? 'Add' : 'Save cost'}</button>
          <button className="btn small" type="button" onClick={() => setDraft(null)}>Cancel</button>
        </div>
      )}
      {(save.isError || remove.isError) && <ul className="errors">{errorLines(save.error ?? remove.error).map((m) => <li key={m}>{m}</li>)}</ul>}
      {!draft && <div><button className="btn small" type="button" onClick={() => { save.reset(); remove.reset(); setDraft(EMPTY) }}>Add cost</button></div>}
    </fieldset>
  )
}
