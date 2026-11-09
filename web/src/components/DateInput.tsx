import { useEffect, useRef, useState } from 'react'
import { parseDmy, toDmy } from '../lib/dates'

interface Props {
  /** ISO yyyy-MM-dd, or ''. */
  value: string
  /** ISO, or '' when cleared. Never called with unparsable text. */
  onChange: (iso: string) => void
  'aria-label'?: string
  required?: boolean
  min?: string
  max?: string
  disabled?: boolean
}

/** Text field that shows and accepts dd/mm/yyyy, with a button opening the native calendar. */
export function DateInput({ value, onChange, 'aria-label': ariaLabel, required, min, max, disabled }: Props) {
  const [text, setText] = useState(toDmy(value))
  const [invalid, setInvalid] = useState(false)
  const picker = useRef<HTMLInputElement>(null)

  // Follow outside changes, but not while the typed text already means the same date.
  useEffect(() => {
    setText((t) => (parseDmy(t) === value || (value === '' && t.trim() === '') ? t : toDmy(value)))
    setInvalid(false)
  }, [value])

  const change = (t: string) => {
    setText(t)
    if (t.trim() === '') { setInvalid(false); onChange(''); return }
    const iso = parseDmy(t)
    if (iso) { setInvalid(false); onChange(iso) }
  }
  const blur = () => {
    if (text.trim() === '') { setInvalid(false); return }
    const iso = parseDmy(text)
    if (iso) { setText(toDmy(iso)); setInvalid(false) } else setInvalid(true)
  }
  const open = () => {
    const el = picker.current
    if (!el) return
    if (typeof el.showPicker === 'function') el.showPicker()
    else el.click()
  }

  return (
    <span className="date-input">
      <span className="date-input-row">
        <input type="text" inputMode="numeric" placeholder="dd/mm/yyyy" maxLength={10} autoComplete="off"
          aria-label={ariaLabel} aria-invalid={invalid || undefined} required={required} disabled={disabled}
          value={text} onChange={(e) => change(e.target.value)} onBlur={blur} />
        <button type="button" className="btn small" aria-label={ariaLabel ? `${ariaLabel}: open calendar` : 'Open calendar'}
          title="Pick from calendar" disabled={disabled} onClick={open}>📅</button>
        <input ref={picker} type="date" className="date-input-native" tabIndex={-1} aria-hidden="true"
          value={value} min={min} max={max} onChange={(e) => change(toDmy(e.target.value))} />
      </span>
      {invalid && <span className="field-error">Use dd/mm/yyyy</span>}
    </span>
  )
}
