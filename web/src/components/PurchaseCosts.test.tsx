import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn(), upload: vi.fn() },
}))
import { api } from '../api/client'
import { fmtMoney } from '../lib/format'
import { PurchaseCosts } from './PurchaseCosts'

const m = vi.mocked(api)
const costs = [
  { id: 1, date: '2025-04-14', amount: 1250000, note: 'Car' },
  { id: 2, date: '2025-04-10', amount: 2500, note: null },
]

function show() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><PurchaseCosts vehicleId={7} currency="CZK" /></QueryClientProvider>)
}

beforeEach(() => { vi.clearAllMocks(); m.get.mockResolvedValue(costs) })
afterEach(cleanup)

describe('PurchaseCosts', () => {
  it('lists costs as dd/mm/yyyy with amount and note', async () => {
    show()
    expect(await screen.findByText('14/04/2025')).toBeTruthy()
    expect(screen.getByText((_, el) => el?.tagName === 'TD' && el.textContent === fmtMoney(1250000, 'CZK'))).toBeTruthy()
    expect(screen.getByText('Car')).toBeTruthy()
    expect(screen.getByText('10/04/2025')).toBeTruthy()
  })

  it('always offers Add cost, even with no costs, and posts a new one', async () => {
    m.get.mockResolvedValue([])
    m.post.mockResolvedValue({ id: 3 })
    show()
    fireEvent.click(await screen.findByRole('button', { name: 'Add cost' }))
    fireEvent.change(screen.getByLabelText('Cost date'), { target: { value: '14/04/2025' } })
    fireEvent.change(screen.getByLabelText('Amount (CZK)'), { target: { value: '35000' } })
    fireEvent.change(screen.getByLabelText('Note'), { target: { value: ' Warranty ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    await waitFor(() => expect(m.post).toHaveBeenCalledTimes(1))
    expect(m.post).toHaveBeenCalledWith('/vehicles/7/purchase-costs', { date: '2025-04-14', amount: 35000, note: 'Warranty' })
  })

  it('does not save without a date or a positive amount', async () => {
    m.get.mockResolvedValue([])
    show()
    fireEvent.click(await screen.findByRole('button', { name: 'Add cost' }))
    expect((screen.getByRole('button', { name: 'Add' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('Amount (CZK)'), { target: { value: '5' } })
    expect((screen.getByRole('button', { name: 'Add' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('edits a cost with PUT and deletes after confirmation', async () => {
    m.put.mockResolvedValue({ id: 2 })
    m.del.mockResolvedValue(undefined)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    show()
    await screen.findByText('10/04/2025')
    fireEvent.click(screen.getAllByRole('button', { name: 'Edit' })[1])
    fireEvent.change(screen.getByLabelText('Amount (CZK)'), { target: { value: '3000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save cost' }))
    await waitFor(() => expect(m.put).toHaveBeenCalledWith('/vehicles/7/purchase-costs/2', { date: '2025-04-10', amount: 3000, note: null }))
    fireEvent.click(screen.getAllByRole('button', { name: 'Delete' })[0])
    await waitFor(() => expect(m.del).toHaveBeenCalledWith('/vehicles/7/purchase-costs/1'))
  })
})
