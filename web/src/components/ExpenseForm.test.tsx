import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Vehicle } from '../api/types'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
}))
import { api } from '../api/client'
import { ExpenseForm } from './ExpenseForm'

const vehicle: Vehicle = {
  id: 7, name: 'Car', homeCurrency: 'CZK', timeZone: 'Europe/Prague', initialTyreSetId: null, isDefault: true,
  historyStart: null, historyStartOdometer: null,
}
const m = vi.mocked(api)

beforeEach(() => { vi.clearAllMocks(); m.get.mockResolvedValue([]) })
afterEach(cleanup)

describe('ExpenseForm save then upload', () => {
  it('keeps the form open on upload failure and retries as an update of the same expense', async () => {
    m.post.mockResolvedValue({ id: 42 })
    m.put.mockResolvedValue({ id: 42 })
    m.upload.mockRejectedValueOnce(Object.assign(new Error('bad'), { errors: ['File too large.'] }))
      .mockResolvedValueOnce([{ id: 1, fileName: 'a.pdf', contentType: 'application/pdf', sizeBytes: 3 }])
    const onDone = vi.fn()
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={qc}>
      <ExpenseForm vehicle={vehicle} tyreSets={[]} lockCategory="Parking" onDone={onDone} />
    </QueryClientProvider>)

    fireEvent.change(screen.getByLabelText('Amount'), { target: { value: '100' } })
    const file = new File(['abc'], 'a.pdf', { type: 'application/pdf' })
    fireEvent.change(screen.getByLabelText('Attach files'), { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Add expense' }))

    expect(await screen.findByText('File too large.')).toBeTruthy()
    expect(m.post).toHaveBeenCalledTimes(1)
    expect(m.upload).toHaveBeenCalledTimes(1)
    expect(m.upload.mock.calls[0][0]).toBe('/vehicles/7/expenses/42/documents')
    expect(onDone).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1))
    expect(m.post).toHaveBeenCalledTimes(1)
    expect(m.put).toHaveBeenCalledTimes(1)
    expect(m.put.mock.calls[0][0]).toBe('/vehicles/7/expenses/42')
    expect(m.upload).toHaveBeenCalledTimes(2)
    expect(m.upload.mock.calls[1][0]).toBe('/vehicles/7/expenses/42/documents')
  })
})
