import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { keepPreviousData, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { api } from '../api/client'
import type { AnalysisParameters } from '../api/types'

export interface ParamsState {
  vehicleId: number
  saved: AnalysisParameters
  draft: AnalysisParameters
  dirty: boolean
  setDraft(p: Partial<AnalysisParameters>): void
  save(): Promise<void>
  reset(): void
  /** The full draft when dirty (the server replaces the saved set entirely), else undefined. */
  override: AnalysisParameters | undefined
}

const Ctx = createContext<ParamsState | null>(null)

function same(a: AnalysisParameters, b: AnalysisParameters) {
  return (Object.keys(a) as (keyof AnalysisParameters)[]).every((k) => a[k] === b[k])
}

/** Mount with key={vehicleId} so a vehicle switch discards the draft. */
export function ParamsProvider({ vehicleId, children }: { vehicleId: number; children: ReactNode }) {
  const qc = useQueryClient()
  const { data: saved, isError } = useQuery({
    queryKey: ['parameters', vehicleId],
    queryFn: () => api.get<AnalysisParameters>(`/vehicles/${vehicleId}/parameters`),
  })
  const [edits, setEdits] = useState<Partial<AnalysisParameters>>({})

  const draft = useMemo(() => (saved ? { ...saved, ...edits } : undefined), [saved, edits])
  const dirty = !!saved && !!draft && !same(saved, draft)

  const setDraft = useCallback((p: Partial<AnalysisParameters>) => setEdits((e) => ({ ...e, ...p })), [])
  const reset = useCallback(() => setEdits({}), [])
  const save = useCallback(async () => {
    if (!draft) return
    const stored = await api.put<AnalysisParameters>(`/vehicles/${vehicleId}/parameters`, draft)
    qc.setQueryData(['parameters', vehicleId], stored)
    setEdits({})
    await qc.invalidateQueries({ queryKey: ['analytics', vehicleId] })
  }, [draft, vehicleId, qc])

  const value = useMemo<ParamsState | null>(
    () => saved && draft
      ? { vehicleId, saved, draft, dirty, setDraft, save, reset, override: dirty ? draft : undefined }
      : null,
    [vehicleId, saved, draft, dirty, setDraft, save, reset],
  )

  if (!value) return <p className="muted pad">{isError ? 'Could not load parameters.' : 'Loading…'}</p>
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function useParams(): ParamsState {
  const v = useContext(Ctx)
  if (!v) throw new Error('useParams must be used inside ParamsProvider')
  return v
}

/** POSTs /vehicles/{vid}/analytics/{path} with the unsaved override (if any) merged in. */
export function useAnalytics<T>(path: string, body: object = {}): UseQueryResult<T> {
  const { vehicleId, override } = useParams()
  return useQuery({
    queryKey: ['analytics', vehicleId, path, body, override ?? null],
    queryFn: () => api.post<T>(`/vehicles/${vehicleId}/analytics/${path}`, { ...body, parameters: override }),
    placeholderData: keepPreviousData,
  })
}
