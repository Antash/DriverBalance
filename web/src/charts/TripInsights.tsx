import type { PeriodStats } from '../api/types'
import { Tile } from '../components/Tile'
import { fmtDate, fmtDuration, fmtLPer100, fmtNum } from '../lib/format'
import { useAnalytics } from '../state/ParamsContext'
import type { RangeProps } from './shared'

/** Headline numbers for the Trips tabs: "overview" (days active, longest journey) or "fuel" (fuel burned). */
export function TripInsights({ from, to, kind }: Required<RangeProps> & { kind: 'overview' | 'fuel' }) {
  const { data } = useAnalytics<PeriodStats[]>('stats', { periods: [{ from, to, label: 'range' }] })
  const s = data?.[0]
  if (!s || s.usage.tripCount === 0) return null
  const u = s.usage
  const j = u.longestJourney
  const sameDay = j && j.start.slice(0, 10) === j.end.slice(0, 10)
  return (
    <section className="card">
      <h2>Insights</h2>
      <div className="kpis">
        {kind === 'overview' ? <>
          <Tile name="Distance" value={`${fmtNum(u.tripKm, 0)} km`} sub={`${fmtNum(u.tripCount, 0)} trips`} />
          <Tile name="Days active" value={fmtNum(u.daysUsed, 0)} sub={`of ${fmtNum(u.elapsedDays, 0)} days · ${fmtNum(u.kmPerDayUsed, 0)} km per active day`} />
          <Tile name="Driving time" value={`${fmtNum(u.drivingHours, 0)} h`} sub={`${fmtNum(u.averageSpeedKmh, 0)} km/h average`} />
          {j && <Tile name="Longest journey" value={`${fmtNum(j.km, 0)} km`}
            sub={`${sameDay ? fmtDate(j.start) : `${fmtDate(j.start)} – ${fmtDate(j.end)}`} · ${fmtDuration(j.drivingTime)} driving · ${j.tripCount} trip${j.tripCount === 1 ? '' : 's'}`} />}
        </> : <>
          <Tile name="Fuel burned" value={`${fmtNum(s.fuel.tripLiters, 0)} L`} sub={`over ${fmtNum(u.tripKm, 0)} km`} />
          <Tile name="Average consumption" value={fmtLPer100(s.fuel.tripLPer100)} sub="from trip data" />
          {u.daysUsed > 0 && <Tile name="Per active day" value={`${fmtNum(s.fuel.tripLiters / u.daysUsed, 1)} L`} />}
        </>}
      </div>
    </section>
  )
}
