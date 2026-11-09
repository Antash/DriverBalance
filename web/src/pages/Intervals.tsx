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
import { allIntervalsWindow, intervalWindow, pickDefaultIntervals } from '../lib/intervals'
import { useAnalytics, useParams } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

/** Inclusive-to display of a half-open interval. */
const describe = (p: PeriodWithStats['period']) => `${p.label} (${p.from} → ${addDays(p.to, -1)})`

type View = 'two' | 'all'

function Compare({ currency }: { currency: string }) {
  const { data, isLoading, isError } = useAnalytics<PeriodWithStats[]>('intervals', { type: 'OilChange' })
  const intervals = data
  const [cur, prev] = useMemo(() => pickDefaultIntervals(intervals ?? []), [intervals])
  // Indices into the newest-first list; A is the earlier interval, B the later one.
  const [pick, setPick] = useState<{ a: number; b: number | null } | null>(null)
  const a = pick?.a ?? (prev ? 1 : 0)
  const b = pick ? pick.b : prev ? 0 : null
  const ia = intervals?.[a], ib = b == null ? undefined : intervals?.[b]
  const canShowAll = (intervals?.length ?? 0) > 1
  const [view, setView] = useState<View>('two')
  const all = canShowAll && view === 'all'
  const pair = ia ? intervalWindow(ia.period, ib?.period ?? null) : null
  const win = !pair ? null
    : all ? allIntervalsWindow(intervals!.map((p) => p.period))!
    : { from: pair.from, to: pair.to, serviceDates: pair.serviceDate ? [pair.serviceDate] : [] }

  if (isError) return <p className="field-error">Could not load intervals.</p>
  if (isLoading && !intervals) return <p className="muted">Loading…</p>
  if (!intervals || !cur || !ia || !win) return <p className="muted empty">No intervals for this type.</p>

  return (
    <>
      {canShowAll && (
        <div className="toolbar">
          <div className="segmented" role="group" aria-label="View">
            {(['two', 'all'] as const).map((v) => (
              <button key={v} className={`btn small${view === v ? ' active' : ''}`} aria-pressed={view === v}
                onClick={() => setView(v)}>{v === 'two' ? 'Compare two' : 'All side by side'}</button>
            ))}
          </div>
        </div>
      )}

      {all ? (
        <section className="card">
          <h2>Every oil change interval</h2>
          <MultiCompareTable intervals={intervals} currency={currency} />
        </section>
      ) : (<>
      {intervals.length > 1 && (
        <div className="toolbar">
          <label className="inline">Compare
            <select aria-label="Interval A" value={a} onChange={(e) => setPick({ a: Number(e.target.value), b })}>
              {intervals.map((p, i) => <option key={i} value={i}>{describe(p.period)}</option>)}
            </select>
          </label>
          <label className="inline">with
            <select aria-label="Interval B" value={b ?? ''} onChange={(e) => setPick({ a, b: Number(e.target.value) })}>
              {intervals.map((p, i) => <option key={i} value={i}>{describe(p.period)}</option>)}
            </select>
          </label>
        </div>
      )}

      <section className="card">
        <h2>{ib && ib !== ia ? 'Comparison' : describe(ia.period)}</h2>
        {ib && ib !== ia
          ? <CompareTable a={ia.stats} b={ib.stats} currency={currency} labelA={ia.period.label} labelB={ib.period.label} />
          : <StatsTable stats={ia.stats} currency={currency} />}
      </section>
      </>)}

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
