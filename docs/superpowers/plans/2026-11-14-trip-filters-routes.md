# Trip Filters and Regular Routes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A "Longer than X km" filter with a summary line on Trips → List (Group and Trip level), and an automatic Regular routes tab whose rows filter the list.

**Architecture:** Core gets `RegularRoutes` (item sets per level, route keys, the routes table). The trips list endpoint gains `level`, `minKm`, `route` and a `summary`; a new `trips/routes` endpoint serves the table. The React Trips page gets a Routes tab, a slider, the summary line and a route chip, all in the URL state.

**Tech Stack:** C# / .NET 10, xUnit, ASP.NET Core minimal APIs; React 19 + TanStack Query + react-router + vitest/testing-library.

**Spec:** `docs/superpowers/specs/2026-11-14-trip-filters-routes-design.md` (binding; read it, including "Builder notes").

## Global Constraints

- Worktree `C:\Users\aashm\source\repos\DriverBalance-tripcost`, branch `feat/trip-filters`. Never touch `C:\Users\aashm\source\repos\DriverBalance`.
- The repo is PUBLIC: made-up addresses and values only.
- Slider steps: 0, 5, 10, 20, 50, 100, 200, 500 km. Route threshold: 5 drives. Typical: ≥ 80 % weekdays/weekends, hours with ≥ 20 % of starts, at most two. Month = 30.44 days.
- Costs come from the existing `TripCosts` (`AnalysisContext.Costs`); money rounded with `Money.Round`.
- UI dates dd/mm/yyyy; money as `≈ 142 CZK` via `fmtApprox`; hide empty things rather than show "nothing" text, except the one Routes empty line the spec gives.
- TDD order: failing test first, run it, see it fail, then implement. `dotnet build` 0 warnings; `npm run build`, `npx vitest run`, `npm run lint` green.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. Old callers of `GET /trips` (TripList today, Overview Recent trips) must get the same groups and order as before (Task 2 test).
2. A trip or group with an empty start or end address must never crash route building and never form a route (Task 1 test).
3. A period with no trips (e.g. a future year) → Routes empty, summary `count 0`, per-month never divides by zero (Task 1 + 2 tests).
4. `minKm` negative / not a number, `level` unknown → 400 with a readable message, not 500 (Task 2 test).
5. A `route` key from the URL that matches nothing → empty list and summary 0, no error (Task 2 test).

---

### Task 1: Core `RegularRoutes`

**Files:**
- Create: `src/DriverBalance.Core/Analysis/RegularRoutes.cs`
- Modify: `src/DriverBalance.Core/Charts/PlacesAnalysis.cs` (move the private `Spellings` class out to an `internal sealed class AddressSpellings` in `RegularRoutes.cs` or its own file, and use it from both)
- Test: `tests/DriverBalance.Core.Tests/RegularRoutesTests.cs`

**Interfaces (produces):**
- `public sealed record RegularRoute(string Key, string From, string To, IReadOnlyList<string> Via, bool RoundTrip, int Count, string Typical, double AvgKm, decimal AvgCost, decimal PerMonth, double Share, string Currency);`
- `public static class RegularRoutes` with:
  - `public const int MinDrives = 5;`
  - `public static IReadOnlyList<TripGroup> Items(AnalysisContext c, Period p, TrendLevel level)` — Group: `c.GroupsIn(p)`; Trip: every trip of `c.Groups` whose own start date is in `p`, wrapped as a one-trip `TripGroup` with `Scoring.Score(trip.Duration, trip.AverageSpeedKmh, c.Parameters)`. Newest first is NOT required here (callers sort).
  - `public static string? KeyOf(TripGroup item)` — null when the first start or last end address is blank. Normal form per the spec's Builder notes (normalized addresses as in `AddressSpellings`; stops = end addresses of all but the last trip, consecutive duplicates collapsed, stops equal to either endpoint dropped; both directions and reversed stops give the same key). The key is the first 16 lowercase hex chars of SHA-256 over the normal form (URL-safe, opaque). A one-trip item and a direct group with the same endpoints get the same key (same route) — accepted.
  - `public static IReadOnlyList<RegularRoute> Build(AnalysisContext c, Period p, TrendLevel level)` — routes with ≥ `MinDrives` items; sorted by Count desc, then total km desc, then From ordinal. Display direction, Typical, PerMonth (months per the Builder notes; first recorded trip = earliest `c.Data.Trips` start date, today = `c.Data.Today`; when months ≤ 0 use 1) and Share (route cost ÷ cost of all `Items` at that level and period; 0 when that is 0) per the spec. Currency = `c.Data.HomeCurrency`.

- [ ] **Step 1: Write the failing tests** (build `VehicleData(trips, expenses, [], "CZK", new DateOnly(2026, 11, 8))`, `AnalysisContext.Create(data, new AnalysisParameters())`; a helper making trips with given start time, addresses, km and minutes; trips in one group are minutes apart, separate drives are hours apart):
  - `TripLevel_BothDirectionsAreOneRoute`: 3× Home→Work and 3× Work→Home on weekday mornings/evenings → one route, Count 6, From "Home" (started from more often or tie → alphabetical; make Home the start 4 times to pin it), To "Work", Via empty.
  - `FewerThanFiveDrives_NotARoute`: 4 drives A↔B → empty.
  - `BlankAddress_Skipped` (Review Focus 2): 6 trips with an empty end address → no route, no exception; `KeyOf` returns null.
  - `GroupLevel_DirectAndViaAreSeparate`: 5 groups Home→School→Work (two trips 5 min apart) and 5 direct Home→Work groups → two routes; the via route has Via ["School"].
  - `GroupLevel_ReversedStopsMerge`: 3 groups Home→School→Work and 2 groups Work→School→Home → one route, Count 5.
  - `RoundTrip_StopOrderIgnored`: 3 groups Home→Shop→Mall→Home and 2 groups Home→Mall→Shop→Home → one route, RoundTrip true.
  - `StopEqualToEndpoint_Dropped`: groups Home→Work→Work (second trip Work→Work) count as the direct Home↔Work route (`KeyOf` equals that of a direct Home→Work group).
  - `Typical_Text`: all starts Mon–Fri at 7:10 and 16:20 → `"Weekdays · 7–8, 16–17"`; all Sat/Sun at 10:xx → `"Weekends · 10–11"`; mixed 50/50 days with starts spread over 10 different hours → `"Any day"`.
  - `PerMonthAndShare`: one route of 6 drives inside a 2025 calendar-year period with the first recorded trip on 2025-07-01 and only an Insurance expense so costs are non-zero; assert PerMonth = round(routeTotal ÷ (184 / 30.44), 2) (2025-07-01..2025-12-31 = 184 days) and Share = routeTotal ÷ allItemsTotal (other non-route drives present so Share < 1).
  - `EmptyPeriod_NoRoutes` (Review Focus 3): a 2030 period → empty, no exception.
  - `Items_TripLevelScoresEachTrip`: a group of two trips → two one-trip items, each `Score` equal to `Scoring.Score` of that trip.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Core.Tests --filter RegularRoutesTests` → FAIL (does not compile).
- [ ] **Step 3: Implement** `RegularRoutes.cs` and the `AddressSpellings` move (PlacesAnalysis behaviour unchanged; its existing tests stay green).
- [ ] **Step 4: Run** `dotnet test tests/DriverBalance.Core.Tests` → all pass; `dotnet build` 0 warnings.
- [ ] **Step 5: Commit** `feat(core): regular routes (auto-detected, both directions, stops)`.

### Task 2: API — list filters, summary, routes endpoint

**Files:**
- Modify: `src/DriverBalance.Web/Endpoints/TripEndpoints.cs`, `src/DriverBalance.Web/Dtos/AnalyticsDtos.cs`
- Test: `tests/DriverBalance.Web.Tests/ApiTests/TripFilterApiTests.cs`

**Interfaces:**
- Consumes: `RegularRoutes.Items`, `RegularRoutes.KeyOf`, `RegularRoutes.Build`, `RegularRoute`, `TripGroupView.From`, `AnalysisSession.Range`.
- Produces:
  - `GET /vehicles/{vid}/trips?from&to&page&pageSize&level=Group|Trip&minKm&route` → `TripPage(int Total, int Page, int PageSize, IReadOnlyList<TripGroupView> Groups, TripSummary Summary)` with `public sealed record TripSummary(int Count, double Km, decimal Cost, string Currency);` (Count = Total, Km = sum of item km, Cost = sum of item `cost.total`, over all matching items, not just the page). Items newest first. `from` inclusive, `to` exclusive as today; build the period with `s.Range(from, to)`. Item matches when `DistanceKm >= minKm` and (route is null or `KeyOf(item) == route`).
  - Validation → 400 via `ApiValidationException`: `level` not Group/Trip (case-insensitive) → "level must be Group or Trip."; `minKm` < 0 or not finite → "minKm must be 0 or more.".
  - `GET /vehicles/{vid}/trips/routes?from&to&level` → `RegularRoute[]` (camelCase JSON; register the route before or alongside `/{tripId:long}`; the long constraint already keeps them apart).

- [ ] **Step 1: Write the failing tests** (fixture pattern from `TripCostApiTests`: `Api.NewVehicleAsync`, `Api.ImportFixtureAsync`; add an Insurance expense so costs are non-zero):
  - `List_Defaults_Unchanged` (Review Focus 1): without new params the groups (ids of first trips, in order) equal those of a pre-change call shape — compare with `pageSize=500` against the Core `AnalysisContext` groups newest first; `summary.count` = `total`.
  - `List_MinKm_GroupLevel`: `minKm=50` → every group `distanceKm >= 50`, `summary.count` < unfiltered total, `summary.km` = sum over all pages (request pageSize 5 and compare to a pageSize 500 sum).
  - `List_TripLevel_FlatOneTripItems`: `level=Trip` → every item has exactly one trip; total = number of trips in the fixture.
  - `List_RouteFilter`: take the first key from `/trips/routes?level=Trip`, list with `level=Trip&route=<key>` → `total` equals that route's `count`.
  - `List_UnknownRoute_Empty` (Review Focus 5): `route=0000000000000000` → total 0, summary count 0, 200.
  - `List_BadInput_400` (Review Focus 4): `minKm=-1`, `minKm=abc` (model binding failure must also be a 400), `level=Journey`.
  - `Routes_Shape`: `/trips/routes?level=Trip` returns ≥ 1 route with `key` (16 hex), `from`, `to`, `count >= 5`, `typical`, `avgCost > 0`, `share` in (0, 1], `currency` "CZK". If the fixture has no route with ≥ 5 trips, add 5 extra made-up trips via a small CSV import in the test.
  - `Routes_EmptyPeriod`: `from=2030-01-01&to=2031-01-01` → `[]` (Review Focus 3).
  - `Routes_OtherUsersVehicle_404`.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Web.Tests --filter TripFilterApiTests` → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test tests/DriverBalance.Web.Tests` → all pass (TripCostApiTests, McpTests, isolation included); `dotnet build` 0 warnings.
- [ ] **Step 5: Commit** `feat(api): trip list level, minKm and route filters with a summary; routes endpoint`.

### Task 3: UI — slider, summary, Trip level, Routes tab, route chip

**Files:**
- Modify: `web/src/api/types.ts` (`TripSummary`, `summary` on `TripPage`, `RegularRoute`), `web/src/lib/urlState.ts` (+ its test), `web/src/components/TripList.tsx` (+ `TripList.test.tsx`), `web/src/pages/Trips.tsx`, `web/src/components/RecentTrips.test.tsx` (its fixture gains `summary`)
- Create: `web/src/components/RoutesTable.tsx` + `RoutesTable.test.tsx`, `web/src/lib/kmSteps.ts` + test
- Modify: `NOTES.md` (short section "Trip filters and routes (2026-11-14, branch feat/trip-filters)"), `README.md` (Trips line mentions the km filter and Routes)

**Interfaces:**
- Consumes: Task 2 JSON.
- Produces:
  - `TripsTab` adds `'routes'` between `'style'` and `'list'`; `TripsUrlState` adds `minKm: number` (one of the steps, else 0; param `minKm`, omitted when 0) and `route: string | null` (param `route`, must match `/^[0-9a-f]{16}$/`, else null).
  - `kmSteps.ts`: `export const KM_STEPS = [0, 5, 10, 20, 50, 100, 200, 500]`.
  - `TripList` props become `{ from?, to?, title?, level?: 'Group' | 'Trip', minKm?: number, onMinKm?(km: number): void, route?: string | null, routeLabel?: string, onClearRoute?(): void }`; it passes `level`, `minKm` (when > 0) and `route` to the API and resets to page 1 when any of them changes. RecentTrips keeps calling the endpoint without them.
  - `RoutesTable` props `{ from: string; to: string; level: 'Group' | 'Trip'; onRoute(key: string): void }`.

UI copy (exact):
- Slider: `<input type="range" min=0 max=7 step=1 aria-label="Minimum distance">` indexing `KM_STEPS`, label text `Longer than 20 km` or `All distances` at 0.
- Summary line above the table: `38 drives · 6,240 km · ≈ 31,900 CZK` (Group) / `38 trips · …` (Trip); singular `1 drive` / `1 trip`; km via `fmtNum(km, 0)`; cost via `fmtApprox`.
- Route chip (when `route` is set): a button `Route: Home ↔ Work ✕` (label from the routes table; `Route: selected route ✕` when unknown) that calls `onClearRoute`.
- Trip level rows: no expand control; columns as the group row (Start, Trips shows `1`, Km, Duration, L/100, Cost, Score); the start cell is the link that opens the trip drawer.
- Routes tab table columns: `Route`, `Drives` (Trip level: `Trips`), `Typical`, `Avg km`, `≈ per drive` (Trip: `≈ per trip`), `≈ per month`, `Share`; Route cell text `Home ↔ Work` plus muted ` · via School` (round trip: `Home ↺` + via); Share as `fmtPct`. Rows are buttons/links (`role="button"`, Enter and click) calling `onRoute(key)`; the page then switches to the List tab with that route.
- Routes empty line: `No regular routes yet: a route shows once it has been driven 5 times in this period.`
- Hint for the Routes card: `Places you drive between at least 5 times in this period, both directions together. Click a route to list its drives.`

- [ ] **Step 1: Write the failing tests**:
  - `kmSteps`/urlState: `minKm=20&route=0123456789abcdef&tab=routes` round-trips; `minKm=7` → 0; `route=bad` → null.
  - `TripList`: slider at index 3 shows `Longer than 20 km` and calls `onMinKm(20)`; the API URL contains `minKm=20` and `level=Trip` when given; summary line `38 drives · 6,240 km · ≈ 31,900 CZK` from a mocked `summary`; Trip level renders no expand buttons; route chip text and clicking it calls `onClearRoute`.
  - `RoutesTable`: renders a route row with `Home ↔ Work`, ` · via School`, typical text, `≈ 62 CZK`; clicking it calls `onRoute(key)`; empty array → the empty line.
- [ ] **Step 2: Run** `cd web && npx vitest run src/lib src/components` → FAIL.
- [ ] **Step 3: Implement** (Trips page: Routes tab renders `RoutesTable` with the period range and level; `onRoute` → `update({ tab: 'list', route: key })`; List passes level/minKm/route from the URL state and `routeLabel` looked up from the same routes query, via TanStack Query cache key `['analytics', vehicleId, 'routes', from, to, level]`).
- [ ] **Step 4: Run** `cd web && npx vitest run && npm run build && npm run lint` → green.
- [ ] **Step 5: Update NOTES.md and README.md, commit** `feat(web): km filter, summary line, trip level list and regular routes tab`.
