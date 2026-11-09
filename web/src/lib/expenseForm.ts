import { iso } from './dates'
import type { Expense, ExpenseCategory, MaintenanceType, MaintenanceWork, PartSource, TyreSeason, WorkPosition } from '../api/types'

export interface WorkDraft {
  id: string
  type: MaintenanceType | null
  name: string
  part: PartSource
  brand: string
  model: string
  details: string
  position?: WorkPosition | null
  rubberOnly?: boolean
}

/** Work types that take a front / rear / both choice, and the wiper types that can be a rubber-only change. */
export const POSITION_TYPES: MaintenanceType[] = ['BrakePads', 'BrakeDiscs', 'Suspension']
export const WIPER_TYPES: MaintenanceType[] = ['WiperBladesFront', 'WiperBladesRear']

export interface TyreDraft {
  mode: 'existing' | 'owned' | 'new'
  brand: string
  model: string
  size: string
  season: TyreSeason
}

export interface TyresPayload { brand: string | null; model: string | null; size: string | null; season: TyreSeason; isNew: boolean }

export interface ExpenseFormState {
  date: string
  category: ExpenseCategory
  amount: string
  currency: string
  homeAmount: string
  odometer: string
  note: string
  coversFrom: string
  coversTo: string
  litres: string
  fullTank: boolean
  station: string
  place: string
  work: WorkDraft[]
  maintenanceDescription: string
  tyreSetId: string
  tyres: TyreDraft
}

export type ExpensePayload = Omit<Expense, 'id' | 'source' | 'documents'> & { tyres: TyresPayload | null }

export const newWorkId = (() => { let n = 0; return () => `w${++n}` })()
export const emptyTyres = (): TyreDraft => ({ mode: 'existing', brand: '', model: '', size: '', season: 'Summer' })

const isBlankCustom = (w: WorkDraft) =>
  w.type === null && w.name.trim() === '' && w.part === 'None' && w.brand.trim() === '' && w.model.trim() === '' && w.details.trim() === ''
const typedTypes = (work: WorkDraft[]): MaintenanceType[] => work.flatMap((w) => (w.type ? [w.type] : []))

export const MOUNT_TYPES: MaintenanceType[] = ['TyreSwap', 'NewTyres']

export const emptyToNull = (v: string): string | null => (v.trim() === '' ? null : v.trim())
/** Czech-style input: spaces/NBSP are thousands separators, comma is the decimal mark. Blank or malformed gives null. */
export function parseDecimal(input: string): number | null {
  const t = input.replace(/[\s ]/g, '').replace(',', '.')
  return /^\d+(\.\d+)?$/.test(t) ? Number(t) : null
}

const num = (v: string): number | null => parseDecimal(v)
const isHome = (currency: string, home: string) => currency.trim().toUpperCase() === home.toUpperCase()

/** Field name -> message for every visible field that cannot be parsed. Empty means the form may be submitted. */
export function validateExpenseForm(f: ExpenseFormState, homeCurrency: string): Record<string, string> {
  const errors: Record<string, string> = {}
  const bad = (v: string) => v.trim() !== '' && parseDecimal(v) === null
  if (f.amount.trim() === '') errors.amount = 'Amount is required.'
  else if (parseDecimal(f.amount) === null) errors.amount = 'Enter a number, e.g. 1 500 or 12,50.'
  if (f.category === 'Fuel' && bad(f.litres)) errors.litres = 'Enter a number, e.g. 40,5.'
  if (!isHome(f.currency, homeCurrency) && bad(f.homeAmount)) errors.homeAmount = 'Enter a number, e.g. 1 500 or 12,50.'
  if (f.odometer.trim() !== '' && !Number.isInteger(parseDecimal(f.odometer))) errors.odometer = 'Enter a whole number of km.'
  if (f.category === 'Maintenance') {
    if (!f.work.some((w) => w.type !== null || w.name.trim() !== '')) errors.work = 'Tick at least one item or add a custom work.'
    for (const w of f.work) {
      if (w.type === null && w.name.trim() === '' && !isBlankCustom(w)) errors[`work.${w.id}.name`] = 'Name the work.'
      if (w.type !== 'TyreSwap' && w.part === 'Aftermarket' && w.brand.trim() === '') errors[`work.${w.id}.brand`] = 'Enter the brand.'
    }
  }
  return errors
}

export function pricePerLitre(totalPrice: number, litres: number): number | null {
  if (!Number.isFinite(totalPrice) || !Number.isFinite(litres) || litres <= 0) return null
  return totalPrice / litres
}

export function usesTyreSet(category: ExpenseCategory, maintenance: readonly MaintenanceType[]): boolean {
  return category === 'Tyres' || (category === 'Maintenance' && maintenance.some((m) => MOUNT_TYPES.includes(m)))
}

function toWork(w: WorkDraft): MaintenanceWork {
  if (w.type === 'TyreSwap') return { type: w.type, name: null, part: 'None', brand: null, model: null, details: emptyToNull(w.details), position: null, rubberOnly: false }
  const after = w.part === 'Aftermarket'
  return {
    type: w.type, name: w.type === null ? w.name.trim() : null, part: w.part,
    brand: after ? emptyToNull(w.brand) : null, model: after ? emptyToNull(w.model) : null, details: emptyToNull(w.details),
    position: w.type && POSITION_TYPES.includes(w.type) ? (w.position ?? null) : null,
    rubberOnly: !!w.type && WIPER_TYPES.includes(w.type) && !!w.rubberOnly,
  }
}

export function buildExpensePayload(f: ExpenseFormState, homeCurrency: string): ExpensePayload {
  const fuel = f.category === 'Fuel'
  const maint = f.category === 'Maintenance'
  const work = maint ? f.work.filter((w) => !isBlankCustom(w)) : []
  const types = typedTypes(work)
  const tyreSwap = maint && types.includes('TyreSwap')
  const fitNew = tyreSwap && f.tyres.mode !== 'existing'
  const usesSet = usesTyreSet(f.category, types)
  return {
    date: f.date,
    category: f.category,
    amount: parseDecimal(f.amount) ?? 0, // validateExpenseForm rejects anything unparsable before submit
    currency: f.currency.trim().toUpperCase(),
    homeAmount: isHome(f.currency, homeCurrency) ? null : num(f.homeAmount),
    odometer: num(f.odometer),
    note: emptyToNull(f.note),
    coversFrom: emptyToNull(f.coversFrom),
    coversTo: emptyToNull(f.coversTo),
    litres: fuel ? num(f.litres) : null,
    fullTank: fuel ? f.fullTank : null,
    station: fuel ? emptyToNull(f.station) : null,
    place: fuel ? null : emptyToNull(f.place),
    maintenance: types,
    work: work.map(toWork),
    maintenanceDescription: maint ? emptyToNull(f.maintenanceDescription) : null,
    tyreSetId: usesSet && !fitNew ? num(f.tyreSetId) : null,
    tyres: fitNew
      ? { brand: emptyToNull(f.tyres.brand), model: emptyToNull(f.tyres.model), size: emptyToNull(f.tyres.size), season: f.tyres.season, isNew: f.tyres.mode === 'new' }
      : null,
  }
}

export function toFormState(e: Expense): ExpenseFormState {
  return {
    date: e.date, category: e.category, amount: String(e.amount), currency: e.currency,
    homeAmount: e.homeAmount == null ? '' : String(e.homeAmount),
    odometer: e.odometer == null ? '' : String(e.odometer), note: e.note ?? '',
    coversFrom: e.coversFrom ?? '', coversTo: e.coversTo ?? '',
    litres: e.litres == null ? '' : String(e.litres), fullTank: e.fullTank ?? false, station: e.station ?? '', place: e.place ?? '',
    work: e.work.length > 0
      ? e.work.map((w) => w.type === 'TyreSwap'
        ? { id: newWorkId(), type: w.type, name: '', part: 'None' as const, brand: '', model: '', details: w.details ?? '' }
        : { id: newWorkId(), type: w.type, name: w.name ?? '', part: w.part, brand: w.brand ?? '', model: w.model ?? '', details: w.details ?? '',
          position: w.position ?? null, rubberOnly: w.rubberOnly ?? false })
      : e.maintenance.map((t) => ({ id: newWorkId(), type: t, name: '', part: 'None' as const, brand: '', model: '', details: '' })),
    maintenanceDescription: e.maintenanceDescription ?? '',
    tyreSetId: e.tyreSetId == null ? '' : String(e.tyreSetId), tyres: emptyTyres(),
  }
}

export function emptyFormState(currency: string, today = new Date()): ExpenseFormState {
  const d = iso(today)
  return {
    date: d, category: 'Fuel', amount: '', currency, homeAmount: '', odometer: '', note: '', coversFrom: '', coversTo: '',
    litres: '', fullTank: true, station: '', place: '', work: [], maintenanceDescription: '', tyreSetId: '', tyres: emptyTyres(),
  }
}
