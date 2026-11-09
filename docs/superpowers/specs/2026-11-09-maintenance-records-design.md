# Maintenance records: checklist, parts, custom works, editing

Date: 2026-11-09. Status: approved by the owner in chat.

## Why
The owner wants to log a service visit quickly, with the current odometer and cost, and record which parts went in
(original Volvo or which brand/model), so later they can see what was done when and with what. Today maintenance is
only a 12-item checklist plus one free-text description inside the generic expense form.

## Owner decisions
- **One record = one service visit** that can contain many work items done together (e.g. engine oil + oil filter +
  air filter + cabin filter + wiper blades). Cost is for the whole visit.
- **Checklist (21 items, grouped):**
  - Fluids: Engine oil (`OilChange`), Transmission oil, Haldex oil, Brake fluid, Coolant
  - Filters: Oil filter (new), Air filter, Cabin filter, Fuel filter
  - Brakes: Brake pads, Brake discs (new)
  - Tyres & wheels: Tyre swap (all tyre-change work), Wheel alignment (new). `NewTyres` is no longer offered;
    legacy records using it still count as a tyre mount and display as "New tyres".
  - Engine & belts: Timing belt (+ water pump) (new), Auxiliary belt (new), Glow plugs (new), DPF service (new)
  - Electrical & other: Battery (new), Wiper blades front (new), Wiper blades rear (new), AC service (`ACCleaning`)
  - No AdBlue, washer fluid, bulbs, spark plugs. Oil filter is separate from engine oil.
- **Custom works:** "+ Add custom work" adds any number of free-named items.
- **Per item (checklist and custom) a parts block:** Part = none / Original (Volvo) / Other brand; with Other brand,
  Brand and Model/part no.; plus optional free Details (e.g. "5.2 L", "front axle only").
- **Dedicated form** opened from the Maintenance page: date defaults to today, odometer pre-filled with the latest known
  reading (editable), cost in home currency by default, optional note.
- **Edit and delete** existing maintenance records: click a row in the Services table to open the same form filled in;
  the form has Delete.
- Oil intervals keep following Engine oil (`OilChange`) only. Existing records are not changed (no oil filter added).

## Tyres (owner decisions, added 2026-11-09)
- **The Tyres tab is removed** (`/tyres` redirects to `/maintenance`). The Maintenance page shows a small **tyre summary**:
  one line per set — brand + model (or name), size, season, status Fitted / Stored / Retired, km driven, fitted since.
  Clicking a set opens a small editor (brand, model, size, season, retired) — the only place left to correct a set.
- **Tyre swap has a "New tyres" flag.**
  - Off: pick which stored (non-retired, not currently fitted) set went on, or "add a set I already own" (brand, model,
    size, season) — creates the set, retires nothing.
  - On: enter the new set's brand, model, size, season (Winter / Summer / All-season). On save the set is created and
    linked to the record (fitted), and **the set fitted before this record's date is retired automatically**.
  - Editing a record later only re-links an existing set; set details are edited from the tyre summary.

## Data model
- `MaintenanceType` flags gain: `OilFilter, BrakeDiscs, WheelAlignment, TimingBelt, AuxiliaryBelt, GlowPlugs, DpfService,
  Battery, WiperBladesFront, WiperBladesRear` (bits 12..21). Names of existing values stay (MCP and stored data use them);
  UI shows friendly labels (OilChange → "Engine oil", ACCleaning → "AC service").
- `Expense` gains `Work: IReadOnlyList<MaintenanceWork>` (empty by default). `MaintenanceWork`:
  `Type: MaintenanceType?` (a single defined non-Custom flag; null for a custom work), `Name: string?` (required for
  custom, ignored otherwise), `Part: PartSource` (`None | Original | Aftermarket`), `Brand`, `Model`, `Details` (strings, optional).
- Stored as a JSON text column `WorkJson` on the expense row (new EF migration; null = no items). Flags column stays
  the source for analytics; on save, flags = flags OR every typed work item's flag.
- Validation (Maintenance category only; other categories must send no work): type defined, single-bit, not Custom; no
  duplicate typed items; custom needs a non-blank Name (≤ 100 chars); Aftermarket needs a Brand; strings ≤ 200 chars;
  ≤ 50 items. Brand/Model are cleared when Part ≠ Aftermarket.
- Legacy `MaintenanceDescription` is kept and still shown when a record has no work items; the new form no longer edits
  it but preserves it on save.
- `TyreSet` gains `Brand`, `Model`, `Size` (nullable strings ≤ 100). `Name` stays required by storage; when blank the
  API derives it: "Brand Model", else "<Season> tyres".
- Expense create/update accepts optional `tyres: { brand, model, size, season, isNew }` (only with a TyreSwap work item;
  mutually exclusive with `tyreSetId`). In one transaction: create the set, set the expense's `TyreSetId` to it, and when
  `isNew` retire the set fitted just before (the `TyreSetId` of the latest earlier mount, else the vehicle's initial set),
  unless that is null.
- New `GET /api/vehicles/{vid}/odometer/latest` → `{ odometer, date }` (largest known reading from trips and expenses), or 204 when none.

## UI
- **The Maintenance page shows all maintenance, oil changes included** (owner, 2026-11-09). Oil intervals stays as an extra
  view because engine stats matter for oil (replaced by load), while other parts are replaced when worn.
- Remove the Tyres tab and page; move the tyre summary + set editor onto the Maintenance page.
- Maintenance page: "Add maintenance" button; Services table rows clickable to edit; under each work item show the part
  ("Original" or "Brand Model") and details. Timeline gets a lane per custom work name. "Since last time" also works
  for custom works (matched by name, case-insensitive).
- Generic expense form uses the same work editor when its category is Maintenance.
- MCP `log_maintenance` keeps working; its type list includes the new names.

## Builder's calls
- Exact layout of the work editor (grouped checkboxes; ticked items expand to show the parts block).
- Delete asks for confirmation.
