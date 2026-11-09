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

  const partsBlock = (w: WorkDraft) => (
    <div className="work-item">
      <div className="row">
        {PARTS.map((p) => (
          <label key={p.value} className="check">
            <input type="radio" name={`part-${w.id}`} checked={w.part === p.value} onChange={() => patch(w.id, { part: p.value })} /> {p.label}
          </label>
        ))}
      </div>
      {w.part === 'Aftermarket' && (
        <div className="row">
          <label>Brand<input value={w.brand} onChange={(e) => patch(w.id, { brand: e.target.value })} />{err(`work.${w.id}.brand`)}</label>
          <label>Model / part no.<input value={w.model} onChange={(e) => patch(w.id, { model: e.target.value })} /></label>
        </div>
      )}
      <label>Details<input value={w.details} onChange={(e) => patch(w.id, { details: e.target.value })} /></label>
    </div>
  )

  const fitting = tyres.mode === 'new' || tyres.mode === 'owned'
  const setTyre = (p: Partial<TyreDraft>) => onTyres({ ...tyres, ...p })
  const tyreBlock = (w: WorkDraft) => (
    <div className="work-item">
      {!tyreLocked && (
        <label className="check">
          <input type="checkbox" checked={tyres.mode === 'new'}
            onChange={(e) => setTyre({ mode: e.target.checked ? 'new' : 'existing' })} /> New tyres
        </label>
      )}
      {tyres.mode !== 'new' && (
        <label>Tyres fitted
          <select value={tyres.mode === 'owned' ? OWNED : tyreSetId}
            onChange={(e) => {
              if (e.target.value === OWNED) setTyre({ mode: 'owned' })
              else { setTyre({ mode: 'existing' }); onTyreSetId(e.target.value) }
            }}>
            {!tyreLocked && <option value="">- none yet -</option>}
            {tyreSets.filter((s) => !s.retired || String(s.id) === tyreSetId).map((s) => (
              <option key={s.id} value={s.id}>{tyreDisplayName(s)} ({seasonLabel(s.season)}){s.retired ? ' (retired)' : ''}</option>
            ))}
            {!tyreLocked && <option value={OWNED}>+ Add a set I already own</option>}
          </select>
        </label>
      )}
      {fitting && (
        <div className="row">
          <label>Brand<input value={tyres.brand} onChange={(e) => setTyre({ brand: e.target.value })} /></label>
          <label>Model<input value={tyres.model} onChange={(e) => setTyre({ model: e.target.value })} /></label>
          <label>Size<input value={tyres.size} placeholder="225/45R17" onChange={(e) => setTyre({ size: e.target.value })} /></label>
          <label>Season
            <select value={tyres.season} onChange={(e) => setTyre({ season: e.target.value as TyreSeason })}>
              {SEASONS.map((s) => <option key={s} value={s}>{seasonLabel(s)}</option>)}
            </select>
          </label>
        </div>
      )}
      <label>Details<input value={w.details} onChange={(e) => patch(w.id, { details: e.target.value })} /></label>
    </div>
  )

  const body = (w: WorkDraft) => (w.type === 'TyreSwap' ? tyreBlock(w) : partsBlock(w))
  const removeLegacy = (w: WorkDraft) => onWork(work.filter((x) => x.id !== w.id))

  return (
    <div className="wide">
      {MAINTENANCE_GROUPS.map((g) => (
        <fieldset key={g.title} className="group"><legend>{g.title}</legend>
          {g.types.map((t) => {
            const w = find(t)
            return (
              <div key={t}>
                <label className="check">
                  <input type="checkbox" checked={!!w} onChange={() => toggle(t)} /> {maintenanceLabel(t)}
                </label>
                {w && body(w)}
              </div>
            )
          })}
        </fieldset>
      ))}
      {legacy.length > 0 && (
        <fieldset className="group"><legend>Legacy</legend>
          {legacy.map((w) => (
            <div key={w.id}>
              <label className="check">
                <input type="checkbox" checked onChange={() => removeLegacy(w)} /> {maintenanceLabel(w.type as MaintenanceType)} (legacy)
              </label>
              {partsBlock(w)}
            </div>
          ))}
        </fieldset>
      )}
      <fieldset className="group"><legend>Custom work</legend>
        {custom.map((w) => (
          <div key={w.id} className="work-item">
            <div className="row">
              <label>Name<input value={w.name} onChange={(e) => patch(w.id, { name: e.target.value })} />{err(`work.${w.id}.name`)}</label>
              <button type="button" className="btn" onClick={() => onWork(work.filter((x) => x.id !== w.id))}>Remove</button>
            </div>
            {partsBlock(w)}
          </div>
        ))}
        <button type="button" className="btn" onClick={() => onWork([...work, { id: newWorkId(), type: null, name: '', part: 'None', brand: '', model: '', details: '' }])}>
          + Add custom work
        </button>
      </fieldset>
    </div>
  )
}
