import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { TripPage } from '../api/types'
import { fmtDateTime, fmtDuration, fmtNum } from '../lib/format'
import { costText, costTitle } from '../lib/tripCost'
import { useParams } from '../state/ParamsContext'

/** How many drives the Overview shows: about a day or two of normal use, short enough to read at a glance. */
const COUNT = 5

/** The newest drives (trip groups, one cold start each) with route and approximate cost; hidden when there are none. */
export function RecentTrips() {
  const { vehicleId } = useParams()
  // Groups follow the saved cold-start gap, so this shares the 'analytics' prefix that saving parameters invalidates.
  const { data } = useQuery({
    queryKey: ['analytics', vehicleId, 'trips', 'recent', COUNT],
    queryFn: () => api.get<TripPage>(`/vehicles/${vehicleId}/trips?page=1&pageSize=${COUNT}`),
  })
  if (!data || data.groups.length === 0) return null
  return (
    <section className="card">
      <div className="card-head">
        <h2>Recent trips</h2>
        <Link to="/trips?tab=list">All trips</Link>
      </div>
      <div className="table-wrap">
        <table className="data">
          <thead>
            <tr><th>Start</th><th>Route</th><th className="num">Km</th><th>Duration</th><th className="num">Cost</th></tr>
          </thead>
          <tbody>
            {data.groups.map((g) => {
              const first = g.trips[0], last = g.trips[g.trips.length - 1]
              const route = first.startAddress || last.endAddress ? `${first.startAddress || '—'} → ${last.endAddress || '—'}` : '—'
              return (
                <tr key={first.id}>
                  <td>{fmtDateTime(g.start)}</td>
                  <td className="wrap">
                    {route}
                    {g.trips.length > 1 && <span className="muted"> · <span>{g.trips.length} trips</span></span>}
                  </td>
                  <td className="num">{fmtNum(g.distanceKm, 1)}</td>
                  <td>{fmtDuration(g.duration)}</td>
                  <td className="num" title={costTitle(g.cost)}>{costText(g.cost)}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </section>
  )
}
