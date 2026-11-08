import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Expense, MaintenanceType, PeriodWithStats } from '../api/types'
import { IntervalTrend } from '../charts/IntervalTrend'
import { SmallMultiples } from '../charts/SmallMultiples'
import { VehicleTimeline } from '../charts/VehicleTimeline'
import { ChartCard } from '../components/ChartCard'
import { CompareTable } from '../components/CompareTable'
import { label } from '../components/ExpenseForm'
import { StatsTable } from '../components/StatsTable'
import { addDays } from '../lib/dates'
import { defaultType, intervalWindow, pickDefaultIntervals, presentTypes } from '../lib/intervals'
import { useAnalytics, useParams } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

/** Inclusive-to display of a half-open interval. */
const describe = (p: PeriodWithStats['period']) => `${p.label} (${p.from} → ${addDays(p.to, -1)})`

function Compare({ type, currency }: { type: MaintenanceType; currency: string }) {
  const { data, isLoading, isError } = useAnalytics<PeriodWithStats[]>('intervals', { type })
  const intervals = data
  const [cur, prev] = useMemo(() => pickDefaultIntervals(intervals ?? []), [intervals])
  // Indices into the newest-first list; A is the earlier interval, B the later one.
  const [pick, setPick] = useState<{ a: number; b: number | null } | null>(null)
  const a = pick?.a ?? (prev ? 1 : 0)
  const b = pick ? pick.b : prev ? 0 : null
  const ia = intervals?.[a], ib = b == null ? undefined : intervals?.[b]
  const win = ia ? intervalWindow(ia.period, ib?.period ?? null) : null

  if (isError) return <p className="field-error">Could not load intervals.</p>
  if (isLoading && !intervals) return <p className="muted">Loading…</p>
  if (!intervals || !cur || !ia || !win) return <p className="muted empty">No intervals for this type.</p>

  return (
    <>
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

      <IntervalTrend from={win.from} to={win.to} serviceDate={win.serviceDate} />
      <VehicleTimeline from={win.from} to={win.to} title="Timeline across the intervals" />
      <SmallMultiples intervals={intervals} currency={currency} />
    </>
  )
}

export default function IntervalsPage() {
  const { vehicleId } = useParams()
  const currency = useVehicle().homeCurrency
  const { data: expenses, isLoading, isError } = useQuery({
    queryKey: ['expenses', vehicleId, 'maintenance'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?category=Maintenance`),
  })
  const types = useMemo(() => presentTypes(expenses ?? []), [expenses])
  const [chosen, setChosen] = useState<MaintenanceType | null>(null)
  const type = chosen && types.includes(chosen) ? chosen : defaultType(types)

  return (
    <section>
      <h1>Intervals</h1>
      {isError ? <p className="field-error">Could not load maintenance history.</p>
        : isLoading ? <p className="muted">Loading…</p>
        : !type ? (
          <ChartCard title="No maintenance recorded" empty emptyText="Add a maintenance expense with the work done to compare intervals." >{null}</ChartCard>
        ) : (
          <>
            <div className="toolbar">
              <label className="inline">Maintenance type
                <select aria-label="Maintenance type" value={type} onChange={(e) => setChosen(e.target.value as MaintenanceType)}>
                  {types.map((t) => <option key={t} value={t}>{label(t)}</option>)}
                </select>
              </label>
            </div>
            <Compare key={type} type={type} currency={currency} />
          </>
        )}
    </section>
  )
}
