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

Every tool takes an optional `vehicleId` (default: your default vehicle, else the first). Dates are `yyyy-MM-dd` and default to today in the vehicle's time zone. Results are JSON of the stored entity plus a one-line `summary`; problems (validation, unknown vehicle or expense id) come back as an MCP error result with a readable message.

| Tool | Arguments (optional marked ?) |
| --- | --- |
| `add_expense` | category, amount, currency? ("home"), date?, odometer?, note?, homeAmount?, rate?, coversFrom?, coversTo? |
| `log_fuel` | litres, totalPrice, fullTank? (true), odometer?, date?, station?, currency? |
| `log_maintenance` | types (e.g. `["OilChange","AirFilter"]`), cost, odometer?, date?, description? |
| `swap_tyres` | tyreSetId, odometer?, date?, cost? (0) |
| `list_tyre_sets` | none |
| `list_recent_expenses` | count? (10, max 100) |
| `delete_expense` | id |
| `get_stats` | kind (`interval`, `range` or `calendar`), maintenanceType?, intervalIndex? (0), from?, to? (exclusive), year?, month? |
| `import_trips_csv` | csvText |

A foreign currency with neither `homeAmount` nor `rate` is saved and the result has `needsConversion: true`; it stays out of totals until a home amount is set. With `rate`, `homeAmount = amount * rate`.

`get_stats` returns `{ stats, parameters, summary }`: the same numbers as the REST API, computed with the vehicle's saved analysis parameters.
