import { describe, expect, it } from 'vitest'
import {
  defaultTripsState, parseStatsState, parseTripsState, statsStateToParams, tripsStateToParams,
  type StatsUrlState, type TripsUrlState,
} from './urlState'

const empty: StatsUrlState = { mode: 'calendar', year: null, month: null, week: null, from: null, to: null, cmpFrom: null, cmpTo: null }

describe('statistics url state', () => {
  it('round-trips every field', () => {
    const full: StatsUrlState = {
      mode: 'custom', year: 2025, month: 3, week: '2025-03-10', from: '2025-01-01', to: '2025-02-28', cmpFrom: '2024-01-01', cmpTo: '2024-02-29',
    }
    expect(parseStatsState(new URLSearchParams(statsStateToParams(full).toString()))).toEqual(full)
  })

  it('round-trips a calendar drill-down', () => {
    const s: StatsUrlState = { ...empty, year: 2024, month: 11 }
    expect(parseStatsState(statsStateToParams(s))).toEqual(s)
  })

  it('writes nothing for the default state and parses an empty query to it', () => {
    expect(statsStateToParams(empty).toString()).toBe('')
    expect(parseStatsState(new URLSearchParams(''))).toEqual(empty)
  })

  it('drops malformed values instead of passing them on', () => {
    const p = new URLSearchParams('mode=bogus&year=abc&month=13&week=nope&from=2025-1-1&to=2025-02-30x')
    expect(parseStatsState(p)).toEqual(empty)
  })
})

describe('trips url state', () => {
  it('round-trips and defaults to the overview', () => {
    const s: TripsUrlState = { tab: 'style', level: 'Trip', range: 'custom', year: 2024, from: '2024-02-01', to: '2024-03-31', heatYear: 2023 }
    expect(parseTripsState(tripsStateToParams(s))).toEqual(s)
    expect(tripsStateToParams(defaultTripsState).toString()).toBe('')
    expect(parseTripsState(new URLSearchParams('tab=zzz&range=x'))).toEqual(defaultTripsState)
  })
})
