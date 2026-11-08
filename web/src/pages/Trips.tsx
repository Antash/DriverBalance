import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { PeriodWithStats } from '../api/types'
import { FuelTrend } from '../charts/FuelTrend'
import { GapHistogram } from '../charts/GapHistogram'
import { Heatmap } from '../charts/Heatmap'
import { MonthlyScore } from '../charts/MonthlyScore'
import { PenaltyCurve } from '../charts/PenaltyCurve'
import { Places } from '../charts/Places'
import { Routes } from '../charts/Routes'
import { ScoreMap } from '../charts/ScoreMap'
import { Seasonality } from '../charts/Seasonality'
import { WhenYouDrive } from '../charts/WhenYouDrive'
import { TripDrawer } from '../components/TripDrawer'
import { TripList } from '../components/TripList'
import { addDays } from '../lib/dates'
import { parseTripsState, tripsStateToParams, TRIPS_TABS, type TripsTab, type TripsUrlState } from '../lib/urlState'
import { useAnalytics } from '../state/ParamsContext'

const TAB_LABEL: Record<TripsTab, string> = {
  overview: 'Overview', fuel: 'Fuel', style: 'Driving style', places: 'Places', list: 'List',
}

/** Half-open range for the charts, or undefined bounds for all data. */
function tripsRange(s: TripsUrlState, latestYear: number | undefined): { from?: string; to?: string } {
  if (s.range === 'year') {
    const y = s.year ?? latestYear
    return y == null ? {} : { from: `${y}-01-01`, to: `${y + 1}-01-01` }
  }
  if (s.range === 'custom' && s.from && s.to) return { from: s.from, to: addDays(s.to, 1) }
  return {}
}

export default function TripsPage() {
  const [sp, setSp] = useSearchParams()
  const state = useMemo(() => parseTripsState(sp), [sp])
  const update = (patch: Partial<TripsUrlState>) => setSp(tripsStateToParams({ ...state, ...patch }), { replace: true })
  const [tripId, setTripId] = useState<number | null>(null)

  const { data: yearRows } = useAnalytics<PeriodWithStats[]>('calendar', { level: 'years' })
  const years = useMemo(
    () => (yearRows ?? []).map((r) => Number(r.period.from.slice(0, 4))).sort((a, b) => b - a),
    [yearRows],
  )
  const range = tripsRange(state, years[0])
  const [thisYear] = useState(() => new Date().getFullYear())
  const heatYear = state.heatYear ?? (state.range === 'year' ? state.year : null) ?? years[0] ?? thisYear
  const customIncomplete = state.range === 'custom' && !(state.from && state.to)

  return (
    <section>
      <h1>Trips</h1>
      <div className="toolbar">
        <label className="inline">Period
          <select aria-label="Period" value={state.range} onChange={(e) => update({ range: e.target.value as TripsUrlState['range'] })}>
            <option value="all">All time</option>
            <option value="year">Year</option>
            <option value="custom">Custom range</option>
          </select>
        </label>
        {state.range === 'year' && (
          <select aria-label="Year" value={state.year ?? years[0] ?? ''} onChange={(e) => update({ year: Number(e.target.value) })}>
            {years.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        )}
        {state.range === 'custom' && (
          <>
            <input type="date" aria-label="From" value={state.from ?? ''} onChange={(e) => update({ from: e.target.value || null })} />
            <input type="date" aria-label="To (inclusive)" value={state.to ?? ''} onChange={(e) => update({ to: e.target.value || null })} />
          </>
        )}
        <div className="segmented" role="group" aria-label="Level">
          {(['Group', 'Trip'] as const).map((l) => (
            <button key={l} className={`btn small${state.level === l ? ' active' : ''}`} aria-pressed={state.level === l}
              onClick={() => update({ level: l })}>{l}</button>
          ))}
        </div>
      </div>

      <div className="tabs" role="tablist">
        {TRIPS_TABS.map((t) => (
          <button key={t} role="tab" aria-selected={state.tab === t} className={state.tab === t ? 'active' : undefined}
            onClick={() => update({ tab: t })}>{TAB_LABEL[t]}</button>
        ))}
      </div>

      {customIncomplete ? <p className="muted">Pick a from and to date.</p> : (
        <div className="tab-body" key={`${state.range}-${range.from}-${range.to}`}>
          {state.tab === 'overview' && (
            <>
              <MonthlyScore {...range} />
              <Heatmap year={heatYear} years={years} onYear={(y) => update({ heatYear: y })} />
            </>
          )}
          {state.tab === 'fuel' && (
            <>
              <FuelTrend {...range} level={state.level} onTrip={setTripId} />
              <div className="chart-grid">
                <PenaltyCurve {...range} />
                <Seasonality />
              </div>
            </>
          )}
          {state.tab === 'style' && (
            <>
              <ScoreMap {...range} level={state.level} onTrip={setTripId} />
              <div className="chart-grid">
                <GapHistogram {...range} />
                <WhenYouDrive {...range} />
              </div>
            </>
          )}
          {state.tab === 'places' && (
            <>
              <Routes {...range} />
              <Places {...range} />
            </>
          )}
          {state.tab === 'list' && <TripList from={range.from} to={range.to} />}
        </div>
      )}
      {tripId != null && <TripDrawer tripId={tripId} onClose={() => setTripId(null)} />}
    </section>
  )
}
