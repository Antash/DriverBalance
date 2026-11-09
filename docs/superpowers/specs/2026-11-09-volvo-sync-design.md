# Volvo trip sync tool (on demand)

Date: 2026-11-09. Status: approved by the owner in chat ("do whatever you need"; on-demand now, scheduling later).

## Why
Trips come only from the Volvo website's journal export (no trip API). The owner wants a separate tool that logs in,
exports the journal and sends the CSV to the app's existing import API. Run on demand for now; a daily schedule and
reading the e-mailed login code automatically come later.

## Recon (done 2026-11-09 in a real Edge session)
- Journal page: `https://www.volvocars.com/cz/account/my-cars/<carId>/journal-log/` (owner's carId
  `TLM4hCH902dywPGz5k2eVYi3n4M5VWPdrGI3p94BpMQ`; the owner thinks the id may change — the tool must discover it from
  `https://www.volvocars.com/cz/account/my-cars/` by finding the link that ends in `/journal-log/`, and only fall back to
  a configured id).
- Not signed in → redirect to `https://auth.volvocars.com/...` (Keycloak "Volvo ID"): e-mail field, password field
  (`input[type=password]`), "Pamatovat si mě" (remember me) checkbox, submit "Přihlásit se". The OIDC request asks for
  `acr_values=urn:volvoid:aal:bronze:2sv`, so a one-time code sent by e-mail may be requested after the password.
- The page opens on the last month. Date filters are native inputs `input[name=filter-start-date]` and
  `input[name=filter-end-date]` (type=date, value `yyyy-MM-dd`). Setting the value with the native setter and dispatching
  `input`/`change` reloads the list via a server action and updates the URL to `?startDate=…&endDate=…`. Putting those
  parameters in the URL directly is ignored.
- Export: button "Exportovat jízdy" opens a side dialog (interval + categories Soukromé/Firma/Nepřiřazeno, all included
  by default) with "Exportovat vybrané (N)"; clicking it downloads `volvo-journal-log.csv` (UTF-8 BOM, Czech headers,
  dates like `08. 11. 26 18:59`, numbers with NBSP thousands separators).
- The app's `POST /api/vehicles/{vid}/import/csv` (multipart field `file`, header `Origin: <app origin>`) imports this
  file unchanged: a test import gave 52 new trips and 12 duplicates skipped. `GET /api/vehicles/{vid}/import/last-trip`
  → `{ lastTrip: "yyyy-MM-dd" | null }`. The local API needs no token (local user); the CrossSiteGuard needs the Origin.

## The tool
- New console project `tools/DriverBalance.VolvoSync` (.NET 10), added to the solution, using **Microsoft.Playwright**
  with the installed Edge (`Channel = "msedge"`, no browser download) and a **persistent profile** at
  `%LOCALAPPDATA%\DriverBalance\volvo-browser` so the Volvo session survives between runs.
- Commands:
  - `sync` (default): headless. From = `--from` or (app last trip − 2 days) or (today − 90 days); To = today.
    Open the journal; if redirected to login, sign in (see below); set the start date (and end date = today), wait for
    the list to reload, open the export dialog, check its interval matches, click "Exportovat vybrané", capture the
    download, keep a copy in `%LOCALAPPDATA%\DriverBalance\volvo-exports\volvo-journal-<from>_<to>.csv`, POST it to
    the app, print the import result (imported / skipped / first / last trip / warnings). Exit 0 on success.
  - `login`: headed. Opens the journal so the owner can sign in by hand (remember me ticked); waits until the journal
    page is shown, then exits. Use when the stored session expired and no credentials are stored or login needs help.
  - `set-credentials`: prompts for the Volvo e-mail and password (password not echoed) and stores them encrypted with
    Windows DPAPI (current user) in `%LOCALAPPDATA%\DriverBalance\volvo-credentials.bin`. `clear-credentials` deletes it.
- Sign-in in `sync`: with stored credentials fill e-mail + password, tick remember me, submit. If a code page appears,
  prompt on the console for the code the owner received by e-mail (later: read it from Gmail). Without stored
  credentials, or when headless sign-in fails: exit with a clear message to run `login` (or `set-credentials`).
- Options: `--app http://localhost:5180`, `--vehicle 1`, `--from yyyy-MM-dd`, `--headed`, `--keep-open` (debug),
  `--car-id <id>`.
- Robustness: every wait has a timeout with a clear error that names the step; on failure save a screenshot to
  `%LOCALAPPDATA%\DriverBalance\volvo-exports\failure-<timestamp>.png`. Never print the password or cookies.
- Tests: unit tests for the pure parts (date range from last trip, file naming, credential store round trip with DPAPI,
  argument parsing) in a new test project or the Web tests; the browser flow is verified live by the controller.
- README section "Syncing trips from Volvo" with the commands.

## Out of scope (later)
- Scheduling (Task Scheduler / daily) and reading the e-mail code from Gmail automatically.
