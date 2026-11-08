# DriverBalance

A local web app for analysing how you drive and what your car costs. It imports trip CSV exports, groups trips into cold-start sessions, scores each session (Excellent / Good / Fair / Bad) and tracks the DPF-friendly share of your driving, fuel consumption, maintenance and tyre costs. It runs on your own machine at `http://localhost:5180`; nothing leaves it.

## Pages

- **Overview**: this month and this year at a glance, DPF health, recent expenses, reminders (for example "export your data" and "long drive due").
- **Trips**: imported trips grouped into cold-start sessions, with scores.
- **Statistics**: usage, score mix, fuel, driving mix and cost over any period, with charts (monthly scores, calendar heatmap, fuel trend, score map, gap histogram, when you drive, penalty curve, seasonality, places and routes, timeline).
- **Intervals**: per-maintenance-type periods (oil change, brake fluid, tyre swap, ...) compared side by side.
- **Tyres**: tyre sets, mounts and cost per km.
- **Expenses**: maintenance and other costs in the vehicle's home currency.
- **Import**: CSV import with duplicate skipping and import history, plus one-off migration from the old console app's JSON files.
- **Settings**: vehicle, analysis parameters, API tokens, data export.

## Development

Requires the .NET 10 SDK and Node.

```
dotnet run --project src/DriverBalance.Web      # API on http://localhost:5180
cd web && npm install && npm run dev            # UI on http://localhost:5173 (proxies /api)
```

Tests and builds:

```
dotnet test
cd web && npm test && npm run build
```

## Publish and autostart

```
dotnet publish src/DriverBalance.Web -c Release -o "$env:LOCALAPPDATA\DriverBalance\app"
& "$env:LOCALAPPDATA\DriverBalance\app\DriverBalance.Web.exe"
```

The published app serves the built UI itself. To start it at every logon (per-user Scheduled Task, no admin needed):

```
powershell -File scripts/register-autostart.ps1
powershell -File scripts/register-autostart.ps1 -Unregister
```

## Data

- Database: SQLite at `%LOCALAPPDATA%\DriverBalance\driverbalance.db`. Override with the `DriverBalance:DatabasePath` setting (environment variable `DriverBalance__DatabasePath`).
- Backup: copy the `.db` file while the app is stopped, or use the data export in Settings (`GET /api/export`). The Overview reminds you when your last export is old.

## Live parameters

Thresholds (cold-start gap, short/medium/long drive limits, heavy-traffic, city and mixed speeds, DPF moving average, fuel-trend limits, and so on) are editable per vehicle in Settings, and every analytics call can pass its own parameters, so you can see the effect immediately without re-importing. Defaults match the old console app.

## MCP

The app exposes an MCP server at `http://localhost:5180/mcp`, authenticated with an API token. Setup is in [docs/mcp.md](docs/mcp.md).

## Migrating from the old console app

The console app has been removed from the repository (history keeps it). To bring its data over, open **Import** and upload `triprecords.json` and `maintenancerecords.json` in the legacy migration section (API: `POST /api/vehicles/{id}/import/legacy`). The originals are only read, never changed.

Note: `DriverBalance/bin/Debug/net10.0/` on the old machine layout still holds those JSON files. Once the data is migrated and you have a backup, you may archive or delete that folder.
