import { describe, expect, it } from 'vitest'
import { initials, signInMessage, signInUrl } from './account'

describe('signInMessage', () => {
  it('explains each way sign-in can end', () => {
    expect(signInMessage('invite-only')).toMatch(/invite-only/)
    expect(signInMessage('blocked')).toMatch(/removed/)
    expect(signInMessage('failed')).toMatch(/did not work/)
    expect(signInMessage('expired')).toMatch(/session/)
  })
  it('says nothing for no code or an unknown one', () => {
    expect(signInMessage(null)).toBeNull()
    expect(signInMessage(undefined)).toBeNull()
    expect(signInMessage('<script>')).toBeNull()
  })
})

describe('signInUrl', () => {
  it('keeps a same-site path', () => {
    expect(signInUrl('/trips?x=1')).toBe('/auth/login?returnUrl=%2Ftrips%3Fx%3D1')
  })
  it('never sends people to another site', () => {
    for (const bad of ['//evil.example', '/\\evil.example', 'https://evil.example', 'trips'])
      expect(signInUrl(bad)).toBe('/auth/login?returnUrl=%2F')
  })
})

describe('initials', () => {
  it('uses first and last name, or the start of a single name', () => {
    expect(initials('Anna Kowalska')).toBe('AK')
    expect(initials('  anna  maria  kowalska ')).toBe('AK')
    expect(initials('Cher')).toBe('CH')
    expect(initials('')).toBe('?')
  })
})
