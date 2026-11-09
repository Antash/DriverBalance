import type { ReactNode } from 'react'
import type { MaintenanceType, PartSource, TyreSeason, TyreSet } from '../api/types'
import { newWorkId, type TyreDraft, type WorkDraft } from '../lib/expenseForm'
import { seasonLabel, tyreDisplayName } from '../lib/tyreSummary'
import { MAINTENANCE_GROUPS, MAINTENANCE_TYPES, maintenanceLabel } from '../lib/maintenanceCatalog'

const OWNED = '__owned'
const SEASONS: TyreSeason[] = ['Winter', 'Summer', 'AllSeason']
const PARTS: { value: PartSource; label: string }[] = [
  { value: 'None', label: 'No part' }, { value: 'Original', label: 'Original (Volvo)' }, { value: 'Aftermarket', label: 'Other brand' },
]

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
}

export function MaintenanceWorkEditor({ work, onWork, tyres, onTyres, tyreSetId, onTyreSetId, tyreSets, tyreLocked, err }: Props) {
  const find = (t: MaintenanceType) => work.find((w) => w.type === t)
  const patch = (id: string, p: Partial<WorkDraft>) => onWork(work.map((w) => (w.id === id ? { ...w, ...p } : w)))
  const toggle = (t: MaintenanceType) => {
    const w = find(t)
    onWork(w ? work.filter((x) => x.id !== w.id) : [...work, { id: newWorkId(), type: t, name: '', part: 'None', brand: '', model: '', details: '' }])
  }
  const legacy = work.filter((w) => w.type !== null && !MAINTENANCE_TYPES.includes(w.type))
  const custom = work.filter((w) => w.type === null)

  // One compact row per ticked item: part source, brand/model when another brand, details.
  const partsBlock = (w: WorkDraft, title: ReactNode) => (
    <div className="work-row">
      <div className="work-row-title">{title}</div>
      <select aria-label="Part" value={w.part} onChange={(e) => patch(w.id, { part: e.target.value as PartSource })}>
        {PARTS.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
      </select>
      {w.part === 'Aftermarket' && (
        <>
          <input aria-label="Brand" placeholder="Brand" value={w.brand} onChange={(e) => patch(w.id, { brand: e.target.value })} />
          <input aria-label="Model / part no." placeholder="Model / part no." value={w.model} onChange={(e) => patch(w.id, { model: e.target.value })} />
        </>
      )}
      <input aria-label="Details" placeholder="Details" className="grow" value={w.details} onChange={(e) => patch(w.id, { details: e.target.value })} />
      {err(`work.${w.id}.brand`)}
    </div>
  )

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
  const ticked = MAINTENANCE_TYPES.map(find).filter((w): w is WorkDraft => !!w)

  return (
    <div className="wide work-editor">
      {MAINTENANCE_GROUPS.map((g) => (
        <div key={g.title} className="work-group" role="group" aria-label={g.title}>
          <div className="work-group-title">{g.title}</div>
          <div className="work-grid">
            {g.types.map((t) => (
              <label key={t} className="check">
                <input type="checkbox" checked={!!find(t)} onChange={() => toggle(t)} /> {maintenanceLabel(t)}
              </label>
            ))}
          </div>
        </div>
      ))}

      {(ticked.length > 0 || legacy.length > 0 || custom.length > 0) && <div className="work-group-title">Work done</div>}
      {ticked.map((w) => (
        <div key={w.id}>
          {w.type === 'TyreSwap' ? tyreBlock(w, maintenanceLabel('TyreSwap')) : partsBlock(w, maintenanceLabel(w.type as MaintenanceType))}
        </div>
      ))}
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
