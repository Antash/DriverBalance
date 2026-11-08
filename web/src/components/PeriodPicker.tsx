import { useState } from 'react'

export interface PeriodValue {
  from: string
  /** Exclusive end, as the API expects. */
  to: string
  label: string
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const addDays = (s: string, n: number) => { const d = new Date(`${s}T00:00:00`); d.setDate(d.getDate() + n); return iso(d) }

function presetPeriods(today = new Date()): Record<string, PeriodValue> {
  const y = today.getFullYear(), m = today.getMonth()
  return {
    thisMonth: { from: iso(new Date(y, m, 1)), to: iso(new Date(y, m + 1, 1)), label: 'This month' },
    lastMonth: { from: iso(new Date(y, m - 1, 1)), to: iso(new Date(y, m, 1)), label: 'Last month' },
    thisYear: { from: iso(new Date(y, 0, 1)), to: iso(new Date(y + 1, 0, 1)), label: 'This year' },
    lastYear: { from: iso(new Date(y - 1, 0, 1)), to: iso(new Date(y, 0, 1)), label: 'Last year' },
  }
}

/** Presets plus a custom range. The UI shows an inclusive "to"; the value carries the exclusive one. */
export function PeriodPicker({ value, onChange }: { value: PeriodValue; onChange(p: PeriodValue): void }) {
  const presets = presetPeriods()
  const [custom, setCustom] = useState(false)
  const matched = Object.keys(presets).find((k) => presets[k].from === value.from && presets[k].to === value.to)
  const selected = custom || !matched ? 'custom' : matched

  return (
    <div className="period-picker">
      <select
        aria-label="Period"
        value={selected}
        onChange={(e) => {
          if (e.target.value === 'custom') setCustom(true)
          else { setCustom(false); onChange(presets[e.target.value]) }
        }}
      >
        {Object.entries(presets).map(([k, p]) => <option key={k} value={k}>{p.label}</option>)}
        <option value="custom">Custom range</option>
      </select>
      {selected === 'custom' && (
        <>
          <input type="date" aria-label="From" value={value.from}
            onChange={(e) => e.target.value && onChange({ ...value, from: e.target.value, label: 'Custom' })} />
          <input type="date" aria-label="To (inclusive)" value={addDays(value.to, -1)}
            onChange={(e) => e.target.value && onChange({ ...value, to: addDays(e.target.value, 1), label: 'Custom' })} />
        </>
      )}
    </div>
  )
}
