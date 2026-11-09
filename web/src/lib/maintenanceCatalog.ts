import type { MaintenanceType } from '../api/types'

export const MAINTENANCE_GROUPS: { title: string; types: MaintenanceType[] }[] = [
  { title: 'Fluids', types: ['OilChange', 'TransmissionOil', 'HaldexOil', 'BrakeFluid', 'Coolant'] },
  { title: 'Filters', types: ['OilFilter', 'AirFilter', 'CabinFilter', 'FuelFilter'] },
  { title: 'Brakes', types: ['BrakePads', 'BrakeDiscs'] },
  { title: 'Tyres & wheels', types: ['TyreSwap', 'WheelAlignment'] },
  { title: 'Engine & belts', types: ['TimingBelt', 'AuxiliaryBelt', 'GlowPlugs', 'DpfService'] },
  { title: 'Electrical & other', types: ['Battery', 'WiperBladesFront', 'WiperBladesRear', 'ACCleaning'] },
]

/** Checklist types in catalogue order (NewTyres is legacy and not offered). */
export const MAINTENANCE_TYPES: MaintenanceType[] = MAINTENANCE_GROUPS.flatMap((g) => g.types)

const LABELS: Record<MaintenanceType, string> = {
  OilChange: 'Engine oil', TransmissionOil: 'Transmission oil', HaldexOil: 'Haldex oil', BrakeFluid: 'Brake fluid',
  Coolant: 'Coolant', OilFilter: 'Oil filter', AirFilter: 'Air filter', CabinFilter: 'Cabin filter',
  FuelFilter: 'Fuel filter', BrakePads: 'Brake pads', BrakeDiscs: 'Brake discs', TyreSwap: 'Tyre swap',
  NewTyres: 'New tyres', WheelAlignment: 'Wheel alignment', TimingBelt: 'Timing belt (+ water pump)',
  AuxiliaryBelt: 'Auxiliary belt', GlowPlugs: 'Glow plugs', DpfService: 'DPF service', Battery: 'Battery',
  WiperBladesFront: 'Wiper blades (front)', WiperBladesRear: 'Wiper blades (rear)', ACCleaning: 'AC service',
}

export const maintenanceLabel = (t: MaintenanceType): string => LABELS[t]
