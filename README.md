# DriverBalance

A local web app for analysing how you drive and what your car costs. It imports trip CSV exports, groups trips into cold-start sessions, scores each session (Excellent / Good / Fair / Bad) and tracks the DPF-friendly share of your driving, fuel consumption, maintenance and tyre costs. It runs on your own machine at `http://localhost:5180`; nothing leaves it.

## Pages

- **Overview**: this month and this year at a glance, DPF health, recent expenses, reminders (for example "your last trip is getting old, export new trips from Volvo" and "long drive due").
- **Trips**: imported trips grouped into cold-start sessions, with scores, and the charts (monthly scores and DPF, calendar heatmap, fuel trend, score map, gap histogram, when you drive, penalty curve, seasonality, places and routes, vehicle timeline). Time-axis charts zoom with the mouse wheel or the slider.
- **Statistics**: usage, score mix, fuel, driving mix and cost over any period, drilling down from years to months to weeks, and comparing two ranges.
- **Intervals**: per-maintenance-type periods (oil change, brake fluid, tyre swap, ...) compared side by side.
- **Tyres**: tyre sets, mounts and cost per km.
- **Expenses**: maintenance and other costs in the vehicle's home currency.
- **Import**: Volvo trip CSV import with duplicate skipping and import history (migrated legacy trips appear there too), plus one-off migration from the old console app's JSON files.
- **Settings**: vehicle, analysis parameters, API tokens, data export.

## Development

Requires the .NET 10 SDK and Node.

```powershell
dotnet run --project src/DriverBalance.Web      # API on http://localhost:5180
cd web && npm install && npm run dev            # UI on http://localhost:5173 (proxies /api)
```

Tests and builds:

```powershell
dotnet test
cd web && npm test && npm run build
```

## Publish and autostart

```powershell
dotnet publish src/DriverBalance.Web -c Release -o "$env:LOCALAPPDATA\DriverBalance\app"
& "$env:LOCALAPPDATA\DriverBalance\app\DriverBalance.Web.exe"
```

The published app serves the built UI itself (the UI is built and added to the publish output only; `dotnet run` from source uses the Vite dev server for the UI instead). **Stop the running app before re-publishing**: while it runs, its exe and DLLs are locked and the publish fails.

To start it at every logon (per-user Scheduled Task, no admin needed; the task starts the exe through `conhost.exe --headless`, so no console window appears):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\register-autostart.ps1
powershell -ExecutionPolicy Bypass -File scripts\register-autostart.ps1 -Unregister
```

Because there is no console, startup failures and unhandled server errors (HTTP 500) go to the Windows Event Log (Application log, source `.NET Runtime`, warning level and above). ASP.NET's default host registers the Event Log provider on Windows, so nothing needs configuring. If the app does not come up after logon, look in Event Viewer, Windows Logs, Application.

## Syncing trips from Volvo

`tools/DriverBalance.VolvoSync` exports the journal from volvocars.com (headless Edge, no browser download; Edge must be installed) and imports the CSV into the running app. The Volvo session is kept in a persistent browser profile in `%LOCALAPPDATA%\DriverBalanceolvo-browser`; exports and failure screenshots go to `%LOCALAPPDATA%\DriverBalanceolvo-exports`.

```powershell
# one time: sign in by hand (tick "Pamatovat si me"), or store credentials for unattended sign-in
dotnet run --project tools/DriverBalance.VolvoSync -- login
dotnet run --project tools/DriverBalance.VolvoSync -- set-credentials      # DPAPI-encrypted, current Windows user

# every time: app must be running
dotnet run --project tools/DriverBalance.VolvoSync -- sync
dotnet run --project tools/DriverBalance.VolvoSync -- sync --from 2026-10-01 --headed
dotnet run --project tools/DriverBalance.VolvoSync -- clear-credentials
```

`sync` exports from 2 days before the app's last trip (or `--from`; 90 days back if there are no trips) to today. Options: `--app http://localhost:5180`, `--vehicle 1`, `--car-id <id>` (only used when the journal link is not found on the account page), `--headed`, `--keep-open`. If Volvo asks for an e-mailed code, `sync` prompts for it on the console. Duplicates are skipped by the importer, so overlapping ranges are safe.

## Data

- Database: SQLite at `%LOCALAPPDATA%\DriverBalance\driverbalance.db`. Override with the `DriverBalance:DatabasePath` setting (environment variable `DriverBalance__DatabasePath`).
- Backup: copy the `.db` file while the app is stopped, or use the JSON data export in Settings (`GET /api/export`). This backup is separate from the Volvo trip export: Volvo deletes trips from its own app after 400 days, so the Overview reminds you to export new trips from Volvo and import the CSV here when your newest trip gets old (the threshold is a parameter).

## Live parameters

Thresholds (cold-start gap, short/medium/long drive limits, heavy-traffic, city and mixed speeds, DPF moving average, fuel-trend limits, and so on) are editable per vehicle in Settings, and every analytics call can pass its own parameters, so you can see the effect immediately without re-importing. Defaults match the old console app.

## MCP

The app exposes an MCP server at `http://localhost:5180/mcp`, authenticated with an API token. Setup is in [docs/mcp.md](docs/mcp.md).

## Migrating from the old console app

The console app has been removed from the repository (history keeps it). To bring its data over, open **Import** and upload `triprecords.json` and `maintenancerecords.json` in the legacy migration section (API: `POST /api/vehicles/{id}/import/legacy`). The originals are only read, never changed.

Note: the local, git-ignored folder `DriverBalance/bin/` (old console app build output) may still hold those JSON files, because the old app wrote them next to its executable. Keep a backup copy of both JSON files somewhere safe before deleting that folder; once the data is migrated and backed up, you may archive or delete it.
