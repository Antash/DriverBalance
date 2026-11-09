import { describe, expect, it } from 'vitest'
import { parseDmy, toDmy } from './dates'

describe('toDmy', () => {
  it('converts ISO', () => expect(toDmy('2026-11-09')).toBe('09/11/2026'))
  it('empty for empty', () => expect(toDmy('')).toBe(''))
})
describe('parseDmy', () => {
  it.each(['09/11/2026', '9/11/2026', '09.11.2026', '09-11-2026', ' 9/1/2026 '.replace('1/', '11/')])('accepts %s', (t) => expect(parseDmy(t)).toBe('2026-11-09'))
  it('pads', () => expect(parseDmy('1/2/2026')).toBe('2026-02-01'))
  it('rejects impossible dates', () => { expect(parseDmy('31/02/2026')).toBeNull(); expect(parseDmy('00/01/2026')).toBeNull(); expect(parseDmy('01/13/2026')).toBeNull() })
  it('rejects garbage', () => { expect(parseDmy('')).toBeNull(); expect(parseDmy('abc')).toBeNull(); expect(parseDmy('1/1/26')).toBeNull() })
  it('leap day', () => { expect(parseDmy('29/02/2028')).toBe('2028-02-29'); expect(parseDmy('29/02/2027')).toBeNull() })
})
