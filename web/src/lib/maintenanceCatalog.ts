import type { MaintenanceType } from '../api/types'

/** Regular consumables (fluids, filters, wipers), longer-lasting engine/transmission/chassis parts, and tyres. */
export const MAINTENANCE_GROUPS: { title: string; types: MaintenanceType[] }[] = [
  { title: 'Consumables', types: ['OilChange', 'OilFilter', 'AirFilter', 'CabinFilter', 'FuelFilter', 'BrakeFluid',
    'Coolant', 'TransmissionOil', 'HaldexOil', 'WiperBladesFront', 'WiperBladesRear', 'ACCleaning'] },
  { title: 'Engine, transmission & chassis', types: ['BrakePads', 'BrakeDiscs', 'Suspension', 'TimingBelt', 'AuxiliaryBelt',
    'GlowPlugs', 'DpfService', 'IntercoolerCleaning', 'Battery'] },
  { title: 'Tyres & wheels', types: ['TyreSwap', 'WheelAlignment'] },
]

/** Checklist types in catalogue order (NewTyres is legacy and not offered). */
export const MAINTENANCE_TYPES: MaintenanceType[] = MAINTENANCE_GROUPS.flatMap((g) => g.types)

const LABELS: Record<MaintenanceType, string> = {
  OilChange: 'Engine oil', TransmissionOil: 'Transmission oil', HaldexOil: 'Haldex oil', BrakeFluid: 'Brake fluid',
  Coolant: 'Coolant', OilFilter: 'Oil filter', AirFilter: 'Air filter', CabinFilter: 'Cabin filter',
  FuelFilter: 'Fuel filter', BrakePads: 'Brake pads', BrakeDiscs: 'Brake discs', TyreSwap: 'Tyre swap',
  NewTyres: 'New tyres', WheelAlignment: 'Wheel alignment', TimingBelt: 'Timing belt (+ water pump)',
  AuxiliaryBelt: 'Auxiliary belt', GlowPlugs: 'Glow plugs', DpfService: 'DPF service', Battery: 'Battery',
  WiperBladesFront: 'Wiper blades (front)', WiperBladesRear: 'Wiper blades (rear)', ACCleaning: 'AC ozone cleaning',
  Suspension: 'Suspension (shocks, arms, bushings)', IntercoolerCleaning: 'Intercooler cleaning',
}

export const maintenanceLabel = (t: MaintenanceType): string => LABELS[t]
