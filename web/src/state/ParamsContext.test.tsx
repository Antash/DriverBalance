import { act, cleanup, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ParamsProvider, useAnalytics, useParams } from './ParamsContext'
import type { AnalysisParameters } from '../api/types'

const defaults: AnalysisParameters = {
  coldStartGapMinutes: 45, shortDurationMinutes: 20, mediumDurationMinutes: 30, longDurationMinutes: 40,
  heavyTrafficSpeedKmh: 25, citySpeedKmh: 35, mixedSpeedKmh: 50, dpfMovingAverageWeeks: 3,
  fuelTrendMinLPer100: 1, fuelTrendMaxLPer100: 30, lowessSpan: 0.25, heatmapKmCap: 100,
  longDriveHintDays: 14, exportReminderDays: 30,
}

let calls: { url: string; method: string; body: unknown }[]

beforeEach(() => {
  calls = []
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    const body = init?.body ? JSON.parse(init.body as string) : undefined
    calls.push({ url, method: init?.method ?? 'GET', body })
    const payload = init?.method === 'PUT' ? body : url.includes('/analytics/') ? { ok: true } : defaults
    return new Response(JSON.stringify(payload), { status: 200, headers: { 'Content-Type': 'application/json' } })
  }))
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

function setup<T>(hook: () => T) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}><ParamsProvider vehicleId={1}>{children}</ParamsProvider></QueryClientProvider>
  )
  return renderHook(hook, { wrapper })
}

describe('ParamsContext', () => {
  it('starts clean with no override', async () => {
    const { result } = setup(() => useParams())
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    expect(result.current.dirty).toBe(false)
    expect(result.current.override).toBeUndefined()
  })

  it('setDraft is immediate; override follows after the debounce; reset clears', async () => {
    const { result } = setup(() => useParams())
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    act(() => result.current.setDraft({ coldStartGapMinutes: 60 }))
    expect(result.current.dirty).toBe(true)
    expect(result.current.draft.coldStartGapMinutes).toBe(60)
    await waitFor(() => expect(result.current.override).toEqual({ ...defaults, coldStartGapMinutes: 60 }))
    act(() => result.current.reset())
    expect(result.current.dirty).toBe(false)
    expect(result.current.override).toBeUndefined()
    expect(result.current.draft).toEqual(defaults)
  })

  it('save immediately after setDraft PUTs the typed value', async () => {
    const { result } = setup(() => useParams())
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    act(() => result.current.setDraft({ coldStartGapMinutes: 61 }))
    await act(async () => { await result.current.save() })
    const put = calls.find((c) => c.method === 'PUT')
    expect(put?.body).toEqual({ ...defaults, coldStartGapMinutes: 61 })
    expect(result.current.dirty).toBe(false)
  })

  it('two quick edits to different fields are both kept', async () => {
    const { result } = setup(() => useParams())
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    act(() => { result.current.setDraft({ coldStartGapMinutes: 50 }); result.current.setDraft({ lowessSpan: 0.5 }) })
    expect(result.current.draft).toMatchObject({ coldStartGapMinutes: 50, lowessSpan: 0.5 })
    await act(async () => { await result.current.save() })
    expect(calls.find((c) => c.method === 'PUT')?.body).toMatchObject({ coldStartGapMinutes: 50, lowessSpan: 0.5 })
  })

  it('useAnalytics posts saved first, then the override after the debounce', async () => {
    const { result } = setup(() => ({ p: useParams(), q: useAnalytics('overview', {}) }))
    await waitFor(() => expect(result.current.q.data).toBeDefined())
    const posts = () => calls.filter((c) => c.method === 'POST')
    expect(posts()).toHaveLength(1)
    expect((posts()[0].body as { parameters?: unknown }).parameters).toBeUndefined()
    act(() => result.current.p.setDraft({ coldStartGapMinutes: 77 }))
    expect(posts()).toHaveLength(1) // not yet: debounced
    await waitFor(() => expect(posts()).toHaveLength(2))
    expect((posts()[1].body as { parameters: AnalysisParameters }).parameters.coldStartGapMinutes).toBe(77)
  })
})
