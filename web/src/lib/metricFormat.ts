import type { MetricGroup, MetricKey } from './compare'
import { fmtMoney, fmtNum, fmtPct } from './format'

const DIGITS: Partial<Record<MetricKey, number>> = {
  tripKm: 0, odometerKm: 0, days: 0, coldStarts: 0, needsConversionCount: 0, kmPerDay: 1, coldStartsPer100Km: 1,
  daysUsed: 0, kmPerDayUsed: 1, drivingHours: 1,
  tripLPer100: 1, fullTankLPer100: 1, averageSpeedKmh: 0, drivingHoursPerDay: 2,
}
const PERCENT: MetricKey[] = ['cityShare', 'mixedShare', 'highwayShare', 'shortShareByKm', 'shortShareByCount', 'dpfIndex', 'tripCoverage']
const MONEY: MetricKey[] = ['total', 'costPerKm']

export function formatMetric(key: MetricKey, v: number | null, currency: string): string {
  if (v == null) return '—'
  if (PERCENT.includes(key)) return fmtPct(v)
  if (MONEY.includes(key) || key.startsWith('cat:')) return fmtMoney(v, currency)
  return fmtNum(v, DIGITS[key] ?? 1)
}

export const GROUPS: MetricGroup[] = ['Usage', 'Engine wear & DPF', 'Fuel', 'Money']
