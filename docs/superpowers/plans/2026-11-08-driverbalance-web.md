# DriverBalance Web App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the DriverBalance console tool with a local, multi-user-ready web app: same numbers, live parameters, rich in-page charts, an expense ledger, tyre tracking and an MCP endpoint. This is the "first working version" in the spec's Delivery order: phases 1–3 + MCP endpoint, with no Volvo pull and no family-assistant wiring.

**Architecture:** A pure `DriverBalance.Core` library holds the domain and all analysis. `DriverBalance.Data` is EF Core SQLite, with per-user global query filters. `DriverBalance.Web` is an ASP.NET Core app that serves the JSON API, `/mcp`, and the built React SPA from `web/`. Analytics are always recomputed from raw rows, and an optional `parameters` override in the request body gives "try mode".

**Tech Stack:** .NET 10 (SDK 10.0.403), ASP.NET Core minimal APIs, EF Core 10 + SQLite, xUnit, `ModelContextProtocol.AspNetCore` (latest stable), React 18 + TypeScript + Vite, react-router-dom, @tanstack/react-query, Apache ECharts (`echarts` + `echarts-for-react`), Vitest.

**Spec:** `docs/superpowers/specs/2026-11-08-driverbalance-web-design.md`. Read it fully, especially "Existing logic to preserve exactly", "Rulings" and "Visualizations". The spec wins over this plan if they ever disagree.

## Global Constraints

- Every persisted row except `Users` carries `UserId`. Rows that belong to a car also carry `VehicleId`. Every query goes through the user-filtered `DbContext`.
- Real personal data (anything under `DriverBalance/bin/`, real CSVs, the owner's DB) is **never** committed or printed in full. Test fixtures are synthetic.
- Default analysis parameters = today's `appsettings.json`: ColdStartGap 45 min, Short 20, Medium 30, Long 40 min, HeavyTraffic 25, City 35, Mixed 50 km/h, DPF moving average 3 weeks. Other defaults: fuel-trend limits 1–30 L/100, LOWESS span 0.25, heatmap cap 100 km, long-drive hint 14 days, export reminder 30 days.
- Periods are half-open `[From, To)` `DateOnly` ranges. The UI shows an inclusive "to".
- Trips belong to a period by their group's first-trip start date. Groups are built once over all trips (Rulings 2).
- Home currency defaults to `CZK`. Vehicle time zone defaults to `Europe/Prague`.
- JSON: camelCase, enums as strings, `DateOnly` as `yyyy-MM-dd`, `MaintenanceType` flags as a string array. **No NaN/Infinity ever reaches JSON**: use `null` for undefined ratios.
- The app listens on `http://localhost:5180` only. Database path config key `DriverBalance:DatabasePath`, default `%LOCALAPPDATA%\DriverBalance\driverbalance.db`.
- No new dependency beyond the Tech Stack list without noting why in the task report.
- UI language is English. Units are km, L and the vehicle's home currency.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Work on branch `feat/web-app`.

## Review Focus

1. **Real Volvo CSV quirks:** NBSP/space thousands separators, comma decimals, `1 h, 5 min` / `0 min` durations, quoted addresses with commas, BOM, CRLF, blank lines. These must parse as the legacy importer does, and zero-duration trips must not divide by zero. *Test: Task 1 `VolvoCsvParserTests`.*
2. **Empty or new vehicle:** every analytics and chart endpoint returns valid, empty JSON (no exception, no NaN). *Tests: Task 3 `Stats_EmptyVehicle_NoNaN`, Task 9 `AllAnalyticsEndpoints_EmptyVehicle_Return200`.*
3. **Bad live parameters:** thresholds out of order, gap ≤ 0, span outside (0,1] produce 400 with readable messages, never a 500. *Tests: Task 1 `Validate_*`, Task 9 `Stats_InvalidParameters_Returns400`.*
4. **Re-import:** an overlapping CSV, or the legacy JSON migration run twice, creates no duplicates. *Tests: Task 8 `ImportCsv_Twice_SkipsDuplicates`, `LegacyMigration_Twice_IsIdempotent`.*
5. **Messy money and odometer:** a foreign currency without a home amount is excluded and counted. Lower or missing odometer readings never produce negative km. *Tests: Task 4 `Timeline_IgnoresLowerReadings`, Task 7 `Money_ForeignWithoutHomeAmount_ExcludedAndCounted`.*

---

## File Structure

```
DriverBalance.slnx                      (modify: list all projects below)
src/DriverBalance.Core/
  Domain/   AnalysisParameters.cs Trip.cs TripGroup.cs DrivingScore.cs MaintenanceType.cs
            Expense.cs ExpenseCategory.cs TyreSet.cs Period.cs OdometerReading.cs VehicleData.cs
  Import/   VolvoCsvParser.cs LegacyJsonReader.cs
  Analysis/ TripGrouping.cs Scoring.cs AnalysisContext.cs PeriodStats.cs
            Intervals.cs CalendarPeriods.cs OdometerTimeline.cs Money.cs FuelEconomy.cs TyreMileage.cs Lowess.cs
  Charts/   ScoreCharts.cs FuelCharts.cs HabitCharts.cs PlacesAnalysis.cs DpfHealth.cs MoneyCharts.cs TimelineChart.cs ChartDtos.cs
src/DriverBalance.Data/
  DriverBalanceDbContext.cs ICurrentUser.cs Rows/*.cs Mapping.cs Migrations/ ImportService.cs LegacyMigrationService.cs
src/DriverBalance.Web/
  Program.cs Auth/LocalUserHandler.cs Auth/ApiTokenHandler.cs Auth/HttpCurrentUser.cs
  Services/VehicleDataLoader.cs Services/StartupSeeding.cs
  Endpoints/{Vehicle,Import,Analytics,Trip,Expense,Tyre,Token,Export}Endpoints.cs Dtos/*.cs
  Mcp/CarTools.cs
web/  (Vite React TS app; built output copied to src/DriverBalance.Web/wwwroot on publish)
tests/DriverBalance.Core.Tests/  tests/DriverBalance.Web.Tests/
tests/fixtures/  synthetic-trips.csv synthetic-maintenance.json legacy/legacy-output.txt
scripts/register-autostart.ps1
DriverBalance/  (legacy console; deleted in Task 15)
```

---

### Task 1: Baseline, solution skeleton, domain types, CSV parser

**Files:**
- Commit as-is first: `DriverBalance/Program.cs` (the owner's uncommitted work), message `chore: baseline owner's maintenance/interval work`.
- Create: `src/DriverBalance.Core/DriverBalance.Core.csproj` (net10.0, nullable, no package refs), the `Domain/*` files, `Import/VolvoCsvParser.cs`.
- Create: `tests/DriverBalance.Core.Tests/` (xUnit), `tests/fixtures/synthetic-trips.csv`.
- Modify: `DriverBalance.slnx`.

**Interfaces (Produces):**
```csharp
namespace DriverBalance.Core;
public enum DrivingScore { Excellent = 1, Good = 2, Fair = 3, Bad = 4 }
[Flags] public enum MaintenanceType { Custom = 0, OilChange = 1, BrakeFluid = 2, TyreSwap = 4, BrakePads = 8, TransmissionOil = 16,
  HaldexOil = 32, Coolant = 64, CabinFilter = 128, AirFilter = 256, FuelFilter = 512, ACCleaning = 1024, NewTyres = 2048 } // values identical to legacy
public sealed record AnalysisParameters {
  public int ColdStartGapMinutes {get; init;} = 45; public int ShortDurationMinutes {get; init;} = 20;
  public int MediumDurationMinutes {get; init;} = 30; public int LongDurationMinutes {get; init;} = 40;
  public double HeavyTrafficSpeedKmh {get; init;} = 25; public double CitySpeedKmh {get; init;} = 35; public double MixedSpeedKmh {get; init;} = 50;
  public int DpfMovingAverageWeeks {get; init;} = 3; public double FuelTrendMinLPer100 {get; init;} = 1; public double FuelTrendMaxLPer100 {get; init;} = 30;
  public double LowessSpan {get; init;} = 0.25; public double HeatmapKmCap {get; init;} = 100; public int LongDriveHintDays {get; init;} = 14;
  public int ExportReminderDays {get; init;} = 30;
  public IReadOnlyList<string> Validate(); // empty = valid
}
public sealed record Trip(string Category, DateTime Start, int StartOdometer, string StartAddress, DateTime End, int EndOdometer,
  string EndAddress, TimeSpan Duration, double DistanceKm, double FuelLiters, string Notes) {
  public long Id { get; init; }
  public double AverageSpeedKmh => Duration.TotalHours > 0 ? DistanceKm / Duration.TotalHours : 0;
}
public sealed record ParseResult(IReadOnlyList<Trip> Trips, IReadOnlyList<string> Warnings);
public static class VolvoCsvParser { public static ParseResult Parse(TextReader reader); }
```

- [ ] **Step 1: Commit the owner's `Program.cs` baseline** (only that file).
- [ ] **Step 2: Create the projects and add them to `DriverBalance.slnx`.** Run `dotnet build`. Expected: success.
- [ ] **Step 3: Write failing tests** `tests/DriverBalance.Core.Tests/VolvoCsvParserTests.cs`:
  - `Parse_TypicalRow`: a row `"Private","14.04.2025 11:45","7 350","Street 1, Praha","14.04.2025 12:30","7 378","Other 2, Brno","45 min","28,4","2,1",""` gives Start 2025-04-14 11:45, StartOdometer 7350, StartAddress `Street 1, Praha`, Duration 45 min, DistanceKm 28.4, FuelLiters 2.1.
  - `Parse_HoursAndMinutes`: `1 h, 5 min` → 65 min. `Parse_ZeroMinutes`: `0 min` → Zero, and `AverageSpeedKmh == 0`.
  - `Parse_NbspThousands`: `"12 345"` → 12345.
  - `Parse_BomCrlfBlankLines`: a BOM, CRLF line endings and blank lines are ignored. The header row is skipped.
  - `Parse_ShortRow_SkippedWithWarning`: a row with fewer than 11 columns is skipped, and `Warnings.Count == 1`.
  - `Validate_Defaults_Valid`, `Validate_ShortAboveMedium_Invalid`, `Validate_CityAboveMixed_Invalid`, `Validate_GapZero_Invalid`, `Validate_SpanOutOfRange_Invalid` (0 and 1.5 invalid, 1 valid).
- [ ] **Step 4: Run** `dotnet test tests/DriverBalance.Core.Tests`. Expected: FAIL (types missing).
- [ ] **Step 5: Implement.** Port `TripImporter` behaviour exactly: `cs-CZ` culture for dates, the same quote-toggle splitter, the same number cleaning. Validation rules: Short ≤ Medium ≤ Long, all > 0; HeavyTraffic ≤ City ≤ Mixed, all > 0; gap > 0; MA weeks ≥ 1; 0 ≤ FuelMin < FuelMax; 0 < Span ≤ 1; HeatmapKmCap > 0; hint/reminder days ≥ 1.
- [ ] **Step 6: Create `tests/fixtures/synthetic-trips.csv`** (used from Task 2 on). It needs about 120 invented trips from 2025-01-06 to 2025-12-20, with fake addresses (`Home St 1`, `Work Ave 5`, `Mall 3`, `Lake Rd 9`) and a consistent odometer starting at 1 000. It must include:
  - same-day chains with gaps under 45 min;
  - long highway runs (≥ 40 min at ≥ 50 km/h);
  - short city hops;
  - two `0 min` trips;
  - one trip crossing midnight;
  - no chain spanning the dates 2025-03-10, 2025-06-02, 2025-09-15, 2025-11-03 (the synthetic maintenance dates).

  Use the same 11-column format as the tests.
- [ ] **Step 7: Run tests.** Expected: PASS.
- [ ] **Step 8: Commit** `feat(core): domain types, parameters validation, Volvo CSV parser`.

### Task 2: Capture legacy output (characterization fixture)

**Files:** Create `tests/fixtures/synthetic-maintenance.json`, `tests/fixtures/legacy/legacy-output.txt`, `tests/fixtures/legacy/README.md` (how it was produced).

- [ ] **Step 1: Write `synthetic-maintenance.json`** in the legacy format (`Date`, `Type` as an int, `Description`, `CurrentOdometer`, `Cost`, `Currency`):
  - 2025-03-10 OilChange|BrakeFluid (3), cost 0, currency "";
  - 2025-06-02 TyreSwap (4), 2 500 CZK;
  - 2025-09-15 OilChange (1), 9 800 CZK;
  - 2025-11-03 TyreSwap (4), 2 700 CZK.

  Odometers must match the trip fixture at those dates.
- [ ] **Step 2: Run the legacy console** in a fresh temp dir containing a copy of `DriverBalance/appsettings.json` and `synthetic-maintenance.json` renamed to `maintenancerecords.json`. Pipe the stdin commands `1` + fixture CSV path, `3`, `4`, `10`, `11`, `0`, using `dotnet run --project <repo>/DriverBalance` with the temp dir as the working directory. Save stdout to `legacy-output.txt`. Expected: the output contains `--- Trip Summary ---`, `--- Trip Analysis (Concatenated) ---` and per-type period blocks, with no exceptions.
- [ ] **Step 3: Write the README** with the exact command used and the note "period end of the current interval = run date + 1; ignore it in tests".
- [ ] **Step 4: Commit** `test: legacy console output on synthetic fixture`.

### Task 3: Grouping, scoring, AnalysisContext, PeriodStats (trip-based) + parity

**Files:** Create `Analysis/TripGrouping.cs`, `Scoring.cs`, `AnalysisContext.cs`, `PeriodStats.cs`, `Domain/TripGroup.cs`, `Domain/Period.cs`, `Domain/VehicleData.cs`, `Domain/OdometerReading.cs` (type only). Tests: `TripGroupingTests.cs`, `ScoringTests.cs`, `LegacyParityTests.cs`, `PeriodStatsTests.cs`.

**Interfaces (Produces):**
```csharp
public sealed record TripGroup(IReadOnlyList<Trip> Trips, DrivingScore Score) {
  DateTime Start; DateTime End; double DistanceKm; TimeSpan Duration /*sum*/; double FuelLiters; double AverageSpeedKmh; double? LPer100; DateOnly StartDate;
}
public static class Scoring { DrivingScore Score(TimeSpan duration, double avgSpeedKmh, AnalysisParameters p);
  DrivingScore BestForGroup(IReadOnlyList<Trip> trips, AnalysisParameters p); }
public static class TripGrouping { IReadOnlyList<TripGroup> Build(IEnumerable<Trip> trips, AnalysisParameters p); } // sorted by Start
public sealed record Period(DateOnly From, DateOnly To, string Label, int? StartOdometer = null, int? EndOdometer = null) { int Days; bool Contains(DateOnly d); }
public enum OdometerSource { TripStart, TripEnd, Expense, Snapshot }
public sealed record OdometerReading(DateTime At, int Km, OdometerSource Source);
public sealed record VehicleData(IReadOnlyList<Trip> Trips, IReadOnlyList<Expense> Expenses, IReadOnlyList<TyreSet> TyreSets,
  string HomeCurrency, DateOnly Today, long? InitialTyreSetId = null);
public sealed class AnalysisContext {
  public static AnalysisContext Create(VehicleData data, AnalysisParameters p);
  public IReadOnlyList<TripGroup> Groups { get; } public AnalysisParameters Parameters { get; } public VehicleData Data { get; }
  public IEnumerable<TripGroup> GroupsIn(Period period);
  public PeriodStats Stats(Period period);
}
public sealed record ScoreBucket(double Km, TimeSpan Duration, int Groups);
public sealed record UsageStats(int TripCount, double TripKm, double? OdometerKm, double? TripCoverage, int Days, double? KmPerDay,
  TimeSpan DrivingTime, double? AverageSpeedKmh, int ColdStarts, double? ColdStartsPer100Km, int ShortGroups, double? ShortShareByCount, double? ShortShareByKm);
public sealed record ScoreBreakdown(IReadOnlyDictionary<DrivingScore, ScoreBucket> Buckets, double? DpfIndex);
public sealed record DrivingMix(double CityKm, double MixedKm, double HighwayKm);
public sealed record FuelStats(double TripLiters, double? TripLPer100, double? FullTankLPer100, double? FullTankKm);
public sealed record MoneyStats(decimal Total, IReadOnlyDictionary<ExpenseCategory, decimal> ByCategory, decimal? CostPerKm, int NeedsConversionCount);
public sealed record PeriodStats(Period Period, UsageStats Usage, ScoreBreakdown Scores, DrivingMix Mix, FuelStats Fuel, MoneyStats Money);
```

For this task, `Expense`, `TyreSet` and `ExpenseCategory` are created as the full records defined in Task 7's Interfaces block (create the types now, logic later). `OdometerKm`, `TripCoverage`, `FullTank*` and `Money` return null or empty until Tasks 4 and 7 fill them. `KmPerDay` uses OdometerKm ?? TripKm.

- [ ] **Step 1: Write failing tests.**
  - `Scoring`: the four boundary cases at exact thresholds. For example, 40 min @ 50 km/h → Excellent, 39 min @ 60 → Good, 20 min @ 25 → Fair, 19 min → Bad.
  - `BestForGroup`: three short trips that together reach 40 min @ 55 → Excellent.
  - `Grouping`: a 44-minute gap joins, a 45-minute gap splits (strict `<`).
  - `Stats_EmptyVehicle_NoNaN`: serialize with System.Text.Json default options (which throws on NaN), and every nullable ratio is null.
  - `Short` = group duration < ShortDuration.
  - `Mix`: group average speed < City → city; < Mixed → mixed; else highway.
- [ ] **Step 2: Write `LegacyParityTests`.** Load the fixture CSV and maintenance, and parse expected values from `legacy-output.txt` with regexes (do not hand-copy numbers):
  - `Summary_MatchesLegacy`: cold starts, total km (F2), fuel, avg speed, L/100, per-score km, DPF index (P2) over the period covering all data.
  - `GroupAnalysis_MatchesLegacy`: each `start - end: km, ..., Score: X` line equals the corresponding group's start, km (F1) and score.
  - `AllPeriods_MatchesLegacy`: for each type and each period block, the same metrics using `Period(eventDate, nextEventDate)`. The current period's end comes from the run date and is ignored.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.** `GroupsIn(period)` = groups whose `StartDate` is in the period. Port the legacy formulas exactly (sums, avg speed = km / summed hours, DPF = (Good+Excellent km) / all km).
- [ ] **Step 5: Run.** Expected: PASS. If a parity number differs only because of Rulings 2, the fixture is wrong (a group crosses an edge). Fix the fixture and regenerate Task 2's output. Do not change the rule.
- [ ] **Step 6: Commit** `feat(core): grouping, scoring, period stats with legacy parity`.

### Task 4: Odometer timeline, intervals, calendar periods

**Files:** Create `Analysis/OdometerTimeline.cs`, `Intervals.cs`, `CalendarPeriods.cs`. Modify `AnalysisContext.cs` (expose `Timeline`, fill `OdometerKm`/`TripCoverage`). Tests to match.

**Interfaces (Produces):**
```csharp
public sealed class OdometerTimeline {
  public static OdometerTimeline Build(IEnumerable<OdometerReading> readings); // drops readings lower than the running max (Dropped list kept)
  public IReadOnlyList<OdometerReading> Readings { get; } public IReadOnlyList<OdometerReading> Dropped { get; }
  public int? AtEndOfDay(DateOnly d); public int? Latest { get; }
  public double? KmIn(Period p); // start = p.StartOdometer ?? last reading before From 00:00 ?? first reading within p; end = p.EndOdometer ?? last reading before To 00:00; null if either missing or end < start
}
public static class Intervals { // newest first: Current, ..., "Before first <type>"
  public static IReadOnlyList<Period> Build(MaintenanceType type, IEnumerable<Expense> expenses, DateOnly? dataStart, DateOnly today);
}
public static class CalendarPeriods {
  public static Period Year(int y); public static Period Month(int y, int m);
  public static IReadOnlyList<Period> Years(DateOnly first, DateOnly last);
  public static IReadOnlyList<Period> Months(int year);
  public static IReadOnlyList<Period> Weeks(int year, int month); // ISO weeks clipped to the month, label "W07"
}
```
Timeline readings come from trip start/end odometers (only when > 0) and from expense odometers (time = end of that day). Interval periods take `StartOdometer`/`EndOdometer` from the bounding events. Labels: `Current (since yyyy-MM-dd)`, `yyyy-MM-dd → yyyy-MM-dd`, `Before first OilChange`. An event matches a type when `expense.Category == Maintenance && expense.Maintenance.HasFlag(type)`. A day with two matching events produces one boundary.

- [ ] **Step 1: Failing tests.**
  - `Timeline_IgnoresLowerReadings`: 1000, 1200, 900, 1300 → readings 1000, 1200, 1300; one dropped; KmIn never negative.
  - `Timeline_UnknownStart_Null`.
  - `Intervals_FixtureOilChange`: with the fixture, OilChange gives 3 periods, current first, the last one "Before first OilChange" starting at the first trip date. The middle period is [2025-03-10, 2025-09-15) with odometers from the events.
  - `Intervals_TripOnServiceDay_InNewInterval`.
  - `Calendar_MonthsSumToYear`: the sum of km and cold starts over `Months(2025)` equals `Year(2025)` on the fixture.
  - `Calendar_WeeksClipped`: the weeks of 2025-03 start on 03-01 and end on 04-01, and their km sum equals the month's.
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): odometer timeline, intervals, calendar periods`.

### Task 5: Charts I: existing three, with fixes

**Files:** Create `Analysis/Lowess.cs`, `Charts/ChartDtos.cs`, `Charts/ScoreCharts.cs`, `Charts/FuelCharts.cs` (fuel trend only for now). Tests: `LowessTests.cs`, `ScoreChartsTests.cs`, `FuelTrendTests.cs`.

**Interfaces (Produces):**
```csharp
public sealed record MonthlyScorePoint(string Month /*yyyy-MM*/, double ExcellentKm, double GoodKm, double FairKm, double BadKm);
public sealed record WeeklyDpfPoint(DateOnly WeekStart, double? Dpf, double? DpfMovingAverage);
public sealed record MonthlyScoreChart(IReadOnlyList<MonthlyScorePoint> Months, IReadOnlyList<WeeklyDpfPoint> Weeks);
public sealed record HeatmapDay(DateOnly Date, double Km, DrivingScore BestScore, double Intensity /*0..1 = min(1, km/cap)*/);
public sealed record FuelTrendPoint(DateTime Time, double LPer100, double Km, DrivingScore Score, long FirstTripId, int TripCount);
public sealed record FuelTrendChart(IReadOnlyList<FuelTrendPoint> Points, IReadOnlyList<(DateTime Time, double LPer100)> Smooth);
public enum TrendLevel { Group, Trip }
public static class ScoreCharts {
  MonthlyScoreChart Monthly(AnalysisContext c, Period p);
  IReadOnlyList<HeatmapDay> Heatmap(AnalysisContext c, int year);
  IReadOnlyList<ScoreMapPoint> ScoreMap(AnalysisContext c, Period p, TrendLevel level); // Task 6
}
public static class FuelCharts { FuelTrendChart Trend(AnalysisContext c, Period p, TrendLevel level); }
public static class Lowess { double[] Smooth(double[] x, double[] y, double[] w, double span); }
```
Fixes (Rulings 3): a weekly DPF group belongs only to the ISO week of its first trip, and the moving average spans `DpfMovingAverageWeeks` weeks that contain data (legacy semantics). Heatmap: the best group score among groups touching that day, km = that day's trip km, which is the legacy behaviour. Fuel trend: points whose L/100 lies outside [FuelTrendMin, FuelTrendMax] or whose km is 0 are dropped. For level Trip, each trip is a point with the trip's own score. LOWESS must never index out of range or divide by zero: use the identity for n < 3, clamp span to n−1, and when `maxDist == 0` or the denominator is 0, use the weighted mean.

- [ ] **Step 1: Failing tests.**
  - `Lowess_SmallN_NoThrow` (n = 0, 1, 2), `Lowess_AllSameX_NoNaN`, `Lowess_LinearData_ReturnsLine` (to within 1e-6).
  - `Monthly_KmSumsMatchStats` on the fixture.
  - `WeeklyDpf_GroupCountedOnce`: a group spanning Sunday–Monday counts in one week only.
  - `Heatmap_IntensityCapped`.
  - `FuelTrend_TripLevel_FiltersOutliers`.
- [ ] **Step 2–4:** Run (fail), implement, run (pass).
- [ ] **Step 5: Commit** `feat(core): monthly score, heatmap, fuel trend charts`.

### Task 6: Charts II: new trip visualizations

**Files:** Create `Charts/HabitCharts.cs`, `Charts/PlacesAnalysis.cs`, `Charts/DpfHealth.cs`. Modify `ScoreCharts.cs` (ScoreMap) and `FuelCharts.cs` (PenaltyCurve, Seasonality). Tests per file.

**Interfaces (Produces):**
```csharp
public sealed record ScoreMapPoint(double DurationMinutes, double AverageSpeedKmh, double Km, DrivingScore Score, DateTime Start, long FirstTripId);
public sealed record ScoreZones(double ShortMin, double MediumMin, double LongMin, double HeavyKmh, double CityKmh, double MixedKmh); // from parameters, for drawing
public sealed record HistogramBin(double From, double To, int Count);
public sealed record GapHistogram(IReadOnlyList<HistogramBin> Bins, double ThresholdMinutes); // gaps between consecutive trips, minutes; bins 0-5,5-10,...,115-120, then 2-4h, 4-8h, 8-24h, >24h
public sealed record HourCell(int DayOfWeek /*0=Mon*/, int Hour, double Km, int Trips, double? BadShare);
public sealed record PenaltyBin(double FromMinutes, double ToMinutes, double? MedianLPer100, double? P25, double? P75, int Count); // group level, 5-min bins up to 60, then 60-90, 90+
public sealed record SeasonPoint(int Year, int Month, double Km, double? LPer100);
public sealed record PlaceStat(string Address, int Starts, int Ends);
public sealed record RouteStat(string From, string To, int Count, double Km, double? AvgLPer100, DrivingScore TypicalScore, IReadOnlyList<(DateOnly Date, double? LPer100)> Trend);
public sealed record DpfHealthPoint(DateOnly Date, int DaysSinceLongDrive);
public sealed record DpfHealthSummary(DateOnly? LastLongDrive, int? DaysSince, double? KmSince, bool NeedsLongDrive,
  IReadOnlyList<DpfHealthPoint> Series, IReadOnlyList<Period> WorstStretches /*top 3 longest gaps*/);
public static class HabitCharts { GapHistogram Gaps(AnalysisContext c, Period p); IReadOnlyList<HourCell> WhenYouDrive(AnalysisContext c, Period p); }
public static class FuelCharts { IReadOnlyList<PenaltyBin> PenaltyCurve(AnalysisContext c, Period p); IReadOnlyList<SeasonPoint> Seasonality(AnalysisContext c); }
public static class PlacesAnalysis { IReadOnlyList<PlaceStat> Places(AnalysisContext c, Period p, int top = 15); IReadOnlyList<RouteStat> Routes(AnalysisContext c, Period p, int top = 15); }
public static class DpfHealth { DpfHealthSummary Compute(AnalysisContext c); } // "long drive" = group scored Good or Excellent; NeedsLongDrive = DaysSince >= LongDriveHintDays
```
Routes are at group level: first trip's start address → last trip's end address. Addresses are normalized (trimmed, case-insensitive, whitespace collapsed) for grouping, and the most common original spelling is displayed. `WhenYouDrive` attributes each trip to its start hour.

- [ ] **Step 1: Failing tests.**
  - `ScoreMap_PointsMatchGroups`.
  - `Gaps_ThresholdFromParameters`.
  - `Gaps_BinsCoverAllGaps`: the sum of counts = trips − 1.
  - `WhenYouDrive_KmSumsToPeriodKm`.
  - `PenaltyCurve_EmptyBinsHaveNullMedian`.
  - `Routes_NormalizesAddresses`.
  - `DpfHealth_NeedsLongDriveAfterHintDays`.
  - `DpfHealth_NoTrips_Nulls`.
- [ ] **Step 2–4:** Run (fail), implement, run (pass).
- [ ] **Step 5: Commit** `feat(core): score map, habits, penalty curve, seasonality, routes, DPF health`.

### Task 7: Money, fuel economy, tyres, money and timeline charts

**Files:** Create `Domain/Expense.cs`, `ExpenseCategory.cs`, `TyreSet.cs` (if they don't exist yet), `Analysis/Money.cs`, `FuelEconomy.cs`, `TyreMileage.cs`, `Charts/MoneyCharts.cs`, `Charts/TimelineChart.cs`. Modify `AnalysisContext.Stats` to fill `Money` and `FullTank*`. Tests per file.

**Interfaces (Produces):**
```csharp
public enum ExpenseCategory { Fuel, Parking, TollsVignette, Wash, Maintenance, Repair, Tyres, Accessories, Insurance, RoadTax, Fines, Financing, Other }
public enum ExpenseSource { Web, Mcp, Migration }
public sealed record Expense {
  long Id; DateOnly Date; ExpenseCategory Category; decimal Amount; string Currency = "CZK"; decimal? HomeAmount; int? Odometer; string? Note;
  DateOnly? CoversFrom; DateOnly? CoversTo; double? Litres; bool? FullTank; string? Station;
  MaintenanceType Maintenance; string? MaintenanceDescription; long? TyreSetId; ExpenseSource Source; // all { get; init; }
}
public enum TyreSeason { Summer, Winter, AllSeason }
public sealed record TyreSet { long Id; string Name; TyreSeason Season; string? Description; DateOnly? PurchaseDate; bool Retired; }
public static class Money {
  decimal? HomeAmountOf(Expense e, string homeCurrency); // same currency → Amount; else HomeAmount (may be null)
  MoneyStats For(Period p, IEnumerable<Expense> expenses, string homeCurrency, double? km); // amortizes CoversFrom..CoversTo (inclusive) by day
}
public sealed record FullTankSegment(DateOnly EndDate, int StartOdometer, int EndOdometer, double Litres, bool Inferred);
public static class FuelEconomy { IReadOnlyList<FullTankSegment> Segments(IEnumerable<Expense> fuelExpenses, OdometerTimeline t); }
public sealed record TyreMountSpan(long? TyreSetId, DateOnly From, DateOnly? To, int? FromOdometer, int? ToOdometer, double? Km);
public sealed record TyreSetSummary(TyreSet Set, double TotalKm, int Seasons, decimal? PurchaseCost, decimal? CostPer1000Km, bool Mounted, IReadOnlyList<TyreMountSpan> Spans);
public static class TyreMileage {
  IReadOnlyList<TyreMountSpan> Spans(IEnumerable<Expense> expenses, long? initialSetId, OdometerTimeline t, DateOnly today);
  IReadOnlyList<TyreSetSummary> Summaries(IReadOnlyList<TyreMountSpan> spans, IEnumerable<TyreSet> sets, IEnumerable<Expense> expenses, string homeCurrency);
}
public sealed record MonthlyCost(string Month, IReadOnlyDictionary<ExpenseCategory, decimal> ByCategory);
public sealed record CumulativeCostPoint(DateOnly Date, decimal CumulativeCost, double? CumulativeKm, decimal? CostPerKm);
public static class MoneyCharts { IReadOnlyList<MonthlyCost> Monthly(AnalysisContext c, Period p);
  IReadOnlyDictionary<ExpenseCategory, decimal> Totals(AnalysisContext c, Period p); IReadOnlyList<CumulativeCostPoint> Cumulative(AnalysisContext c); }
public enum TimelineEventKind { Maintenance, TyreMount, Expense, Import }
public sealed record TimelineEvent(DateOnly Date, TimelineEventKind Kind, string Label, int? Odometer, decimal? Amount, long? RefId);
public sealed record TimelineChart(IReadOnlyList<(DateOnly Date, int Km)> Odometer, IReadOnlyList<TimelineEvent> Events, IReadOnlyList<TyreMountSpan> TyreBands);
public static class TimelineBuilder { TimelineChart Build(AnalysisContext c, Period p); }
```
Rules:
- Money totals use `HomeAmountOf`. A null home amount is excluded and counted in `NeedsConversionCount`.
- `CostPerKm` = Total / (OdometerKm ?? TripKm), null when km is 0 or null.
- Fuel segments run between consecutive full-tank fill-ups in odometer order. Litres = the sum of litres of all fill-ups after the start fill-up, up to and including the end fill-up. A missing odometer is taken from `t.AtEndOfDay(date)` and the segment is marked `Inferred`; if that is null too, the fill-up is skipped. `FullTankLPer100` sums the segments whose `EndDate` is in the period.
- Tyre mounts come from Maintenance expenses with the TyreSwap or NewTyres flag. A span runs from one mount to the next; the last span runs to today using `t.Latest`. Before the first mount there is a span with `initialSetId`, starting at the first reading. "Seasons" = number of spans.

- [ ] **Step 1: Failing tests.**
  - `Money_ForeignWithoutHomeAmount_ExcludedAndCounted`.
  - `Money_YearlyInsuranceAmortized`: 3 650 CZK covering 2026-01-01..2026-12-31 gives 310 for January (31 days × 10).
  - `Money_CostPerKm_NullWhenNoKm`.
  - `FuelEconomy_FullToFull`: full tank at 1 000, partial 20 L at 1 300, full 25 L at 1 600 → 45 L / 600 km = 7.5.
  - `FuelEconomy_InferredOdometer`.
  - `Tyres_KmSummedAcrossSeasons`: set A mounted 1 000→5 000 and 9 000→now (latest 10 000) → 5 000 km over 2 seasons.
  - `Tyres_UnassignedSwapSpanHasNullSet`.
  - `Timeline_EventsSortedAndBandsMatchSpans`.
- [ ] **Step 2–4:** Run (fail), implement, run (pass).
- [ ] **Step 5: Commit** `feat(core): money, full-tank economy, tyre mileage, money and timeline charts`.

### Task 8: Data layer: EF Core SQLite, user scoping, import, legacy migration

**Files:** Create `src/DriverBalance.Data/*` (packages `Microsoft.EntityFrameworkCore.Sqlite` 10.x and `Microsoft.EntityFrameworkCore.Design`; local tool `dotnet-ef` via `.config/dotnet-tools.json`) and `Import/LegacyJsonReader.cs` in Core. Tests: `tests/DriverBalance.Web.Tests/Data/*` (create the Web.Tests project now; it references Data, and Web is added in Task 9) using SQLite `:memory:` with an open connection.

**Interfaces (Produces):**
```csharp
public interface ICurrentUser { long UserId { get; } }
// Rows (all with long Id): UserRow(Name, CreatedAt) ; VehicleRow(UserId, Name, Vin?, FuelType, HomeCurrency="CZK", TimeZone="Europe/Prague",
//   ParametersJson, InitialTyreSetId?, IsDefault) ; TripRow(UserId, VehicleId, ImportBatchId?, + Trip fields) unique (VehicleId, Start, End) ;
//   ExpenseRow(UserId, VehicleId, + Expense fields, MaintenanceFlags int, CreatedAt) ; TyreSetRow(UserId, VehicleId, + TyreSet fields) ;
//   ImportBatchRow(UserId, VehicleId, FileName, ImportedAt, Imported, Skipped, FirstTrip?, LastTrip?) ; ApiTokenRow(UserId, Name, TokenHash, CreatedAt, LastUsedAt?)
public sealed class DriverBalanceDbContext(DbContextOptions<DriverBalanceDbContext> o, ICurrentUser user) : DbContext(o) // global filter UserId == user.UserId on every row type except UserRow; SaveChanges stamps UserId on Added rows
public static class Mapping { Trip ToDomain(this TripRow r); Expense ToDomain(this ExpenseRow r); TyreSet ToDomain(this TyreSetRow r); /* + reverse */ }
public sealed record ImportResult(long BatchId, int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip, IReadOnlyList<string> Warnings);
public sealed class ImportService(DriverBalanceDbContext db) { Task<ImportResult> ImportCsvAsync(long vehicleId, string fileName, TextReader csv, CancellationToken ct); }
public sealed record LegacyMigrationResult(int TripsImported, int TripsSkipped, int ExpensesImported, int ExpensesSkipped);
public sealed class LegacyMigrationService(DriverBalanceDbContext db) { Task<LegacyMigrationResult> MigrateAsync(long vehicleId, Stream? tripsJson, Stream? maintenanceJson, CancellationToken ct); }
// Core: public static class LegacyJsonReader { IReadOnlyList<Trip> ReadTrips(Stream s); IReadOnlyList<Expense> ReadMaintenance(Stream s); }
```
Migration rules (Rulings 9):
- A legacy maintenance record becomes a Maintenance expense: Amount = Cost; an empty currency becomes CZK; `Source = Migration`.
- A cost of 0 gets the note `imported — check cost`.
- The dedup key for maintenance is (date, flags, odometer, amount, description).
- Trips dedup on (VehicleId, Start, End).
- `TimeSpan Duration` is stored as ticks (long). `ParametersJson` holds `AnalysisParameters`.
- Create the initial migration `Initial`.

- [ ] **Step 1: Failing tests.**
  - `ImportCsv_Twice_SkipsDuplicates`: the fixture imported twice gives a second result with Imported = 0 and Skipped = 120 (or the fixture's count).
  - `LegacyMigration_Twice_IsIdempotent`.
  - `LegacyMigration_ZeroCostGetsNote`.
  - `UserScoping_OtherUserSeesNothing`: rows written as user 1 are invisible to a context for user 2, including `Find`-style queries via `FirstOrDefault`.
  - `Parameters_RoundTrip`.
- [ ] **Step 2–4:** Run (fail), implement, add the migration, run (pass).
- [ ] **Step 5: Commit** `feat(data): EF Core SQLite with user scoping, CSV import, legacy migration`.

### Task 9: Web API host and endpoints

**Files:** Create `src/DriverBalance.Web/` (sdk Web), with `Program.cs`, `Auth/*`, `Services/*`, `Endpoints/*`, `Dtos/*`. Test project `tests/DriverBalance.Web.Tests` (`Microsoft.AspNetCore.Mvc.Testing`) gets `ApiTests/*`.

**Interfaces (Produces): HTTP API** (all under `/api`, JSON per Global Constraints, `{vid}` = vehicle id; a vehicle the user doesn't own → 404):
- `GET /vehicles` and `POST /vehicles`; `GET|PUT /vehicles/{vid}` (name, homeCurrency, timeZone, initialTyreSetId, isDefault).
- `GET /vehicles/{vid}/parameters`, `PUT /vehicles/{vid}/parameters` (invalid → 400 `{errors: string[]}`), `GET /parameters/defaults`.
- `POST /vehicles/{vid}/import/csv` (multipart `file`) → ImportResult. `POST /vehicles/{vid}/import/legacy` (multipart `trips`, `maintenance`, both optional) → LegacyMigrationResult. `GET /vehicles/{vid}/import/batches`.
- Analytics, all `POST` with body `{ ..., parameters?: AnalysisParameters }`. An override is validated (400 on errors) and used instead of the saved parameters, **without saving**.
  - `/vehicles/{vid}/analytics/stats` `{periods:[{from,to,label}]}` → `PeriodStats[]`
  - `/analytics/calendar` `{level:"years"|"months"|"weeks", year?, month?}` → `[{period, stats}]`
  - `/analytics/intervals` `{type: MaintenanceType}` → `[{period, stats}]`
  - `/analytics/charts/{name}` `{from?, to?, options?: {level?, year?}}` with name ∈ `monthly-score, heatmap, fuel-trend, score-map, gap-histogram, when-you-drive, penalty-curve, seasonality, places, routes, dpf-health, money-monthly, money-totals, money-cumulative, timeline` → the Core DTO. Score map also returns `ScoreZones`.
  - `/analytics/overview` `{}` → `{ thisMonth: PeriodStats, thisYear: PeriodStats, dpfHealth, recentExpenses: Expense[5], reminders: [{kind, message, severity}] }`. Reminders: export (last trip older than ExportReminderDays → warn; > 300 days → alarm), needs-conversion count, unassigned tyre swaps.
  - Missing from/to = all data (first trip date or first expense date → today+1). "Today" uses the vehicle's time zone.
- `GET /vehicles/{vid}/trips?from&to&page&pageSize` → groups with their trips, newest first. `GET /vehicles/{vid}/trips/{tripId}`.
- `GET|POST /vehicles/{vid}/expenses` (filters: from, to, category), `PUT|DELETE /vehicles/{vid}/expenses/{id}`. Validation: Amount > 0, Currency is 3 letters, Litres > 0 when given, CoversFrom ≤ CoversTo, and the `TyreSetId` must exist on that vehicle.
- `GET|POST /vehicles/{vid}/tyres`, `PUT|DELETE /vehicles/{vid}/tyres/{id}`, `GET /vehicles/{vid}/tyres/summary` → TyreSetSummary[] plus unassigned spans.
- `GET /export` → every user-owned row as one JSON document (a download).
- Auth: the default scheme `LocalUser` signs in as user 1. `HttpCurrentUser : ICurrentUser` reads the `sub` claim. Startup applies migrations and seeds user 1 plus a default vehicle "My Volvo" with default parameters when there are no vehicles. Kestrel listens on `http://localhost:5180`. SPA fallback serves `wwwroot/index.html` for non-`/api` and non-`/mcp` paths.
- `VehicleDataLoader.LoadAsync(long vid, CancellationToken)` → `(VehicleRow row, VehicleData data, AnalysisParameters saved)`.

- [ ] **Step 1: Failing tests** with WebApplicationFactory, a temp SQLite file per test class, and a test auth handler that sets the user id from an `X-Test-User` header.
  - `AllAnalyticsEndpoints_EmptyVehicle_Return200`: every chart name, plus stats, calendar, intervals and overview.
  - `Stats_InvalidParameters_Returns400`.
  - `Stats_ParameterOverride_ChangesResultButNotSaved`: gap 1 min gives more cold starts; the saved parameters are unchanged.
  - `ImportCsv_ThenStats_MatchesCore`.
  - `Vehicle_OtherUser_404` (user 2 cannot read user 1's vehicle or expenses).
  - `Expense_Validation_400`.
  - `Export_ContainsOnlyOwnRows`.
- [ ] **Step 2–4:** Run (fail), implement, run (pass).
- [ ] **Step 5: Smoke-test manually.** `dotnet run --project src/DriverBalance.Web` with `DriverBalance__DatabasePath` set to a scratch file, then `curl -s localhost:5180/api/vehicles`. Expected: the seeded vehicle. Then POST the fixture CSV and call `/analytics/overview`. Expected: 200.
- [ ] **Step 6: Commit** `feat(web): API host, local auth, analytics/expense/tyre/import endpoints`.

### Task 10: API tokens and MCP server

**Files:** Create `Endpoints/TokenEndpoints.cs`, `Auth/ApiTokenHandler.cs`, `Mcp/CarTools.cs`. Modify `Program.cs`. Tests: `ApiTests/McpTests.cs`.

**Interfaces (Produces):**
- `POST /api/tokens {name}` → `{id, name, token}`. The token is shown once: 32 random bytes, base64url, prefix `dbk_`. Only its SHA-256 is stored. `GET /api/tokens` returns `[{id, name, createdAt, lastUsedAt}]`. `DELETE /api/tokens/{id}`.
- `/mcp` uses Streamable HTTP via `AddMcpServer().WithHttpTransport().WithTools<CarTools>()` and `MapMcp("/mcp").RequireAuthorization("ApiToken")`. Scheme `ApiToken` checks `Authorization: Bearer dbk_…` against the hash, sets `sub`, and updates LastUsedAt. **The LocalUser scheme must not authenticate /mcp.** Check the SDK's current API on NuGet/GitHub before writing it.
- Tools. Every tool takes optional `vehicleId` (default: the user's `IsDefault` vehicle, else the first). They return the stored entity as JSON plus a one-line `summary`, and descriptions say which fields are optional and what the defaults are.
  - `add_expense(category, amount, currency?="home", date?=today in vehicle tz, odometer?, note?, homeAmount?, rate?, coversFrom?, coversTo?)`
  - `log_fuel(litres, totalPrice, fullTank=true, odometer?, date?, station?, currency?)`
  - `log_maintenance(types: string[], cost, odometer?, date?, description?)`
  - `swap_tyres(tyreSetId, odometer?, date?, cost?)`
  - `list_tyre_sets()`
  - `list_recent_expenses(count=10)`
  - `delete_expense(id)`
  - `get_stats(kind: "interval"|"range"|"calendar", maintenanceType?, intervalIndex?=0, from?, to?, year?, month?)` → PeriodStats with saved parameters
  - `import_trips_csv(csvText)` → ImportResult; NBSP and `\r\n` survive

  A foreign currency without homeAmount or rate is saved, and the result says `needsConversion: true` (Rulings 6).
- The token UI (create/list/revoke) comes in Task 12. A short `docs/mcp.md` gives a client config example for the family-assistant bot.

- [ ] **Step 1: Failing tests.**
  - `Mcp_NoToken_401`.
  - `Mcp_LocalUserCookieless_401`: no bearer header → 401 even though LocalUser would succeed elsewhere.
  - `Mcp_ListTools_ContainsAll`.
  - `Mcp_AddExpense_LandsOnDefaultVehicleOfTokenUser`.
  - `Mcp_ForeignCurrency_NeedsConversion`.
  - `Mcp_GetStats_EqualsApiStats`.
  - `Token_Revoked_401`.

  Use the SDK's client (`McpClient` over HTTP against the test server's handler) or raw JSON-RPC POSTs.
- [ ] **Step 2–4:** Run (fail), implement, run (pass).
- [ ] **Step 5: Commit** `feat(web): API tokens and MCP server`.

### Task 11: Frontend scaffold, layout, parameters panel, build integration

**Files:** Create `web/` (Vite react-ts): `src/main.tsx`, `src/App.tsx` (router + layout + nav), `src/api/client.ts`, `src/api/types.ts`, `src/state/ParamsContext.tsx`, `src/components/ParamsPanel.tsx`, `src/components/Chart.tsx` (a thin `echarts-for-react` wrapper with theme), `src/components/PeriodPicker.tsx`, `src/styles/tokens.css`, `src/pages/*` (stubs). Modify `src/DriverBalance.Web/DriverBalance.Web.csproj`: a target before publish runs `npm ci && npm run build` in `web/` and copies `web/dist` → `wwwroot`. `vite.config.ts` proxies `/api` and `/mcp` to `http://localhost:5180`.

**Interfaces (Produces):**
```ts
// api/types.ts mirrors the Task 9 DTOs (camelCase). api/client.ts:
export const api = { get<T>(path: string): Promise<T>; post<T>(path: string, body?: unknown): Promise<T>; put; del; upload<T>(path, form: FormData) };
// throws ApiError { status: number; errors?: string[] }
// state/ParamsContext.tsx
export interface ParamsState { vehicleId: number; saved: AnalysisParameters; draft: AnalysisParameters; dirty: boolean;
  setDraft(p: Partial<AnalysisParameters>): void; save(): Promise<void>; reset(): void; override: AnalysisParameters | undefined /* = dirty ? draft : undefined */ }
export function useParams(): ParamsState;
export function useAnalytics<T>(path: string, body: object): UseQueryResult<T>; // posts {...body, parameters: override}; query key includes override
```
- Nav: Overview · Trips · Statistics · Intervals · Tyres · Expenses · Import · Settings. A vehicle switcher appears in the header only when there is more than one vehicle.
- `ParamsPanel`: a right-hand drawer opened from a header button on every page. It has grouped numeric inputs (Cold start, Score thresholds, Driving mix, Charts, Reminders). Changes debounce for 300 ms and recompute through the override. An "Unsaved — trying changes" badge appears in the header while dirty. Save and Reset buttons. A 400 shows the server's errors inline.
- Theme: CSS variables on `:root`, dark mode via `prefers-color-scheme`. Score colours stay consistent everywhere (Excellent, Good, Fair, Bad). **Load the `dataviz` skill before choosing chart colours or writing the Chart wrapper.**
- Layout works from 360 px width up.

- [ ] **Step 1: Failing Vitest tests.**
  - `ParamsContext`: `setDraft` makes `dirty` true and `override` equal to the draft; `reset` clears it.
  - `client`: a 400 response throws ApiError with `errors`.
- [ ] **Step 2–4:** Run `npm test` (fail), implement, run (pass). `npm run build` and `npx tsc --noEmit` must be clean.
- [ ] **Step 5: Verify publish.** `dotnet publish src/DriverBalance.Web -c Release -o <scratch>`, then run the exe. `http://localhost:5180/` serves the SPA, and deep links like `/trips` load.
- [ ] **Step 6: Commit** `feat(web-ui): scaffold, layout, parameters panel, publish integration`.

### Task 12: Frontend: Import, Settings, Expenses, Tyres

**Files:** `web/src/pages/ImportPage.tsx`, `SettingsPage.tsx`, `ExpensesPage.tsx`, `TyresPage.tsx`, `web/src/components/ExpenseForm.tsx`, plus small shared components as needed.

- **Import:**
  - a drag-and-drop zone (with a click-to-pick fallback) for the Volvo CSV, then a result card (new / duplicates / date range / warnings);
  - a separate "Migrate old DriverBalance data" card taking two JSON files;
  - the batch history table;
  - an export reminder line ("last trip: N days ago").
- **Settings:** vehicle fields (name, currency, time zone, initial tyre set, default), a link that opens the parameters panel, API tokens (create shows the token once with a copy button; list; revoke), and an "Export all data" button.
- **Expenses:**
  - a filter bar (period picker, category multi-select) and a table (date, category, amount, home amount, odometer, note), with a needs-conversion badge on rows;
  - quick add/edit in a dialog with fields per category: Fuel (litres, full tank, station, with price per litre derived and shown), Maintenance (type checkboxes + description, tyre set picker when TyreSwap or NewTyres is checked), Tyres (set picker), and every category gets covers-from/to under "Spread cost over a period";
  - delete with confirm;
  - charts: monthly cost stacked by category, a category treemap for the filtered period, cumulative cost per km.
- **Tyres:**
  - set cards (name, season, total km, seasons, cost per 1 000 km, mounted badge);
  - a bar chart of km per span coloured by set;
  - an "Assign set" control on unassigned swaps (edits that expense's TyreSetId);
  - add/edit/retire set.

- [ ] **Step 1: Failing Vitest tests** for the pure helpers you add: `pricePerLitre(totalPrice, litres)` returns null for 0 litres; the expense form builds the correct payload per category (Fuel includes litres; Maintenance's types become a string array).
- [ ] **Step 2–4:** Run (fail), implement, run (pass). `npm run build` must be clean.
- [ ] **Step 5: Manual check.** With the API running on a scratch DB: import the fixture CSV, migrate the fixture maintenance, add a fuel expense, assign a set to a swap, and see the tyre km appear. Use the `run` skill or a browser if available. Otherwise curl the API and check that the pages build.
- [ ] **Step 6: Commit** `feat(web-ui): import, settings, expenses, tyres pages`.

### Task 13: Frontend: Trips and Statistics

**Files:** `web/src/pages/TripsPage.tsx`, `StatisticsPage.tsx`, `web/src/charts/*.tsx` (one file per chart: MonthlyScore, Heatmap, FuelTrend, ScoreMap, GapHistogram, WhenYouDrive, PenaltyCurve, Seasonality, Routes), `web/src/components/StatsTable.tsx`, `CompareTable.tsx`, `web/src/lib/compare.ts`.

**Interfaces (Produces):**
```ts
// lib/compare.ts
export type MetricKey = 'kmPerDay'|'coldStartsPer100Km'|'shortShareByKm'|'dpfIndex'|'cityShare'|'tripLPer100'|'fullTankLPer100'|'costPerKm'|'total'|'tripKm'|'days'|'coldStarts';
export interface MetricRow { key: MetricKey; label: string; a: number|null; b: number|null; change: number|null; better: 'up'|'down'|null; isRate: boolean }
export function compareStats(a: PeriodStats, b: PeriodStats): MetricRow[]; // change = (b-a)/a for rates, null if a null/0; 'better' direction per metric (higher DPF better, lower cold starts/100km better, ...)
export function StatsTable / CompareTable components use MetricRow[]; StatsTable groups rows under Usage / Engine wear & DPF / Fuel / Money
```
- **Trips page:** a period picker (all / year / custom) and a Group | Trip level toggle at the top. Tabs:
  - **Overview charts:** monthly score + DPF line, and the calendar heatmap with a year picker.
  - **Fuel:** fuel trend with its LOWESS line, penalty curve, seasonality.
  - **Driving style:** score map with shaded zones from the live parameters, gap histogram with a threshold line, when-you-drive heatmap.
  - **Places:** the routes table with a per-route sparkline, and the top places.
  - **List:** paged groups that expand into trips.

  Clicking a point on the fuel trend or score map opens a trip detail drawer.
- **Statistics page:**
  - mode switch between Calendar and Custom range;
  - Calendar: a years table → click a year → months table + chart → click a month → weeks table → click a week → that week's trip list;
  - every table row shows km, cold starts, DPF, L/100 and cost;
  - a breadcrumb keeps the path;
  - Custom: from/to (inclusive "to" in the UI), an optional "Compare with" second range, then `CompareTable`;
  - selections are kept in the URL query string so views can be shared or bookmarked.
- Every chart has a sensible empty state ("No trips in this period"). Charts read colours from the theme tokens.

- [ ] **Step 1: Failing Vitest tests.**
  - `compareStats`: a null/0 baseline gives a null change; a DPF rise is better = 'up'; a cold-starts/100km rise is better = 'down'.
  - The URL-state helper round-trips `{mode, year, month, week, from, to, cmpFrom, cmpTo}`.
- [ ] **Step 2–4:** Run (fail), implement, run (pass). `npm run build` must be clean.
- [ ] **Step 5: Manual check** on the scratch DB with the fixture. Every tab renders. Changing the cold-start gap in the parameters panel changes the gap-histogram threshold line and the cold-start numbers immediately.
- [ ] **Step 6: Commit** `feat(web-ui): trips charts and statistics drill-down/compare`.

### Task 14: Frontend: Intervals and Overview

**Files:** `web/src/pages/IntervalsPage.tsx`, `OverviewPage.tsx`, `web/src/charts/IntervalTrend.tsx`, `SmallMultiples.tsx`, `VehicleTimeline.tsx`, `DpfHealthStrip.tsx`.

- **Intervals page** (the mockup agreed in brainstorming):
  - a maintenance-type select (default OilChange, only types that have events);
  - interval A/B selects (default Current vs Previous) feeding `CompareTable`;
  - below it, a chart of weekly L/100 km and DPF index across both intervals with a vertical marker at the service date (built from the `fuel-trend` and `monthly-score` charts over A∪B);
  - a vehicle timeline for A∪B (odometer line, event markers, tyre bands);
  - small multiples: one mini bar chart per metric (km/day, cold starts/100 km, DPF, L/100 km, cost/km) across all intervals of the type, oldest → newest, with the current interval highlighted.
- **Overview page:**
  - KPI tiles for this month and this year (km, cost, cost/km, DPF index, L/100);
  - a DPF health strip (days since a long drive, a "needs a long drive" callout, a sparkline of the series, the worst stretches);
  - reminders list;
  - recent expenses with a quick "Add expense" button that opens `ExpenseForm`;
  - the vehicle timeline for the last 12 months.

- [ ] **Step 1: Failing Vitest test.** The helper `pickDefaultIntervals(intervals)` returns [current, previous]. With one interval it returns [current, null], and the page then shows the single interval without the compare column.
- [ ] **Step 2–4:** Run (fail), implement, run (pass). `npm run build` must be clean.
- [ ] **Step 5: Manual check** on the fixture DB. OilChange shows Current vs Previous with deltas, the service-date marker shows, and the small multiples have 3 bars.
- [ ] **Step 6: Commit** `feat(web-ui): intervals compare and overview`.

### Task 15: Retire console, docs, autostart, real-data handover

**Files:** Delete `DriverBalance/` (the legacy project) from the solution and repo, once Task 3's parity tests are green; the history keeps it. The parity tests read only `tests/fixtures/legacy/legacy-output.txt`, so they keep working. Create `scripts/register-autostart.ps1` (registers a per-user Scheduled Task "DriverBalance" at logon that runs the published exe from `%LOCALAPPDATA%\DriverBalance\app`, plus `-Unregister`). Rewrite `README.md` (what it is, dev run, publish + autostart, data location, MCP setup pointer). Update `.gitignore` (`web/node_modules`, `web/dist`, `src/DriverBalance.Web/wwwroot`).

- [ ] **Step 1: Run the full suite.** `dotnet test` and `npm test` all green, plus `npm run build`.
- [ ] **Step 2: Local-only real-data parity check, not committed.** Run the legacy console against a **copy** of `DriverBalance/bin/Debug/net10.0/*.json` (menu 3 and 11), and compare its numbers with the new API after migrating the same copy into a scratch DB. Report the differences as counts and metric names only; no addresses. Any difference must be explained by Rulings 2/3 or fixed.
- [ ] **Step 3: Delete the legacy project**, update the solution, build, and run the tests.
- [ ] **Step 4: Publish to `%LOCALAPPDATA%\DriverBalance\app`, start it, and migrate the owner's real data.** Use the Import page's legacy migration with the two JSON files from `bin/Debug` into the real DB at the default path; keep the original JSON files untouched. Verify `/api/analytics/overview` returns 200 with 1 500 trips accounted for. **Do not register autostart** (the owner decides). Mention the script in the handover.
- [ ] **Step 5: Commit** `chore: retire console app; README, autostart script`.

---

## Self-review notes

- Spec coverage:
  - parity → T2/T3; periods and rulings 1–2 → T3/T4; rulings 3 → T5; 4 → T4; 5 → T9 (tz "today"); 6 → T7/T10; 7 → T7; 8 → T7/T12; 9 → T8; 10 → T1/T9; 11 → T10/T15; 12 → T11;
  - live parameters → T1/T9/T11; visualizations 1–10 → T5–T7 + T13/T14; pages → T11–T14; MCP → T10;
  - multi-user scoping → T8/T9; export → T9/T12; reminders → T9/T14.
  - Volvo pull and bot wiring are out of scope by the first-version decision.
- Type names are pinned in the Interfaces blocks. The timeline builder is `TimelineBuilder`, and its record is `TimelineChart`.
