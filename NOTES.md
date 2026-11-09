# DriverBalance — working notes (read first)

Last updated: 2026-11-09 (evening). Branch `feat/web-app` (over `master` c1c16dc), **not merged**, HEAD a296644.
Tests: Core 112, Web 129, VolvoSync 28, web (vitest) 200, all green; `dotnet build` 0 warnings.

## PAUSED HERE: public web service (design written, awaiting owner approval)
- Design: `docs/superpowers/specs/2026-11-09-public-service-design.md` (committed). Invite-only Google sign-in, per-user
  profiles and analysis parameters, Admin page (invites, users), export my data / delete my account, privacy page,
  Azure App Service **F1 Linux**, SQLite at `/home/data` (no WAL), documents + daily DB backup in Blob Storage,
  GitHub Actions deploy, `DriverBalance:Mode = Local | Hosted`. Its "Calls made by Claude" section lists 7 decisions for
  the owner to confirm (access re-checked every 5 min; admin can't self-delete; 200 MB docs per user; hosted becomes the
  source of truth; API tokens re-issued; two-user isolation tests).
- Owner said: invite-only for now; Google only for now; shared cars not needed; other users import trips by hand (only
  the owner has the sync tool / bot); parameters belong to the user, defaults = current values (= code defaults).
- Next step once approved: superpowers:writing-plans → plan in `docs/superpowers/plans/`, then subagent-driven build.
  The owner does Google console / Azure portal steps; never handle the secrets.

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
- **Ownership:** vehicle has Bought on (2025-04-14 = first recorded trip; earlier records are tagged "previous owner"),
  estimated resale value + date. Expense category **Purchase** (car 1 250 000, warranty 35 000, registration transfer 2 500)
  is excluded from all period money stats and used only by the Overview "Cost of ownership" card (`GET /ownership`).
- 2025 running costs imported from the owner's sheet as one record per category, spread 2025-04-14 → 2025-12-31
  (2025 total 100 000 = sheet total). Backup before: `driverbalance.pre-ownership.db`.
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
- 2026 sheet imported (1 Jan – 7 Nov, one record per category). Car details (Volvo V90 B4 Momentum MY2024, equipment,
  Crystal White, black leather), resale estimate 1 100 000 CZK on 09/11/2026.
- Categories: Purchase is hidden (API refuses new ones; only Cost of ownership uses it); Financing removed; Road tax
  merged into Road tolls (TollsVignette). The old JSON-data migration feature was removed entirely.
- **Dates are dd/mm/yyyy everywhere** (owner: "don't use American date style"); `DateInput` replaces native date
  pickers; `fmtDate`/`fmtDateTime` in `web/src/lib/format.ts`; server interval labels dd/MM/yyyy. MCP text keeps ISO.
  Month labels like "2026-11" are still yyyy-MM (owner not asked yet).
- **Fuel type** (Settings): Diesel, Diesel MHEV, Petrol, Petrol MHEV, Petrol PHEV, Electric (no plain hybrid — owner).
  DPF health/index/charts/parameters only for the diesels (or unset). Car details also have Transmission and AWD;
  the owner's car is set in Settings.
- Statistics tables: Days used, Km / day used, Driving hours columns; periods newest first.
- Overview order: car card, This month/This year, odometer chart, DPF health (no sparkline), Reminders (only if any),
  Cost of ownership last. Recent expenses removed.
- DPF health explained to the owner: a long drive = Excellent (≥40 min, ≥50 km/h avg) or Good (≥30 min, ≥35 km/h),
  single trip or back-to-back trips; "needs a long drive" after 14 days; it is an estimate from trips only.
- DB backups this session: `pre-categories.db`, `pre-fuel-type.db` (all next to the real DB).

## 2026-11-09 late: Trips page polish (commit 261e389)
- Insights card on Trips Overview (distance, days active, driving time, **longest journey** = trips chained while stops
  are <= 4 h, across midnight and cold starts; `Journeys.Longest`, `UsageStats.LongestJourney`) and on Fuel (fuel burned,
  average, per active day). Owner's longest so far: 900 km on 06/08/2026.
- Monthly score and DPF index are separate cards (DPF hidden for non-diesels).
- Fuel trend y axis is a "focus scale" (`web/src/lib/fuelScale.ts`): 3rd-90th percentile band linear (5-11 L on real
  data), tails log-compressed and shaded, ticks in real litres; chart 420 px high.
- When you drive: legend on top, "Trip start hour" axis title, same grid/hint layout as Gaps between trips.
- Republished to the live app on :5180 (same evening).

## State
- Published app: `%LOCALAPPDATA%\DriverBalance\app\DriverBalance.Web.exe` (serves on http://localhost:5180).
- Real DB: `%LOCALAPPDATA%\DriverBalance\driverbalance.db`: the owner's trips through 2026-11-08, services, tyres, 2025 + 2026 costs.
- Scratch testing: run the published exe with `--urls http://localhost:5182 --DriverBalance:DatabasePath=<copy> --DriverBalance:DocumentsPath=<dir>`;
  POST/PUT/DELETE need header `Origin: http://localhost:5180` (CrossSiteGuard). PUT /vehicles replaces fields: GET, change, PUT back.
- Parity: the old console and the new engine agree exactly on the real data (900 cold starts, 30 000.00 km, 6.90 L/100, DPF 70.00 %).
- `DriverBalance/bin/Debug/net10.0/*.json` is the old console's data and the only original copy. It is git-ignored and intentionally kept. Back it up before deleting.
- Autostart is NOT registered. The owner decides.
- A stray dev instance (`src/DriverBalance.Web/bin/Release/.../DriverBalance.Web.exe --urls :5180`, scratch DB) may still hold port 5180. Stop it before starting the real app.

## Not built yet (by design: after the owner tries v1)
- **Volvo Connected Vehicle pull** (spec phase 5). Needs the owner's developer account and API key.
- **Family-assistant bot wiring:** separate repo. Fix that bot's admin-fence gap first.
- **Before wiring the bot:** in MCP `get_stats`, add a worked example for the exclusive `to` and print the inclusive last day. Also validate `rate`.

## Deferred polish (from reviews, none blocking)
- **Charts:**
  - The score map's y-axis label is clipped.
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
