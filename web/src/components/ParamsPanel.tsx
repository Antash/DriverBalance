import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'
import type { AnalysisParameters } from '../api/types'
import { useParams } from '../state/ParamsContext'

type Field = { key: keyof AnalysisParameters; label: string; step?: string }

const GROUPS: { title: string; fields: Field[] }[] = [
  { title: 'Cold start', fields: [{ key: 'coldStartGapMinutes', label: 'Gap between trips (min)' }] },
  { title: 'Score thresholds', fields: [
    { key: 'shortDurationMinutes', label: 'Short trip (min)' },
    { key: 'mediumDurationMinutes', label: 'Medium trip (min)' },
    { key: 'longDurationMinutes', label: 'Long trip (min)' },
  ] },
  { title: 'Driving mix', fields: [
    { key: 'heavyTrafficSpeedKmh', label: 'Heavy traffic below (km/h)' },
    { key: 'citySpeedKmh', label: 'City below (km/h)' },
    { key: 'mixedSpeedKmh', label: 'Mixed below (km/h)' },
  ] },
  { title: 'Charts', fields: [
    { key: 'dpfMovingAverageWeeks', label: 'DPF moving average (weeks)' },
    { key: 'fuelTrendMinLPer100', label: 'Fuel trend min (L/100)' },
    { key: 'fuelTrendMaxLPer100', label: 'Fuel trend max (L/100)' },
    { key: 'lowessSpan', label: 'LOWESS span', step: '0.05' },
    { key: 'heatmapKmCap', label: 'Heatmap cap (km)' },
  ] },
  { title: 'Reminders', fields: [
    { key: 'longDriveHintDays', label: 'Long-drive hint (days)' },
    { key: 'exportReminderDays', label: 'Export reminder (days)' },
  ] },
]

export function ParamsPanel({ onClose }: { onClose(): void }) {
  const { draft, dirty, setDraft, save, reset } = useParams()
  const [text, setText] = useState<Record<string, string>>({})
  const [errors, setErrors] = useState<string[]>([])
  const [saving, setSaving] = useState(false)
  const timer = useRef<number | undefined>(undefined)

  useEffect(() => () => window.clearTimeout(timer.current), [])

  const change = (key: keyof AnalysisParameters, raw: string) => {
    setText((t) => ({ ...t, [key]: raw }))
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => {
      const n = Number(raw)
      if (raw.trim() !== '' && Number.isFinite(n)) setDraft({ [key]: n })
    }, 300)
  }

  const onSave = async () => {
    window.clearTimeout(timer.current)
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
          {GROUPS.map((g) => (
            <fieldset className="group" key={g.title}>
              <legend>{g.title}</legend>
              {g.fields.map((f) => (
                <label className="field" key={f.key}>
                  <span>{f.label}</span>
                  <input type="number" inputMode="decimal" step={f.step ?? 'any'}
                    value={text[f.key] ?? String(draft[f.key])}
                    onChange={(e) => change(f.key, e.target.value)} />
                </label>
              ))}
            </fieldset>
          ))}
          {errors.length > 0 && <ul className="errors" role="alert">{errors.map((e) => <li key={e}>{e}</li>)}</ul>}
        </div>
        <div className="drawer-foot">
          <button className="btn primary" disabled={!dirty || saving} onClick={onSave}>Save</button>
          <button className="btn" disabled={!dirty} onClick={() => { window.clearTimeout(timer.current); reset(); setText({}); setErrors([]) }}>Reset</button>
        </div>
      </aside>
    </>
  )
}
