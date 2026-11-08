import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ParamsProvider, useParams } from './ParamsContext'
import type { AnalysisParameters } from '../api/types'

const defaults: AnalysisParameters = {
  coldStartGapMinutes: 45, shortDurationMinutes: 20, mediumDurationMinutes: 30, longDurationMinutes: 40,
  heavyTrafficSpeedKmh: 25, citySpeedKmh: 35, mixedSpeedKmh: 50, dpfMovingAverageWeeks: 3,
  fuelTrendMinLPer100: 1, fuelTrendMaxLPer100: 30, lowessSpan: 0.25, heatmapKmCap: 100,
  longDriveHintDays: 14, exportReminderDays: 30,
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async () =>
    new Response(JSON.stringify(defaults), { status: 200, headers: { 'Content-Type': 'application/json' } })))
})
afterEach(() => vi.unstubAllGlobals())

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}><ParamsProvider vehicleId={1}>{children}</ParamsProvider></QueryClientProvider>
  )
  return renderHook(() => useParams(), { wrapper })
}

describe('ParamsContext', () => {
  it('starts clean with no override', async () => {
    const { result } = setup()
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    expect(result.current.dirty).toBe(false)
    expect(result.current.override).toBeUndefined()
  })

  it('setDraft makes dirty and override equals the full draft; reset clears', async () => {
    const { result } = setup()
    await waitFor(() => expect(result.current.saved.coldStartGapMinutes).toBe(45))
    act(() => result.current.setDraft({ coldStartGapMinutes: 60 }))
    expect(result.current.dirty).toBe(true)
    expect(result.current.draft.coldStartGapMinutes).toBe(60)
    expect(result.current.override).toEqual({ ...defaults, coldStartGapMinutes: 60 })
    act(() => result.current.reset())
    expect(result.current.dirty).toBe(false)
    expect(result.current.override).toBeUndefined()
    expect(result.current.draft).toEqual(defaults)
  })
})
