# DriverBalance — working notes (read first)

Last updated: 2026-11-08. Branch `feat/web-app` (over `master` c1c16dc), **not merged**.
Tests: Core 97, Web 76, web (vitest) 107, all green; `dotnet build` 0 warnings.

## Where things are
- Spec (binding): `docs/superpowers/specs/2026-11-08-driverbalance-web-design.md` (see "Rulings")
- Plan (15 tasks, all done): `docs/superpowers/plans/2026-11-08-driverbalance-web.md`
- MCP connector: `docs/mcp.md`. Autostart: `scripts/register-autostart.ps1`. Run/publish: `README.md`.

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
  - The Tyres page doesn't show retire errors.
- **API hardening before any public use:**
  - Malformed multipart returns 500.
  - Enum values aren't checked with IsDefined.
  - Expense Date isn't required.
  - LocalUser should be gated behind a config switch.
  - Add a file log sink.
- **Data:**
  - The legacy-migration dedup key includes Amount, so re-migrating after editing a 0-cost record duplicates it.
  - `ImportService` has no transaction.
