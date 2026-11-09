import type { ReactNode } from 'react'
import type { Expense, MaintenanceType, MaintenanceWork, PartSource, TyreSeason, TyreSet, WorkPosition } from '../api/types'
import { newWorkId, POSITION_TYPES, WIPER_TYPES, type TyreDraft, type WorkDraft } from '../lib/expenseForm'
import { seasonLabel, tyreDisplayName } from '../lib/tyreSummary'
import { MAINTENANCE_GROUPS, MAINTENANCE_TYPES, maintenanceLabel } from '../lib/maintenanceCatalog'
import { previousParts, workKey } from '../lib/previousParts'

const OWNED = '__owned'
const SEASONS: TyreSeason[] = ['Winter', 'Summer', 'AllSeason']
const NEW_BRAND = 'new'
const PREVIOUS = 'prev:'

interface Props {
  work: WorkDraft[]
  onWork(w: WorkDraft[]): void
  tyres: TyreDraft
  onTyres(t: TyreDraft): void
  tyreSetId: string
  onTyreSetId(v: string): void
  tyreSets: TyreSet[]
  /** The stored expense already has a tyre set: only picking a set is allowed. */
  tyreLocked: boolean
  err(key: string): ReactNode
  /** Earlier maintenance records, for offering the products used last time. */
  history: Expense[]
  /** The record being edited, left out of the earlier products. */
  editingId: number | null
}

export function MaintenanceWorkEditor({ work, onWork, tyres, onTyres, tyreSetId, onTyreSetId, tyreSets, tyreLocked, err, history, editingId }: Props) {
  const find = (t: MaintenanceType) => work.find((w) => w.type === t)
  const patch = (id: string, p: Partial<WorkDraft>) => onWork(work.map((w) => (w.id === id ? { ...w, ...p } : w)))
  const toggle = (t: MaintenanceType) => {
    if (WIPER_TYPES.includes(t)) {
      const has = work.some((w) => w.type !== null && WIPER_TYPES.includes(w.type))
      onWork(has ? work.filter((w) => !(w.type !== null && WIPER_TYPES.includes(w.type)))
        : [...work, { id: newWorkId(), type: 'WiperBladesFront', name: '', part: 'None', brand: '', model: '', details: '' }])
      return
    }
    const w = find(t)
    onWork(w ? work.filter((x) => x.id !== w.id) : [...work, { id: newWorkId(), type: t, name: '', part: 'None', brand: '', model: '', details: '' }])
  }
  const legacy = work.filter((w) => w.type !== null && !MAINTENANCE_TYPES.includes(w.type))
  const custom = work.filter((w) => w.type === null)

  // One compact row per ticked item: part source (none, original, a product used before for this item, or a new brand) and details.
  const partsBlock = (w: WorkDraft, title: ReactNode, opts: {
    onPatch?: (p: Partial<WorkDraft>) => void; extra?: ReactNode; match?: (m: MaintenanceWork) => boolean
  } = {}) => {
    const doPatch = opts.onPatch ?? ((p: Partial<WorkDraft>) => patch(w.id, p))
    const used = previousParts(history, workKey(w.type, w.name), editingId, opts.match)
    const same = (p: { brand: string; model: string | null }) =>
      p.brand.toLowerCase() === w.brand.trim().toLowerCase() && (p.model ?? '').toLowerCase() === w.model.trim().toLowerCase()
    const usedIndex = w.part === 'Aftermarket' ? used.findIndex(same) : -1
    const choice = w.part !== 'Aftermarket' ? w.part : usedIndex >= 0 ? `${PREVIOUS}${usedIndex}` : NEW_BRAND
    const choose = (v: string) => {
      if (v === 'None' || v === 'Original') doPatch({ part: v as PartSource, brand: '', model: '' })
      else if (v === NEW_BRAND) doPatch({ part: 'Aftermarket', brand: '', model: '' })
      else { const p = used[Number(v.slice(PREVIOUS.length))]; doPatch({ part: 'Aftermarket', brand: p.brand, model: p.model ?? '' }) }
    }
    return (
      <div className="work-row">
        <div className="work-row-title">{title}</div>
        {opts.extra}
        <select aria-label="Part" value={choice} onChange={(e) => choose(e.target.value)}>
          <option value="None">No part</option>
          <option value="Original">Original (Volvo)</option>
          {used.length > 0 && (
            <optgroup label="Used before">
              {used.map((p, i) => <option key={i} value={`${PREVIOUS}${i}`}>{[p.brand, p.model].filter(Boolean).join(' ')}</option>)}
            </optgroup>
          )}
          <option value={NEW_BRAND}>Other brand…</option>
        </select>
        {choice === NEW_BRAND && (
          <>
            <input aria-label="Brand" placeholder="Brand" value={w.brand} onChange={(e) => doPatch({ brand: e.target.value })} />
            <input aria-label="Model / part no." placeholder="Model / part no." value={w.model} onChange={(e) => doPatch({ model: e.target.value })} />
          </>
        )}
        <input aria-label="Details" placeholder="Details" className="grow" value={w.details} onChange={(e) => doPatch({ details: e.target.value })} />
        {err(`work.${w.id}.brand`)}
      </div>
    )
  }

  // Brake pads, brake discs and suspension: which axle.
  const positionSelect = (w: WorkDraft) => (
    <select aria-label="Position" value={w.position ?? ''} onChange={(e) => patch(w.id, { position: (e.target.value || null) as WorkPosition | null })}>
      <option value="">Axle…</option>
      <option value="Front">Front</option>
      <option value="Rear">Rear</option>
      <option value="Both">Both</option>
    </select>
  )

  // Wiper blades: one row for the front and/or rear items, which share the product, change kind and details.
  const isWiper = (w: WorkDraft) => w.type !== null && WIPER_TYPES.includes(w.type)
  const wipers = work.filter(isWiper)
  const patchWipers = (p: Partial<WorkDraft>) => onWork(work.map((w) => (isWiper(w) ? { ...w, ...p } : w)))
  const setWiperPosition = (pos: WorkPosition) => {
    const template = wipers[0]
    const keep = (t: MaintenanceType): WorkDraft => ({ ...template, id: wipers.find((w) => w.type === t)?.id ?? newWorkId(), type: t })
    const next = pos === 'Front' ? [keep('WiperBladesFront')] : pos === 'Rear' ? [keep('WiperBladesRear')] : [keep('WiperBladesFront'), keep('WiperBladesRear')]
    onWork([...work.filter((w) => !isWiper(w)), ...next])
  }
  const wiperRow = () => {
    const w = wipers[0]
    const pos: WorkPosition = wipers.length > 1 ? 'Both' : w.type === 'WiperBladesRear' ? 'Rear' : 'Front'
    return partsBlock(w, 'Wiper blades', {
      onPatch: patchWipers,
      match: (m) => !!m.rubberOnly === !!w.rubberOnly,
      extra: (
        <>
          <select aria-label="Wiper position" value={pos} onChange={(e) => setWiperPosition(e.target.value as WorkPosition)}>
            <option value="Front">Front</option>
            <option value="Rear">Rear</option>
            <option value="Both">Both</option>
          </select>
          <select aria-label="Change" value={w.rubberOnly ? 'rubber' : 'blade'}
            onChange={(e) => patchWipers({ rubberOnly: e.target.value === 'rubber', part: 'None', brand: '', model: '' })}>
            <option value="blade">Whole blade</option>
            <option value="rubber">Rubber only</option>
          </select>
        </>
      ),
    })
  }

  const fitting = tyres.mode === 'new' || tyres.mode === 'owned'
  const setTyre = (p: Partial<TyreDraft>) => onTyres({ ...tyres, ...p })
  const tyreBlock = (w: WorkDraft, title: ReactNode) => (
    <div className="work-row">
      <div className="work-row-title">{title}</div>
      {!tyreLocked && (
        <label className="check">
          <input type="checkbox" checked={tyres.mode === 'new'}
            onChange={(e) => setTyre({ mode: e.target.checked ? 'new' : 'existing' })} /> New tyres
        </label>
      )}
      {tyres.mode !== 'new' && (
        <select aria-label="Tyres fitted" value={tyres.mode === 'owned' ? OWNED : tyreSetId}
          onChange={(e) => {
            if (e.target.value === OWNED) setTyre({ mode: 'owned' })
            else { setTyre({ mode: 'existing' }); onTyreSetId(e.target.value) }
          }}>
          {!tyreLocked && <option value="">Tyres fitted: not recorded</option>}
          {tyreSets.filter((s) => !s.retired || String(s.id) === tyreSetId).map((s) => (
            <option key={s.id} value={s.id}>{tyreDisplayName(s)} ({seasonLabel(s.season)}){s.retired ? ' (retired)' : ''}</option>
          ))}
          {!tyreLocked && <option value={OWNED}>+ Add a set I already own</option>}
        </select>
      )}
      {fitting && (
        <>
          <input aria-label="Brand" placeholder="Brand" value={tyres.brand} onChange={(e) => setTyre({ brand: e.target.value })} />
          <input aria-label="Model" placeholder="Model" value={tyres.model} onChange={(e) => setTyre({ model: e.target.value })} />
          <input aria-label="Size" placeholder="Size, e.g. 225/45R17" value={tyres.size} onChange={(e) => setTyre({ size: e.target.value })} />
          <select aria-label="Season" value={tyres.season} onChange={(e) => setTyre({ season: e.target.value as TyreSeason })}>
            {SEASONS.map((s) => <option key={s} value={s}>{seasonLabel(s)}</option>)}
          </select>
        </>
      )}
      <input aria-label="Details" placeholder="Details" className="grow" value={w.details} onChange={(e) => patch(w.id, { details: e.target.value })} />
    </div>
  )

  const removeLegacy = (w: WorkDraft) => onWork(work.filter((x) => x.id !== w.id))
  // Ticked checklist items in catalogue order, then legacy items, then custom works.
  const ticked = MAINTENANCE_TYPES.filter((t) => !WIPER_TYPES.includes(t)).map(find).filter((w): w is WorkDraft => !!w)
  // The wiper row sits where wiper blades come in the catalogue.
  const wiperAt = ticked.filter((w) => MAINTENANCE_TYPES.indexOf(w.type!) < MAINTENANCE_TYPES.indexOf('WiperBladesFront')).length

  return (
    <div className="wide work-editor">
      {MAINTENANCE_GROUPS.map((g) => (
        <div key={g.title} className="work-group" role="group" aria-label={g.title}>
          <div className="work-group-title">{g.title}</div>
          <div className="work-grid">
            {g.types.filter((t) => t !== 'WiperBladesRear').map((t) => (
              <label key={t} className="check">
                {t === 'WiperBladesFront'
                  ? <><input type="checkbox" checked={wipers.length > 0} onChange={() => toggle(t)} /> Wiper blades</>
                  : <><input type="checkbox" checked={!!find(t)} onChange={() => toggle(t)} /> {maintenanceLabel(t)}</>}
              </label>
            ))}
          </div>
        </div>
      ))}

      {(ticked.length > 0 || wipers.length > 0 || legacy.length > 0 || custom.length > 0) && <div className="work-group-title">Work done</div>}
      {ticked.map((w, i) => (
        <div key={w.id}>
          {i === wiperAt && wipers.length > 0 && wiperRow()}
          {w.type === 'TyreSwap' ? tyreBlock(w, maintenanceLabel('TyreSwap'))
            : partsBlock(w, maintenanceLabel(w.type as MaintenanceType), POSITION_TYPES.includes(w.type!) ? { extra: positionSelect(w) } : {})}
        </div>
      ))}
      {wiperAt >= ticked.length && wipers.length > 0 && <div>{wiperRow()}</div>}
      {legacy.map((w) => (
        <div key={w.id}>
          {partsBlock(w, (
            <label className="check">
              <input type="checkbox" checked onChange={() => removeLegacy(w)} /> {maintenanceLabel(w.type as MaintenanceType)} (legacy)
            </label>
          ))}
        </div>
      ))}
      {custom.map((w) => (
        <div key={w.id}>
          {partsBlock(w, (
            <span className="work-custom-name">
              <input aria-label="Custom work" placeholder="What was done" value={w.name} onChange={(e) => patch(w.id, { name: e.target.value })} />
              <button type="button" className="btn small" aria-label="Remove custom work" onClick={() => onWork(work.filter((x) => x.id !== w.id))}>✕</button>
              {err(`work.${w.id}.name`)}
            </span>
          ))}
        </div>
      ))}
      <button type="button" className="btn small" onClick={() => onWork([...work, { id: newWorkId(), type: null, name: '', part: 'None', brand: '', model: '', details: '' }])}>
        + Add custom work
      </button>
    </div>
  )
}
