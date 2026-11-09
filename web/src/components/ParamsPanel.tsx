import { useState } from 'react'
import { ApiError } from '../api/client'
import type { AnalysisParameters } from '../api/types'
import { useParams } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

type Field = { key: keyof AnalysisParameters; label: string; step?: string; int?: boolean }

const INT = true

const GROUPS: { title: string; fields: Field[] }[] = [
  { title: 'Cold start', fields: [{ key: 'coldStartGapMinutes', label: 'Gap between trips (min)', int: INT }] },
  { title: 'Score thresholds', fields: [
    { key: 'shortDurationMinutes', label: 'Short trip (min)', int: INT },
    { key: 'mediumDurationMinutes', label: 'Medium trip (min)', int: INT },
    { key: 'longDurationMinutes', label: 'Long trip (min)', int: INT },
  ] },
  { title: 'Driving mix', fields: [
    { key: 'heavyTrafficSpeedKmh', label: 'Heavy traffic below (km/h)' },
    { key: 'citySpeedKmh', label: 'City below (km/h)' },
    { key: 'mixedSpeedKmh', label: 'Mixed below (km/h)' },
  ] },
  { title: 'Charts', fields: [
    { key: 'dpfMovingAverageWeeks', label: 'DPF moving average (weeks)', int: INT },
    { key: 'fuelTrendMinLPer100', label: 'Fuel trend min (L/100)' },
    { key: 'fuelTrendMaxLPer100', label: 'Fuel trend max (L/100)' },
    { key: 'lowessSpan', label: 'LOWESS span', step: '0.05' },
    { key: 'heatmapKmCap', label: 'Heatmap cap (km)' },
  ] },
  { title: 'Reminders', fields: [
    { key: 'longDriveHintDays', label: 'Long-drive hint (days)', int: INT },
    { key: 'exportReminderDays', label: 'Export reminder (days)', int: INT },
  ] },
]

/** Parameters that only matter for cars with a DPF. */
const DPF_FIELDS = ['dpfMovingAverageWeeks', 'longDriveHintDays']

const LABELS: Record<string, string> = Object.fromEntries(GROUPS.flatMap((g) => g.fields.map((f) => [f.key, f.label])))

export function ParamsPanel({ onClose }: { onClose(): void }) {
  const { draft, dirty, errors: problems, valid, setDraft, save, reset } = useParams()
  const { hasDpf } = useVehicle()
  const [text, setText] = useState<Record<string, string>>({})
  const [errors, setErrors] = useState<string[]>([])
  const [saving, setSaving] = useState(false)
  const [invalid, setInvalid] = useState<Record<string, string>>({})

  const change = (f: Field, raw: string) => {
    setText((t) => ({ ...t, [f.key]: raw }))
    const n = Number(raw)
    const problem = raw.trim() === '' || !Number.isFinite(n) ? 'Enter a number.'
      : f.int && !Number.isInteger(n) ? 'Enter a whole number.' : null
    setInvalid((v) => {
      const next = { ...v }
      if (problem) next[f.key] = problem; else delete next[f.key]
      return next
    })
    if (!problem) setDraft({ [f.key]: n })
  }

  // Leaving a field drops any invalid text so the input shows the draft again.
  const blur = (key: string) => {
    setText((t) => { const next = { ...t }; delete next[key]; return next })
    setInvalid((v) => { const next = { ...v }; delete next[key]; return next })
  }

  const hasInvalid = Object.keys(invalid).length > 0

  const onSave = async () => {
    setSaving(true)
    setErrors([])
    try { await save(); setText({}) }
    catch (e) { setErrors(e instanceof ApiError && e.errors ? e.errors : [e instanceof Error ? e.message : 'Save failed']) }
    finally { setSaving(false) }
  }

  return (
    <>
      <div className="scrim" onClick={onClose} />
      <aside className="drawer" role="dialog" aria-label="Parameters">
        <div className="drawer-head">
          <h2>Parameters</h2>
          <button className="btn" onClick={onClose} aria-label="Close">✕</button>
        </div>
        <div className="drawer-body">
          {GROUPS.map((g) => ({ ...g, fields: g.fields.filter((f) => hasDpf || !DPF_FIELDS.includes(f.key)) })).filter((g) => g.fields.length > 0).map((g) => (
            <fieldset className="group" key={g.title}>
              <legend>{g.title}</legend>
              {g.fields.map((f) => (
                <label className="field" key={f.key}>
                  <span>{f.label}</span>
                  <input type="number" inputMode="decimal" step={f.int ? '1' : f.step ?? 'any'} aria-invalid={!!invalid[f.key]}
                    value={text[f.key] ?? String(draft[f.key])}
                    onChange={(e) => change(f, e.target.value)} onBlur={() => blur(f.key)} />
                </label>
              ))}
            </fieldset>
          ))}
          {problems.length > 0 && (
            <ul className="errors" role="alert" aria-label="Parameter problems">{problems.map((m) => <li key={m}>{m}</li>)}</ul>
          )}
          {Object.entries(invalid).map(([k, m]) => <p className="errors" role="alert" key={k}>{LABELS[k]}: {m}</p>)}
          {errors.length > 0 && <ul className="errors" role="status">{errors.map((e) => <li key={e}>{e}</li>)}</ul>}
        </div>
        <div className="drawer-foot">
          <button className="btn primary" disabled={!dirty || saving || hasInvalid || !valid} onClick={onSave}>Save</button>
          <button className="btn" disabled={!dirty} onClick={() => { reset(); setText({}); setInvalid({}); setErrors([]) }}>Reset</button>
        </div>
      </aside>
    </>
  )
}
