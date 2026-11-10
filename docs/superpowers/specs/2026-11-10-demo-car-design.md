# Demo car for new profiles — design

Date: 2026-11-10. Branch: `feat/hosted-auth`. Status: described in chat and approved by the owner ("go for it").

## Why
An invited person signs in to an empty profile and sees "Add your first car". Before they bother exporting and importing
their own trips they should be able to see what the app does with years of data. So the first-car screen offers a
**demo car**: a made-up but believable German BMW with about 3.5 years of trips, maintenance and expenses that fills every
page (Overview, Trips incl. fuel/DPF, Statistics, Oil intervals, Maintenance, Expenses, Cost of ownership).

## Goals
- On "Add your first car", a second choice **"Try with a demo car"** creates the demo car in one click.
- While the current car is the demo, every page shows a slim banner: *"Demo car with made-up data."* with
  **Add your real car** and **Delete demo car** (confirm first).
- **Delete demo car** removes the car and everything attached (trips, expenses, tyre sets, import batches, documents).
- **Adding a real car removes the demo car automatically** (owner: "if they add a real car you can consider demo is not necessary").
- Works in Local and Hosted mode (the API does not care), but the UI entry point is the first-car screen, which is only
  shown to hosted profiles with no car.

## Non-goals
- No general "delete any vehicle" feature (only demo vehicles can be deleted through the new endpoint).
- No demo documents (no files on disk).
- No way to re-create the demo once a real car exists (the first-car screen is gone by then). The API refuses a second demo.
- No settings or UI to tune the demo.

## Anti-goals
- The demo must never touch real data: the auto-removal and delete only ever hit vehicles flagged as demo of the current user.
- Data must not look broken: no overlapping trips, odometer never goes backwards, no dates after today, fuel use and
  refuels agree, charts have no wild outliers.

## The demo car (owner: Germany, BMW; the rest were Claude's guesses, shown to the owner and accepted)
- Name `Demo · BMW 320d Touring`. Fuel type `DieselMhev`, currency `EUR`, time zone `Europe/Berlin`.
- Car details: model `BMW 320d Touring (G21)`, engine `2.0 diesel 48V mild hybrid, 140 kW`, trim `M Sport`, model year =
  purchase year, Automatic, AWD (xDrive), a handful of equipment items, colour e.g. `Mineral White`, interior `Black Vernasca leather`.
- **Bought new** ~3.5 years before the day the demo is created (purchase date = today minus 42 months, day of month kept, clamped).
  History start = purchase date @ ~10 km. Purchase costs (category Purchase): car ~€58 900, registration/plates ~€180.
  Estimated resale value ~€31 000 dated today. No financing.
- **Home**: a Munich suburb (e.g. Unterhaching); **work**: Munich centre. Addresses are generic German street addresses in
  real towns (e.g. `Hauptstraße 14, 82008 Unterhaching`); no real businesses or people.
- **Driving, ~17 000 km/year:**
  - Weekday commute ~25 km each way, partly Autobahn, ~30–40 min, mostly 07:00–08:30 / 16:30–18:30; some home-office days,
    public holidays (Bavaria) and ~25 vacation days a year off.
  - Short trips (2–8 km): shopping, gym, kids, mostly evenings/weekends; enough to make DPF health dip at times.
  - Weekend trips: occasionally 60–150 km (lakes, mountains).
  - **Summer holiday** each year: Munich → Lake Garda / Verona or Croatian coast via Austria (A8/A93/Brenner), split into legs
    with fuel/rest stops of 20–45 min every 2.5–3.5 h, ~1 week there with local trips, then back.
  - **Christmas**: a drive to family (e.g. Hamburg or Cologne, 600–800 km) and back.
- **Trips** look like the Volvo export rows the app already stores: Category `Private`, start/end time, start/end
  odometer (integers, continuous), start/end address, duration, distance (0.1 km), fuel litres (0.1 L).
  Consumption realistic for a 320d xDrive: cold short trips 8–11 L/100, commute 5.5–6.8, Autobahn 6.5–7.8, holiday legs
  ~6.5; winter +8–12 %. Overall average ~6.2–6.8 L/100.
- **Fuel expenses**: a full-tank refuel (59 L tank) when the tank is down to roughly 10–20 % (or before a long leg), litres = fuel used since
  the last fill (±small noise), FullTank true, odometer set, station names generic (`Aral`, `Shell`, `Esso`, `JET`, `OMV` in Austria,
  `Eni` in Italy) with a town as Place. Diesel price per litre follows the date: 2023 ~€1.70–1.85, 2024 ~€1.62–1.75,
  2025 ~€1.58–1.70, 2026 ~€1.60–1.72; Austria ~5 % less, Italy ~5 % more.
- **Maintenance** (category Maintenance with WorkJson + flags, like real records; place = a BMW dealer in Munich, generic name like `BMW Service München-Süd`):
  - BMW service every ~12 months or ~25 000 km whichever first: oil (0W-20 / Original), oil filter; air + cabin filter every
    second service; €320–480.
  - Brake fluid at 3 years (then every 2): ~€120 (with the 3rd-year service).
  - Front brake pads (+discs) once at ~50–60 000 km: ~€550.
  - HU (TÜV) at 36 months as a custom work item "HU/AU (TÜV)" ~€140 (if within the history).
  - Tyre swaps every late October and mid April at a tyre shop (~€60–80, TyreSwap with the tyre set fitted).
  - Tyre sets: summer set that came with the car (Michelin Primacy 4, 225/45 R18) fitted at purchase; winter set
    (Continental WinterContact TS 870, 225/45 R18, bought with rims ~€1 450 under category Tyres) in the first October;
    the summer set is replaced by a new one (NewTyres, ~€780) around year 3.
  - Odd items: front wiper blades once (~€45), a 12 V battery is NOT needed (new car).
- **Other expenses**: insurance yearly (Haftpflicht + Vollkasko, ~€780–920, covers a year), Kfz-Steuer yearly (~€232; category
  TollsVignette as road tax is merged there), Austrian 10-day vignette + Brenner toll + Italian/Croatian motorway tolls on
  holidays, car washes ~monthly (€8–18), parking (city, ~2–4 a month, €2–15), AdBlue top-ups every ~10 000 km
  (€15–25, category Other), a fine or two is fine but optional.
- Volume: ~4 000–5 000 trips and ~250–350 expenses in total.

## How it is built (Claude's calls)
1. **Core: `DemoData.Generate(DateOnly today, int seed = 2026)`** in `DriverBalance.Core` (new folder `Demo`) — a pure,
   deterministic function returning a `DemoCar` record: vehicle fields (name, fuel type, currency, time zone, purchase date,
   history start, resale value + date, details as plain fields), tyre sets, trips, expenses (with WorkJson work items and a
   tyre-set reference by index/key, not id). Uses `System.Random(seed)`; the same `today` gives the same data. Times are
   local (Europe/Berlin) wall-clock like the Volvo import.
2. **Data: `VehicleRow.IsDemo` (bool, default false)** + EF migration `DemoVehicle`. `VehicleDto` exposes `isDemo`.
3. **Web: `DemoCarService`** writes a generated `DemoCar` for the current user in one transaction (vehicle, tyre sets,
   then trips and expenses with real tyre-set ids, initial tyre set = the summer set, an ImportBatch row "Demo data" for the trips).
   The demo becomes the default vehicle only if the user has no other car.
4. **Endpoints:**
   - `POST /api/vehicles/demo` → 201 with the vehicle DTO. 400 if the user already has a demo car or any real car
     (keeps "demo is for empty profiles" simple).
   - `DELETE /api/vehicles/{vid}` → 204; 400 `"Only the demo car can be deleted."` for non-demo vehicles; 404 for
     another user's/unknown vehicle (existing loader behaviour). Deletes expenses first (they restrict tyre sets), then the
     vehicle (cascades), then its document folder. If the deleted car was default and another exists, the lowest-id one becomes default.
   - `POST /api/vehicles` (existing, real car): after creating, delete all demo vehicles of the user (same deletion
     code) and make the new car the default.
   - CSV import into the demo car is allowed (no special case).
5. **UI:**
   - `FirstCar`: under the form a divider "or" and a secondary button **Try with a demo car** with one line of text:
     "A made-up BMW with 3½ years of trips, services and costs. Delete it any time; adding your real car removes it."
   - `DemoBanner` above the page content when the current vehicle `isDemo`: text + **Add your real car** (opens the
     FirstCar form in a modal/drawer with a Cancel; on success the vehicle list refreshes and the new car is selected) +
     **Delete demo car** (confirm dialog "Delete the demo car and all its data?"). After delete, the vehicle list refreshes;
     with no cars left the first-car screen shows again.
6. **Performance**: generation + insert should take < 3 s on the F1 plan; insert with `AddRange` and one `SaveChanges`
   per table (or chunks), no per-row round trips.

## Tests (builder decides details)
- Core: deterministic; trips ordered, non-overlapping, odometer continuous and monotonic; no date after `today`; first trip
  on/after purchase date; yearly km 15–19 k; overall L/100 6.0–7.0; refuel litres ≈ fuel burned (±3 %); services spaced ≤ 12.5
  months / ≤ 26 000 km; tyre swaps in Oct/Nov and Mar/Apr–May; every expense category above present; WorkJson parses.
- Web API: create demo (201, isDemo, trips/expenses present, stats endpoints return data); second demo refused; demo refused
  when a real car exists; delete demo removes all rows; delete non-demo refused; another user can't delete it; creating a real
  car removes the demo and makes the real car default; the analytics endpoints (overview, ownership, intervals, maintenance) do not
  error on the demo car.
- Web (vitest): FirstCar shows the demo button and posts to `/vehicles/demo`; banner shows only for a demo vehicle and its
  delete asks for confirmation.
- A headless browser smoke check of the demo car on the published build before handing over (see the memory lesson).
