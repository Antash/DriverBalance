# DriverBalance web app: design

Date: 2026-11-08. Status: draft for owner approval.

## Why

DriverBalance is a single-file .NET console tool (`DriverBalance/Program.cs`). It imports trip-journal CSVs exported from the Volvo Cars app, scores driving for DPF health, keeps maintenance records and saves ScottPlot PNG charts. In the owner's words, "navigating current thing is miserable". The owner wants:

- a web interface with charts drawn in the page, not exported to images;
- richer statistics, mainly "is my last interval different from the previous one": cold starts, city/highway mix, short trips, fuel use, cost;
- tracking of **all** money spent on the car (fuel, parking, accessories, wash, maintenance, fines, insurance and so on);
- tyre mileage between swaps;
- statistics over custom date ranges and over calendar periods (whole year, each month, each week);
- automatic or on-demand data pull from Volvo where possible, with manual CSV export still supported;
- a way to record entries by talking to the owner's Telegram personal assistant (the family-assistant bot) through an MCP server.

## Goals

1. A local web app (C# backend, browser UI) replacing the console menu entirely.
2. Every existing feature carried over with the **same numbers**: CSV import with duplicate skipping, trip grouping into cold starts, DPF scoring, summary, per-maintenance-type reports, the three charts.
3. A statistics engine that works on any period: maintenance interval, custom range, or calendar period. Any two periods can be compared side by side.
4. One expense ledger covering every kind of car cost.
5. Tyre sets with total km per set across seasons.
6. An MCP server so the Telegram assistant can log entries and ask for stats.
7. Volvo Connected Vehicle API polling (odometer, fuel, trip meters, diagnostics) for automatic odometer and distance data.

## Non-goals (for now)

- Public hosting, sign-up or real logins. The app runs locally on the owner's Windows PC.
- Brands other than Volvo for automatic data pull. CSV import and manual entry work for any car anyway.
- Automatic currency exchange rates.
- A mobile app. Phone entry goes through Telegram.

## Anti-goals (failure even if it "works")

- Statistics that silently disagree with the current console tool on the same data.
- An architecture that would need a rewrite to become multi-user. The owner said: "potentially multi-user service available globally... just bear this in mind when building architecture". Every record belongs to a user and a vehicle from day one.
- Losing or corrupting the existing data (1 500 trips, 4 maintenance records).
- Committing personal data. Trip CSVs contain home and work addresses, so real data never goes into git.
- Navigation as painful as the console: deep menus, re-typing paths, prompts.

## Current state (facts the builder needs)

- Repo: `C:\Users\user\source\repos\DriverBalance`, remote `github.com/Antash/DriverBalance`, branch `master`. `.slnx` solution, `net10.0`, single package dependency ScottPlot 5.1.57.
- `DriverBalance/Program.cs` has **uncommitted local changes (+498 lines)**. That working copy, which adds maintenance records and interval reports, is the latest version of the logic and the reference for parity. Commit it as a baseline (owner's work, separate commit) before restructuring.
- Live data sits next to the binary in `DriverBalance/bin/Debug/net10.0/`. It is not in git (bin is ignored):
  - `triprecords.json`: 1 500 `TripRecord`s, 2025-04-14 to 2026-10-01, categories `""`, `Unassigned`, `Private`.
  - `maintenancerecords.json`: 4 records (flags enum `MaintenanceType`): 2025-02-06 OilChange|BrakeFluid at 7 350 km cost 0; 2025-10-22 TyreSwap at 19 200 km 2 500 CZK; 2026-01-13 OilChange|BrakeFluid at 22 000 km 15 000 CZK; 2026-03-18 TyreSwap at 27 400 km 2 800 CZK.
- `appsettings.json` holds `TripAnalysisParameters`. The values in that file differ from the class defaults (`ModerateCitySpeed` 35 vs 30, `MixedSpeed` 50 vs 40, `DpfMovingAverageWeeks` 3 vs 4). The file values are the ones in use.
- CSV format: Volvo app export, `cs-CZ` culture, 11 columns (category, start time, start odometer, start address, end time, end odometer, end address, duration like `1 h, 5 min`, distance km, fuel L, notes). Numbers use NBSP/space thousands separators and comma decimals. See `TripImporter`.
- Volvo deletes trips from the app after **400 days**, so old trips must be exported before they disappear.
- Toolchain on the PC: .NET SDK 10.0.403, Node 26.

### Existing logic to preserve exactly

- **Cold-start grouping** (`ConcatenateTrips`): trips sorted by start. A trip joins the previous group if `next.Start - prev.End < ColdStartThreshold` (45 min). One group = one cold start.
- **Trip score** (`AnalyzeScore`, on duration and average speed):
  - Excellent: ≥ LongDuration and ≥ MixedSpeed.
  - Good: ≥ MediumDuration and ≥ ModerateCitySpeed.
  - Fair: ≥ ShortDuration and ≥ HeavyTrafficSpeed.
  - Bad: everything else.
- **Group score** (`GetBestScoreForGroup`): the best score over every single trip, every run of consecutive trips, and the whole group.
- **Concatenated trip**: durations, distances and fuel are summed. Duration is the sum of driving time, not wall-clock time.
- **DPF index** = km in Good+Excellent groups ÷ all km.
- **Period membership**: a trip belongs to a period by `StartTime.Date`, and grouping is done on that period's trips only.
- **Maintenance periods**: for each type flag, the periods run between consecutive events with that flag, and the last one runs to today. Odometer distance comes from event odometers.
- **Charts**:
  1. Monthly stacked km by group score, plus a weekly DPF index line with an N-week moving average (ISO weeks).
  2. Yearly calendar heatmap of days: colour = best group score that day, intensity = km.
  3. Fuel trend: one point per group, L/100 km, filtered to 1–30, point size ∝ √km, with a km-weighted LOWESS curve (frac 0.25).
- Known defects that should be **fixed, not ported**:
  - In LOWESS, `ElementAt(span)` can go out of range for small n, and it divides by zero when `maxDist = 0`.
  - The heatmap colour-map indexing (`colorMap[i*b]`) is wrong. It goes away with ScottPlot.

## What gets built

### Architecture (owner approved)

- **Backend:** ASP.NET Core (.NET 10). One process serves the JSON API, the MCP endpoint, background jobs and the built frontend.
- **Core library** with no web or database dependencies: domain model, CSV import, analysis (grouping, scoring, period stats, intervals, tyres, LOWESS). Unit-tested.
- **Frontend:** React + TypeScript (Vite), charts with Apache ECharts: hover tooltips, zoom, click to drill down. In production it is built into the backend's static files. Running it takes one command.
- **Storage:** SQLite through EF Core with migrations. Moving to a server database later should be a provider and connection-string change. Default database location is under the user's local app data, and it is configurable.
- **Multi-user ready:**
  - `User` owns `Vehicle`s, and every trip, expense, tyre set, snapshot and token row carries `VehicleId` (and so a user). Every query is scoped by the current user.
  - Locally, an authentication handler signs every request in as the single default user. The app listens on localhost only.
  - Adding real logins later must not touch the domain or queries.
- **Secrets** (Volvo tokens, MCP tokens) are encrypted at rest with ASP.NET Data Protection and never logged.

Suggested layout (builder may adjust): `src/DriverBalance.Core`, `src/DriverBalance.Data`, `src/DriverBalance.Web`, `src/DriverBalance.Volvo`, `web/`, `tests/...`. The console project is removed once parity is proven. It stays in git history.

### Data model (the shape, not the final schema)

- **Vehicle:** name, VIN (optional), fuel type, home currency (default CZK), analysis parameters (from today's `appsettings.json`, editable per vehicle in Settings).
- **Trip:** the current `TripRecord` fields, plus an import-batch reference. Duplicate key = (vehicle, start time, end time), as today.
- **ImportBatch:** file name, time, counts imported/skipped, first and last trip date.
- **Expense:** date, category, amount, currency, home-currency amount, optional odometer, note, optional "covers from/to" for costs like yearly insurance, plus category details:
  - Fuel: litres, price per litre, full tank yes/no, station (optional).
  - Maintenance: `MaintenanceType` flags + custom description. This replaces `MaintenanceRecord`.
  - Tyres: the tyre set mounted, for swaps and new tyres.
- **Expense categories:** Fuel, Parking, Tolls & vignette, Wash, Maintenance, Repair, Tyres, Accessories, Insurance, Road tax, Fines, Financing, Other. Users can add their own later, but the fixed list is fine for v1.
- **TyreSet:** name, season (summer/winter/all-season), brand/model/size, purchase date and cost (an Expense), retired yes/no.
- **TyreMount:** set, mounted at date + odometer, unmounted at date + odometer. Derived from tyre-swap expenses. The user chooses which set went on.
- **VehicleSnapshot:** time, odometer, fuel amount/level, range, trip-meter statistics, raw diagnostics JSON. From Volvo polling.
- **ApiToken:** per user, hashed, with name, created and last-used times. Used by MCP.

Migration: a one-time import reads the two existing JSON files (path chosen in the UI or passed on the command line). Maintenance records become Maintenance expenses. Tyre swaps become TyreSwap expenses with the set left unassigned until the user picks it. The migration is idempotent, so running it twice changes nothing.

### Statistics engine

One function: `PeriodStats(vehicle, from, to)`. It returns:

- **Usage:**
  - km from trips, km from odometer (snapshots, expenses and trip odometers), and trip coverage (share of odometer km that has trip data);
  - days, km/day, driving hours, average speed;
  - cold starts and cold starts per 100 km;
  - short-trip share (groups with total driving duration < ShortDuration), by count and by km.
- **DPF:** km and hours per score and the DPF index.
- **Driving mix by km**, using each group's average speed:
  - city: below ModerateCitySpeed;
  - mixed: below MixedSpeed;
  - highway: the rest.

  *(Builder's call: these thresholds reuse the existing parameters.)*
- **Fuel:**
  - L/100 km from trip data;
  - when Fuel expenses exist, real consumption calculated full tank to full tank.
- **Money:**
  - total, per category, cost per km;
  - an expense with a "covers" range is spread across days, so yearly insurance doesn't spike one month.

**Period sources:**

- **Maintenance interval:** pick a type, and the list of its intervals appears, newest first. The default comparison is current vs previous.
- **Custom range:** from/to date pickers, optionally compared with a second range.
- **Calendar:** year → month → ISO week → trips, as a drill-down table with a chart from the same rows.

**Compare view:** two periods side by side with an absolute or percent change for each metric. Rates are compared, not raw totals, when the periods have different lengths. Colour shows better or worse for the car or wallet.

### Pages

The mockup was agreed during brainstorming (`.superpowers/brainstorm/.../intervals.html`, not committed).

- **Overview:**
  - this month and this year: km, cost, cost per km, DPF index;
  - recent expenses;
  - reminders: "last trip export was N days ago" (warn from 30 days, alarm well before 400), Volvo service warnings from diagnostics, Volvo connection expired.
- **Trips:** filterable list of groups and trips, plus the three existing charts redone in ECharts.
- **Statistics:** calendar drill-down and custom range, with compare.
- **Intervals:** maintenance-interval compare, as in the mockup: usage, engine wear/DPF, money, a chart with the service date marked, and a timeline of other events in the interval.
- **Tyres:** sets with total km, km per season, cost per 1 000 km, and the set currently mounted.
- **Expenses:** list with filters, quick add/edit/delete, category breakdown over time.
- **Import:** drag-and-drop the Volvo CSV, then a result summary (new / duplicates / date range) and import history.
- **Settings:** vehicle, analysis parameters, tyre sets, Volvo connection, MCP tokens, export all data as JSON (backup, and a future right to download/delete one's data).

UI language is English. Units are km, L and the home currency.

### MCP server (Telegram assistant)

- Built with the official C# MCP SDK (`ModelContextProtocol.AspNetCore`), streamable HTTP at `/mcp` on the same app. Requests need a bearer `ApiToken` (created in Settings), which also sets the user. There are no anonymous calls, even locally.
- **Tools** (names indicative):
  - `add_expense` (category, amount, currency, date default today, odometer, note, details);
  - `log_fuel` (litres, total price, full tank, odometer);
  - `log_maintenance` (types, cost, odometer, description);
  - `swap_tyres` (set mounted, odometer);
  - `list_recent_expenses`;
  - `delete_expense` (by id, for "undo that");
  - `get_stats` (period: interval, range or calendar; returns the same numbers as the UI);
  - `import_trips_csv` (CSV text).
- Descriptions are written so an LLM fills fields correctly. For example, the odometer is optional and comes from the latest snapshot when missing. Each result returns the stored entry so the assistant can confirm it.
- **Wiring into the family-assistant bot** (`C:\Users\user\source\repos\family-assistant`, TypeScript, Claude Agent SDK) is a separate change in that repo, done last. The bot currently runs only in-process MCP servers with `strictMcpConfig`, so it needs a new `car` server entry (HTTP MCP or an in-process proxy) added to its policy. The bot is deliberately stopped until its open admin-fence security gap is fixed (see that repo's `NOTES.md`). That fix comes first. *(Builder's call: the `car` tool is enabled only in the owner's private DM scope, not the family group, until the owner says otherwise.)* Sending the CSV to the bot as a file is in scope only if the bot can hand file contents to tools. Otherwise CSV import stays in the web UI.

### Volvo automatic pull

- Volvo has **no API for the trip journal**, so trips keep coming from CSV. The Connected Vehicle API v2 (`api.volvocars.com/connected-vehicle/v2`) gives odometer, fuel amount/range, trip-meter statistics and diagnostics. It needs a Volvo developer account + `vcc-api-key`, and a Volvo ID OAuth2 login (authorization code + PKCE, refresh token). Logins expire after a grant period and must be redone, and there's a daily request limit.
- **The owner does the manual steps:** creating the developer account and application, and putting the key in user secrets or settings. Never paste keys into chat.
- **Polling:**
  - a background job polls about hourly while the app runs, plus a "Sync now" button;
  - it stores snapshots;
  - when the fuel level jumps up between snapshots, it creates a "refuel detected, add price?" prompt on the Overview;
  - an expired login shows a reconnect banner.
- This phase can slip without affecting anything else. Snapshots only add data.
- Unknown: whether a Volvo application can serve other people's accounts without Volvo's approval. That only matters for the future public version.

### Running locally

`dotnet run` on the web project (or a published exe) serves everything at `http://localhost:<port>`. Development uses the Vite dev server proxying to the API. Starting at Windows login is required from phase 4 (see Rulings 11).

## Delivery order

Each phase ends usable:

1. **Core + data:** solution restructure, Core library with existing logic, characterization tests, SQLite, migration of the existing JSON files, CSV import.
2. **Web UI:** Overview, Trips (three charts), Statistics (calendar + custom + compare), Intervals, Import, Settings (parameters). The console app is deleted once parity is verified.
3. **Expenses + tyres:** ledger, categories, fuel full-tank consumption, amortized costs, tyre sets/mounts, money in all stats.
4. **MCP server:** tokens, tools, tests. Then the family-assistant fix + wiring (in that repo).
5. **Volvo pull:** OAuth connect, polling, snapshots, odometer-based km, refuel prompts, reminders.

## Testing and verification

- **Parity:** before the console code is deleted, capture its outputs (summary, per-type and all-period reports, chart series) on a **synthetic fixture CSV** that is committed. Assert the new engine reproduces them. Also run a local-only parity check against the real data in `bin/Debug` that is never committed.
- Unit tests for grouping, scoring, period membership, interval building, tyre km, amortization, full-tank consumption, currency handling, LOWESS edge cases.
- API/integration tests that every query is user-scoped: a second test user must never see the first user's data.
- MCP tool tests: auth required, entries land on the right vehicle.

## Rulings (settled after design review; all are the designer's calls, not the owner's)

1. **One period rule everywhere.** A period is a half-open range of dates `[from, to)`. The UI shows an inclusive "to" date and adds one day internally.
   - **Maintenance intervals** run `[event date, next event date of the same type)`. The current interval runs to today, inclusive.
   - A trip on the service day belongs to the new interval. Services have no time of day.
   - The stretch before the first recorded event of a type is shown, labelled "before first recorded <type>".
   - "Since last <type>" (old menu 10) is the current interval.
2. **Grouping is global, and groups go to the period of their first trip.** Cold-start groups are built once over all trips. A group belongs to the period that contains its first trip's start date. This makes month totals add up to year totals, and stops a cold start being counted twice across a boundary. The old code grouped within each period, so the results differ only for groups that cross a period edge. Parity tests use fixtures with no group crossing a period edge, and the difference is documented in the tests.
3. **Chart quirks are fixed, not ported.** The weekly DPF line assigns each group to the ISO week of its first trip. The old code counted a group in every week it touched. Heatmap days keep the per-day trip km as today. Intensity is capped at 100 km, as today.
4. **Odometer timeline.** All readings (trip start/end, expense odometers, snapshots) form one timeline per vehicle.
   - The odometer at a date is the highest reading on or before the end of that day. Readings are trusted to only go up; a lower reading is ignored and logged.
   - Odometer km for a period = reading at the end − reading at the start.
   - If the start is unknown, odometer km and coverage are shown as "unknown", not 0.
   - The current interval ends at the latest known reading.
5. **Time.** v1 stores trip times as naive local times, as in the CSV. Each vehicle gets a time-zone field (default `Europe/Prague`) that is used for "today" in the UI, in reminders, and in MCP date defaults. Converting times on storage is deferred to the public version.
6. **Foreign currency.** An expense in a foreign currency needs either the home amount or a rate. If neither is given, it is saved with the home amount empty. It is then left out of money totals, and a "needs conversion" banner appears with its count. The MCP tool returns that state so the assistant can ask a follow-up.
7. **Fuel money vs fuel litres.**
   - Fuel cost, and so cost per km, comes only from Fuel expenses.
   - L/100 km from the CSV litres stays as the trip-based figure.
   - Full-tank-to-full-tank consumption is shown when the fill-ups have odometers. A missing odometer is filled from the timeline and marked "inferred".
8. **Tyres.**
   - The `TyreSwap` / `NewTyres` maintenance flags stay the interval markers, as today.
   - The mount (which set went on) is an optional detail on that same expense.
   - Buying a set is a separate Tyres-category expense linked to the set.
   - Tyre km per set comes from the mounts and the odometer timeline.
   - Migrated swaps start with the set unassigned. Tyres shows "assign sets" until the user fills them in.
9. **Migration specifics.** Empty currency becomes CZK. All known costs are CZK. A recorded cost of 0 is imported as 0 with the note "imported — check cost", and the user can edit it. Idempotency keys: trips (vehicle, start, end), as today; maintenance (date, type, odometer, cost, description), as the old `Equals` does.
10. **Parameters.** The first vehicle's parameters are seeded from `appsettings.json`: 45/20/30/40/25/35/50, moving average 3 weeks. `DpfMovingAverageDays` is dropped because nothing uses it. Stats are always recomputed from raw trips; no scores are cached. 1–2k trips is trivially fast.
11. **MCP deployment and identity.**
    - The bot and the app run on the same PC, and the bot calls `http://localhost:<fixed port>/mcp`. When the PC is off both are off, so there is no queue.
    - Starting at Windows login stops being a nice-to-have and becomes **required in phase 4**, so the tool is there whenever the bot runs.
    - One token belongs to one user. Every tool takes an optional vehicle and otherwise uses the user's default vehicle (a setting).
12. **Build.** Development uses `npm run dev` (Vite) plus `dotnet watch`. `dotnet publish` builds the SPA through an MSBuild target and embeds it, giving one exe. A documented `dotnet run` after `npm run build` is fine for local use.

## Left to the builder

Project and folder names, endpoint shapes, exact schema, component library and styling, chart details beyond what's above, dev tooling, autostart, date formatting, how the frontend build is embedded. Also any calls marked *builder's call* above.
