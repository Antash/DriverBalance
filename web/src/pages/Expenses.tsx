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
import { CATEGORY_COLOR_INDEX, errorLines, fmtMoney, invalidateVehicleData } from '../lib/format'
import { useParams, useAnalytics } from '../state/ParamsContext'
import { useTyreSets, useVehicle } from '../state/useVehicle'

/** Fixed colour per category (the first seven); all remaining categories fold into one neutral "Other". */
function colorFor(cat: string, t: ReturnType<typeof chartTheme>) {
  const i = CATEGORY_COLOR_INDEX[cat]
  return i === undefined ? t.muted : t.series[i]
}
const groupOf = (c: string) => (CATEGORY_COLOR_INDEX[c] === undefined ? 'Other' : c)
const GROUP_ORDER = ['Fuel', 'Maintenance', 'Tyres', 'Insurance', 'Repair', 'Parking', 'TollsVignette', 'Other']

function foldTotals(by: Partial<Record<ExpenseCategory, number>>) {
  const out: Record<string, number> = {}
  for (const [c, v] of Object.entries(by)) out[groupOf(c)] = (out[groupOf(c)] ?? 0) + (v ?? 0)
  return out
}

function treemapOption(by: Partial<Record<ExpenseCategory, number>>, currency: string): EChartsOption {
  const t = chartTheme()
  const folded = foldTotals(by)
  const data = GROUP_ORDER.filter((g) => (folded[g] ?? 0) > 0)
    .map((g) => ({ name: label(g), value: folded[g], itemStyle: { color: colorFor(g, t), borderColor: t.surface, borderWidth: 2 } }))
  return {
    legend: { show: false },
    tooltip: { trigger: 'item', valueFormatter: (v) => fmtMoney(Number(v), currency) } as EChartsOption['tooltip'],
    series: [{
      type: 'treemap', roam: false, nodeClick: false, breadcrumb: { show: false }, width: '100%', height: '100%',
      label: { show: true, formatter: '{b}', color: '#0b0b0b' }, data,
    }],
  }
}

function cumulativeOption(points: CumulativeCostPoint[], currency: string): EChartsOption {
  const t = chartTheme()
  const pts = points.filter((p) => p.costPerKm != null)
  return {
    legend: { show: false },
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${fmtMoney(Number(v), currency)} / km` } as EChartsOption['tooltip'],
    grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
    xAxis: { type: 'time', ...axisStyle() },
    yAxis: { type: 'value', name: `${currency} / km`, nameTextStyle: { color: t.muted }, ...axisStyle() },
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
  const periods = useMemo(() => expensePeriods(vehicle.purchaseDate ?? null, today), [vehicle.purchaseDate, today])
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

  const rows = useMemo(() => (expenses ?? []).filter((e) => cats.length === 0 || cats.includes(e.category)), [expenses, cats])
  const cur = vehicle.homeCurrency
  const treemapOpt = useMemo(() => (totals.data ? treemapOption(totals.data, cur) : null), [totals.data, cur])
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

      <div className="chart-grid">
        <section className="card"><h2>Cost by category</h2>{treemapOpt ? <Chart option={treemapOpt} /> : <p className="muted">Loading…</p>}</section>
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
            <tr><th>Date</th><th>Category</th><th className="num">Amount</th><th className="num">Home amount</th><th className="num">Odometer</th><th>Note</th><th /></tr>
          </thead>
          <tbody>
            {rows.map((e) => {
              const needsConversion = e.currency !== cur && e.homeAmount == null
              return (
                <tr key={e.id}>
                  <td>{e.date}<Clip n={e.documents.length} /></td>
                  <td>{label(e.category)}</td>
                  <td className="num">{fmtMoney(e.amount, e.currency)}</td>
                  <td className="num">
                    {e.currency === cur ? '-' : fmtMoney(e.homeAmount, cur)}
                    {needsConversion && <> <span className="badge">needs conversion</span></>}
                  </td>
                  <td className="num">{e.odometer ?? '-'}</td>
                  <td>{e.note ?? ''}</td>
                  <td>
                    <button className="btn small" onClick={() => setEditing(e)}>Edit</button>{' '}
                    <button className="btn small danger" onClick={() => { if (window.confirm(`Delete this ${label(e.category)} expense from ${e.date}${e.documents.length > 0 ? ` and its ${e.documents.length} document(s)` : ''}?`)) del.mutate(e.id) }}>Delete</button>
                  </td>
                </tr>
              )
            })}
            {expenses && rows.length === 0 && <tr><td colSpan={7} className="muted">No expenses in this period.</td></tr>}
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
