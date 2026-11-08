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
