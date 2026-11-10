import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
  ApiError: class extends Error {},
}))
import { api } from '../api/client'
import FirstCar from './FirstCar'

const m = vi.mocked(api)
beforeEach(() => vi.clearAllMocks())
afterEach(cleanup)

function renderIt(props: React.ComponentProps<typeof FirstCar> = {}) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={qc}><FirstCar {...props} /></QueryClientProvider>)
}

describe('FirstCar demo option', () => {
  it('offers a demo car and posts once, disabled while pending', async () => {
    let resolve!: (v: unknown) => void
    m.post.mockReturnValue(new Promise((r) => { resolve = r }))
    const onDone = vi.fn()
    renderIt({ onDone })
    expect(screen.getByText('A made-up BMW with 3½ years of trips, services and costs. Delete it any time; adding your real car removes it.')).toBeTruthy()
    const btn = screen.getByRole('button', { name: 'Try with a demo car' }) as HTMLButtonElement
    fireEvent.click(btn)
    await waitFor(() => expect(btn.disabled).toBe(true))
    fireEvent.click(btn)
    expect(m.post).toHaveBeenCalledTimes(1)
    expect(m.post.mock.calls[0][0]).toBe('/vehicles/demo')
    resolve({ id: 1 })
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1))
  })

  it('without the demo option shows Cancel instead', () => {
    const onCancel = vi.fn()
    renderIt({ allowDemo: false, onCancel })
    expect(screen.queryByRole('button', { name: 'Try with a demo car' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(onCancel).toHaveBeenCalledTimes(1)
  })
})
