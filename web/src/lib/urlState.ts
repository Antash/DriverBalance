import { isIsoDate } from './dates'

export interface StatsUrlState {
  mode: 'calendar' | 'custom'
  year: number | null
  month: number | null
  week: string | null
  /** Custom range as shown in the UI: "to" is inclusive. */
  from: string | null
  to: string | null
  cmpFrom: string | null
  cmpTo: string | null
}

export const defaultStatsState: StatsUrlState = {
  mode: 'calendar', year: null, month: null, week: null, from: null, to: null, cmpFrom: null, cmpTo: null,
}

const intIn = (v: string | null, lo: number, hi: number) => {
  if (v == null || !/^\d+$/.test(v)) return null
  const n = Number(v)
  return n >= lo && n <= hi ? n : null
}
const date = (v: string | null) => (isIsoDate(v) && !Number.isNaN(Date.parse(v)) ? v : null)

export function parseStatsState(p: URLSearchParams): StatsUrlState {
  return {
    mode: p.get('mode') === 'custom' ? 'custom' : 'calendar',
    year: intIn(p.get('year'), 1, 9998),
    month: intIn(p.get('month'), 1, 12),
    week: date(p.get('week')),
    from: date(p.get('from')),
    to: date(p.get('to')),
    cmpFrom: date(p.get('cmpFrom')),
    cmpTo: date(p.get('cmpTo')),
  }
}

export function statsStateToParams(s: StatsUrlState): URLSearchParams {
  const p = new URLSearchParams()
  if (s.mode !== 'calendar') p.set('mode', s.mode)
  if (s.year != null) p.set('year', String(s.year))
  if (s.month != null) p.set('month', String(s.month))
  if (s.week) p.set('week', s.week)
  if (s.from) p.set('from', s.from)
  if (s.to) p.set('to', s.to)
  if (s.cmpFrom) p.set('cmpFrom', s.cmpFrom)
  if (s.cmpTo) p.set('cmpTo', s.cmpTo)
  return p
}

// ---- Trips page ----

export type TripsTab = 'overview' | 'fuel' | 'style' | 'list'
export const TRIPS_TABS: TripsTab[] = ['overview', 'fuel', 'style', 'list']

export interface TripsUrlState {
  tab: TripsTab
  level: 'Group' | 'Trip'
  /** 'total' (since purchase), 'last12' (past 365 days) or a calendar year such as '2025'. */
  period: string
  /** Year shown by the calendar heatmap. */
  heatYear: number | null
}

export const defaultTripsState: TripsUrlState = { tab: 'overview', level: 'Group', period: 'total', heatYear: null }

export function parseTripsState(p: URLSearchParams): TripsUrlState {
  const tab = p.get('tab') as TripsTab
  const period = p.get('period') ?? ''
  return {
    tab: TRIPS_TABS.includes(tab) ? tab : 'overview',
    level: p.get('level') === 'Trip' ? 'Trip' : 'Group',
    period: /^(total|last12|\d{4})$/.test(period) ? period : 'total',
    heatYear: intIn(p.get('hy'), 1, 9998),
  }
}

export function tripsStateToParams(s: TripsUrlState): URLSearchParams {
  const p = new URLSearchParams()
  if (s.tab !== 'overview') p.set('tab', s.tab)
  if (s.level !== 'Group') p.set('level', s.level)
  if (s.period !== 'total') p.set('period', s.period)
  if (s.heatYear != null) p.set('hy', String(s.heatYear))
  return p
}
