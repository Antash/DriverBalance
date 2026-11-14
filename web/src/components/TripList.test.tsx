import { cleanup, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { TripPage } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
vi.mock('../state/ParamsContext', () => ({ useParams: () => ({ vehicleId: 1 }) }))
vi.mock('./SavedParamsNote', () => ({ SavedParamsNote: () => null }))
import { api } from '../api/client'
import { TripList } from './TripList'

const cost = { fuel: 80, time: 40, wear: 22.4, total: 142.4, pricePerLitre: 38.4, currency: 'CZK' }
const trip = {
  id: 5, category: 'Private', start: '2026-11-09T08:05:00', startOdometer: 100, startAddress: 'A',
  end: '2026-11-09T09:05:00', endOdometer: 150, endAddress: 'B', duration: '01:00:00', distanceKm: 50,
  fuelLiters: 3, notes: '', averageSpeedKmh: 50, cost,
}
const page: TripPage = {
  total: 1, page: 1, pageSize: 25,
  groups: [{ trips: [trip], score: 'Good', start: trip.start, end: trip.end, distanceKm: 50, duration: '01:00:00',
    fuelLiters: 3, averageSpeedKmh: 50, lPer100: 6, startDate: '2026-11-09', cost }],
} as TripPage

beforeEach(() => vi.clearAllMocks())
afterEach(() => cleanup())

describe('TripList', () => {
  it('renders the cost column', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={qc}><TripList /></QueryClientProvider>)
    expect(await screen.findByText('≈ 142 CZK')).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: 'Cost' })).toBeTruthy()
    expect(screen.getByText('≈ 142 CZK').getAttribute('title')).toContain('Fuel ≈')
  })
})
