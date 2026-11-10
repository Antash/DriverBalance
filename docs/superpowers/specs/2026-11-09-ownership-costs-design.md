# Ownership: purchase date, purchase costs, cost of ownership, year-only import

Date: 2026-11-09. Status: approved by the owner in chat ("go on").

## Why
The owner keeps a yearly spreadsheet of running costs without dates and wants it in the stats. They also want the
car's starting costs (price, warranty, registration transfer) recorded for total cost of ownership (TCO) — but kept out
of year-to-year stats — and a clear line between the previous owner and the current owner. Owner: "Real cost of the
car is a purchase value - resale value which we don't really know, can only guess based on the market prices."

## Decisions
- **Vehicle gains** `PurchaseDate: DateOnly?` ("Bought on" — the current owner's first day), `EstimatedResaleValue:
  decimal?` and `ResaleEstimateDate: DateOnly?` (a manual guess of today's market value, in the home currency). Settings
  edits them. When PurchaseDate is empty the UI suggests the first recorded trip date.
- **Previous owner:** maintenance records dated before PurchaseDate get a muted "previous owner" tag in the Services
  table; Oil intervals columns whose interval ends on or before PurchaseDate get "previous owner" under the heading.
- **New expense category `Purchase`** (label "Purchase (car, warranty, transfer)"): appended to the enum; stored as a
  string. Purchase expenses are **excluded from all period money stats** (`Money.For`, so totals, by-category, cost per
  km, money charts, intervals, overview KPIs and MCP get_stats all skip them).
- **Cost of ownership** (`GET /api/vehicles/{vid}/ownership`, Overview card "Cost of ownership"), all in home currency:
  `purchaseDate`, `purchaseCosts` (list of {date, amount, note}), `purchaseTotal`, `runningCosts` (all non-Purchase
  expenses from PurchaseDate to today, with covers-period spreading as elsewhere), `kmDriven` (odometer from PurchaseDate
  to today via the timeline), `months` (elapsed, 1 decimal), `estimatedResaleValue`, `resaleEstimateDate`,
  `netCost` = purchaseTotal + runningCosts − (estimatedResaleValue ?? 0), `netCostPerKm`, `netCostPerMonth`
  (null when km/months are 0 or no PurchaseDate). The card marks resale-based figures as estimates and shows
  "Set the purchase date in Settings" when PurchaseDate is missing.
- **Import** (done by the controller through the API, not a product feature): from the owner's yearly cost sheet, one
  expense per category with the year's total, dated the purchase date and covering purchase date → 2025-12-31, note
  "Imported from the 2025 sheet (N items)". A service already recorded is skipped. Purchase costs (car, warranty,
  registration transfer) go in on the purchase date. Back up the DB first.
