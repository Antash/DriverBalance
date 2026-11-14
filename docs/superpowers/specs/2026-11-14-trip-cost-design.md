# Trip cost — design

Date: 2026-11-14. Branch `feat/trip-cost`. Status: agreed with the owner in chat ("agree. go for it").

## Why

The owner wants to see roughly what each trip and each trip group (one cold start) cost to drive. A trip's fuel economy
should show in its cost. The cost of owning the car (purchase, financing) is out of scope; this is about the running
cost of a trip.

## The model (owner-approved)

Trip cost = **fuel** + **time share** + **wear share**, all in the vehicle's home currency.

1. **Fuel** = the trip's litres × the average price of 1 L in a **3-month window centred on the trip's start date**
   (start date − 45 days to start date + 45 days, inclusive).
2. Non-fuel running costs fall into two pools, valued over a **12-month window centred on the trip's start date**
   (−182 to +182 days, inclusive):
   - **Time pool** (costs of having the car available): Insurance, TollsVignette, Parking, Wash, Accessories, Fines,
     Other. Spread by **driving time**.
   - **Wear pool** (costs that come with use): Maintenance, Repair, Tyres. Spread **half by km, half by driving time**.
3. A trip's shares:
   - time share = trip hours × (time pool ÷ hours driven in the window)
   - wear share = 0.5 × trip km × (wear pool ÷ km driven in the window) + 0.5 × trip hours × (wear pool ÷ hours driven in the window)
4. A **group's cost is the sum of its trips** (each trip uses its own windows).

Why driving time and not km (from the chat): a 15 km city commute and a 100 km motorway run can take the same time.
Per km the commute is the harder use (cold start, brakes, clutch, DPF). Pure km would charge the motorway run ~7× more.
Insurance and tax are paid to have the car at all; an hour of use is what they buy.

Worked example (made-up figures, to be used as a unit test): window with time pool 20 000, wear pool 10 000,
15 000 km and 300 h driven; the fuel part is left out here.
- Commute 15 km, 30 min: time 0.5 × 66.67 = 33.33; wear 0.5 × 15 × 0.6667 + 0.5 × 0.5 × 33.33 = 5 + 8.33 = 13.33; total ≈ 46.67.
- Motorway 100 km, 30 min: time 33.33; wear 0.5 × 100 × 0.6667 + 8.33 = 33.33 + 8.33 = 41.67; total ≈ 75.

## Details

- **What counts as cost in a window:** the same rules as `Money.For`. Purchase is never included. An expense with a
  coverage period (`CoversFrom`..`CoversTo`) counts pro rata by days overlapping the window. An expense in another
  currency without a home amount is skipped. Fuel is its own category and never enters the pools.
- **Driven km and hours in a window:** the sum over the recorded trips whose start date falls in the window
  (`Trip.DistanceKm`, `Trip.Duration`). They're not taken from the odometer, so costs are only spread over driving we have trips for.
- **Price of 1 L in the 3-month window:**
  - If any fuel expenses in the window have `Litres` > 0 and a home amount, the price = their amount ÷ their litres.
  - Otherwise the price = all fuel spend in the window (pro rata as above) ÷ litres burned by trips in the window.
    Most imported fuel records have no litres.
  - If neither gives a price (no fuel spend, or no litres), use the same rule over **all** data. If that also gives
    nothing, the fuel part is null.
- **Missing data:** if a window has no driving hours (or km), that rate is 0. In practice this can't happen, because the
  trip itself is in its window. The total is still shown when the fuel part is null, but marked incomplete
  (`fuel: null`).
- **Rounding:** money to 2 decimals in the API. The UI shows whole units plus the currency code.
- **Performance:** a vehicle can have thousands of trips. Build per-day totals once per analysis context (time-pool
  cost, wear-pool cost, fuel spend, fuel litres from expenses, fuel spend of expenses with litres, trip km, trip hours,
  trip litres), then use prefix sums so each window is O(1). The builder chooses the implementation, but the
  trip list must not get noticeably slower.
- Builder notes (my calls): the trip's litres are `Trip.FuelLiters`. The start date is `Trip.Start`'s date, with no timezone
  shift. The time and wear pool windows (and the km/hours behind their rates) are clipped to
  [first trip day, max(last trip day, Today)], so prepaid cover reaching past the last trip, with no driving to share it, does
  not inflate recent trips' shares; the fuel window is not clipped. Trips dated outside [Today - 100 years, Today + 1 year]
  are left out and get zero shares; a trip with 0 L gets fuel 0 even when no price exists.
  The trip GET endpoint has to open an analysis session to get the windows.
- Uses the trip grouping from the saved parameters, like the trip list already does. The cost of a trip doesn't depend
  on the grouping.

## Where it shows

My call (the owner didn't pick a placement):
- **Trips → List tab:** a **Cost** column on group rows and on expanded trip rows (e.g. `142 CZK`). Hovering a group's
  cost shows the split: fuel / time / wear.
- **Trip drawer:** a "Cost" section: fuel (litres × price per litre used), time share, wear share, total. One muted
  line explains the model: "Fuel at the local average price, plus a share of the other running costs over the
  12 months around the trip (by driving time, and by km for wear)."
- Not built now: cost charts, cost in the Insights card, MCP tools, a way to change the 50/50 or the windows.

## API

- `GET /vehicles/{vid}/trips` (trip groups page): each group gets `cost: TripCost`, and each of its trips gets `cost`.
  Add these without breaking the existing fields the UI and tests use.
- `GET /vehicles/{vid}/trips/{tripId}`: the trip gets `cost`.
- `TripCost = { fuel: number | null, time: number, wear: number, total: number, pricePerLitre: number | null, currency: string }`.
  For a group, `pricePerLitre` is null, `fuel` is the sum (null if any trip's fuel is null) and `total` is the sum of the trips' totals.

## Non-goals and anti-goals

- No ownership or depreciation in the trip cost.
- Nothing is stored. The cost is computed on read, so imports and new expenses change it at once.
- Don't touch the money stats, the Expenses page or the purchase/financing logic.
- Anti-goal: a number that looks exact. Label it "≈" and keep the explanation one hover or one line away.

## Tests

- Core: the worked example above; the fuel price from litres-bearing expenses vs the spend ÷ trip-litres fallback;
  a coverage-period expense counted pro rata; Purchase and a fuel expense kept out of the pools; the all-data price fallback;
  a group's cost equals the sum of its trips.
- Web: the trips page JSON has `cost` on groups and trips; the trip GET has `cost`.
- vitest: the List tab renders the cost column; the drawer renders the cost section.
