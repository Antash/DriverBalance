import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Vehicle } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
import { api } from '../api/client'
import DemoBanner from './DemoBanner'

const vehicle = { id: 9, name: 'Demo', isDemo: true } as Vehicle
const m = vi.mocked(api)
beforeEach(() => vi.clearAllMocks())
afterEach(() => { cleanup(); vi.restoreAllMocks() })

function renderIt() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const spy = vi.spyOn(qc, 'invalidateQueries')
  render(<QueryClientProvider client={qc}><DemoBanner vehicle={vehicle} /></QueryClientProvider>)
  return spy
}

describe('DemoBanner', () => {
  it('shows the notice and both actions', () => {
    renderIt()
    expect(screen.getByText('Demo car with made-up data.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Add your real car' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Delete demo car' })).toBeTruthy()
  })

  it('does not delete when the confirmation is declined', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderIt()
    fireEvent.click(screen.getByRole('button', { name: 'Delete demo car' }))
    expect(confirm).toHaveBeenCalledWith('Delete the demo car and all its data?')
    expect(m.del).not.toHaveBeenCalled()
  })

  it('deletes and refreshes the vehicles when confirmed', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    m.del.mockResolvedValue(undefined)
    const spy = renderIt()
    fireEvent.click(screen.getByRole('button', { name: 'Delete demo car' }))
    await waitFor(() => expect(m.del).toHaveBeenCalledWith('/vehicles/9'))
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ queryKey: ['vehicles'] }))
  })

  it('opens the first-car form in a modal and closes it on Cancel', () => {
    renderIt()
    fireEvent.click(screen.getByRole('button', { name: 'Add your real car' }))
    expect(screen.getByRole('dialog')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Try with a demo car' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
