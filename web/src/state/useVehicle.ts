import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { TyreSet, Vehicle } from '../api/types'
import { useParams } from './ParamsContext'

export function useVehicle(): Vehicle {
  const { vehicleId } = useParams()
  const { data } = useQuery({ queryKey: ['vehicles'], queryFn: () => api.get<Vehicle[]>('/vehicles') })
  return data?.find((v) => v.id === vehicleId)
    ?? { id: vehicleId, name: '', homeCurrency: 'CZK', timeZone: 'Europe/Prague', initialTyreSetId: null, isDefault: false, historyStart: null, historyStartOdometer: null }
}

export function useTyreSets() {
  const { vehicleId } = useParams()
  return useQuery({ queryKey: ['tyres', vehicleId, 'list'], queryFn: () => api.get<TyreSet[]>(`/vehicles/${vehicleId}/tyres`) })
}
