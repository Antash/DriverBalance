# DriverBalance — working notes (read first)

Last updated: 2026-11-14. Branch `feat/hosted-auth` (demo car added), **not merged**.
Tests: Core 192, Web 181, VolvoSync 35, web (vitest) 245, all green; `dotnet build` 0 warnings.

## Trip cost (2026-11-14, branch `feat/trip-cost`)
- Model: per trip, fuel (litres x local average price) + a time share + a wear share of the other running costs, taken over the 12 months around the trip (time by driving time; wear half by km, half by driving time). The pool windows are clipped to [first trip day, max(last trip day, Today)], so prepaid cover reaching past the last trip is not charged to recent trips; the fuel window is not clipped. Trips dated more than 100 years back or more than a year ahead are ignored (zero shares); a trip with 0 L gets fuel 0 even without a price.
- Shows as an approximate `Cost` column in the Trips list (group and trip rows, breakdown in the cell tooltip) and a Cost section in the trip drawer.
- Files: `src/DriverBalance.Core/Analysis/TripCosts.cs`, `Dtos/AnalyticsDtos.cs` (TripView / TripGroupView carry `cost`), `web/src/components/TripList.tsx`, `web/src/components/TripDrawer.tsx`, `fmtApprox` in `web/src/lib/format.ts`.
- Deferred: cost charts, an Insights cost line, MCP exposure, configurable split and windows.

## Start page (2026-11-10, branch `feat/landing-page`)
Google Safe Browsing flagged the azurewebsites.net host as phishing (an empty SPA shell whose only content was "Sign in
with Google"). Now: a real start page for signed-out visitors (`SignIn.tsx`, words in `web/src/lib/landing.ts`), a
static copy inside `#root` of `web/index.html` between `<!--landing-->` markers (for no-JS scanners; the server strips it
for signed-in requests, `SpaIndex` in Program.cs; a vitest keeps both copies in step), meta description / og tags, own
favicon (was Vite's logo), "not affiliated with Volvo Cars" footer. The owner still has to request a Google review.

## Demo car (2026-11-10)

A user with no car can create a demo BMW 320d Touring full of made-up data to try the app.
- `POST /api/vehicles/demo` creates it (400 "You already have a car." if the user has one; about 3 s). `DELETE /api/vehicles/{id}` removes a demo car.
- `Vehicles.IsDemo` column, migration `DemoVehicle`. The UI shows a "Demo car with made-up data." banner with "Add your real car" / "Delete demo car".
- Creating a real car removes the demo automatically.
- The data generator lives in `src/DriverBalance.Core/Demo`.
- Smoke-tested on a published build in Local mode (Local mode seeds "My Volvo" on an empty DB, so delete that row first): all six pages show the banner, no console errors.

## Status: hosted mode built on `feat/hosted-auth` (not merged)
- Built: hosted mode (Google sign-in, invites, admin, account export/delete, privacy; commits 95e6b1f, 11700b4; see
  `docs/hosting.md`), the period-switch fix, and the demo car. Design: `docs/superpowers/specs/2026-11-09-public-service-design.md`.
- The owner tested hosted sign-in locally on https://localhost:5181 with a scratch launcher.
- Still to do for deployment: Blob storage for documents + daily DB backup, per-user analysis parameters, GitHub Actions deploy
  (Azure App Service F1 Linux, SQLite at `/home/data`). The owner does Google console / Azure portal steps; never handle the secrets.

## Purchase costs & financing (branch `feat/purchase-costs`, not merged)
- Settings has a **Purchase costs** list (add / edit / delete, saved at once) and a **Financing** block (saved with the vehicle PUT).
  Endpoints `GET/POST /vehicles/{vid}/purchase-costs`, `PUT/DELETE .../{id}` body `{date, amount, note}` (amount > 0, note <= 200);
  stored as Purchase expenses in the home currency. General expense endpoints still refuse new Purchase ones; Expenses page hides them.
- Financing = nullable vehicle columns `FinancingStart/Amount/Frequency/Payments/Balloon` (migration `VehicleFinancing`); all of start,
  amount, payments or none; frequency Monthly (default) / Quarterly / Yearly. Whole contract (payments x amount + balloon) counts.
  Ownership: `purchaseTotal` = `lumpSumTotal` + `financing.total`; `financing` null when none; `purchaseCosts[].id` = expense id.
  No interest, no generated expenses, never in running-cost stats. Overview card shows a Financing line with a schedule text.

## Where things are
- Spec (binding): `docs/superpowers/specs/2026-11-08-driverbalance-web-design.md` (see "Rulings")
- Plan (15 tasks, all done): `docs/superpowers/plans/2026-11-08-driverbalance-web.md`
- MCP connector: `docs/mcp.md`. Autostart: `scripts/register-autostart.ps1`. Run/publish: `README.md`.

## 2026-11-09 session (owner review of v1)
- UI fixes: weekday labels Mon..Sun, KPI overlap, calendar legend overlap.
- **Oil intervals** page (was Intervals): engine oil only; "Compare two" or "All side by side" (best green / worst red).
- **Maintenance** page: all service visits incl. oil; timeline (lane per work, custom works by name), services table with
  part details and days/km since last time, add / edit (click row) / delete, tyre summary with set editor.
  Spec: `docs/superpowers/specs/2026-11-09-maintenance-records-design.md`; plan: `docs/superpowers/plans/2026-11-09-maintenance-records.md`.
- Work items per visit (21-item grouped checklist + custom works), each with part None/Original/Other brand (+brand, model) and details;
  stored in `Expenses.WorkJson`, flags still drive analytics. Tyre swap: pick a stored set, add an owned set, or "New tyres"
  (creates the set, retires the previously fitted one). Tyres tab removed.
- Real DB migrated on start (WorkJson, TyreSets Brand/Model/Size); pre-migration backup: `%LOCALAPPDATA%\DriverBalance\driverbalance.pre-work-items.db`.
- Odometer chart (Overview, Oil intervals, Maintenance "Service milestones") shows maintenance visits as stacked icons:
  engine oil, tyres, brakes, filters, other service (`web/src/lib/milestones.ts`); the Maintenance lane chart is gone.
- Trips "Places" tab removed (Top places + routes charts; API endpoints `charts/places`, `charts/routes` left in place).
- Expenses have a **place** ("Where", suggestions from earlier places) and **documents** (PDF/images, ≤ 20 MB, stored in
  `%LOCALAPPDATA%\DriverBalance\documents\<vehicle>\<expense>\`; back this folder up with the DB). Spec:
  `docs/superpowers/specs/2026-11-09-documents-places-history-design.md`.
- **Car history start** (Settings): date + odometer (0 for new) becomes the earliest odometer reading; intervals and
  calendar periods count from it, km in the gap before the first real reading is interpolated linearly in time.
  Pre-documents DB backup: `driverbalance.pre-documents.db`.
- Odometer chart only on Maintenance (removed from Oil intervals; Overview still has "Last 12 months").
- **Ownership:** vehicle has Bought on (defaults to the first recorded trip; earlier records are tagged "previous owner"),
  estimated resale value + date. Expense category **Purchase** (car, warranty, registration transfer)
  is excluded from all period money stats and used only by the Overview "Cost of ownership" card (`GET /ownership`).
- 2025 running costs imported from the owner's sheet as one record per category, spread from the purchase date
  to the year end (totals match the sheet). Backup before: `driverbalance.pre-ownership.db`.
- **Volvo sync tool** (`tools/DriverBalance.VolvoSync`, spec `docs/superpowers/specs/2026-11-09-volvo-sync-design.md`):
  `dotnet run --project tools/DriverBalance.VolvoSync -- sync` opens a visible Edge window (headless is blocked by Volvo's
  Akamai bot protection; we do not disguise it), exports the journal from the app's last trip − 2 days and posts it to
  `/api/vehicles/1/import/csv`. Session lives in `%LOCALAPPDATA%\DriverBalance\volvo-browser`. Volvo asks for an e-mailed
  6-digit code at new sessions: typed on the console, or `--code-file <path>` (Claude fills it from Gmail). Scheduling and
  reading the code from Gmail automatically are not built yet. Odometer chart shows no import/expense markers now.
- Owner's two old tyre swaps have no tyre set yet — they can edit them to attach sets.

## 2026-11-09 evening session
- Expenses page: period switch (Total / Last 12 months / each year), treemap of category shares (amount on hover),
  total spent, smoothed weekly cost per km, records at the bottom. Same period switch on Trips (+ group/trip switch);
  Oil intervals views: All / Current vs average / Current vs last.
- 2026 sheet imported (1 Jan – 7 Nov, one record per category). Car details and the resale estimate
  are entered in Settings.
- Categories: Purchase is hidden (API refuses new ones; only Cost of ownership uses it); Financing removed; Road tax
  merged into Road tolls (TollsVignette). The old JSON-data migration feature was removed entirely.
- **Dates are dd/mm/yyyy everywhere** (owner: "don't use American date style"); `DateInput` replaces native date
  pickers; `fmtDate`/`fmtDateTime` in `web/src/lib/format.ts`; server interval labels dd/MM/yyyy. MCP text keeps ISO.
  Month labels like "2026-11" are still yyyy-MM (owner not asked yet).
- **Fuel type** (Settings): Diesel, Diesel MHEV, Petrol, Petrol MHEV, Petrol PHEV, Electric (no plain hybrid — owner).
  DPF health/index/charts/parameters only for the diesels (or unset). Car details also have Transmission and AWD;
  set the owner's car in Settings.
- Statistics tables: Days used, Km / day used, Driving hours columns; periods newest first.
- Overview order: car card, Reminders (only if any; moved up 11-14), This month/This year, Recent trips (5 newest drives with route and ≈ cost, hidden when none; added 11-14), odometer chart, DPF health (no sparkline),
  Cost of ownership last. Recent expenses removed.
- DPF health explained to the owner: a long drive = Excellent (≥40 min, ≥50 km/h avg) or Good (≥30 min, ≥35 km/h),
  single trip or back-to-back trips; "needs a long drive" after 14 days; it is an estimate from trips only.
- DB backups this session: `pre-categories.db`, `pre-fuel-type.db` (all next to the real DB).

## 2026-11-09 late: Trips page polish (commit 261e389)
- Insights card on Trips Overview (distance, days active, driving time, **longest journey** = trips chained while stops
  are <= 4 h, across midnight and cold starts; `Journeys.Longest`, `UsageStats.LongestJourney`) and on Fuel (fuel burned,
  average, per active day).
- Monthly score and DPF index are separate cards (DPF hidden for non-diesels).
- Fuel trend y axis is a "focus scale" (`web/src/lib/fuelScale.ts`): 3rd-90th percentile band linear (5-11 L on real
  data), tails log-compressed and shaded, ticks in real litres; chart 420 px high.
- When you drive: legend on top, "Trip start hour" axis title, same grid/hint layout as Gaps between trips.
- Driving calendar follows the period switch (no year dropdown): one calendar strip, stepped year delimiter + year labels (commit c232812).
- Republished to the live app on :5180 (same evening).

## State
- Published app: `%LOCALAPPDATA%\DriverBalance\app\DriverBalance.Web.exe` (serves on http://localhost:5180).
- Real DB: `%LOCALAPPDATA%\DriverBalance\driverbalance.db`: the owner's trips through 2026-11-08, services, tyres, 2025 + 2026 costs.
- Scratch testing: run the published exe with `--urls http://localhost:5182 --DriverBalance:DatabasePath=<copy> --DriverBalance:DocumentsPath=<dir>`;
  POST/PUT/DELETE need header `Origin: http://localhost:5180` (CrossSiteGuard). PUT /vehicles replaces fields: GET, change, PUT back.
- Parity: the old console and the new engine agree exactly on the real data (cold starts, km, L/100 and DPF %).
- `DriverBalance/bin/Debug/net10.0/*.json` is the old console's data and the only original copy. It is git-ignored and intentionally kept. Back it up before deleting.
- Autostart is NOT registered. The owner decides.
- A stray dev instance (`src/DriverBalance.Web/bin/Release/.../DriverBalance.Web.exe --urls :5180`, scratch DB) may still hold port 5180. Stop it before starting the real app.

## Not built yet (by design: after the owner tries v1)
- **Volvo Connected Vehicle pull** (spec phase 5). Needs the owner's developer account and API key.
- **Family-assistant bot wiring:** separate repo. Fix that bot's admin-fence gap first.
- **Before wiring the bot:** in MCP `get_stats`, add a worked example for the exclusive `to` and print the inclusive last day. Also validate `rate`.
- **Trip cost follow-ups:** cost charts, an Insights cost line, MCP exposure, configurable split and windows.

## Deferred polish (from reviews, none blocking)
- **Charts:**
  - The driving-calendar year defaults to the current year, even when it has no data.
  - Interval dropdown labels show the end date twice (exclusive and inclusive).
  - The penalty curve doesn't draw the short-trip limit.
- **UX:**
  - Modals and drawers lack Escape-to-close and focus handling.
  - Changing the vehicle's home currency doesn't refresh money caches until a reload.
  - Changing an expense to a different tyre set can leave the old set orphaned (delete handles it; edit doesn't).
  - Services rows open on Enter only (no Space / role=button); no component tests for the work editor or page wiring.
- **API hardening before any public use:**
  - Malformed multipart returns 500.
  - Enum values aren't checked with IsDefined.
  - Expense Date isn't required.
  - LocalUser should be gated behind a config switch.
  - Add a file log sink.
- **Data:**
  - The legacy-migration dedup key includes Amount, so re-migrating after editing a 0-cost record duplicates it.
  - `ImportService` has no transaction.
