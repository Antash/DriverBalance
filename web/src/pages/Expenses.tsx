import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import { api } from '../api/client'
import type { CumulativeCostPoint, Expense, ExpenseCategory } from '../api/types'
import { chartTheme, axisStyle } from '../components/chartTheme'
import { Chart } from '../components/Chart'
import { Clip } from '../components/Clip'
import { CATEGORIES, ExpenseForm, label } from '../components/ExpenseForm'
import { Modal } from '../components/Modal'
import { iso } from '../lib/dates'
import { expensePeriods } from '../lib/expensePeriods'
import { errorLines, fmtMoney, fmtNum, invalidateVehicleData, fmtDate } from '../lib/format'
import { useParams, useAnalytics } from '../state/ParamsContext'
import { useTyreSets, useVehicle } from '../state/useVehicle'
import { timeAxisTooltip } from '../charts/dateTooltip'

/** Category name for tables and charts: the purchase category without its long hint. */
const shortLabel = (c: ExpenseCategory) => label(c)

type CategoryShare = { category: ExpenseCategory; amount: number; share: number; color: string; opacity: number }

/** Categories with spending, largest first, with their share of the total and a colour (the palette repeats lighter). */
function categoryShares(by: Partial<Record<ExpenseCategory, number>>, t: ReturnType<typeof chartTheme>): CategoryShare[] {
  const entries = Object.entries(by).filter(([, v]) => (v ?? 0) > 0).sort((x, y) => (y[1] ?? 0) - (x[1] ?? 0)) as [ExpenseCategory, number][]
  const total = entries.reduce((sum, [, v]) => sum + v, 0)
  return entries.map(([category, amount], i) => ({
    category, amount, share: total > 0 ? amount / total : 0,
    color: t.series[i % t.series.length], opacity: i < t.series.length ? 1 : 0.6,
  }))
}

/** Blocks sized by spending, labelled with the category and its percentage only (amounts are in the summary). */
function treemapOption(shares: CategoryShare[], currency: string): EChartsOption {
  const t = chartTheme()
  return {
    legend: { show: false },
    tooltip: { trigger: 'item', formatter: (p: unknown) => { const d = (p as { data: { name: string; value: number; pct: number } }).data; return `${d.name}: ${fmtMoney(d.value, currency)} (${d.pct.toFixed(1)} %)` } },
    series: [{
      type: 'treemap', roam: false, nodeClick: false, breadcrumb: { show: false },
      left: 0, top: 0, right: 0, bottom: 0, width: '100%', height: '100%',
      label: {
        show: true, position: 'inside', align: 'center', verticalAlign: 'middle', overflow: 'truncate', color: '#0b0b0b', fontSize: 12,
        formatter: (p: { data: { name: string; pct: number } }) => `${p.data.name}\n${p.data.pct.toFixed(0)} %`,
      },
      data: shares.map((x) => ({
        name: shortLabel(x.category), value: x.amount, pct: x.share * 100,
        itemStyle: { color: x.color, opacity: x.opacity, borderColor: t.surface, borderWidth: 2 },
      })),
    }],
  } as EChartsOption
}

function cumulativeOption(points: CumulativeCostPoint[], currency: string): EChartsOption {
  const t = chartTheme()
  const pts = points.filter((p) => p.costPerKm != null)
  return {
    legend: { show: false },
    tooltip: { trigger: 'axis', formatter: timeAxisTooltip((v) => `${fmtMoney(v, currency)} / km`) } as EChartsOption['tooltip'],
    grid: { left: 8, right: 16, top: 36, bottom: 8, containLabel: true },
    xAxis: { type: 'time', ...axisStyle() },
    yAxis: { type: 'value', name: `${currency} / km`, nameGap: 14, nameTextStyle: { color: t.muted, align: 'left' }, ...axisStyle() },
    series: [{
      name: 'Cost per km', type: 'line', smooth: true, showSymbol: false, symbolSize: 8, lineStyle: { width: 2 },
      itemStyle: { color: t.series[0], borderColor: t.surface, borderWidth: 2 }, data: pts.map((p) => [p.date, p.costPerKm]),
    }],
  }
}

export default function ExpensesPage() {
  const { vehicleId } = useParams()
  const vehicle = useVehicle()
  const qc = useQueryClient()
  const { data: tyreSets = [] } = useTyreSets()
  const [today] = useState(() => iso(new Date()))
  const { data: allExpenses } = useQuery({
    queryKey: ['expenses', vehicleId, 'all'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses`),
  })
  const firstRecord = useMemo(() => (allExpenses ?? []).reduce<string | null>((m, e) => (m == null || e.date < m ? e.date : m), null), [allExpenses])
  const periods = useMemo(() => expensePeriods(vehicle.purchaseDate ?? null, today, firstRecord), [vehicle.purchaseDate, today, firstRecord])
  const [periodKey, setPeriodKey] = useState<string>('total')
  const period = periods.find((p) => p.key === periodKey) ?? periods[0]
  const [cats, setCats] = useState<ExpenseCategory[]>([])
  const [editing, setEditing] = useState<Expense | 'new' | null>(null)

  const { data: expenses } = useQuery({
    queryKey: ['expenses', vehicleId, period.from, period.to],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?from=${period.from}&to=${period.to}`),
  })
  const range = { from: period.from, to: period.to }
  const totals = useAnalytics<Partial<Record<ExpenseCategory, number>>>('charts/money-totals', range)
  const cumulative = useAnalytics<CumulativeCostPoint[]>('charts/money-cumulative', range)

  const del = useMutation({
    mutationFn: (id: number) => api.del<void>(`/vehicles/${vehicleId}/expenses/${id}`),
    onSuccess: () => invalidateVehicleData(qc, vehicleId),
  })

  // Purchase records are hidden here: they belong to the car and are shown in Cost of ownership on Overview.
  const rows = useMemo(() => (expenses ?? []).filter((e) => e.category !== 'Purchase' && (cats.length === 0 || cats.includes(e.category))), [expenses, cats])
  const cur = vehicle.homeCurrency
  const foreignShown = rows.some((e) => e.currency !== cur)
  const shares = useMemo(() => (totals.data ? categoryShares(totals.data, chartTheme()) : null), [totals.data])
  const treemapOpt = useMemo(() => (shares && shares.length > 0 ? treemapOption(shares, cur) : null), [shares, cur])
  const totalSpent = shares?.reduce((sum, x) => sum + x.amount, 0) ?? null
  const cumulativeOpt = useMemo(() => (cumulative.data ? cumulativeOption(cumulative.data, cur) : null), [cumulative.data, cur])

  return (
    <section>
      <h1>Expenses</h1>
      <div className="toolbar">
        <div className="segmented" role="group" aria-label="Period">
          {periods.map((p) => (
            <button key={p.key} className={`btn small${p.key === period.key ? ' active' : ''}`} aria-pressed={p.key === period.key}
              onClick={() => setPeriodKey(p.key)}>{p.label}</button>
          ))}
        </div>
        <span className="spacer" />
        <button className="btn primary" onClick={() => setEditing('new')}>Add expense</button>
      </div>

      <section className="card">
        <h2>Cost by category</h2>
        {!shares ? <p className="muted">Loading…</p> : shares.length === 0 ? <p className="muted empty">No expenses in this period.</p> : (
          <div className="breakdown">
            <div className="breakdown-chart">{treemapOpt && <Chart option={treemapOpt} height={300} />}</div>
            <div className="breakdown-summary">
              <div className="kpi-name">Total spent</div>
              <div className="kpi-value">{fmtMoney(totalSpent, cur)}</div>
              <table className="data compact">
                <tbody>
                  {shares.map((x) => (
                    <tr key={x.category}>
                      <td><span className="swatch" style={{ background: x.color, opacity: x.opacity }} />{shortLabel(x.category)}</td>
                      <td className="num">{fmtMoney(x.amount, cur)}</td>
                      <td className="num muted">{(x.share * 100).toFixed(1)} %</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="muted hint">Purchase costs are not included; see Cost of ownership on Overview.</p>
            </div>
          </div>
        )}
      </section>

      <div>
        <section className="card">
          <h2>Cost per km</h2>
          <p className="muted hint">Running cost so far divided by km driven so far in the period; yearly costs are spread over their period.</p>
          {cumulativeOpt ? <Chart option={cumulativeOpt} height={280} /> : <p className="muted">Loading…</p>}
        </section>
      </div>

      <section className="card">
        <h2>Records</h2>
        <fieldset className="group chips" aria-label="Categories" style={{ margin: '0 0 8px' }}>
          {CATEGORIES.map((c) => (
            <label key={c} className="check">
              <input type="checkbox" checked={cats.includes(c)}
                onChange={() => setCats((s) => (s.includes(c) ? s.filter((x) => x !== c) : [...s, c]))} /> {label(c)}
            </label>
          ))}
        </fieldset>
        <div className="table-wrap">
        {del.isError && <ul className="errors">{errorLines(del.error).map((m) => <li key={m}>{m}</li>)}</ul>}
        <table className="data">
          <thead>
            <tr><th>Date</th><th>Category</th><th className="num">Amount</th>{foreignShown && <th className="num">In {cur}</th>}<th className="num">Odometer</th><th>Note</th><th /></tr>
          </thead>
          <tbody>
            {rows.map((e) => {
              const needsConversion = e.currency !== cur && e.homeAmount == null
              return (
                <tr key={e.id}>
                  <td>{fmtDate(e.date)}<Clip n={e.documents.length} /></td>
                  <td>{shortLabel(e.category)}</td>
                  <td className="num">{fmtMoney(e.amount, e.currency)}</td>
                  {foreignShown && (
                    <td className="num">
                      {e.currency === cur ? '' : fmtMoney(e.homeAmount, cur)}
                      {needsConversion && <> <span className="badge">needs conversion</span></>}
                    </td>
                  )}
                  <td className="num">{e.odometer == null ? '' : `${fmtNum(e.odometer, 0)} km`}</td>
                  <td className="wrap">{e.note ?? ''}</td>
                  <td className="actions">
                    <button className="btn small" aria-label={`Edit ${shortLabel(e.category)} from ${fmtDate(e.date)}`} title="Edit" onClick={() => setEditing(e)}>✎</button>{' '}
                    <button className="btn small danger" aria-label={`Delete ${shortLabel(e.category)} from ${fmtDate(e.date)}`} title="Delete" onClick={() => { if (window.confirm(`Delete this ${label(e.category)} expense from ${fmtDate(e.date)}${e.documents.length > 0 ? ` and its ${e.documents.length} document(s)` : ''}?`)) del.mutate(e.id) }}>✕</button>
                  </td>
                </tr>
              )
            })}
            {expenses && rows.length === 0 && <tr><td colSpan={foreignShown ? 7 : 6} className="muted">No expenses in this period.</td></tr>}
          </tbody>
        </table>
        </div>
      </section>

      {editing && (
        <Modal title={editing === 'new' ? 'Add expense' : 'Edit expense'} onClose={() => setEditing(null)}>
          <ExpenseForm vehicle={vehicle} tyreSets={tyreSets} initial={editing === 'new' ? undefined : editing} onDone={() => setEditing(null)} />
        </Modal>
      )}
    </section>
  )
}
