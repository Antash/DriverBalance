import { describe, expect, it } from 'vitest'
import { fmtApprox, fmtDate, fmtDateTime, fmtInstant } from './format'

describe('fmtDate', () => {
  it('formats ISO as dd/mm/yyyy', () => expect(fmtDate('2026-11-09')).toBe('09/11/2026'))
  it('accepts a datetime string', () => expect(fmtDate('2026-11-09T08:05:00')).toBe('09/11/2026'))
  it('dashes for empty', () => { expect(fmtDate(null)).toBe('—'); expect(fmtDate(undefined)).toBe('—'); expect(fmtDate('')).toBe('—') })
})
describe('fmtDateTime', () => {
  it('formats as dd/mm/yyyy HH:mm', () => expect(fmtDateTime('2026-11-09T08:05:00')).toBe('09/11/2026 08:05'))
})

describe('fmtInstant', () => {
  it('renders local time as dd/mm/yyyy HH:mm', () => {
    expect(fmtInstant(new Date(2026, 10, 9, 8, 5).toISOString())).toBe('09/11/2026 08:05')
  })
  it('dashes for garbage', () => expect(fmtInstant('nope')).toBe('—'))
})
describe('fmtApprox', () => {
  it('rounds with a currency', () => expect(fmtApprox(142.4, 'CZK')).toBe('≈ 142 CZK'))
  it('dashes for null', () => expect(fmtApprox(null, 'CZK')).toBe('—'))
  it('one decimal below 10', () => expect(fmtApprox(2.13, 'EUR')).toBe('≈ 2.1 EUR'))
  it('no trailing zero below 10', () => expect(fmtApprox(2.04, 'EUR')).toBe('≈ 2 EUR'))
  it('whole units from 10', () => expect(fmtApprox(9.96, 'EUR')).toBe('≈ 10 EUR'))
})
