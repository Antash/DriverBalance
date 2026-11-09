# Documents, Places and Car History Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Attach documents to expenses, record where a service was done with suggestions, and count stats from a configurable car-history start (0 km).

**Architecture:** Additive EF columns/table with migrations that run on start; files on local disk next to the DB; the history start becomes one more odometer reading so every analytics consumer picks it up.

**Tech Stack:** .NET 10 minimal API + EF Core SQLite, xUnit; React + TS + Vite, TanStack Query, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-09-documents-places-history-design.md`

## Global Constraints
- Branch `feat/web-app`. TDD: failing test first, run red, implement, run green.
- Never touch `DriverBalance/bin` or `%LOCALAPPDATA%\DriverBalance` (owner's real DB, documents and running app). Tests use temp DBs and temp document folders.
- Validation failures → 400 `{errors: [...]}`; never 500 for bad input.
- `dotnet build` 0 warnings; `dotnet test`; `cd web && npx vitest run && npx tsc -b` all green.
- Limits: Place ≤ 100 chars; documents ≤ 20 MB each; types application/pdf, image/jpeg, image/png, image/webp, image/heic (also accept by extension .pdf .jpg .jpeg .png .webp .heic when the browser sends application/octet-stream).
- Commit messages end with a blank line then `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus
1. Deleting an expense removes its document rows and files; a failed file delete must not leave the expense half-deleted (rows deleted in the transaction, files after commit, missing files ignored). → Task 2 test.
2. Path traversal: a file name like `..\..\x.pdf` is stored under a sanitised name inside the expense folder. → Task 2 test.
3. Another vehicle's document id returns 404. → Task 2 test.
4. HistoryStart later than the first trip is ignored for the timeline (it must be the earliest reading). → Task 3 test.
5. A period with no trips but odometer coverage reports odometerKm and kmPerDay, null trip metrics, no exception. → Task 3 test.

---

### Task 1: Backend — Place field and suggestions
**Files:** `src/DriverBalance.Core/Domain/Expense.cs` (`Place`), `src/DriverBalance.Data/Rows/Rows.cs`, `src/DriverBalance.Data/Mapping.cs`, migration `ExpensePlace`, `src/DriverBalance.Web/Services/ExpenseRules.cs` (trim, ≤ 100, blank → null; Fuel keeps Place null), `src/DriverBalance.Web/Endpoints/ExpenseEndpoints.cs` (Update copies Place; `GET /vehicles/{vid}/expenses/places`), `src/DriverBalance.Web/Mcp/CarTools.cs` (`log_maintenance` gains optional `place`).
**Interfaces:** Expense JSON `place: string | null`. `GET /api/vehicles/{vid}/expenses/places` → `string[]`, most recent first, case-insensitive distinct keeping the latest spelling, max 50.
- [ ] **Step 1: Failing tests** in `tests/DriverBalance.Web.Tests/ApiTests/PlaceApiTests.cs`: round trip; value trimmed; 101 chars → 400; Fuel with place → stored null; suggestions: "City Garage" (2025-01-10), "Pneu Servis" (2025-06-01), "city garage" (2026-01-10) → `["city garage", "Pneu Servis"]`.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Web.Tests --filter PlaceApiTests` — fails.
- [ ] **Step 3: Implement.** Route `/expenses/places` must not collide with `/expenses/{id:long}`.
- [ ] **Step 4: Run** `dotnet test` — green, 0 warnings.
- [ ] **Step 5: Commit** `feat(api): service place on expenses with suggestions`.

### Task 2: Backend — documents
**Files:** `ExpenseDocumentRow` in `src/DriverBalance.Data/Rows/Rows.cs`, DbSet + user filter in `src/DriverBalance.Data/DriverBalanceDbContext.cs`, migration `ExpenseDocuments`; create `src/DriverBalance.Web/Services/DocumentStore.cs` (root = config `DriverBalance:DocumentsPath`, else `<db dir>/documents`; save, open, delete file, delete expense folder) and `src/DriverBalance.Web/Endpoints/DocumentEndpoints.cs`; modify `ExpenseEndpoints.cs` (List/Create/Update responses include `documents`; Delete removes rows in its transaction and the folder after commit), `Program.cs` registration.
**Interfaces:** Expense JSON `documents: { id: number; fileName: string; contentType: string; sizeBytes: number }[]` (always present). `POST /api/vehicles/{vid}/expenses/{id}/documents` multipart, field `files` (1..10 files) → 201 with the created list. `GET /api/vehicles/{vid}/documents/{docId}` → the file, inline, original file name in Content-Disposition. `DELETE /api/vehicles/{vid}/documents/{docId}` → 204. Stored path `<root>/<vehicleId>/<expenseId>/<docId>-<sanitised name>`.
- [ ] **Step 1: Failing tests** in `DocumentApiTests.cs` (temp DocumentsPath via factory configuration): upload PDF + PNG → listed on the expense; download returns the same bytes and content type; delete → GET 404 and file gone; expense delete removes rows and folder; 21 MB → 400; `.exe` → 400; malformed multipart and no files → 400; `..\..\x.pdf` stored inside the expense folder; a document of another vehicle → 404.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Web.Tests --filter DocumentApiTests` — fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test` — green, 0 warnings.
- [ ] **Step 5: Commit** `feat(api): documents attached to expenses`.

### Task 3: Backend — car history start
**Files:** vehicle domain, row, mapping and DTOs (`HistoryStart: DateOnly?`, `HistoryStartOdometer: int?`), migration `VehicleHistoryStart`, `src/DriverBalance.Web/Endpoints/VehicleEndpoints.cs` (validation: odometer ≥ 0; date not after today; odometer defaults to 0 when a date is set; both null clears), `src/DriverBalance.Core/Domain/VehicleData.cs` (+ the loader that fills it), `src/DriverBalance.Core/Analysis/AnalysisContext.cs` BuildTimeline (adds `OdometerReading(HistoryStart 00:00, odo, OdometerSource.HistoryStart)` only when earlier than every other reading; new enum value), the analysis session's `DataStart` = min(HistoryStart, current data start).
**Interfaces:** Vehicle JSON `historyStart: "yyyy-MM-dd" | null`, `historyStartOdometer: number | null`.
- [ ] **Step 1: Failing tests** (Core `HistoryStartTests.cs`; Web vehicle round trip + intervals): oil intervals with HistoryStart 2023-05-01 @ 0, first oil change 2025-02-06 @ 7 350 and trips only from 2025-03 → an extra oldest period 2023-05-01 → 2025-02-06 with StartOdometer 0, odometerKm 7 350, kmPerDay = 7 350 / elapsed days, tripCount 0, dpfIndex null; HistoryStart after the first trip → ignored by the timeline; future date → 400; negative odometer → 400.
- [ ] **Step 2: Run** the new tests — fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test` — green, 0 warnings.
- [ ] **Step 5: Commit** `feat(api): car history start counts intervals from 0 km`.

### Task 4: Frontend — place, documents, history start
**Files:** `web/src/api/types.ts`, `web/src/lib/expenseForm.ts` (+ test), `web/src/components/ExpenseForm.tsx`, new `web/src/components/DocumentList.tsx`, `web/src/pages/Maintenance.tsx`, `web/src/pages/Expenses.tsx`, `web/src/pages/Settings.tsx`, `web/src/api/client.ts` (multipart upload helper if missing).
- Place: input "Where" with a `<datalist>` from `GET …/expenses/places` for every category except Fuel; payload `place` (Fuel → null).
- Documents: the form lists existing documents (link opening `/api/vehicles/{vid}/documents/{id}` in a new tab, size, Remove → DELETE after confirm) and has an "Attach files" input (multiple; accept the allowed types). On save, create/update the expense, then upload pending files to its id; show upload errors in the form; invalidate after.
- Maintenance Services table: "Where" column and a 📎 count next to the date when documents exist. Expenses page: the same 📎 count.
- Settings: "Car history start" date + "Odometer at that date (km)" (default 0) with the hint "When the car was new (or when you bought it). Stats count from here."
- [ ] **Step 1: Failing tests**: payload includes trimmed `place` and null for Fuel; `toFormState` maps place.
- [ ] **Step 2: Run** — fails. **Step 3: Implement.** **Step 4:** `npx vitest run && npx tsc -b` green.
- [ ] **Step 5: Commit** `feat(web): service place, documents and car history start`.

### Task 5: Publish and smoke test (controller)
- [ ] Back up the real DB to `driverbalance.pre-documents.db`; publish; on a scratch copy add a record with a place and 2 documents, open one, delete one, delete the record; set the history start in Settings and check Oil intervals shows the new oldest interval. Start the real app. Update NOTES.md. Commit.
