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

// ---- Analytics (statistics, trips, charts) ----

export interface UsageStats {
  tripCount: number
  tripKm: number
  odometerKm: number | null
  tripCoverage: number | null
  days: number
  kmPerDay: number | null
  /** .NET TimeSpan, "[d.]hh:mm:ss". */
  drivingTime: string
  averageSpeedKmh: number | null
  coldStarts: number
  coldStartsPer100Km: number | null
  shortGroups: number
  shortShareByCount: number | null
  shortShareByKm: number | null
}

export interface ScoreBucket { km: number; duration: string; groups: number }

export interface PeriodStats {
  period: Period
  usage: UsageStats
  scores: { buckets: Partial<Record<DrivingScore, ScoreBucket>>; dpfIndex: number | null }
  mix: { cityKm: number; mixedKm: number; highwayKm: number }
  fuel: { tripLiters: number; tripLPer100: number | null; fullTankLPer100: number | null; fullTankKm: number | null }
  money: { total: number; byCategory: Partial<Record<ExpenseCategory, number>>; costPerKm: number | null; needsConversionCount: number }
}

export interface PeriodWithStats { period: Period; stats: PeriodStats }

export interface Trip {
  id: number
  category: string
  start: string
  startOdometer: number
  startAddress: string
  end: string
  endOdometer: number
  endAddress: string
  duration: string
  distanceKm: number
  fuelLiters: number
  notes: string
  averageSpeedKmh: number
}

export interface TripGroup {
  trips: Trip[]
  score: DrivingScore
  start: string
  end: string
  distanceKm: number
  duration: string
  fuelLiters: number
  averageSpeedKmh: number
  lPer100: number | null
  startDate: DateOnly
}

export interface TripPage { total: number; page: number; pageSize: number; groups: TripGroup[] }

export type TrendLevel = 'Group' | 'Trip'
export interface ChartBody { from?: DateOnly; to?: DateOnly; options?: { level?: TrendLevel; year?: number } }

export interface MonthlyScoreChart {
  months: { month: string; excellentKm: number; goodKm: number; fairKm: number; badKm: number }[]
  weeks: { weekStart: DateOnly; dpf: number | null; dpfMovingAverage: number | null }[]
}
export interface HeatmapDay { date: DateOnly; km: number; bestScore: DrivingScore; intensity: number }
export interface FuelTrendChart {
  points: { time: string; lPer100: number; km: number; score: DrivingScore; firstTripId: number; tripCount: number }[]
  smooth: { time: string; lPer100: number }[]
}
export interface ScoreMapChart {
  points: { durationMinutes: number; averageSpeedKmh: number; km: number; score: DrivingScore; start: string; firstTripId: number }[]
  zones: { shortMin: number; mediumMin: number; longMin: number; heavyKmh: number; cityKmh: number; mixedKmh: number }
}
export interface GapHistogram { bins: { from: number; to: number | null; count: number }[]; thresholdMinutes: number }
/** dayOfWeek 0 = Monday. */
export interface HourCell { dayOfWeek: number; hour: number; km: number; trips: number; badShare: number | null }
export interface PenaltyBin { fromMinutes: number; toMinutes: number | null; medianLPer100: number | null; p25: number | null; p75: number | null; count: number }
export interface SeasonPoint { year: number; month: number; km: number; lPer100: number | null }
export interface PlaceStat { address: string; starts: number; ends: number }
export interface RouteStat {
  from: string; to: string; count: number; km: number; avgLPer100: number | null; typicalScore: DrivingScore
  trend: { date: DateOnly; lPer100: number | null }[]
}
