# DriverBalance MCP server

DriverBalance exposes your car data to MCP clients (Claude Code, the family-assistant bot, any Streamable HTTP MCP client).

- Endpoint: `http://localhost:5180/mcp` (Streamable HTTP, stateless, no session id needed).
- Auth: `Authorization: Bearer dbk_...` only. Being on localhost is not enough, and the web app's own sign-in does not work on `/mcp`.

## Create a token

In the web app: Settings, API tokens, Create (the UI arrives with the Settings page). Until then use the REST API from the same machine:

```
curl -X POST http://localhost:5180/api/tokens -H "Content-Type: application/json" -d "{\"name\":\"family-assistant\"}"
```

The response is `{ "id": 1, "name": "family-assistant", "token": "dbk_..." }`. The token is shown once and only its SHA-256 is stored; if you lose it, revoke it and create a new one.

- `GET /api/tokens` lists `[{id, name, createdAt, lastUsedAt}]`.
- `DELETE /api/tokens/{id}` revokes a token immediately.

## Client configuration

Claude Code:

```
claude mcp add --transport http driverbalance http://localhost:5180/mcp --header "Authorization: Bearer dbk_..."
```

Generic MCP client config (`.mcp.json` style):

```json
{
  "mcpServers": {
    "driverbalance": {
      "type": "http",
      "url": "http://localhost:5180/mcp",
      "headers": { "Authorization": "Bearer dbk_..." }
    }
  }
}
```

## Tools

Every tool takes an optional `vehicleId` (default: the owner's default vehicle, else the first; callers should not ask which car). Dates are `yyyy-MM-dd` and default to today in the vehicle's time zone. Results are JSON of the stored entity plus a one-line `summary`; problems (validation, unknown vehicle or expense id) come back as an MCP error result with a readable message.

| Tool | Arguments (optional marked ?) |
| --- | --- |
| `add_expense` | category, amount, currency? ("home"), date?, odometer?, note?, homeAmount?, rate?, coversFrom?, coversTo?, place? (max 100) |
| `log_fuel` | litres, totalPrice, fullTank? (true), odometer?, date?, station?, currency? |
| `log_maintenance` | types (e.g. `["OilChange","AirFilter"]`), cost, odometer?, date?, description? |
| `swap_tyres` | tyreSetId, odometer?, date?, cost? (0) |
| `list_tyre_sets` | none |
| `list_recent_expenses` | count? (10, max 100) |
| `delete_expense` | id |
| `get_stats` | kind (`interval`, `range` or `calendar`), maintenanceType?, intervalIndex? (0), from?, to? (inclusive), year?, month? |
| `import_trips_csv` | csvText |
| `list_recent_trips` | count? (1 = the last trip, max 50), level? (`trip` or `group`), from?, to? (inclusive) |

A foreign currency with neither `homeAmount` nor `rate` is saved and the result has `needsConversion: true`; it stays out of totals until a home amount is set. With `rate`, `homeAmount = amount * rate`.

For `kind: "range"`, both `from` and `to` are inclusive: `from=2026-10-01, to=2026-10-31` is all of October (31 days, the same numbers as REST stats for `[2026-10-01, 2026-11-01)`). A `to` before `from` is an error. The summary line prints the range as `01/10/2026 – 31/10/2026`.

`place` on `add_expense` is where it happened (garage, car park, shop). `log_fuel` keeps its location in `station` (the app shows `station` for fuel and `place` for everything else, and a `place` on a Fuel expense is dropped by the shared rules).

`get_stats` returns `{ stats, parameters, summary }`: the same numbers as the REST API, computed with the vehicle's saved analysis parameters.

`list_recent_trips` returns `{ trips, count, level, totalCost, currency, costModel, summary }`, newest first. Each item has `start`, `end`, `distanceKm`, `durationMinutes`, `fuelLiters`, `from`, `to`, `tripCount` and `cost { fuel, time, wear, total, pricePerLitre, currency }`: the same approximate trip cost as the Trips list (fuel at the local average price plus shares of the other running costs). `fuel` is null when no fuel price is known yet. `costModel` is a one-paragraph explanation a bot can repeat.
