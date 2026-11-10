# Ownership Costs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Purchase date and resale estimate on the vehicle, a Purchase expense category kept out of period stats, and a cost-of-ownership summary.

**Architecture:** Purchase expenses are filtered out in `Money.For`, the single money calculation every stat uses; a new endpoint computes TCO from the vehicle fields, purchase expenses, running expenses since purchase and the odometer timeline.

**Tech Stack:** .NET 10 minimal API + EF Core SQLite, xUnit; React + TS + Vite, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-09-ownership-costs-design.md`

## Global Constraints
- Branch `feat/web-app`. TDD: failing test first, run red, implement, run green.
- Never touch `DriverBalance/bin` or `%LOCALAPPDATA%\DriverBalance`. Tests use their own DBs.
- `ExpenseCategory.Purchase` is appended at the end of the enum (categories are stored as strings).
- Vehicle PUT clears fields that are omitted (existing convention) — the Settings save must always send every field.
- Validation → 400 `{errors}`. `dotnet build` 0 warnings; `dotnet test`; `cd web && npx vitest run && npx tsc -b` green.
- Commit messages end with a blank line then `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus
1. Purchase expenses never appear in any period total, by-category figure, cost per km or money chart. → Task 1 test on Money.For and an analytics API test.
2. Running costs since purchase spread covers-period expenses only for the part after PurchaseDate. → Task 1 test.
3. Missing PurchaseDate: ownership returns nulls without throwing. → Task 1 test.
4. Settings save keeps historyStart, initialTyreSetId and the new fields. → Task 2 check.

---

### Task 1: Backend — vehicle fields, Purchase category, ownership endpoint
**Files:** Core `Enums`/`Expense.cs` (ExpenseCategory.Purchase), `src/DriverBalance.Core/Analysis/Money.cs` (skip Purchase), new `src/DriverBalance.Core/Analysis/Ownership.cs` (pure calculation), vehicle domain/row/mapping/DTOs + migration `VehicleOwnership`, `src/DriverBalance.Web/Endpoints/VehicleEndpoints.cs` (fields + validation: resale ≥ 0; purchase date and resale date not in the future), new route `GET /api/vehicles/{vid}/ownership`, VehicleData + loader carry the new fields as needed, MCP `add_expense` category list mentions Purchase.
**Interfaces:** Vehicle JSON `purchaseDate`, `estimatedResaleValue`, `resaleEstimateDate` (nullable). Ownership JSON: `{ purchaseDate, purchaseCosts: {date, amount, note}[], purchaseTotal, runningCosts, kmDriven, months, estimatedResaleValue, resaleEstimateDate, netCost, netCostPerKm, netCostPerMonth }` — numbers nullable when not computable; amounts in home currency (use `Money.HomeAmountOf`; skip expenses without a home amount).
- [ ] Failing tests: Money.For ignores a Purchase expense (total, byCategory, costPerKm); Ownership with purchase 2025-04-14, purchase costs 1 000 000 + 30 000, a running expense 1 200 covering 2025-01-01→2025-12-31 (only the share from 2025-04-14 counts), odometer 10 000 → 20 000 since purchase, resale 600 000 → netCost = 1 030 000 + share − 600 000, perKm = netCost / 10 000; no PurchaseDate → all derived values null; vehicle round trip and validation (future dates, negative resale → 400).
- [ ] Implement; `dotnet test` green, 0 warnings; commit `feat(api): purchase date, purchase costs outside period stats, cost of ownership`.

### Task 2: Frontend — Settings, category, ownership card, previous-owner tags
**Files:** `web/src/api/types.ts`, `web/src/components/ExpenseForm.tsx` (CATEGORIES + label "Purchase (car, warranty, transfer)"), `web/src/pages/Settings.tsx` ("Bought on" date with hint "Your first day with the car; earlier records belong to the previous owner." and a "Use first trip (yyyy-mm-dd)" button when empty; "Estimated resale value" + "Estimated on" date), `web/src/pages/Overview.tsx` (new "Cost of ownership" card), `web/src/pages/Maintenance.tsx` (muted "previous owner" tag on rows dated before purchaseDate), `web/src/components/MultiCompareTable.tsx` + Oil intervals compare-two headings ("previous owner" under headings of intervals whose `to` ≤ purchaseDate), new pure helpers in `web/src/lib/ownership.ts` with tests.
- Card content: Bought on; Purchase costs (total, with the items listed small); Running costs since purchase; Km driven since purchase; Estimated resale value (date) or "not set"; **Net cost of ownership** with per km and per month; a muted line "Resale value is your estimate; net figures are estimates." When no purchase date: "Set the purchase date in Settings to see the cost of ownership."
- [ ] Failing tests for the pure helpers (previous-owner check, ownership display rows).
- [ ] Implement; vitest + tsc green; commit `feat(web): purchase date, cost of ownership, previous-owner tags`.

### Task 3: Import and verify (controller)
- [ ] Back up the DB (`driverbalance.pre-ownership.db`), publish, set the owner's PurchaseDate via the API, import the records listed in the spec through the expenses API, verify Overview, Statistics 2025 and the ownership card, update NOTES.md, commit.
