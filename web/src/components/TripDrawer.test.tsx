import { cleanup, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Trip } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
vi.mock('../state/ParamsContext', () => ({ useParams: () => ({ vehicleId: 1 }) }))
import { api } from '../api/client'
import { TripDrawer } from './TripDrawer'

const base = {
  id: 5, category: 'Private', start: '2026-11-09T08:05:00', startOdometer: 100, startAddress: 'A',
  end: '2026-11-09T09:05:00', endOdometer: 150, endAddress: 'B', duration: '01:00:00', distanceKm: 50,
  fuelLiters: 2.08, notes: '', averageSpeedKmh: 50,
}
function renderIt(t: Trip) {
  vi.mocked(api.get).mockResolvedValue(t)
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><TripDrawer tripId={5} onClose={() => {}} /></QueryClientProvider>)
}
beforeEach(() => vi.clearAllMocks())
afterEach(() => cleanup())

describe('TripDrawer', () => {
  it('renders the cost section', async () => {
    renderIt({ ...base, cost: { fuel: 80, time: 40, wear: 22.4, total: 142.4, pricePerLitre: 38.4, currency: 'CZK' } })
    expect(await screen.findByText('Trip cost')).toBeTruthy()
    expect(screen.getByText('≈ 142 CZK')).toBeTruthy()
    expect(screen.getByText(/× 38\.40 CZK\/L/)).toBeTruthy()
    expect(screen.getByText(/Fuel at the local average price, plus a share of the other running costs over the 12 months around the trip/)).toBeTruthy()
  })

  it('shows zero fuel cost for a trip without litres and without a price', async () => {
    renderIt({ ...base, fuelLiters: 0, cost: { fuel: 0, time: 40, wear: 22.4, total: 62.4, pricePerLitre: null, currency: 'CZK' } })
    expect(await screen.findByText('≈ 0 CZK')).toBeTruthy()
    expect(screen.queryByText('— (no fuel price yet)')).toBeNull()
  })

  it('shows no fuel price', async () => {
    renderIt({ ...base, cost: { fuel: null, time: 40, wear: 22.4, total: 62.4, pricePerLitre: null, currency: 'CZK' } })
    expect(await screen.findByText('— (no fuel price yet)')).toBeTruthy()
  })
})
