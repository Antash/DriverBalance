import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import indexHtml from '../../index.html?raw'
import { LANDING } from '../lib/landing'
import SignIn from './SignIn'

afterEach(cleanup)

const renderIt = (code: string | null = null) => render(<MemoryRouter><SignIn code={code} /></MemoryRouter>)

describe('SignIn start page', () => {
  it('explains the app before asking anyone to sign in', () => {
    renderIt()
    expect(screen.getByRole('heading', { level: 1, name: LANDING.title })).toBeTruthy()
    for (const f of LANDING.features) expect(screen.getByRole('heading', { name: f.title })).toBeTruthy()
    expect(screen.getByText(/not affiliated with or endorsed by Volvo Cars/)).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Privacy' }).getAttribute('href')).toBe('/privacy')
  })

  it('signs in with Google and comes back to the same place', () => {
    renderIt()
    const links = screen.getAllByRole('link', { name: /Sign in with Google/ })
    expect(links.length).toBeGreaterThan(0)
    for (const l of links) expect(l.getAttribute('href')).toBe('/auth/login?returnUrl=%2F')
  })

  it('shows why the last sign-in did not work', () => {
    renderIt('invite-only')
    expect(screen.getByRole('alert').textContent).toMatch(/invite-only/)
  })
})

describe('static start page in index.html', () => {
  const plain = (s: string) => s.replace(/\s+/g, ' ').replace(/&amp;/g, '&')
  const html = plain(indexHtml)

  it('carries the same words as the app, between the markers the server strips for signed-in people', () => {
    const start = html.indexOf('<!--landing-->'), end = html.indexOf('<!--/landing-->')
    expect(start).toBeGreaterThan(html.indexOf('<div id="root">'))
    expect(end).toBeGreaterThan(start)
    const block = html.slice(start, end)
    for (const s of [LANDING.title, LANDING.intro, LANDING.signInNote, LANDING.about, ...LANDING.steps,
      ...LANDING.features.flatMap((f) => [f.title, f.text])]) expect(block).toContain(plain(s))
    expect(block).toContain('href="/auth/login?returnUrl=%2F"')
    expect(block).toContain('href="/privacy"')
  })

  it('describes the site in the head', () => {
    expect(html).toMatch(/<meta name="description" content="[^"]{50,}"/)
    expect(html).toMatch(/<meta property="og:title"/)
    expect(html).toMatch(/<meta property="og:description"/)
    expect(html).toMatch(/<title>DriverBalance – [^<]+<\/title>/)
  })
})
