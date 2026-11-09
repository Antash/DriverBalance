import type { Expense, MaintenanceType } from '../api/types'
import { rawItems, type Raw } from './maintenance'

export type MilestoneGroupKey = 'oil' | 'tyres' | 'brakes' | 'filters' | 'other'

/** Frequent work gets its own icon; everything else (fluids, belts, battery, custom works…) is "Other service". */
export const MILESTONE_GROUPS: { key: MilestoneGroupKey; name: string }[] = [
  { key: 'oil', name: 'Engine oil' },
  { key: 'tyres', name: 'Tyres' },
  { key: 'brakes', name: 'Brakes' },
  { key: 'filters', name: 'Filters' },
  { key: 'other', name: 'Other service' },
]

const GROUP_OF: Partial<Record<MaintenanceType, MilestoneGroupKey>> = {
  OilChange: 'oil',
  TyreSwap: 'tyres', NewTyres: 'tyres', WheelAlignment: 'tyres',
  BrakePads: 'brakes', BrakeDiscs: 'brakes',
  OilFilter: 'filters', AirFilter: 'filters', CabinFilter: 'filters', FuelFilter: 'filters',
}

export const milestoneGroup = (t: MaintenanceType | null): MilestoneGroupKey => (t && GROUP_OF[t]) || 'other'

export interface ServiceMilestone { date: string; odometer: number | null; groups: MilestoneGroupKey[]; items: Raw[]; expense: Expense }

/** Odometer at a date: the last point on or before it, else the first point. */
export function odometerAt(line: [string, number][], date: string): number | null {
  let km: number | null = null
  for (const [d, v] of line) { if (d <= date) km = v; else break }
  return km ?? line[0]?.[1] ?? null
}

/** One milestone per maintenance visit dated in [from, to), oldest first, with the icon groups of its work. */
export function serviceMilestones(expenses: Expense[], from: string, to: string, line: [string, number][]): ServiceMilestone[] {
  return expenses
    .filter((e) => e.date >= from && e.date < to)
    .map((e) => ({ e, items: rawItems(e) }))
    .filter((x) => x.items.length > 0)
    .sort((a, b) => (a.e.date < b.e.date ? -1 : a.e.date > b.e.date ? 1 : a.e.id - b.e.id))
    .map(({ e, items }) => {
      const present = new Set(items.map((i) => milestoneGroup(i.type)))
      return {
        date: e.date, expense: e, items,
        odometer: e.odometer ?? odometerAt(line, e.date),
        groups: MILESTONE_GROUPS.map((g) => g.key).filter((k) => present.has(k)),
      }
    })
}
