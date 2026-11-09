# DriverBalance — working notes (read first)

Last updated: 2026-11-09. Branch `feat/web-app` (over `master` c1c16dc), **not merged**.
Tests: Core 108, Web 118, web (vitest) 154, all green; `dotnet build` 0 warnings.

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
  `/api/vehicles/1/import/csv`. Session lives in `%LOCALAPPDATA%\DriverBalanceolvo-browser`. Volvo asks for an e-mailed
  6-digit code at new sessions: typed on the console, or `--code-file <path>` (Claude fills it from Gmail). Scheduling and
  reading the code from Gmail automatically are not built yet. Odometer chart shows no import/expense markers now.
- Owner's two old tyre swaps have no tyre set yet — they can edit them to attach sets.

## State
- Published app: `%LOCALAPPDATA%\DriverBalance\app\DriverBalance.Web.exe` (serves on http://localhost:5180).
- Real DB: `%LOCALAPPDATA%\DriverBalance\driverbalance.db`. It holds the owner's migrated data: 1 500 trips and 4 services.
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
