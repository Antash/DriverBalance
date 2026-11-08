import { METRICS, type MetricGroup, type MetricKey } from './compare'
import { fmtMoney, fmtNum, fmtPct } from './format'

const DIGITS: Partial<Record<MetricKey, number>> = {
  tripKm: 0, days: 0, coldStarts: 0, kmPerDay: 1, coldStartsPer100Km: 1, tripLPer100: 1, fullTankLPer100: 1,
}
const PERCENT: MetricKey[] = ['cityShare', 'shortShareByKm', 'dpfIndex']
const MONEY: MetricKey[] = ['total', 'costPerKm']

export function formatMetric(key: MetricKey, v: number | null, currency: string): string {
  if (v == null) return '—'
  if (PERCENT.includes(key)) return fmtPct(v)
  if (MONEY.includes(key)) return fmtMoney(v, currency)
  return fmtNum(v, DIGITS[key] ?? 1)
}

export const GROUPS: MetricGroup[] = ['Usage', 'Engine wear & DPF', 'Fuel', 'Money']
export const groupOf = (key: MetricKey): MetricGroup => METRICS.find((m) => m.key === key)!.group
