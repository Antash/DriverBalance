import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import { api } from '../api/client'
import type { Expense } from '../api/types'
import { Chart } from '../components/Chart'
import { ChartCard } from '../components/ChartCard'
import { axisStyle, chartTheme } from '../components/chartTheme'
import { fmtMoney, fmtNum } from '../lib/format'
import { maintenanceHistory, type ServiceRow, type WorkItem } from '../lib/maintenance'
import { useParams } from '../state/ParamsContext'

const cost = (e: Expense) => fmtMoney(e.amount, e.currency)

/** One lane per maintenance type, a marker each time it was done. */
function timelineOption(services: ServiceRow<Expense>[], lanes: string[]): () => EChartsOption {
  return () => {
    const t = chartTheme()
    const pad = 20 * 86_400_000
    return {
      legend: { show: false },
      tooltip: {
        trigger: 'item',
        formatter: (p: unknown) => {
          const s = (p as { data: { service: ServiceRow<Expense> } }).data.service
          const odo = s.odometer != null ? `<br/>${fmtNum(s.odometer, 0)} km` : ''
          return `${s.date}${odo}<br/>${s.work.map((w) => w.label).join(', ')}<br/>${cost(s.expense)}`
        },
      },
      grid: { left: 8, right: 24, top: 16, bottom: 8, containLabel: true },
      xAxis: { type: 'time', min: (v: { min: number }) => v.min - pad, max: (v: { max: number }) => v.max + pad, ...axisStyle() },
      yAxis: { type: 'category', data: lanes, inverse: true, ...axisStyle(), splitLine: { show: true, lineStyle: { color: t.grid } } },
      series: lanes.map((lane, i) => ({
        name: lane, type: 'scatter' as const, symbol: 'diamond', symbolSize: 14,
        itemStyle: { color: t.series[i % t.series.length], borderColor: t.surface, borderWidth: 1.5 },
        data: services.filter((s) => s.work.some((w) => w.label === lane))
          .map((s) => ({ value: [s.date, lane], service: s })),
      })),
    } as EChartsOption
  }
}

function Since({ w }: { w: WorkItem }) {
  if (w.sinceDays == null) return <span className="muted">first recorded</span>
  return <span>{fmtNum(w.sinceDays, 0)} days{w.sinceKm != null ? ` · ${fmtNum(w.sinceKm, 0)} km` : ''}</span>
}

export default function MaintenancePage() {
  const { vehicleId } = useParams()
  const { data: expenses, isLoading, isError } = useQuery({
    queryKey: ['expenses', vehicleId, 'maintenance'],
    queryFn: () => api.get<Expense[]>(`/vehicles/${vehicleId}/expenses?category=Maintenance`),
  })
  const { services, lanes } = useMemo(() => maintenanceHistory(expenses ?? []), [expenses])
  const opt = useMemo(() => (services.length ? timelineOption(services, lanes) : undefined), [services, lanes])
  const empty = !!expenses && services.length === 0
  const emptyText = 'No maintenance other than oil changes yet. Add a maintenance expense with the work done.'

  return (
    <section>
      <h1>Maintenance</h1>
      <p className="muted">Everything except oil changes, which have their own page under Oil intervals.</p>
      <ChartCard title="Maintenance timeline" loading={isLoading} error={isError} empty={empty} emptyText={emptyText}
        hint="One lane per kind of work; hover a marker for the date, odometer and cost.">
        {opt && <Chart option={opt} height={Math.max(140, 48 + lanes.length * 44)} />}
      </ChartCard>

      {services.length > 0 && (
        <section className="card">
          <h2>Services</h2>
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr><th>Date</th><th className="num">Odometer</th><th>Work done</th><th>Since last time</th><th className="num">Cost</th><th>Note</th></tr>
              </thead>
              <tbody>
                {services.map((s) => (
                  <tr key={s.id}>
                    <td>{s.date}</td>
                    <td className="num">{s.odometer != null ? `${fmtNum(s.odometer, 0)} km` : '—'}</td>
                    <td>{s.work.map((w) => <div key={w.key}>{w.label}</div>)}</td>
                    <td>{s.work.map((w) => <div key={w.key}><Since w={w} /></div>)}</td>
                    <td className="num">{cost(s.expense)}</td>
                    <td>{s.expense.note ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted">The cost is the whole service, including any oil change done at the same visit.</p>
        </section>
      )}
    </section>
  )
}
