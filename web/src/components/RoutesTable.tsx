import { fmtApprox, fmtNum, fmtPct } from '../lib/format'
import { routeName, useRoutes } from '../lib/routes'
import { ChartCard } from './ChartCard'

/** Regular routes of the period (driven at least 5 times). `to` is exclusive. Click a row to list its drives. */
export function RoutesTable({ from, to, level, onRoute }: { from: string; to: string; level: 'Group' | 'Trip'; onRoute: (key: string) => void }) {
  const { data, isError, isLoading } = useRoutes(from, to, level)
  const noShares = !!data && data.every((r) => r.share === 0)
  const noun = level === 'Trip' ? 'trip' : 'drive'

  return (
    <ChartCard title="Regular routes" loading={isLoading} error={isError} empty={data?.length === 0}
      emptyText="No regular routes yet: a route shows once it has been driven 5 times in this period."
      hint="Places you drive between at least 5 times in this period, both directions together. Per month spreads a route's cost over the whole period. Click a route to list its drives.">
      <div className="table-wrap">
        <table className="data">
          <thead>
            <tr>
              <th>Route</th><th className="num">{level === 'Trip' ? 'Trips' : 'Drives'}</th><th>Typical</th>
              <th className="num">Avg km</th><th className="num">≈ per {noun}</th><th className="num">≈ per month</th><th className="num">Share</th>
            </tr>
          </thead>
          <tbody>
            {data?.map((r) => (
              <tr key={r.key} role="button" tabIndex={0} className="clickable" onClick={() => onRoute(r.key)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onRoute(r.key) } }}>
                <td className="wrap">
                  {routeName(r)}
                  {r.via.length > 0 && <span className="muted"> · via {r.via.join(', ')}</span>}
                </td>
                <td className="num">{r.count}</td>
                <td>{r.typical}</td>
                <td className="num">{fmtNum(r.avgKm, 1)}</td>
                <td className="num">{r.avgCost > 0 ? fmtApprox(r.avgCost, r.currency) : '—'}</td>
                <td className="num">{r.perMonth > 0 ? fmtApprox(r.perMonth, r.currency) : '—'}</td>
                <td className="num">{noShares ? '—' : fmtPct(r.share)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ChartCard>
  )
}
