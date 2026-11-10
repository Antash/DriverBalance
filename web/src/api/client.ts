import type { ApiErrorBody } from './types'

export class ApiError extends Error {
  status: number
  errors?: string[]

  constructor(status: number, errors?: string[], message?: string) {
    super(message ?? errors?.join('; ') ?? `Request failed (${status})`)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

let onUnauthorized: (() => void) | undefined
/** Called when any API call answers 401 (the session ended): the app then shows the sign-in page. */
export function setUnauthorizedHandler(handler: (() => void) | undefined) { onUnauthorized = handler }

async function request<T>(method: string, path: string, body?: unknown, form?: FormData): Promise<T> {
  const init: RequestInit = { method }
  if (form) init.body = form
  else if (body !== undefined) {
    init.body = JSON.stringify(body)
    init.headers = { 'Content-Type': 'application/json' }
  }
  const res = await fetch(`/api${path}`, init)
  if (!res.ok) {
    if (res.status === 401 && path !== '/me') onUnauthorized?.() // /me is how the app asks; reacting to it would loop
    let errors: string[] | undefined
    try {
      const parsed = (await res.json()) as Partial<ApiErrorBody>
      if (Array.isArray(parsed.errors)) errors = parsed.errors
    } catch {
      // body was empty or not JSON
    }
    throw new ApiError(res.status, errors)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  del: <T>(path: string, body?: unknown) => request<T>('DELETE', path, body),
  upload: <T>(path: string, form: FormData) => request<T>('POST', path, undefined, form),
}
