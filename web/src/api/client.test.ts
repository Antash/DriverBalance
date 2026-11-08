import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError } from './client'

afterEach(() => vi.unstubAllGlobals())

function stubFetch(status: number, body: unknown) {
  vi.stubGlobal('fetch', vi.fn(async () =>
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })))
}

describe('api client', () => {
  it('throws ApiError with errors on a 400', async () => {
    stubFetch(400, { errors: ['Durations must satisfy 0 < short <= medium <= long.'] })
    const err = (await api.put('/vehicles/1/parameters', {}).catch((e: unknown) => e)) as ApiError
    expect(err).toBeInstanceOf(ApiError)
    expect(err.status).toBe(400)
    expect(err.errors).toEqual(['Durations must satisfy 0 < short <= medium <= long.'])
  })

  it('returns parsed JSON on success and prefixes /api', async () => {
    stubFetch(200, { a: 1 })
    expect(await api.get<{ a: number }>('/vehicles')).toEqual({ a: 1 })
    expect((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0][0]).toBe('/api/vehicles')
  })
})
