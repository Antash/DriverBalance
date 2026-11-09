import { describe, expect, it } from 'vitest'
import type { Expense, MaintenanceType, MaintenanceWork } from '../api/types'
import { MILESTONE_GROUPS, milestoneGroup, serviceMilestones } from './milestones'

const exp = (id: number, date: string, maintenance: MaintenanceType[], work: Partial<MaintenanceWork>[] = [], odometer: number | null = 1000) =>
  ({ id, date, odometer, maintenance, amount: 100, currency: 'CZK', homeAmount: null, note: null, tyreSetId: null,
     maintenanceDescription: null, work: work.map((w) => ({ type: null, name: null, part: 'None', brand: null, model: null, details: null, ...w })) }) as unknown as Expense

describe('milestoneGroup', () => {
  it('maps frequent work to its own group and the rest to other', () => {
    expect(milestoneGroup('OilChange')).toBe('oil')
    expect(milestoneGroup('TyreSwap')).toBe('tyres')
    expect(milestoneGroup('NewTyres')).toBe('tyres')
    expect(milestoneGroup('WheelAlignment')).toBe('tyres')
    expect(milestoneGroup('BrakePads')).toBe('brakes')
    expect(milestoneGroup('BrakeDiscs')).toBe('brakes')
    expect(milestoneGroup('OilFilter')).toBe('filters')
    expect(milestoneGroup('CabinFilter')).toBe('filters')
    expect(milestoneGroup('BrakeFluid')).toBe('other')
    expect(milestoneGroup(null)).toBe('other')
  })
  it('lists groups in display order', () => {
    expect(MILESTONE_GROUPS.map((g) => g.key)).toEqual(['oil', 'tyres', 'brakes', 'filters', 'other'])
  })
})

describe('serviceMilestones', () => {
  it('gives one milestone per visit in [from, to) with its groups in display order', () => {
    const m = serviceMilestones([
      exp(1, '2025-12-31', ['OilChange']),
      exp(2, '2026-01-13', ['OilChange', 'BrakeFluid'], [{ type: 'BrakeFluid' }, { type: 'OilChange' }, { type: null, name: 'Polish' }]),
      exp(3, '2026-03-18', ['TyreSwap']),
      exp(4, '2026-06-01', ['Battery']),
    ], '2026-01-01', '2026-06-01', [])
    expect(m.map((x) => x.expense.id)).toEqual([2, 3])
    expect(m[0].groups).toEqual(['oil', 'other'])
    expect(m[1].groups).toEqual(['tyres'])
  })
  it('uses the record odometer, else the odometer line on or before the date', () => {
    const line: [string, number][] = [['2026-01-01', 20000], ['2026-02-01', 21000]]
    const m = serviceMilestones([exp(1, '2026-02-10', ['Coolant'], [], null), exp(2, '2026-02-11', ['Coolant'], [], 25000)], '2026-01-01', '2027-01-01', line)
    expect(m.map((x) => x.odometer)).toEqual([21000, 25000])
  })
  it('skips records with no work', () => {
    expect(serviceMilestones([exp(1, '2026-02-10', [])], '2026-01-01', '2027-01-01', [])).toEqual([])
  })
})
