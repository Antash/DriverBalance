// Mirrors the DriverBalance.Web DTOs (camelCase JSON, enums as PascalCase strings,
// DateOnly as "yyyy-MM-dd"). Later tasks extend this as pages need more shapes.

export type DateOnly = string

export type DrivingScore = 'Excellent' | 'Good' | 'Fair' | 'Bad'

export type MaintenanceType =
  | 'OilChange' | 'BrakeFluid' | 'TyreSwap' | 'BrakePads' | 'TransmissionOil' | 'HaldexOil'
  | 'Coolant' | 'CabinFilter' | 'AirFilter' | 'FuelFilter' | 'ACCleaning' | 'NewTyres'

export type ExpenseCategory =
  | 'Fuel' | 'Parking' | 'TollsVignette' | 'Wash' | 'Maintenance' | 'Repair' | 'Tyres'
  | 'Accessories' | 'Insurance' | 'RoadTax' | 'Fines' | 'Financing' | 'Other'

export type TyreSeason = 'Summer' | 'Winter' | 'AllSeason'

export interface AnalysisParameters {
  coldStartGapMinutes: number
  shortDurationMinutes: number
  mediumDurationMinutes: number
  longDurationMinutes: number
  heavyTrafficSpeedKmh: number
  citySpeedKmh: number
  mixedSpeedKmh: number
  dpfMovingAverageWeeks: number
  fuelTrendMinLPer100: number
  fuelTrendMaxLPer100: number
  lowessSpan: number
  heatmapKmCap: number
  longDriveHintDays: number
  exportReminderDays: number
}

export interface Vehicle {
  id: number
  name: string
  homeCurrency: string
  timeZone: string
  initialTyreSetId: number | null
  isDefault: boolean
}

export interface Period {
  from: DateOnly
  /** Exclusive. */
  to: DateOnly
  label: string
  startOdometer?: number | null
  endOdometer?: number | null
}

export interface PeriodInput {
  from: DateOnly
  to: DateOnly
  label?: string | null
}

export interface Reminder {
  kind: string
  message: string
  severity: string
}

/** Body of 400/403/409 responses. */
export interface ApiErrorBody {
  errors: string[]
}

export type ExpenseSource = 'Web' | 'Mcp' | 'Migration'

export interface Expense {
  id: number
  date: DateOnly
  category: ExpenseCategory
  amount: number
  currency: string
  homeAmount: number | null
  odometer: number | null
  note: string | null
  coversFrom: DateOnly | null
  coversTo: DateOnly | null
  litres: number | null
  fullTank: boolean | null
  station: string | null
  maintenance: MaintenanceType[]
  maintenanceDescription: string | null
  tyreSetId: number | null
  source: ExpenseSource
}

export interface TyreSet {
  id: number
  name: string
  season: TyreSeason
  description: string | null
  purchaseDate: DateOnly | null
  retired: boolean
}

export interface TyreMountSpan {
  tyreSetId: number | null
  from: DateOnly
  /** Null for the open (current) span. */
  to: DateOnly | null
  fromOdometer: number | null
  toOdometer: number | null
  km: number | null
}

export interface TyreSetSummary {
  set: TyreSet
  totalKm: number
  seasons: number
  purchaseCost: number | null
  costPer1000Km: number | null
  mounted: boolean
  spans: TyreMountSpan[]
}

export interface TyreSummaryResponse {
  summaries: TyreSetSummary[]
  unassignedSpans: TyreMountSpan[]
}

export interface ImportResult {
  batchId: number
  imported: number
  skipped: number
  firstTrip: DateOnly | null
  lastTrip: DateOnly | null
  warnings: string[]
}

export interface LegacyMigrationResult {
  tripsImported: number
  tripsSkipped: number
  expensesImported: number
  expensesSkipped: number
}

export interface ImportBatch {
  id: number
  fileName: string
  importedAt: string
  imported: number
  skipped: number
  firstTrip: DateOnly | null
  lastTrip: DateOnly | null
}

export interface TokenInfo {
  id: number
  name: string
  createdAt: string
  lastUsedAt: string | null
}

export interface TokenCreated {
  id: number
  name: string
  token: string
}

export interface VehicleInput {
  name: string
  homeCurrency: string
  timeZone: string
  initialTyreSetId: number | null
  isDefault: boolean
}

export interface MonthlyCost {
  month: string
  byCategory: Partial<Record<ExpenseCategory, number>>
}

export interface CumulativeCostPoint {
  date: DateOnly
  cumulativeCost: number
  cumulativeKm: number | null
  costPerKm: number | null
}
