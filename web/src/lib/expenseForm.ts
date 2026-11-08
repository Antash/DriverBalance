import type { Expense, ExpenseCategory, MaintenanceType } from '../api/types'

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
  maintenance: MaintenanceType[]
  maintenanceDescription: string
  tyreSetId: string
}

export type ExpensePayload = Omit<Expense, 'id' | 'source'>

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
  return errors
}

export function pricePerLitre(totalPrice: number, litres: number): number | null {
  if (!Number.isFinite(totalPrice) || !Number.isFinite(litres) || litres <= 0) return null
  return totalPrice / litres
}

export function usesTyreSet(category: ExpenseCategory, maintenance: MaintenanceType[]): boolean {
  return category === 'Tyres' || (category === 'Maintenance' && maintenance.some((m) => MOUNT_TYPES.includes(m)))
}

export function buildExpensePayload(f: ExpenseFormState, homeCurrency: string): ExpensePayload {
  const fuel = f.category === 'Fuel'
  const maint = f.category === 'Maintenance'
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
    maintenance: maint ? [...f.maintenance] : [],
    maintenanceDescription: maint ? emptyToNull(f.maintenanceDescription) : null,
    tyreSetId: usesTyreSet(f.category, f.maintenance) ? num(f.tyreSetId) : null,
  }
}

export function toFormState(e: Expense): ExpenseFormState {
  return {
    date: e.date, category: e.category, amount: String(e.amount), currency: e.currency,
    homeAmount: e.homeAmount == null ? '' : String(e.homeAmount),
    odometer: e.odometer == null ? '' : String(e.odometer), note: e.note ?? '',
    coversFrom: e.coversFrom ?? '', coversTo: e.coversTo ?? '',
    litres: e.litres == null ? '' : String(e.litres), fullTank: e.fullTank ?? false, station: e.station ?? '',
    maintenance: [...e.maintenance], maintenanceDescription: e.maintenanceDescription ?? '',
    tyreSetId: e.tyreSetId == null ? '' : String(e.tyreSetId),
  }
}

export function emptyFormState(currency: string, today = new Date()): ExpenseFormState {
  const d = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
  return {
    date: d, category: 'Fuel', amount: '', currency, homeAmount: '', odometer: '', note: '', coversFrom: '', coversTo: '',
    litres: '', fullTank: true, station: '', maintenance: [], maintenanceDescription: '', tyreSetId: '',
  }
}
