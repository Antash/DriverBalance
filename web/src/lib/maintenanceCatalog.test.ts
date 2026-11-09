import { describe, expect, it } from 'vitest'
import { MAINTENANCE_GROUPS, MAINTENANCE_TYPES, maintenanceLabel } from './maintenanceCatalog'

describe('maintenance catalogue', () => {
  it('has 21 unique types, without NewTyres', () => {
    expect(MAINTENANCE_TYPES).toHaveLength(21)
    expect(new Set(MAINTENANCE_TYPES).size).toBe(21)
    expect(MAINTENANCE_TYPES).not.toContain('NewTyres')
  })
  it('groups in the agreed order', () => {
    expect(MAINTENANCE_GROUPS.map((g) => g.title)).toEqual(
      ['Fluids', 'Filters', 'Brakes', 'Tyres & wheels', 'Engine & belts', 'Electrical & other'])
    expect(MAINTENANCE_GROUPS[0].types).toEqual(['OilChange', 'TransmissionOil', 'HaldexOil', 'BrakeFluid', 'Coolant'])
    expect(MAINTENANCE_GROUPS[3].types).toEqual(['TyreSwap', 'WheelAlignment'])
    expect(MAINTENANCE_GROUPS[5].types).toEqual(['Battery', 'WiperBladesFront', 'WiperBladesRear', 'ACCleaning'])
  })
  it('labels', () => {
    expect(maintenanceLabel('OilChange')).toBe('Engine oil')
    expect(maintenanceLabel('ACCleaning')).toBe('AC service')
    expect(maintenanceLabel('NewTyres')).toBe('New tyres')
    expect(maintenanceLabel('TimingBelt')).toBe('Timing belt (+ water pump)')
  })
})
