# Trip list filters and regular routes — design

Date: 2026-11-14. Branch `feat/trip-filters` (from master 3181090, which already has the trip cost feature).
Status: agreed with the owner in chat; route naming deliberately left for later.

## Why

The owner wants to answer two kinds of question from the Trips page:
1. "What did my long trips cost?" — a quick **minimum distance** filter on the trip list.
2. "What does my regular commute / school run cost?" — **recurring routes** found automatically, with what they cost
   per trip and per month. People's patterns differ (straight A ↔ B, stops on the way, varying errands), so the design
   must not assume one commute shape.

## Part 1: "Longer than X km" filter (Trips → List)

- A slider above the list with the steps **0, 5, 10, 20, 50, 100, 200, 500 km** (0 = no filter). Label: `Longer than 20 km`
  (at 0: `All distances`).
- It follows the page's existing **Group / Trip** switch (today the List tab ignores it and always shows groups):
  - **Group**: drives (trip groups, one cold start each) whose total distance is ≥ X km; rows expand into their trips as today.
  - **Trip**: single trips with distance ≥ X km in a flat list (no expand control). Each row shows the trip's own score
    (`Scoring.Score` of that trip), its route and its own cost.
- A **summary line** above the table for everything that matches (all pages, not just the shown one):
  `38 drives · 6,240 km · ≈ 31,900 CZK` (Trip level: `trips`). The cost total is the sum of the rows' `cost.total`.
- It respects the page's period switch (Total / Last 12 months / each year), like the list does today.
- Filtering, paging and the summary happen on the server so totals stay right.
- The slider value goes into the URL (`minKm=20`) with the rest of the Trips page state; an invalid value means 0.

## Part 2: Regular routes

A new **Routes** tab on the Trips page (after Driving style, before List). It follows the period switch and the Group / Trip switch.

### What a route is
- Addresses are matched the way `PlacesAnalysis` already does (trim, collapse whitespace, case-insensitive; the most
  common spelling is displayed). No geocoding; slightly different spellings of one place stay different places (known limitation).
- **Trip level**: a route is the pair of a trip's start and end address. **Both directions are one route**: Home → Work
  and Work → Home are `Home ↔ Work`.
- **Group level**: a route is the group's first start and last end address plus its **stops** (the end addresses of all but the
  last trip, in order). Both directions are one route when the stops are the same in reverse order: Home → Work via School and
  Work → Home via School are one route `Home ↔ Work · via School`, while a direct Home → Work drive is a separate route
  `Home ↔ Work`. A group whose first start equals its last end (a round trip, e.g. Home → Shop → Home) is shown as
  `Home ↺ · via Shop`.
- A trip or group with an empty start or end address is never part of a route.
- Only routes driven **at least 5 times** in the period are listed (a constant, not a setting). Trips that never repeat
  stay in the list only.

### The Routes table
Most driven first (then more km). Columns:

| Column | Meaning |
|---|---|
| Route | `A ↔ B`, with ` · via S1, S2` for group stops (each address shown in full; the cell wraps) |
| Drives (or Trips) | how many times it was driven in the period |
| Typical | when it is driven: `Weekdays` if ≥ 80 % of starts are Mon–Fri, `Weekends` if ≥ 80 % are Sat–Sun, else `Any day`; then the one or two most common start hours, e.g. `Weekdays · 7–8, 16–17` (an hour shows only if it has ≥ 20 % of the starts) |
| Avg km | mean distance |
| ≈ per drive | mean `cost.total` |
| ≈ per month | the route's total cost ÷ months in the period. Months = elapsed days of the period (clipped to today, and to the first recorded trip for Total) ÷ 30.44 |
| Share | the route's total cost ÷ the total cost of all drives (or trips) in the period, as a % |

- Costs use the existing trip cost (`TripCosts`), so they match the List tab and the Overview.
- **Clicking a route** opens the List tab filtered to that route (URL `route=<key>`), with a chip above the list:
  `Route: Home ↔ Work ✕` (✕ clears it). The km slider and summary line still apply; the summary then reads like
  `212 drives · 3,900 km · ≈ 13,100 CZK` — the answer to "what does my commute cost".
- Hidden when empty: if no route reaches 5 drives, the tab shows one muted line
  `No regular routes yet: a route shows once it has been driven 5 times in this period.`

### Route key
An opaque string the server builds from the normalized addresses (both directions map to the same key) and returns with
each route; the client only passes it back. The trip list endpoint filters by it at the requested level. A key from Group
level used with Trip level (or a stale key) simply matches nothing.

## API (my calls; the builder may adjust names but keep the behaviour)

- `GET /vehicles/{vid}/trips` gains `level=Group|Trip` (default Group), `minKm` (≥ 0, default 0) and `route` (key) and
  returns, besides today's fields, `summary: { count, km, cost, currency }` over all matching items. At Trip level each
  item is a one-trip group whose `score` is that trip's own score (so the existing `TripPage`/`TripGroupView` shape and
  the UI rows are reused). Existing callers that pass none of the new parameters get exactly today's result plus `summary`.
- `GET /vehicles/{vid}/trips/routes?from&to&level` → `[{ key, from, to, via: string[], roundTrip, count, typical,
  avgKm, avgCost, perMonth, share, currency }]` (money rounded to 2 decimals, share 0–1).
- The old unused `charts/routes` endpoint and `PlacesAnalysis.Routes` may be reused or replaced; the Places/Routes charts
  stay unused by the UI.

## Builder notes (my calls, from the review)

- **Item sets:** Group level = `AnalysisContext.GroupsIn(period)`; Trip level = the trips of those groups, each filtered by its
  own start date (the same source the score map uses at Trip level). `Trip.Category` is ignored, as everywhere else. The list,
  the summary, the routes and the Share denominator all use the same item set for the chosen level and period.
- **Cost:** a group's cost is already the sum of its trips' costs (`TripCosts.Of(TripGroup)`); nothing in `TripCosts` changes.
- **Stops (normal form):** the end addresses of all but the last trip, normalized; consecutive duplicates collapse to one and a
  stop equal to the route's start or end address is dropped (e.g. Home → Work → Work has no stop). The key uses the stop
  sequence or its reverse, whichever sorts first, so Home → Shop → Mall → Home and Home → Mall → Shop → Home are one round trip.
- **Display direction:** the endpoint that is the start more often is shown first; ties alphabetical. Stops are listed in the
  order of that direction.
- **Exact matching only:** the `route` filter matches whole trips (Trip level) or whole groups (Group level), never a route
  inside a longer group. A route key that is not in the current Routes table (fewer than 5 drives in this period, or another
  level) still filters the list; it may match a few drives or none.
- **Months for "per month":** days from the later of the period start and the first recorded trip to the earlier of the
  period end and today (inclusive), ÷ 30.44; this applies to every period, not only Total.
- **Time:** `Trip.Start` is local time (the Volvo journal's), as in the existing driving-style charts; weekday and hour use it as is.

## Non-goals (this round)

- Naming routes (planned next), route charts, routes on the Overview, routes in the MCP tool / bot.
- Geocoding or fuzzy address matching.
- A km filter on charts or other tabs.

## Tests

- Core: route building at Trip level (both directions merge; empty address skipped; < 5 drives dropped), at Group level
  (direct vs via-stop are separate routes; reversed stops merge; round trip), `typical` text (weekday / weekend / any day,
  hour threshold), per-month and share arithmetic on made-up data.
- Web API: `minKm` at both levels, `route` filter, `summary` totals across pages, defaults unchanged for old callers,
  routes endpoint shape; another user's vehicle → 404.
- vitest: slider steps and URL round-trip, Trip-level flat rows, summary line text, route chip and clearing it, Routes
  table rendering and the empty line.
- Made-up addresses only (the repo is public).
