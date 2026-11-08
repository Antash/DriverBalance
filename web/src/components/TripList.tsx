import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { TripPage } from '../api/types'
import { fmtDateTime, fmtDuration, fmtNum } from '../lib/format'
import { useParams } from '../state/ParamsContext'
import { ChartCard } from './ChartCard'
import { SavedParamsNote } from './SavedParamsNote'
import { TripDrawer } from './TripDrawer'

const PAGE_SIZE = 25

/** Paged trip groups (one cold start each) that expand into their trips. `to` is exclusive. Uses the saved parameters. */
export function TripList({ from, to, title = 'Trips' }: { from?: string; to?: string; title?: string }) {
  const { vehicleId } = useParams()
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<Set<number>>(new Set())
  const [tripId, setTripId] = useState<number | null>(null)

  const qs = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
  if (from) qs.set('from', from)
  if (to) qs.set('to', to)
  // Groups depend on the saved cold-start gap, so this shares the 'analytics' prefix that Save invalidates.
  const { data, isError, isLoading } = useQuery({
    queryKey: ['analytics', vehicleId, 'trips', from ?? null, to ?? null, page],
    queryFn: () => api.get<TripPage>(`/vehicles/${vehicleId}/trips?${qs}`),
  })
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1
  const toggle = (id: number) => setOpen((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })

  return (
    <ChartCard title={title} loading={isLoading} error={isError} empty={data?.total === 0}
      hint="Each row is one cold start; trips chained by short gaps are grouped.">
      <SavedParamsNote />
      <div className="table-wrap">
        <table className="data">
          <thead>
            <tr><th /><th>Start</th><th className="num">Trips</th><th className="num">Km</th><th>Duration</th><th className="num">L/100</th><th>Score</th></tr>
          </thead>
          <tbody>
            {data?.groups.map((g) => {
              const id = g.trips[0].id
              const expanded = open.has(id)
              return [
                <tr key={id}>
                  <td><button className="btn small" aria-expanded={expanded} aria-label={expanded ? 'Collapse group' : 'Expand group'}
                    onClick={() => toggle(id)}>{expanded ? '▾' : '▸'}</button></td>
                  <td>{fmtDateTime(g.start)}</td>
                  <td className="num">{g.trips.length}</td>
                  <td className="num">{fmtNum(g.distanceKm, 1)}</td>
                  <td>{fmtDuration(g.duration)}</td>
                  <td className="num">{fmtNum(g.lPer100, 1)}</td>
                  <td><span className={`score score-${g.score}`}>{g.score}</span></td>
                </tr>,
                ...(expanded ? g.trips.map((t) => (
                  <tr key={`t${t.id}`} className="sub">
                    <td />
                    <td><button className="link" onClick={() => setTripId(t.id)}>{fmtDateTime(t.start)}</button></td>
                    <td colSpan={2} className="num">{fmtNum(t.distanceKm, 1)} km</td>
                    <td>{fmtDuration(t.duration)}</td>
                    <td className="num">{t.distanceKm > 0 ? fmtNum((t.fuelLiters / t.distanceKm) * 100, 1) : '—'}</td>
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
          <button className="btn small" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>
          <span className="muted">Page {page} of {pages}</span>
          <button className="btn small" disabled={page >= pages} onClick={() => setPage(page + 1)}>Next</button>
        </div>
      )}
      {tripId != null && <TripDrawer tripId={tripId} onClose={() => setTripId(null)} />}
    </ChartCard>
  )
}
