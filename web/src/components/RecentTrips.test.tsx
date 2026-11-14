import { cleanup, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { TripPage } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
vi.mock('../state/ParamsContext', () => ({ useParams: () => ({ vehicleId: 1 }) }))
import { api } from '../api/client'
import { RecentTrips } from './RecentTrips'

const cost = { fuel: 80, time: 40, wear: 22.4, total: 142.4, pricePerLitre: 38.4, currency: 'CZK' }
const trip = (id: number, from: string, to: string) => ({
  id, category: 'Private', start: '2026-11-09T08:05:00', startOdometer: 100, startAddress: from,
  end: '2026-11-09T09:05:00', endOdometer: 150, endAddress: to, duration: '01:00:00', distanceKm: 50,
  fuelLiters: 3, notes: '', averageSpeedKmh: 50, cost,
})
const group = (trips: ReturnType<typeof trip>[]) => ({
  trips, score: 'Good', start: trips[0].start, end: trips.at(-1)!.end, distanceKm: 50, duration: '01:00:00',
  fuelLiters: 3, averageSpeedKmh: 50, lPer100: 6, startDate: '2026-11-09', cost,
})

beforeEach(() => vi.clearAllMocks())
afterEach(() => cleanup())

function renderIt() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={qc}><MemoryRouter><RecentTrips /></MemoryRouter></QueryClientProvider>)
}

describe('RecentTrips', () => {
  it('asks for the five newest drives and lists them with route and cost', async () => {
    vi.mocked(api.get).mockResolvedValue({
      total: 40, page: 1, pageSize: 5,
      groups: [group([trip(1, 'Home St 1', 'Shop 2'), trip(2, 'Shop 2', 'Work Ave 5')]), group([trip(3, 'Work Ave 5', 'Home St 1')])],
      summary: { count: 40, km: 2000, cost: 5000, currency: 'CZK' },
    } as TripPage)
    renderIt()
    expect(await screen.findByRole('heading', { name: 'Recent trips' })).toBeTruthy()
    expect(vi.mocked(api.get).mock.calls[0][0]).toContain('pageSize=5')
    expect(screen.getByText('Home St 1 → Work Ave 5')).toBeTruthy()
    expect(screen.getByText('2 trips')).toBeTruthy()
    expect(screen.getAllByText('≈ 142 CZK')[0].getAttribute('title')).toContain('Fuel ≈')
    expect(screen.getByRole('link', { name: 'All trips' }).getAttribute('href')).toBe('/trips?tab=list')
  })

  it('renders nothing without trips', async () => {
    vi.mocked(api.get).mockResolvedValue({ total: 0, page: 1, pageSize: 5, groups: [], summary: { count: 0, km: 0, cost: 0, currency: 'CZK' } } as TripPage)
    const { container } = renderIt()
    await vi.waitFor(() => expect(api.get).toHaveBeenCalled())
    await new Promise((r) => setTimeout(r, 0))
    expect(container.textContent).toBe('')
  })
})
