import type { FinancingFrequency, VehicleInput } from '../api/types'
import { DateInput } from './DateInput'

export type FinancingValue = Pick<VehicleInput, 'financingStart' | 'financingAmount' | 'financingFrequency' | 'financingPayments' | 'financingBalloon'>

export const FREQUENCIES: [FinancingFrequency, string][] = [['Monthly', 'Monthly'], ['Quarterly', 'Quarterly'], ['Yearly', 'Yearly']]

const num = (s: string) => (s === '' ? null : Math.max(0, Number(s)))

/** Optional financing contract: leave start, amount and payments empty for none. Saved with the vehicle. */
export function FinancingFields({ value: v, currency, onChange }: { value: FinancingValue; currency: string; onChange: (v: FinancingValue) => void }) {
  const set = (p: Partial<FinancingValue>) => onChange({ ...v, ...p })
  return (
    <fieldset className="group"><legend>Financing</legend>
      <div className="stack">
        <label>First payment on
          <DateInput aria-label="Financing start" value={v.financingStart ?? ''} onChange={(d) => set({ financingStart: d || null })} />
        </label>
        <label>Amount per payment ({currency})
          <input type="number" min={0} step="any" value={v.financingAmount ?? ''} onChange={(e) => set({ financingAmount: num(e.target.value) })} />
        </label>
        <label>Frequency
          <select value={v.financingFrequency ?? 'Monthly'} onChange={(e) => set({ financingFrequency: e.target.value as FinancingFrequency })}>
            {FREQUENCIES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select>
        </label>
        <label>Number of payments
          <input type="number" min={1} step={1} value={v.financingPayments ?? ''}
            onChange={(e) => set({ financingPayments: e.target.value === '' ? null : Math.max(0, Math.trunc(Number(e.target.value))) })} />
        </label>
        <label>Final (balloon) payment ({currency}, optional)
          <input type="number" min={0} step="any" value={v.financingBalloon ?? ''} onChange={(e) => set({ financingBalloon: num(e.target.value) })} />
        </label>
        <p className="muted">The whole contract (payments × amount + final payment) counts as a purchase cost. No interest is calculated.</p>
      </div>
    </fieldset>
  )
}
