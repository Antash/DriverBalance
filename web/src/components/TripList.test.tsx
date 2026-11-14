import { cleanup, fireEvent, render, screen } from '@testing-library/react'
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
  summary: { count: 38, km: 6240, cost: 31900, currency: 'CZK' },
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

  it('shows a dash when nothing is known about the cost', async () => {
    const empty = { fuel: null, time: 0, wear: 0, total: 0, pricePerLitre: null, currency: 'CZK' }
    const p = {
      ...page,
      groups: [{ ...page.groups[0], cost: empty, trips: [{ ...trip, cost: empty }] }],
    } as TripPage
    vi.mocked(api.get).mockResolvedValue(p)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={qc}><TripList /></QueryClientProvider>)
    expect(await screen.findByRole('columnheader', { name: 'Cost' })).toBeTruthy()
    expect(screen.queryByText('≈ 0 CZK')).toBeNull()
    expect(screen.getAllByText('—').length).toBeGreaterThan(0)
  })

  const renderIt = (ui: React.ReactElement) => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>)
  }
  const lastUrl = () => String(vi.mocked(api.get).mock.calls.at(-1)![0])

  it('explains an empty list when filters are on', async () => {
    const none = { ...page, total: 0, groups: [], summary: { count: 0, km: 0, cost: 0, currency: 'CZK' } } as TripPage
    vi.mocked(api.get).mockResolvedValue(none)
    renderIt(<TripList minKm={20} onMinKm={vi.fn()} />)
    expect(await screen.findByText('No drives match these filters.')).toBeTruthy()
    cleanup()
    renderIt(<TripList level="Trip" route="abc" routeLabel="A ↔ B" onClearRoute={vi.fn()} />)
    expect(await screen.findByText('No trips match these filters.')).toBeTruthy()
    cleanup()
    renderIt(<TripList />)
    expect(await screen.findByText('No trips in this period')).toBeTruthy()
  })

  it('shows a dash in the summary when the cost is unknown', async () => {
    vi.mocked(api.get).mockResolvedValue({ ...page, summary: { count: 38, km: 6240, cost: 0, currency: 'CZK' } })
    renderIt(<TripList />)
    expect(await screen.findByText('38 drives · 6,240 km · —')).toBeTruthy()
  })

  it('shows the summary line for everything that matches', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    renderIt(<TripList />)
    expect(await screen.findByText('38 drives · 6,240 km · ≈ 31,900 CZK')).toBeTruthy()
  })

  it('says trips at Trip level, singular for one, and renders no expand buttons', async () => {
    vi.mocked(api.get).mockResolvedValue({ ...page, summary: { count: 1, km: 50, cost: 142.4, currency: 'CZK' } })
    renderIt(<TripList level="Trip" />)
    expect(await screen.findByText('1 trip · 50 km · ≈ 142 CZK')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /group/i })).toBeNull()
    expect(screen.getByRole('button', { name: /11/ })).toBeTruthy()
    expect(lastUrl()).toContain('level=Trip')
  })

  it('moves the slider through the steps and reports the km', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    const onMinKm = vi.fn()
    renderIt(<TripList minKm={0} onMinKm={onMinKm} />)
    expect(await screen.findByText('All distances')).toBeTruthy()
    fireEvent.change(screen.getByRole('slider', { name: 'Minimum distance' }), { target: { value: '3' } })
    expect(onMinKm).toHaveBeenCalledWith(20)
  })

  it('labels the slider and sends minKm to the API', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    renderIt(<TripList minKm={20} onMinKm={() => {}} />)
    expect(await screen.findByText('Longer than 20 km')).toBeTruthy()
    expect((screen.getByRole('slider', { name: 'Minimum distance' }) as HTMLInputElement).value).toBe('3')
    expect(lastUrl()).toContain('minKm=20')
  })

  it('shows the route chip, sends the key and clears it on click', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    const onClear = vi.fn()
    renderIt(<TripList route="0123456789abcdef" routeLabel="Home ↔ Work" onClearRoute={onClear} />)
    const chip = await screen.findByRole('button', { name: 'Route: Home ↔ Work ✕' })
    expect(lastUrl()).toContain('route=0123456789abcdef')
    fireEvent.click(chip)
    expect(onClear).toHaveBeenCalled()
  })

  it('falls back to a generic chip label', async () => {
    vi.mocked(api.get).mockResolvedValue(page)
    renderIt(<TripList route="0123456789abcdef" onClearRoute={() => {}} />)
    expect(await screen.findByRole('button', { name: 'Route: selected route ✕' })).toBeTruthy()
  })
})
