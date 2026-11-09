import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { PeriodWithStats } from '../api/types'
import { FuelTrend } from '../charts/FuelTrend'
import { GapHistogram } from '../charts/GapHistogram'
import { Heatmap } from '../charts/Heatmap'
import { DpfIndexChart, MonthlyScore } from '../charts/MonthlyScore'
import { PenaltyCurve } from '../charts/PenaltyCurve'
import { ScoreMap } from '../charts/ScoreMap'
import { Seasonality } from '../charts/Seasonality'
import { TripInsights } from '../charts/TripInsights'
import { WhenYouDrive } from '../charts/WhenYouDrive'
import { TripDrawer } from '../components/TripDrawer'
import { TripList } from '../components/TripList'
import { iso } from '../lib/dates'
import { expensePeriods } from '../lib/expensePeriods'
import { parseTripsState, tripsStateToParams, TRIPS_TABS, type TripsTab, type TripsUrlState } from '../lib/urlState'
import { useAnalytics } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

const TAB_LABEL: Record<TripsTab, string> = {
  overview: 'Overview', fuel: 'Fuel', style: 'Driving style', list: 'List',
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
  const vehicle = useVehicle()
  const [today] = useState(() => iso(new Date()))
  // Same switch as Expenses: Total (since purchase), last 12 months, then every year with trips.
  const tripYears = useMemo(() => (yearRows ?? []).filter((r) => r.stats.usage.tripCount > 0).map((r) => Number(r.period.from.slice(0, 4))), [yearRows])
  const firstTripYear = tripYears.length > 0 ? `${Math.min(...tripYears)}-01-01` : null
  const periods = useMemo(() => expensePeriods(vehicle.purchaseDate ?? null, today, firstTripYear), [vehicle.purchaseDate, today, firstTripYear])
  const current = periods.find((p) => p.key === state.period) ?? periods[0]
  const range = { from: current.from, to: current.to }
  const [thisYear] = useState(() => new Date().getFullYear())
  const heatYear = state.heatYear ?? (/^\d{4}$/.test(current.key) ? Number(current.key) : null) ?? years[0] ?? thisYear

  return (
    <section>
      <h1>Trips</h1>
      <div className="toolbar">
        <div className="segmented" role="group" aria-label="Period">
          {periods.map((p) => (
            <button key={p.key} className={`btn small${p.key === current.key ? ' active' : ''}`} aria-pressed={p.key === current.key}
              onClick={() => update({ period: p.key })}>{p.label}</button>
          ))}
        </div>
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

      {(
        <div className="tab-body" key={`${range.from}-${range.to}`}>
          {state.tab === 'overview' && (
            <>
              <TripInsights {...range} kind="overview" />
              <MonthlyScore {...range} />
              <DpfIndexChart {...range} />
              <Heatmap year={heatYear} years={years} onYear={(y) => update({ heatYear: y })} />
            </>
          )}
          {state.tab === 'fuel' && (
            <>
              <TripInsights {...range} kind="fuel" />
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
          {state.tab === 'list' && <TripList from={range.from} to={range.to} />}
        </div>
      )}
      {tripId != null && <TripDrawer tripId={tripId} onClose={() => setTripId(null)} />}
    </section>
  )
}
