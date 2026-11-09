import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Expense, PeriodWithStats } from '../api/types'
import { IntervalTrend } from '../charts/IntervalTrend'
import { SmallMultiples } from '../charts/SmallMultiples'
import { ChartCard } from '../components/ChartCard'
import { CompareTable } from '../components/CompareTable'
import { MultiCompareTable } from '../components/MultiCompareTable'
import { StatsTable } from '../components/StatsTable'
import { addDays } from '../lib/dates'
import { averageStats } from '../lib/compare'
import { allIntervalsWindow, intervalWindow } from '../lib/intervals'
import { isPreviousOwnerInterval } from '../lib/ownership'
import { useAnalytics, useParams } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

/** Inclusive-to display of a half-open interval. */
const describe = (p: PeriodWithStats['period']) => `${p.label} (${p.from} → ${addDays(p.to, -1)})`

type View = 'all' | 'avg' | 'last'
const VIEWS: { key: View; label: string }[] = [
  { key: 'all', label: 'All' }, { key: 'avg', label: 'Current vs average' }, { key: 'last', label: 'Current vs last' },
]

function Compare({ currency }: { currency: string }) {
  const purchaseDate = useVehicle().purchaseDate
  const { data: intervals, isLoading, isError } = useAnalytics<PeriodWithStats[]>('intervals', { type: 'OilChange' })
  const [view, setView] = useState<View>('all')
  // Newest first: [current, last completed, ...]. The average covers completed intervals with trip data only.
  const current = intervals?.[0]
  const last = intervals?.[1]
  const completedWithTrips = useMemo(() => (intervals ?? []).slice(1).filter((p) => p.stats.usage.tripCount > 0), [intervals])
  const average = useMemo(() => (completedWithTrips.length > 0 ? averageStats(completedWithTrips.map((p) => p.stats)) : null), [completedWithTrips])

  if (isError) return <p className="field-error">Could not load intervals.</p>
  if (isLoading && !intervals) return <p className="muted">Loading…</p>
  if (!intervals || !current) return <p className="muted empty">No intervals for this type.</p>

  const win = view === 'last' && last
    ? (({ from, to, serviceDate }) => ({ from, to, serviceDates: serviceDate ? [serviceDate] : [] }))(intervalWindow(last.period, current.period))
    : allIntervalsWindow(intervals.map((p) => p.period))!

  return (
    <>
      {intervals.length > 1 && (
        <div className="toolbar">
          <div className="segmented" role="group" aria-label="View">
            {VIEWS.map((v) => (
              <button key={v.key} className={`btn small${view === v.key ? ' active' : ''}`} aria-pressed={view === v.key}
                onClick={() => setView(v.key)}>{v.label}</button>
            ))}
          </div>
        </div>
      )}

      {intervals.length === 1 ? (
        <section className="card"><h2>{describe(current.period)}</h2><StatsTable stats={current.stats} currency={currency} /></section>
      ) : view === 'all' ? (
        <section className="card">
          <h2>Every oil change interval</h2>
          <MultiCompareTable intervals={intervals} currency={currency} purchaseDate={purchaseDate} />
        </section>
      ) : view === 'avg' ? (
        <section className="card">
          <h2>Current interval vs the average</h2>
          {average ? (
            <>
              <p className="muted hint">Average of {completedWithTrips.length} completed interval{completedWithTrips.length === 1 ? '' : 's'} with trip data.</p>
              <CompareTable a={average} b={current.stats} currency={currency}
                labelA={`Average (${completedWithTrips.length})`} labelB={current.period.label} />
            </>
          ) : <p className="muted empty">No completed interval has trip data yet.</p>}
        </section>
      ) : (
        <section className="card">
          <h2>Current interval vs the last one</h2>
          {last && <CompareTable a={last.stats} b={current.stats} currency={currency} labelA={last.period.label} labelB={current.period.label}
            prevA={isPreviousOwnerInterval(last.period.to, purchaseDate)} prevB={isPreviousOwnerInterval(current.period.to, purchaseDate)} />}
        </section>
      )}

      <IntervalTrend from={win.from} to={win.to} serviceDates={win.serviceDates} />
      <SmallMultiples intervals={intervals} currency={currency} />
    </>
  )
}

/** Engine oil change intervals only; other maintenance has its own page. */
export default function IntervalsPage() {
  const { vehicleId } = useParams()
  const currency = useVehicle().homeCurrency
  const { data: expenses, isLoading, isError } = useQuery({
    queryKey: ['expenses', vehicleId, 'maintenance'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?category=Maintenance`),
  })
  const hasOil = (expenses ?? []).some((e) => e.maintenance.includes('OilChange'))

  return (
    <section>
      <h1>Oil change intervals</h1>
      {isError ? <p className="field-error">Could not load maintenance history.</p>
        : isLoading ? <p className="muted">Loading…</p>
        : !hasOil ? (
          <ChartCard title="No oil changes recorded" empty emptyText="Add a maintenance expense with Engine oil ticked to compare intervals.">{null}</ChartCard>
        ) : <Compare currency={currency} />}
    </section>
  )
}
