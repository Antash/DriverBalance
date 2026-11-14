import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { TripPage } from '../api/types'
import { fmtApprox, fmtDateTime, fmtDuration, fmtNum } from '../lib/format'
import { KM_STEPS } from '../lib/kmSteps'
import { costText, costTitle } from '../lib/tripCost'
import { useParams } from '../state/ParamsContext'
import { ChartCard } from './ChartCard'
import { SavedParamsNote } from './SavedParamsNote'
import { TripDrawer } from './TripDrawer'

const PAGE_SIZE = 25

interface Props {
  from?: string
  to?: string
  title?: string
  level?: 'Group' | 'Trip'
  minKm?: number
  onMinKm?: (km: number) => void
  route?: string | null
  routeLabel?: string
  onClearRoute?: () => void
}

/** Paged trip groups (one cold start each) that expand into their trips, or single trips at Trip level. `to` is exclusive. Uses the saved parameters. */
export function TripList({ from, to, title = 'Trips', level = 'Group', minKm = 0, onMinKm, route = null, routeLabel, onClearRoute }: Props) {
  const { vehicleId } = useParams()
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<Set<number>>(new Set())
  const [tripId, setTripId] = useState<number | null>(null)

  // Back to the first page whenever the filters change.
  const filterKey = `${level}|${minKm}|${route ?? ''}`
  const [seenKey, setSeenKey] = useState(filterKey)
  if (seenKey !== filterKey) { setSeenKey(filterKey); setPage(1) }
  const shownPage = seenKey !== filterKey ? 1 : page

  const qs = new URLSearchParams({ page: String(shownPage), pageSize: String(PAGE_SIZE) })
  if (from) qs.set('from', from)
  if (to) qs.set('to', to)
  if (level === 'Trip') qs.set('level', 'Trip')
  if (minKm > 0) qs.set('minKm', String(minKm))
  if (route) qs.set('route', route)
  // Groups depend on the saved cold-start gap, so this shares the 'analytics' prefix that Save invalidates.
  const { data, isError, isLoading } = useQuery({
    queryKey: ['analytics', vehicleId, 'trips', from ?? null, to ?? null, level, minKm, route, shownPage],
    queryFn: () => api.get<TripPage>(`/vehicles/${vehicleId}/trips?${qs}`),
  })
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1
  const stepIndex = Math.max(0, KM_STEPS.indexOf(minKm))
  const noun = level === 'Trip' ? 'trip' : 'drive'
  const summary = data?.summary
  const toggle = (id: number) => setOpen((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })

  return (
    <ChartCard title={title} loading={isLoading} error={isError} empty={data?.total === 0}
      hint={level === 'Trip' ? 'Each row is one trip.' : 'Each row is one cold start; trips chained by short gaps are grouped.'}
      actions={(onMinKm || (route && onClearRoute)) && (
        <div className="row list-filters">
          {route && onClearRoute && (
            <button className="btn small" onClick={onClearRoute}>Route: {routeLabel ?? 'selected route'} ✕</button>
          )}
          {onMinKm && (
            <label className="row km-filter">
              <input type="range" min={0} max={KM_STEPS.length - 1} step={1} value={stepIndex} aria-label="Minimum distance"
                onChange={(e) => onMinKm(KM_STEPS[Number(e.target.value)])} />
              <span>{minKm > 0 ? `Longer than ${minKm} km` : 'All distances'}</span>
            </label>
          )}
        </div>
      )}>
      <SavedParamsNote />
      {summary && (
        <p className="summary-line">
          {`${summary.count} ${noun}${summary.count === 1 ? '' : 's'} · ${fmtNum(summary.km, 0)} km · ${fmtApprox(summary.cost, summary.currency)}`}
        </p>
      )}
      <div className="table-wrap">
        <table className="data">
          <thead>
            <tr><th /><th>Start</th><th className="num">Trips</th><th className="num">Km</th><th>Duration</th><th className="num">L/100</th><th className="num">Cost</th><th>Score</th></tr>
          </thead>
          <tbody>
            {data?.groups.map((g) => {
              const id = g.trips[0].id
              const expanded = level === 'Group' && open.has(id)
              return [
                <tr key={id}>
                  <td>{level === 'Group' && (
                    <button className="btn small" aria-expanded={expanded} aria-label={expanded ? 'Collapse group' : 'Expand group'}
                      onClick={() => toggle(id)}>{expanded ? '▾' : '▸'}</button>
                  )}</td>
                  <td>{level === 'Trip'
                    ? <button className="link" onClick={() => setTripId(id)}>{fmtDateTime(g.start)}</button>
                    : fmtDateTime(g.start)}</td>
                  <td className="num">{g.trips.length}</td>
                  <td className="num">{fmtNum(g.distanceKm, 1)}</td>
                  <td>{fmtDuration(g.duration)}</td>
                  <td className="num">{fmtNum(g.lPer100, 1)}</td>
                  <td className="num" title={costTitle(g.cost)}>{costText(g.cost)}</td>
                  <td><span className={`score score-${g.score}`}>{g.score}</span></td>
                </tr>,
                ...(expanded ? g.trips.map((t) => (
                  <tr key={`t${t.id}`} className="sub">
                    <td />
                    <td><button className="link" onClick={() => setTripId(t.id)}>{fmtDateTime(t.start)}</button></td>
                    <td colSpan={2} className="num">{fmtNum(t.distanceKm, 1)} km</td>
                    <td>{fmtDuration(t.duration)}</td>
                    <td className="num">{t.distanceKm > 0 ? fmtNum((t.fuelLiters / t.distanceKm) * 100, 1) : '—'}</td>
                    <td className="num" title={costTitle(t.cost)}>{costText(t.cost)}</td>
                    <td className="muted">{t.startAddress} → {t.endAddress}</td>
                  </tr>
                )) : []),
              ]
            })}
          </tbody>
        </table>
      </div>
      {pages > 1 && (
        <div className="row pager">
          <button className="btn small" disabled={shownPage <= 1} onClick={() => setPage(shownPage - 1)}>Previous</button>
          <span className="muted">Page {shownPage} of {pages}</span>
          <button className="btn small" disabled={shownPage >= pages} onClick={() => setPage(shownPage + 1)}>Next</button>
        </div>
      )}
      {tripId != null && <TripDrawer tripId={tripId} onClose={() => setTripId(null)} />}
    </ChartCard>
  )
}
