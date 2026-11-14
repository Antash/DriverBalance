import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Trip } from '../api/types'
import { fmtApprox, fmtDateTime, fmtDuration, fmtNum } from '../lib/format'
import { useParams } from '../state/ParamsContext'

/** Side drawer with one trip's details (GET /trips/{id}). */
export function TripDrawer({ tripId, onClose }: { tripId: number; onClose(): void }) {
  const { vehicleId } = useParams()
  const { data: t, isError, isLoading } = useQuery({
    queryKey: ['analytics', vehicleId, 'trip', tripId],
    queryFn: () => api.get<Trip>(`/vehicles/${vehicleId}/trips/${tripId}`),
  })
  const lPer100 = t && t.distanceKm > 0 ? (t.fuelLiters / t.distanceKm) * 100 : null
  const rows: [string, string][] = t ? [
    ['Start', fmtDateTime(t.start)],
    ['End', fmtDateTime(t.end)],
    ['From', t.startAddress || '—'],
    ['To', t.endAddress || '—'],
    ['Distance', `${fmtNum(t.distanceKm, 1)} km`],
    ['Duration', fmtDuration(t.duration)],
    ['Average speed', `${fmtNum(t.averageSpeedKmh, 0)} km/h`],
    ['Fuel', `${fmtNum(t.fuelLiters, 2)} L`],
    ['Consumption', lPer100 == null ? '—' : `${fmtNum(lPer100, 1)} L/100 km`],
    ['Odometer', `${t.startOdometer} → ${t.endOdometer} km`],
    ['Category', t.category || '—'],
    ['Notes', t.notes || '—'],
  ] : []
  const cost = t?.cost
  const costRows: [string, string][] = t && cost ? [
    ['Fuel cost', cost.fuel == null ? '— (no fuel price yet)'
      : cost.pricePerLitre == null ? fmtApprox(cost.fuel, cost.currency)
      : `${fmtApprox(cost.fuel, cost.currency)} (${fmtNum(t.fuelLiters, 1)} L × ${cost.pricePerLitre.toFixed(2)} ${cost.currency}/L)`],
    ['Time share', fmtApprox(cost.time, cost.currency)],
    ['Wear share', fmtApprox(cost.wear, cost.currency)],
    ['Trip cost', fmtApprox(cost.total, cost.currency)],
  ] : []
  return (
    <>
      <div className="scrim" onClick={onClose} />
      <aside className="drawer" role="dialog" aria-label="Trip details">
        <div className="drawer-head">
          <h2>Trip details</h2>
          <button className="btn" onClick={onClose} aria-label="Close">✕</button>
        </div>
        <div className="drawer-body">
          {isError ? <p className="field-error">Could not load this trip.</p>
            : isLoading ? <p className="muted">Loading…</p>
            : <>
              <dl className="kv">{rows.map(([k, v]) => <div key={k}><dt>{k}</dt><dd>{v}</dd></div>)}</dl>
              {costRows.length > 0 && <>
                <h3>Cost</h3>
                <dl className="kv">{costRows.map(([k, v]) => <div key={k}><dt>{k}</dt><dd>{v}</dd></div>)}</dl>
                <p className="muted">Fuel at the local average price, plus a share of the other running costs over the 12 months around the trip (by driving time, and by km for wear).</p>
              </>}
            </>}
        </div>
      </aside>
    </>
  )
}
