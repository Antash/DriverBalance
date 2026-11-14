// Mirrors the DriverBalance.Web DTOs (camelCase JSON, enums as PascalCase strings,
// DateOnly as "yyyy-MM-dd"). Later tasks extend this as pages need more shapes.

export type DateOnly = string

export type DrivingScore = 'Excellent' | 'Good' | 'Fair' | 'Bad'

export type MaintenanceType =
  | 'OilChange' | 'BrakeFluid' | 'TyreSwap' | 'BrakePads' | 'TransmissionOil' | 'HaldexOil'
  | 'Coolant' | 'CabinFilter' | 'AirFilter' | 'FuelFilter' | 'ACCleaning' | 'NewTyres'
  | 'OilFilter' | 'BrakeDiscs' | 'WheelAlignment' | 'TimingBelt' | 'AuxiliaryBelt' | 'GlowPlugs' | 'DpfService'
  | 'Battery' | 'WiperBladesFront' | 'WiperBladesRear' | 'Suspension' | 'IntercoolerCleaning'

export type PartSource = 'None' | 'Original' | 'Aftermarket'

export interface MaintenanceWork {
  type: MaintenanceType | null
  name: string | null
  part: PartSource
  brand: string | null
  model: string | null
  details: string | null
  /** Brake pads, brake discs and suspension only. */
  position?: WorkPosition | null
  /** Wiper blades only: just the rubber refill. */
  rubberOnly?: boolean
}

export type WorkPosition = 'Front' | 'Rear' | 'Both'

export interface LatestOdometer {
  odometer: number
  date: DateOnly
}

export type ExpenseCategory =
  | 'Fuel' | 'Parking' | 'TollsVignette' | 'Wash' | 'Maintenance' | 'Repair' | 'Tyres'
  | 'Accessories' | 'Insurance' | 'Fines' | 'Other'
  /** Hidden: the car's purchase costs, shown only in Cost of ownership; never offered for new expenses. */
  | 'Purchase'

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
  /** The made-up example car; removed when the real car is added. */
  isDemo: boolean
  historyStart: DateOnly | null
  historyStartOdometer: number | null
  purchaseDate: DateOnly | null
  estimatedResaleValue: number | null
  resaleEstimateDate: DateOnly | null
  details: CarDetails
  fuelType: FuelType | null
  /** Diesel or fuel type not set: DPF health and index are shown. */
  hasDpf: boolean
  /** Optional financing contract; all null = none. Amounts are in the home currency. */
  financingStart: DateOnly | null
  financingAmount: number | null
  financingFrequency: FinancingFrequency | null
  financingPayments: number | null
  financingBalloon: number | null
}

export type FinancingFrequency = 'Monthly' | 'Quarterly' | 'Yearly'

/** A lump-sum purchase cost (stored as a Purchase expense in the home currency). */
export interface PurchaseCost { id: number; date: DateOnly; amount: number; note: string | null }
/** A cost as listed in Settings: amount in its stored currency; homeAmount is what Cost of ownership counts (null when unknown). */
export interface PurchaseCostRow extends PurchaseCost { currency: string; homeAmount: number | null }
export interface PurchaseCostInput { date: DateOnly; amount: number; note: string | null; currency?: string }

export type FuelType = 'Diesel' | 'DieselMhev' | 'Petrol' | 'PetrolMhev' | 'PetrolPhev' | 'Electric'

/** What the car is (all optional). */
export interface CarDetails {
  model: string | null
  engine: string | null
  trim: string | null
  modelYear: number | null
  exteriorColour: string | null
  interior: string | null
  equipment: string[]
  transmission: 'Manual' | 'Automatic' | null
  awd: boolean
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

export interface ExpenseDocument {
  id: number
  fileName: string
  contentType: string
  sizeBytes: number
}

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
  place: string | null
  documents: ExpenseDocument[]
  maintenance: MaintenanceType[]
  work: MaintenanceWork[]
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
  brand: string | null
  model: string | null
  size: string | null
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
  historyStart: DateOnly | null
  historyStartOdometer: number | null
  purchaseDate: DateOnly | null
  estimatedResaleValue: number | null
  resaleEstimateDate: DateOnly | null
  /** Omitted or null keeps the stored details. */
  details?: CarDetails | null
  fuelType: FuelType | null
  /** All of start, amount and payments, or none (clears the financing). */
  financingStart: DateOnly | null
  financingAmount: number | null
  financingFrequency: FinancingFrequency | null
  financingPayments: number | null
  financingBalloon: number | null
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
  /** Days of the period that have elapsed (clipped to today); denominator of per-day rates. */
  elapsedDays: number
  /** Days with at least one trip. */
  daysUsed: number
  /** Trip km per day used. */
  kmPerDayUsed: number | null
  /** Total driving time in hours. */
  drivingHours: number
  /** Longest run of trips with stops of at most 4 h (midnight and cold starts don't split it); null without trips. */
  longestJourney: Journey | null
}

export interface Journey { start: string; end: string; km: number; tripCount: number; drivingTime: string }

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

export interface TripCost { fuel: number | null; time: number; wear: number; total: number; pricePerLitre: number | null; currency: string }

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
  cost: TripCost
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
  cost: TripCost
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

// ---- Overview, timeline ----

export interface DpfHealthSummary {
  lastLongDrive: DateOnly | null
  daysSince: number | null
  kmSince: number | null
  needsLongDrive: boolean
  series: { date: DateOnly; daysSinceLongDrive: number }[]
  /** `label` reads "<n> days"; `from`..`to` is the stretch without a long drive. */
  worstStretches: Period[]
}

export interface OverviewData {
  thisMonth: PeriodStats
  thisYear: PeriodStats
  /** Null when the car has no DPF. */
  dpfHealth: DpfHealthSummary | null
  recentExpenses: Expense[]
  reminders: Reminder[]
}

export type TimelineEventKind = 'Maintenance' | 'TyreMount' | 'Expense' | 'Import'
export interface TimelineEvent {
  date: DateOnly
  kind: TimelineEventKind
  label: string
  odometer: number | null
  amount: number | null
  refId: number | null
}
export interface TimelineChart {
  odometer: { date: DateOnly; km: number }[]
  events: TimelineEvent[]
  tyreBands: TyreMountSpan[]
}

export interface OwnershipFinancing {
  start: DateOnly
  amountPerPayment: number
  frequency: FinancingFrequency
  payments: number
  balloon: number
  /** payments x amount + balloon */
  total: number
  /** Date of the last regular payment. */
  endDate: DateOnly
}

export interface Ownership {
  purchaseDate: DateOnly | null
  purchaseCosts: PurchaseCost[]
  /** Lump sums plus the whole financing contract. */
  purchaseTotal: number | null
  /** Lump sums only. */
  lumpSumTotal: number
  financing: OwnershipFinancing | null
  runningCosts: number | null
  kmDriven: number | null
  months: number | null
  estimatedResaleValue: number | null
  resaleEstimateDate: DateOnly | null
  netCost: number | null
  netCostPerKm: number | null
  netCostPerMonth: number | null
}
