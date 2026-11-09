import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { FinancingFields, type FinancingValue } from './FinancingFields'

afterEach(cleanup)
const empty: FinancingValue = { financingStart: null, financingAmount: null, financingFrequency: null, financingPayments: null, financingBalloon: null }

describe('FinancingFields', () => {
  it('shows an existing contract with a dd/mm/yyyy start date', () => {
    render(<FinancingFields currency="CZK" onChange={() => {}}
      value={{ financingStart: '2025-05-01', financingAmount: 9500, financingFrequency: 'Quarterly', financingPayments: 36, financingBalloon: 200000 }} />)
    expect((screen.getByLabelText('Financing start') as HTMLInputElement).value).toBe('01/05/2025')
    expect((screen.getByLabelText('Amount per payment (CZK)') as HTMLInputElement).value).toBe('9500')
    expect((screen.getByLabelText('Frequency') as HTMLSelectElement).value).toBe('Quarterly')
    expect((screen.getByLabelText('Number of payments') as HTMLInputElement).value).toBe('36')
  })

  it('Remove financing clears all five fields, and is hidden when there is none', () => {
    const onChange = vi.fn()
    const { rerender } = render(<FinancingFields currency="CZK" value={empty} onChange={onChange} />)
    expect(screen.queryByRole('button', { name: 'Remove financing' })).toBeNull()
    rerender(<FinancingFields currency="CZK" onChange={onChange}
      value={{ financingStart: '2025-05-01', financingAmount: 9500, financingFrequency: 'Quarterly', financingPayments: 36, financingBalloon: 5 }} />)
    fireEvent.click(screen.getByRole('button', { name: 'Remove financing' }))
    expect(onChange).toHaveBeenCalledWith(empty)
  })

  it('defaults the frequency to Monthly and reports edits; clearing a field gives null', () => {
    const onChange = vi.fn()
    render(<FinancingFields currency="CZK" value={empty} onChange={onChange} />)
    expect((screen.getByLabelText('Frequency') as HTMLSelectElement).value).toBe('Monthly')
    fireEvent.change(screen.getByLabelText('Financing start'), { target: { value: '01/05/2025' } })
    expect(onChange).toHaveBeenLastCalledWith({ ...empty, financingStart: '2025-05-01' })
    fireEvent.change(screen.getByLabelText('Number of payments'), { target: { value: '36' } })
    expect(onChange).toHaveBeenLastCalledWith({ ...empty, financingPayments: 36 })
    fireEvent.change(screen.getByLabelText('Frequency'), { target: { value: 'Yearly' } })
    expect(onChange).toHaveBeenLastCalledWith({ ...empty, financingFrequency: 'Yearly' })
  })
})
