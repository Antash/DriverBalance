import { describe, expect, it } from 'vitest'
import { MAINTENANCE_GROUPS, MAINTENANCE_TYPES, maintenanceLabel } from './maintenanceCatalog'

describe('maintenance catalogue', () => {
  it('has 22 unique types, without NewTyres', () => {
    expect(MAINTENANCE_TYPES).toHaveLength(22)
    expect(new Set(MAINTENANCE_TYPES).size).toBe(22)
    expect(MAINTENANCE_TYPES).not.toContain('NewTyres')
  })
  it('groups regular consumables, longer-lasting parts and tyres', () => {
    expect(MAINTENANCE_GROUPS.map((g) => g.title)).toEqual(['Consumables', 'Engine, transmission & chassis', 'Tyres & wheels'])
    expect(MAINTENANCE_GROUPS[0].types).toEqual(['OilChange', 'OilFilter', 'AirFilter', 'CabinFilter', 'FuelFilter', 'BrakeFluid',
      'Coolant', 'TransmissionOil', 'HaldexOil', 'WiperBladesFront', 'WiperBladesRear', 'ACCleaning'])
    expect(MAINTENANCE_GROUPS[1].types).toEqual(['BrakePads', 'BrakeDiscs', 'Suspension', 'TimingBelt', 'AuxiliaryBelt', 'GlowPlugs', 'DpfService', 'Battery'])
    expect(MAINTENANCE_GROUPS[2].types).toEqual(['TyreSwap', 'WheelAlignment'])
  })
  it('labels', () => {
    expect(maintenanceLabel('OilChange')).toBe('Engine oil')
    expect(maintenanceLabel('ACCleaning')).toBe('AC service')
    expect(maintenanceLabel('NewTyres')).toBe('New tyres')
    expect(maintenanceLabel('TimingBelt')).toBe('Timing belt (+ water pump)')
    expect(maintenanceLabel('Suspension')).toBe('Suspension (shocks, arms, bushings)')
  })
})
