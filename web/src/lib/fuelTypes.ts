import type { FuelType } from '../api/types'

/** Fuel types offered in Settings with their labels; only the diesels have a DPF. */
export const FUEL_TYPES: [FuelType | '', string][] = [
  ['Diesel', 'Diesel'], ['DieselMhev', 'Diesel MHEV (mild hybrid)'], ['Petrol', 'Petrol'], ['PetrolMhev', 'Petrol MHEV (mild hybrid)'],
  ['PetrolPhev', 'Petrol PHEV (plug-in hybrid)'], ['Electric', 'Electric'], ['', 'Not set'],
]
