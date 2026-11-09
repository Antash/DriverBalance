import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { EChartsOption } from 'echarts'
import type { PeriodStats, PeriodWithStats } from '../api/types'
import { ChartCard } from '../components/ChartCard'
import { Chart } from '../components/Chart'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { CompareTable } from '../components/CompareTable'
import { DateInput } from '../components/DateInput'
import { PeriodTable } from '../components/PeriodTable'
import { StatsTable } from '../components/StatsTable'
import { TripList } from '../components/TripList'
import { comparisonRequest } from '../lib/compare'
import { addDays, addYears, iso } from '../lib/dates'
import { fmtNum, fmtDate } from '../lib/format'
import { parseStatsState, statsStateToParams, type StatsUrlState } from '../lib/urlState'
import { useAnalytics } from '../state/ParamsContext'
import { useVehicle } from '../state/useVehicle'

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

function monthsOption(rows: PeriodWithStats[]): () => EChartsOption {
  return () => {
    const t = chartTheme()
    return {
      legend: { show: false },
      tooltip: { trigger: 'axis', valueFormatter: (v) => `${fmtNum(Number(v), 0)} km` } as EChartsOption['tooltip'],
      grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
      xAxis: { type: 'category', data: rows.map((r) => MONTHS[Number(r.period.from.slice(5, 7)) - 1].slice(0, 3)), ...axisStyle() },
      yAxis: { type: 'value', name: 'km', nameTextStyle: { color: t.muted }, ...axisStyle() },
      series: [{
        name: 'Distance', type: 'bar', barMaxWidth: 28,
        itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2, borderRadius: [4, 4, 0, 0] },
        data: rows.map((r) => r.stats.usage.tripKm),
      }],
    }
  }
}

type LevelProps = { state: StatsUrlState; update(p: Partial<StatsUrlState>): void; currency: string }

function MonthsLevel({ year, update, currency }: { year: number; update(p: Partial<StatsUrlState>): void; currency: string }) {
  const months = useAnalytics<PeriodWithStats[]>('calendar', { level: 'months', year })
  const rows = months.data
  const opt = useMemo(() => (rows ? monthsOption(rows) : undefined), [rows])
  return (
    <ChartCard title={`Months of ${year}`} loading={months.isLoading && !rows} error={months.isError}
      empty={!!rows && rows.every((r) => r.stats.usage.tripCount === 0)}>
      {opt && <Chart option={opt} height={240} />}
      {rows && <PeriodTable rows={rows} currency={currency} onSelect={(r) => update({ month: Number(r.period.from.slice(5, 7)), week: null })} />}
    </ChartCard>
  )
}

function WeeksLevel({ year, month, week, update, currency }: {
  year: number; month: number; week: string | null; update(p: Partial<StatsUrlState>): void; currency: string
}) {
  const weeks = useAnalytics<PeriodWithStats[]>('calendar', { level: 'weeks', year, month })
  const rows = weeks.data
  const selected = rows?.find((w) => w.period.from === week)
  return (
    <>
      <ChartCard title={`Weeks of ${MONTHS[month - 1]} ${year}`} loading={weeks.isLoading && !rows} error={weeks.isError}
        empty={!!rows && rows.length === 0}>
        {rows && <PeriodTable rows={rows} currency={currency} selected={week} onSelect={(r) => update({ week: r.period.from })} />}
      </ChartCard>
      {selected && (
        <TripList key={selected.period.from} from={selected.period.from} to={selected.period.to}
          title={`Trips in the week of ${fmtDate(selected.period.from)}`} />
      )}
    </>
  )
}

function Calendar({ state, update, currency }: LevelProps) {
  const { year, month, week } = state
  const years = useAnalytics<PeriodWithStats[]>('calendar', { level: 'years' })

  const crumbs: { label: string; go(): void }[] = [{ label: 'All years', go: () => update({ year: null, month: null, week: null }) }]
  if (year != null) crumbs.push({ label: String(year), go: () => update({ month: null, week: null }) })
  if (year != null && month != null) crumbs.push({ label: MONTHS[month - 1], go: () => update({ week: null }) })
  if (year != null && month != null && week) crumbs.push({ label: `Week of ${fmtDate(week)}`, go: () => undefined })

  return (
    <>
      <nav className="breadcrumb" aria-label="Breadcrumb">
        {crumbs.map((c, i) => (
          <span key={i}>
            {i > 0 && <span className="muted"> › </span>}
            {i < crumbs.length - 1 ? <button className="link" onClick={c.go}>{c.label}</button> : <strong>{c.label}</strong>}
          </span>
        ))}
      </nav>

      {year == null && (
        <ChartCard title="Years" loading={years.isLoading && !years.data} error={years.isError} empty={years.data?.length === 0}>
          {years.data && <PeriodTable rows={years.data} currency={currency}
            onSelect={(r) => update({ year: Number(r.period.from.slice(0, 4)), month: null, week: null })} />}
        </ChartCard>
      )}
      {year != null && month == null && <MonthsLevel year={year} update={update} currency={currency} />}
      {year != null && month != null && <WeeksLevel year={year} month={month} week={week} update={update} currency={currency} />}
    </>
  )
}

function Custom({ state, update, currency }: LevelProps) {
  const { from, to, cmpFrom, cmpTo } = state
  const comparing = !!(cmpFrom || cmpTo)
  const aOk = !!from && !!to && to >= from
  const bOk = comparing && !!cmpFrom && !!cmpTo && cmpTo >= cmpFrom
  const periods = useMemo(() => {
    if (!aOk) return []
    // Baseline (the "Compare with" range) first: the table reads baseline -> main range.
    return comparisonRequest({ from: from!, to: addDays(to!, 1) }, bOk ? { from: cmpFrom!, to: addDays(cmpTo!, 1) } : null).periods
  }, [aOk, bOk, from, to, cmpFrom, cmpTo])
  const stats = useAnalytics<PeriodStats[]>('stats', { periods })

  const rangeLabel = (a: string, b: string) => `${fmtDate(a)} – ${fmtDate(b)}`
  const show = periods.length > 0 && stats.data?.length === periods.length

  return (
    <>
      <div className="toolbar">
        <label className="inline">From <DateInput value={from ?? ''} onChange={(v) => update({ from: v || null })} /></label>
        <label className="inline">To <DateInput aria-label="To (inclusive)" value={to ?? ''} onChange={(v) => update({ to: v || null })} /></label>
        <label className="check">
          <input type="checkbox" checked={comparing}
            onChange={(e) => update(e.target.checked
              ? { cmpFrom: from ? addYears(from, -1) : null, cmpTo: to ? addYears(to, -1) : null }
              : { cmpFrom: null, cmpTo: null })} />
          Compare with
        </label>
        {comparing && (
          <>
            <DateInput aria-label="Compare from" value={cmpFrom ?? ''} onChange={(v) => update({ cmpFrom: v || null })} />
            <DateInput aria-label="Compare to (inclusive)" value={cmpTo ?? ''} onChange={(v) => update({ cmpTo: v || null })} />
          </>
        )}
      </div>
      {!aOk && <p className="muted">{from && to ? 'The end date is before the start date.' : 'Pick a from and to date.'}</p>}
      {aOk && comparing && !bOk && <p className="muted">Pick the comparison range too.</p>}
      {stats.isError && <p className="field-error">Could not load statistics.</p>}
      {show && aOk && (
        <section className="card">
          <h2>{bOk ? 'Comparison' : rangeLabel(from!, to!)}</h2>
          {stats.data!.every((s) => s.usage.tripCount === 0) && <p className="muted empty">No trips in this period</p>}
          {bOk
            ? <CompareTable a={stats.data![0]} b={stats.data![1]} currency={currency}
                labelA={`Baseline ${rangeLabel(cmpFrom!, cmpTo!)}`} labelB={rangeLabel(from!, to!)} />
            : <StatsTable stats={stats.data![0]} currency={currency} />}
        </section>
      )}
    </>
  )
}

export default function StatisticsPage() {
  const [sp, setSp] = useSearchParams()
  const state = useMemo(() => parseStatsState(sp), [sp])
  const update = (patch: Partial<StatsUrlState>) => setSp(statsStateToParams({ ...state, ...patch }), { replace: true })
  const currency = useVehicle().homeCurrency

  const setMode = (mode: StatsUrlState['mode']) => {
    if (mode === 'custom' && !state.from && !state.to) {
      const today = new Date()
      update({ mode, from: iso(new Date(today.getFullYear(), 0, 1)), to: iso(today) })
    } else update({ mode })
  }

  return (
    <section>
      <h1>Statistics</h1>
      <div className="toolbar">
        <div className="segmented" role="group" aria-label="Mode">
          {(['calendar', 'custom'] as const).map((m) => (
            <button key={m} className={`btn small${state.mode === m ? ' active' : ''}`} aria-pressed={state.mode === m}
              onClick={() => setMode(m)}>{m === 'calendar' ? 'Calendar' : 'Custom range'}</button>
          ))}
        </div>
      </div>
      {state.mode === 'calendar'
        ? <Calendar state={state} update={update} currency={currency} />
        : <Custom state={state} update={update} currency={currency} />}
    </section>
  )
}
