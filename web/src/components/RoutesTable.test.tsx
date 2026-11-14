import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RegularRoute } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
vi.mock('../state/ParamsContext', () => ({ useParams: () => ({ vehicleId: 1 }) }))
import { api } from '../api/client'
import { RoutesTable } from './RoutesTable'

const route: RegularRoute = {
  key: '0123456789abcdef', from: 'Home', to: 'Work', via: ['School'], roundTrip: false, count: 120,
  typical: 'Weekdays · 7–8, 16–17', avgKm: 18.4, avgCost: 62.3, perMonth: 1240, share: 0.27, currency: 'CZK',
}

beforeEach(() => vi.clearAllMocks())
afterEach(() => cleanup())

const renderIt = (onRoute = vi.fn(), level: 'Group' | 'Trip' = 'Group') => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><RoutesTable from="2026-01-01" to="2026-12-31" level={level} onRoute={onRoute} /></QueryClientProvider>)
  return onRoute
}

describe('RoutesTable', () => {
  it('renders a route row and reports a click', async () => {
    vi.mocked(api.get).mockResolvedValue([route])
    const onRoute = renderIt()
    const row = await screen.findByRole('button', { name: /Home ↔ Work/ })
    expect(row.textContent).toContain(' · via School')
    expect(row.textContent).toContain('Weekdays · 7–8, 16–17')
    expect(row.textContent).toContain('≈ 62 CZK')
    expect(row.textContent).toContain('27%')
    expect(String(vi.mocked(api.get).mock.calls[0][0])).toContain('/vehicles/1/trips/routes?')
    expect(String(vi.mocked(api.get).mock.calls[0][0])).toContain('level=Group')
    expect(screen.getByRole('columnheader', { name: '≈ per drive' })).toBeTruthy()
    fireEvent.click(row)
    expect(onRoute).toHaveBeenCalledWith('0123456789abcdef')
  })

  it('opens a route with Enter', async () => {
    vi.mocked(api.get).mockResolvedValue([route])
    const onRoute = renderIt()
    fireEvent.keyDown(await screen.findByRole('button', { name: /Home ↔ Work/ }), { key: 'Enter' })
    expect(onRoute).toHaveBeenCalledWith('0123456789abcdef')
  })

  it('shows a round trip and uses trip wording at Trip level', async () => {
    vi.mocked(api.get).mockResolvedValue([{ ...route, roundTrip: true, to: 'Home', via: [] }])
    renderIt(vi.fn(), 'Trip')
    expect(await screen.findByRole('button', { name: /Home ↺/ })).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: 'Trips' })).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: '≈ per trip' })).toBeTruthy()
  })

  it('shows the empty line when there are no regular routes', async () => {
    vi.mocked(api.get).mockResolvedValue([])
    renderIt()
    expect(await screen.findByText('No regular routes yet: a route shows once it has been driven 5 times in this period.')).toBeTruthy()
  })
})
