import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ParamsProvider } from '../state/ParamsContext'
import { ParamsPanel } from './ParamsPanel'

const defaults = {
  coldStartGapMinutes: 45, shortDurationMinutes: 20, mediumDurationMinutes: 30, longDurationMinutes: 40,
  heavyTrafficSpeedKmh: 25, citySpeedKmh: 35, mixedSpeedKmh: 50, dpfMovingAverageWeeks: 3,
  fuelTrendMinLPer100: 1, fuelTrendMaxLPer100: 30, lowessSpan: 0.25, heatmapKmCap: 100,
  longDriveHintDays: 14, exportReminderDays: 30,
}
let puts: unknown[]

beforeEach(() => {
  puts = []
  vi.stubGlobal('fetch', vi.fn(async (_u: string, init?: RequestInit) => {
    const body = init?.body ? JSON.parse(init.body as string) : undefined
    if (init?.method === 'PUT') puts.push(body)
    return new Response(JSON.stringify(init?.method === 'PUT' ? body : defaults), { status: 200, headers: { 'Content-Type': 'application/json' } })
  }))
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

async function open() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><ParamsProvider vehicleId={1}><ParamsPanel onClose={() => {}} /></ParamsProvider></QueryClientProvider>)
  await screen.findByLabelText('Gap between trips (min)')
}
const input = (l: string) => screen.getByLabelText(l) as HTMLInputElement

describe('ParamsPanel', () => {
  it('Save right after typing persists the typed values, including two fields', async () => {
    await open()
    fireEvent.change(input('Gap between trips (min)'), { target: { value: '50' } })
    fireEvent.change(input('LOWESS span'), { target: { value: '0.5' } })
    fireEvent.click(screen.getByText('Save'))
    await waitFor(() => expect(puts).toHaveLength(1))
    expect(puts[0]).toMatchObject({ coldStartGapMinutes: 50, lowessSpan: 0.5 })
  })

  it('rejects a decimal in an integer field with a message and disables Save', async () => {
    await open()
    fireEvent.change(input('Gap between trips (min)'), { target: { value: '45.5' } })
    expect(screen.getByRole('alert').textContent).toMatch(/whole number/i)
    expect((screen.getByText('Save') as HTMLButtonElement).disabled).toBe(true)
    fireEvent.blur(input('Gap between trips (min)'))
    expect(input('Gap between trips (min)').value).toBe('45')
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('marks an empty field invalid and reverts to the draft on blur', async () => {
    await open()
    fireEvent.change(input('Heatmap cap (km)'), { target: { value: '' } })
    expect(screen.getByRole('alert').textContent).toMatch(/number/i)
    fireEvent.blur(input('Heatmap cap (km)'))
    expect(input('Heatmap cap (km)').value).toBe('100')
  })

  it('shows the cross-field validation message live and disables Save', async () => {
    await open()
    fireEvent.change(input('Short trip (min)'), { target: { value: '35' } })
    expect(screen.getByRole('alert').textContent).toMatch(/0 < short <= medium <= long/)
    expect((screen.getByText('Save') as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(input('Medium trip (min)'), { target: { value: '36' } })
    expect(screen.queryByRole('alert')).toBeNull()
    expect((screen.getByText('Save') as HTMLButtonElement).disabled).toBe(false)
  })
})
