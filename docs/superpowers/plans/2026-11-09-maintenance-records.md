# Maintenance Records Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Log, edit and delete service visits with a grouped 21-item checklist plus custom works, each item carrying part source (original / brand + model) and details; tyre swaps fit stored sets or new sets (retiring the old), and the Tyres tab folds into the Maintenance page.

**Architecture:** Keep the `MaintenanceType` flags as the analytics source; add a per-expense list of `MaintenanceWork` items stored as a JSON text column. The web form gets a work-item editor shared by the Maintenance page (add/edit/delete) and the generic expense form.

**Tech Stack:** .NET 10 minimal API + EF Core (SQLite), xUnit; React + TypeScript + Vite, TanStack Query, ECharts, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-09-maintenance-records-design.md`

## Global Constraints
- Work on branch `feat/web-app`. TDD order: write the failing test, run it, implement, run it. Do not skip the red step.
- Existing `MaintenanceType` names and bit values never change (stored data and MCP depend on them). New values: `OilFilter = 1<<12, BrakeDiscs = 1<<13, WheelAlignment = 1<<14, TimingBelt = 1<<15, AuxiliaryBelt = 1<<16, GlowPlugs = 1<<17, DpfService = 1<<18, Battery = 1<<19, WiperBladesFront = 1<<20, WiperBladesRear = 1<<21`.
- Real data stays out of git. Never touch `DriverBalance/bin`.
- Build: `dotnet build` 0 warnings. Tests: `dotnet test` (Core + Web) and `cd web && npx vitest run && npx tsc -b`.
- UI labels (exact): OilChange "Engine oil", TransmissionOil "Transmission oil", HaldexOil "Haldex oil", BrakeFluid "Brake fluid", Coolant "Coolant", OilFilter "Oil filter", AirFilter "Air filter", CabinFilter "Cabin filter", FuelFilter "Fuel filter", BrakePads "Brake pads", BrakeDiscs "Brake discs", TyreSwap "Tyre swap", NewTyres "New tyres" (legacy display only, not in the checklist), WheelAlignment "Wheel alignment", TimingBelt "Timing belt (+ water pump)", AuxiliaryBelt "Auxiliary belt", GlowPlugs "Glow plugs", DpfService "DPF service", Battery "Battery", WiperBladesFront "Wiper blades (front)", WiperBladesRear "Wiper blades (rear)", ACCleaning "AC service".
- Groups (exact order): Fluids [OilChange, TransmissionOil, HaldexOil, BrakeFluid, Coolant]; Filters [OilFilter, AirFilter, CabinFilter, FuelFilter]; Brakes [BrakePads, BrakeDiscs]; Tyres & wheels [TyreSwap, WheelAlignment]; Engine & belts [TimingBelt, AuxiliaryBelt, GlowPlugs, DpfService]; Electrical & other [Battery, WiperBladesFront, WiperBladesRear, ACCleaning].
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus
1. Editing a legacy record (flags set, no work items, has `maintenanceDescription`) must not drop its flags or description. → Task 3 test `toFormState_legacyFlagsBecomeWorkItems` + Task 1 test `Update_PreservesDescription`.
2. Unticking a checklist item on edit must remove its flag (flags derived from items when items are sent). → Task 1 test `Update_FlagsFollowWorkItems`.
3. Switching part from Other brand back to Original must not keep stale brand/model. → Task 1 test `Aftermarket_FieldsClearedWhenNotAftermarket`, Task 3 payload test.
4. A non-Maintenance expense posted with work items is rejected (400), not silently stored. → Task 1 validation table.
5. Custom works with the same name in different case are one lane / one "since last" chain. → Task 2 test.

---

### Task 1: Backend — work items, new flags, latest odometer

**Files:**
- Modify: `src/DriverBalance.Core/Domain/Enums.cs`, `src/DriverBalance.Core/Domain/Expense.cs`
- Create: `src/DriverBalance.Core/Domain/MaintenanceWork.cs`
- Modify: `src/DriverBalance.Data/Rows/Rows.cs` (`ExpenseRow.WorkJson: string?`), `src/DriverBalance.Data/Mapping.cs`, new migration `MaintenanceWork` in `src/DriverBalance.Data/Migrations`
- Modify: `src/DriverBalance.Web/Services/ExpenseRules.cs`, `src/DriverBalance.Web/Endpoints/ExpenseEndpoints.cs` (Update copies `WorkJson`), `src/DriverBalance.Web/Endpoints/VehicleEndpoints.cs` (latest odometer), `src/DriverBalance.Web/Mcp/CarTools.cs` (`MaintenanceNames` lists all non-Custom names)
- Test: `tests/DriverBalance.Web.Tests/ApiTests/ExpenseApiTests.cs`, `tests/DriverBalance.Web.Tests/ApiTests/MaintenanceWorkApiTests.cs` (new)

**Interfaces:**
- Produces (JSON, camelCase): expense gains `work: { type: string | null, name: string | null, part: "None"|"Original"|"Aftermarket", brand: string | null, model: string | null, details: string | null }[]` (always present, `[]` when none). `type` is a single enum name string, NOT an array: put `[JsonConverter(typeof(JsonStringEnumConverter<MaintenanceType>))]` on `MaintenanceWork.Type` so it beats the options-level `MaintenanceTypeJsonConverter`.
- Produces: `GET /api/vehicles/{vid}/odometer/latest` → 200 `{ odometer: int, date: "yyyy-MM-dd" }` (max over trip `EndOdometer > 0` and expense `Odometer`, date of that reading), 204 when none.
- C#: `public enum PartSource { None, Original, Aftermarket }`; `public sealed record MaintenanceWork { MaintenanceType? Type; string? Name; PartSource Part; string? Brand; string? Model; string? Details }`; `Expense.Work: IReadOnlyList<MaintenanceWork>` default empty.

- [ ] **Step 1: Write failing tests** in `MaintenanceWorkApiTests`:
  - `Create_RoundTripsWorkItems`: POST Maintenance with `work = [{type:"OilChange", part:"Aftermarket", brand:"Castrol", model:"Edge 5W-30", details:"5.2 L"}, {type:"OilFilter", part:"Original"}, {type:null, name:"Headlight polish", part:"None"}]`, `maintenance = []`; GET list returns the same 3 items in order and `maintenance` equals `["OilChange","OilFilter"]`.
  - `Update_FlagsFollowWorkItems`: create with work [OilChange, AirFilter]; PUT with work [OilChange] and `maintenance: ["OilChange","AirFilter"]` → stored `maintenance == ["OilChange"]` (when `work` is non-empty, flags = OR of typed items only).
  - `Update_PreservesDescription`: create legacy-style (`maintenance:["BrakeFluid"]`, `maintenanceDescription:"old"`, no work); PUT same body → description `"old"`, flags `["BrakeFluid"]`, work `[]`.
  - `Aftermarket_FieldsClearedWhenNotAftermarket`: POST item `{type:"BrakePads", part:"Original", brand:"X", model:"Y"}` → stored brand and model null.
  - `Work_Validation_400` (table, each 400 with errors): work on category Fuel; type `"Custom"`; unknown type `"Foo"`; duplicate typed item; custom with blank name; custom name 101 chars; Aftermarket without brand; details 201 chars; 51 items.
  - `LatestOdometer`: empty vehicle → 204; after posting an expense with odometer 30000 dated 2026-05-01 → `{odometer:30000, date:"2026-05-01"}`.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Web.Tests --filter MaintenanceWorkApiTests` — expect failures (compile errors count).
- [ ] **Step 3: Implement** domain, enum values, row column + mapping (serialize `Work` with `System.Text.Json` default options plus the property attribute; null when empty), migration (`dotnet ef migrations add MaintenanceWork -p src/DriverBalance.Data`), rules (normalise: trim strings, blank → null, clear brand/model unless Aftermarket, derive flags), endpoint, MCP names.
- [ ] **Step 4: Run** `dotnet test` (all) — all green, including existing expense and MCP tests; `dotnet build` 0 warnings.
- [ ] **Step 5: Commit** `feat(api): maintenance work items with parts, new consumables, latest odometer`.

### Task 1b: Backend — tyre set details and fitting from a tyre swap

**Files:**
- Modify: `src/DriverBalance.Core/Domain/TyreSet.cs` (`Brand`, `Model`, `Size`), `src/DriverBalance.Data/Rows/Rows.cs`, `Mapping.cs`, new migration `TyreSetDetails`, `src/DriverBalance.Web/Endpoints/TyreEndpoints.cs` (derive Name, validate lengths ≤ 100), `src/DriverBalance.Web/Endpoints/ExpenseEndpoints.cs`, `src/DriverBalance.Web/Services/ExpenseRules.cs`
- Test: `tests/DriverBalance.Web.Tests/ApiTests/TyreFittingApiTests.cs` (new)

**Interfaces:**
- Produces JSON: tyre set gains `brand`, `model`, `size` (string | null). Expense request body gains optional `tyres: { brand: string|null, model: string|null, size: string|null, season: "Summer"|"Winter"|"AllSeason", isNew: boolean } | null` (request only, not stored on the expense; responses don't include it).
- Rules: `tyres` only allowed when category is Maintenance and a `TyreSwap` work item (or flag) is present → else 400; `tyres` together with `tyreSetId` → 400. Create and Update both run in one DB transaction: insert the set (Name derived "Brand Model" or "<Season> tyres", e.g. "Winter tyres"), link the expense, and when `isNew` retire the previously fitted set = `TyreSetId` of the latest Maintenance mount expense (TyreSwap or NewTyres flag) of this vehicle with (Date, Id) before this one, else the vehicle's `InitialTyreSetId`; skip when null or the same set.

- [ ] **Step 1: Failing tests**: `NewTyres_CreatesSetAndRetiresPrevious` (set A fitted by an earlier swap; POST swap with `tyres{brand:"Nokian", model:"Snowproof 2", size:"205/55 R16", season:"Winter", isNew:true}` → new set named "Nokian Snowproof 2", expense `tyreSetId` = new id, A retired); `OwnedTyres_CreatesSetRetiresNothing` (`isNew:false` → A not retired); `DerivedName_WhenNoBrand` ("Summer tyres"); `Tyres_Validation_400` (tyres on Fuel expense; tyres without TyreSwap item; tyres + tyreSetId); `TyreSet_RoundTripsBrandModelSize` via `/tyres` POST/GET with blank name → derived.
- [ ] **Step 2: Run** `dotnet test tests/DriverBalance.Web.Tests --filter "TyreFittingApiTests"` — fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test` — all green, 0 warnings.
- [ ] **Step 5: Commit** `feat(api): tyre set brand/model/size; fit new or owned tyres from a tyre swap`.

### Task 2: Frontend data — types, catalogue, history

**Files:**
- Modify: `web/src/api/types.ts` (`MaintenanceType` union + 10 names; `PartSource`; `MaintenanceWork`; `Expense.work: MaintenanceWork[]`)
- Create: `web/src/lib/maintenanceCatalog.ts`; Modify: `web/src/lib/maintenance.ts`, `web/src/components/ExpenseForm.tsx` (`MAINTENANCE_TYPES` becomes the catalogue order re-exported from `maintenanceCatalog.ts`)
- Test: `web/src/lib/maintenance.test.ts`, `web/src/lib/maintenanceCatalog.test.ts`

**Interfaces:**
- Produces: `MAINTENANCE_GROUPS: { title: string; types: MaintenanceType[] }[]` (Global Constraints order), `MAINTENANCE_TYPES` (flattened), `maintenanceLabel(t: MaintenanceType): string` (exact labels above).
- Produces: `maintenanceHistory(expenses)` now returns `{ services: ServiceRow[]; lanes: string[] }` where each `WorkItem = { key: string; label: string; type: MaintenanceType | null; part: PartSource; brand: string | null; model: string | null; details: string | null; sinceDays: number | null; sinceKm: number | null }`; `key` is the type name or `custom:<lower-cased trimmed name>`; `label` the friendly label or the custom name as first typed. Items come from `work` when present, else from flags (part `None`, no details). OilChange excluded. Lanes = typed labels in catalogue order, then custom names in first-seen order.

- [ ] **Step 1: Failing tests**: catalogue has 21 types (no NewTyres; `maintenanceLabel('NewTyres') === 'New tyres'` still works), all unique, groups in the exact order, `maintenanceLabel('OilChange') === 'Engine oil'`, `maintenanceLabel('ACCleaning') === 'AC service'`; history: existing tests updated to the new shape; a record with `work` carrying `{type:'BrakePads', part:'Aftermarket', brand:'Brembo', model:'P 86 024'}` gives that item's part fields; custom "Headlight polish" then "headlight POLISH " 30 days later → second has `sinceDays 30` and both share key `custom:headlight polish`; lanes put custom names after typed ones.
- [ ] **Step 2: Run** `cd web && npx vitest run src/lib` — fails.
- [ ] **Step 3: Implement.** Update `Maintenance.tsx` only as far as needed to compile (use `lanes`, `w.label`).
- [ ] **Step 4: Run** `npx vitest run && npx tsc -b` — green.
- [ ] **Step 5: Commit** `feat(web): maintenance catalogue, labels and work-item history`.

### Task 3: Work editor in the expense form

**Files:**
- Create: `web/src/components/MaintenanceWorkEditor.tsx`
- Modify: `web/src/lib/expenseForm.ts`, `web/src/components/ExpenseForm.tsx`
- Test: `web/src/lib/expenseForm.test.ts`

**Interfaces:**
- Consumes: Task 2 catalogue and types.
- Produces: `ExpenseFormState.work: WorkDraft[]` replacing `maintenance`, where `WorkDraft = { id: string; type: MaintenanceType | null; name: string; part: PartSource; brand: string; model: string; details: string }`. `maintenanceDescription` stays in state (not editable for new items, preserved on save).
- Produces: `ExpenseForm` props gain `lockCategory?: ExpenseCategory` (hides the category select, forces it), `onDelete?: () => void` (shows a Delete button when editing), and `defaultOdometer?: number`.
- `buildExpensePayload`: for Maintenance sends `work` (custom with blank name dropped; brand/model only when Aftermarket) and `maintenance` = typed item types; other categories send `work: []`, `maintenance: []`. `toFormState`: uses `e.work` when non-empty, else one draft per flag (part None). `usesTyreSet` takes the typed types.
- `validateExpenseForm`: custom item with blank name → `errors['work.<id>.name']`; Aftermarket with blank brand → `errors['work.<id>.brand']`.

- Tyre swap block (shown under a ticked Tyre swap, instead of the generic parts block): "New tyres" checkbox. Off → select "Tyres fitted" listing non-retired sets (label = name + season) plus "+ Add a set I already own"; choosing the latter or ticking New tyres shows Brand, Model, Size, Season (select Winter/Summer/All-season) and sends `tyres{..., isNew}` instead of `tyreSetId`. `ExpenseFormState` gains `tyres: { mode: 'existing'|'owned'|'new'; brand; model; size; season: TyreSeason }`. The old standalone Tyre set select is removed for Maintenance (kept for category Tyres).
- [ ] **Step 1: Failing tests**: payload with mode 'new' sends `tyres.isNew true` and `tyreSetId null`; mode 'existing' sends `tyreSetId` and no `tyres`; `toFormState_legacyFlagsBecomeWorkItems` (flags [OilChange, BrakeFluid], no work → two drafts, description kept); payload clears brand/model for Original; payload drops a custom draft with blank name only if it is also otherwise empty, otherwise validation error; Aftermarket without brand → error key; payload `maintenance` mirrors typed drafts.
- [ ] **Step 2: Run** `npx vitest run src/lib/expenseForm.test.ts` — fails.
- [ ] **Step 3: Implement** helpers, then the editor: grouped checkbox fieldsets (one per group, title as legend); a ticked or custom item shows a row: Part radio (None / Original (Volvo) / Other brand), Brand + Model/part no. inputs when Other brand, Details input; "+ Add custom work" appends a draft with a Name input and a Remove button. Replace the old Maintenance section and Description field in `ExpenseForm` (show legacy description read-only as muted text when present).
- [ ] **Step 4: Run** `npx vitest run && npx tsc -b` — green.
- [ ] **Step 5: Commit** `feat(web): maintenance work editor with parts and custom works`.

### Task 4: Maintenance page — add, edit, delete, show parts, tyre summary; remove Tyres tab

**Files:**
- Modify: `web/src/pages/Maintenance.tsx`, `web/src/api/client.ts` only if a helper is needed
- Test: none new beyond Task 2/3 (page is wiring); verified in Task 5.

**Interfaces:**
- Consumes: `ExpenseForm` props from Task 3, history from Task 2, `GET /odometer/latest` from Task 1.

- [ ] **Step 1: Implement**: "Add maintenance" primary button in the page header → `Modal` "Add maintenance" with `ExpenseForm lockCategory="Maintenance" defaultOdometer={latest?.odometer}`. Services rows get `className="clickable"`, keyboard-activatable (`tabIndex=0`, Enter), open `Modal` "Edit maintenance" with `initial` and `onDelete` (confirm "Delete this maintenance record from <date>?", then DELETE, invalidate via `invalidateVehicleData`, close). Work cell: each item label, then muted "Original" or "Brand Model", then details. Records with legacy description and no work show it in the Note column after the note. Timeline uses `lanes`. Tyre summary card (from `GET /tyres/summary` + `/tyres`): rows sorted Fitted, Stored, Retired; columns Tyres (name), Size, Season (Winter/Summer/All-season), Status, Km, Fitted since; plus one muted line per unassigned span "Unrecorded tyres: <from> → <to>, <km> km". Clicking a set opens a modal editor (brand, model, size, season, retired) → PUT `/tyres/{id}`. Delete `web/src/pages/Tyres.tsx`, its NAV entry and route (`/tyres` → `<Navigate to="/maintenance" replace />`); move anything else still imported from it.
- [ ] **Step 2: Run** `npx vitest run && npx tsc -b` — green.
- [ ] **Step 3: Commit** `feat(web): add, edit and delete maintenance from the Maintenance page`.

### Task 5: Publish and smoke test (controller does this)
- [ ] Stop the app and copy `%LOCALAPPDATA%\DriverBalance\driverbalance.db` to `driverbalance.pre-work-items.db` next to it (the migration runs on start via `DbSetup`).
- [ ] `dotnet publish src/DriverBalance.Web -c Release -o "$env:LOCALAPPDATA\DriverBalance\app"`, start it (migration applies on start — confirm the real DB still shows 4 maintenance records).
- [ ] Headless browser: open Maintenance, add a record with 3 items + 1 custom work, edit it, delete it; check Oil intervals unchanged. Update `NOTES.md`. Commit.
