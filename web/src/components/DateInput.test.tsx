import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DateInput } from './DateInput'

afterEach(cleanup)
const field = () => screen.getByLabelText('When') as HTMLInputElement

describe('DateInput', () => {
  it('shows an ISO value as dd/mm/yyyy', () => {
    render(<DateInput aria-label="When" value="2026-11-09" onChange={() => {}} />)
    expect(field().value).toBe('09/11/2026')
  })
  it('reports ISO for valid typed text and normalises on blur', () => {
    const onChange = vi.fn()
    render(<DateInput aria-label="When" value="" onChange={onChange} />)
    fireEvent.change(field(), { target: { value: '9.11.2026' } })
    expect(onChange).toHaveBeenLastCalledWith('2026-11-09')
    fireEvent.blur(field())
    expect(field().value).toBe('09/11/2026')
  })
  it('does not report garbage and flags it on blur', () => {
    const onChange = vi.fn()
    render(<DateInput aria-label="When" value="" onChange={onChange} />)
    fireEvent.change(field(), { target: { value: '31/02/2026' } })
    fireEvent.blur(field())
    expect(onChange).not.toHaveBeenCalled()
    expect(screen.getByText('Use dd/mm/yyyy')).toBeTruthy()
  })
  it('reports empty when cleared', () => {
    const onChange = vi.fn()
    render(<DateInput aria-label="When" value="2026-11-09" onChange={onChange} />)
    fireEvent.change(field(), { target: { value: '' } })
    expect(onChange).toHaveBeenLastCalledWith('')
  })
  it('takes a pick from the native calendar', () => {
    const onChange = vi.fn()
    const { container } = render(<DateInput aria-label="When" value="" onChange={onChange} />)
    fireEvent.change(container.querySelector('input[type="date"]')!, { target: { value: '2026-01-05' } })
    expect(onChange).toHaveBeenLastCalledWith('2026-01-05')
    expect(field().value).toBe('05/01/2026')
  })
})
