import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { RegularRoute } from '../api/types'
import { useParams } from '../state/ParamsContext'

/** "Home ↔ Work" (round trip: "Home ↺"), used by the routes table and the list's route chip. */
export const routeName = (r: RegularRoute) => (r.roundTrip ? `${r.from} ↺` : `${r.from} ↔ ${r.to}`)

/** The regular routes of a period; shared by the table and the list's route chip through the query cache. */
export function useRoutes(from: string, to: string, level: 'Group' | 'Trip', enabled = true) {
  const { vehicleId } = useParams()
  return useQuery({
    queryKey: ['analytics', vehicleId, 'routes', from, to, level],
    queryFn: () => api.get<RegularRoute[]>(`/vehicles/${vehicleId}/trips/routes?${new URLSearchParams({ from, to, level })}`),
    enabled,
  })
}
