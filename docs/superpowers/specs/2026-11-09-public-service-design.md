# DriverBalance as an invite-only public web service — design

Date: 2026-11-09. Branch: `feat/web-app` (builds on everything there).

## Intent

DriverBalance today is a single-user app on the owner's laptop: every request from localhost is signed in as user 1.
The owner wants it to become a public web service that invited people sign in to with Google, add their own cars,
import their trip exports and see the same stats the owner sees. The owner keeps his extras (the Volvo sync tool,
the MCP/bot connection) working against the hosted service with his own API token.

## Goals

1. **Google sign-in.** Users sign in with their Google account (OpenID Connect). The design leaves room for more
   providers later, but only Google ships now.
2. **Invite-only.** Only email addresses on an invite list can create a profile. Others who sign in with Google see
   "This service is invite-only for now" and nothing is stored about them.
3. **Profiles own their data.** Each user sees and changes only their own cars, trips, expenses, maintenance, tyres,
   documents, import batches and API tokens. (Rows are already user-owned through `IUserOwned` and a global query filter;
   this design relies on that and verifies it.)
4. **Analysis parameters belong to the user, not the car.** One parameter set per user, applied to all their cars.
   A new user starts with `new AnalysisParameters()` — today's code defaults, which equal the owner's current stored values.
   The Parameters panel edits the user's set.
5. **Admin.** The owner is the admin. An Admin page lets him add and remove invited emails, see users (email, name,
   created, last sign-in, number of cars) and delete a user with all their data.
6. **Privacy basics for a public service** (trip exports contain start/end addresses):
   - "Export my data" (a ZIP of the user's data as JSON and the original documents) in Settings.
   - "Delete my account" in Settings: removes the user, all their rows and documents, after typing a confirmation.
   - A short privacy page linked from the sign-in page and the footer: what is stored, why, who can see it (only the
     user and, technically, the admin as operator), how to export and delete.
7. **Hosting on Azure App Service F1** (free), deployed from GitHub Actions.
   - The address is `https://<app-name>.azurewebsites.net` (HTTPS included; no custom domain on F1).
   - The SQLite database lives on the app's persistent storage (`%HOME%\data\driverbalance.db`). F1 runs a single
     instance, so SQLite's single-writer model is fine.
   - Documents (receipts, orders) go to Azure Blob Storage, not the 1 GB app disk.
   - A daily database backup is copied to Blob Storage (keep the last 14).
8. **The owner's data moves to the hosted service** as his Google-signed-in profile (user 1 becomes his account).
9. **Local development keeps working:** the laptop setup (localhost, auto sign-in as user 1, local document folder)
   stays as a development mode.

## Non-goals (for now)

- Sharing a car between users.
- Sign-in providers other than Google; email/password accounts.
- Open self-service sign-up (later: switch off the invite check).
- Sending invite emails (the owner tells people the link himself).
- Hosting the Volvo sync tool or the bot for other users; other users import trip files by hand on the Import page.
- Car makes other than Volvo's trip CSV format (other exports can be added later as import formats).
- Azure SQL / Postgres. Move off SQLite only when it is outgrown.
- A custom domain (needs the paid B1 plan).

## Anti-goals (would make this a failure even if it works)

- One user seeing or changing another user's data through any route: API, MCP, documents, exports, analytics.
- The auto sign-in ("localhost = user 1") being active on the public site.
- Losing the owner's existing data in the move.
- Secrets (Google client secret, storage keys) in git or in the front-end bundle.

## Constraints

- .NET 10 minimal API, EF Core SQLite, React + TS + Vite, as today. Keep the existing test suites green and extend them.
- F1 limits: 60 CPU-minutes/day, 1 GB disk, no always-on (the app sleeps after ~20 min idle; a cold start takes 10–20 s).
  Avoid background work that keeps the app awake; the daily backup runs on the first request after the backup is due
  (or from a scheduled GitHub Action calling a protected endpoint — builder's call).
- Real data never goes into git. The owner approves merges, pushes and anything that publishes to Azure.
- Back up the real database before any data migration.

## How it works — decided

### Sign-in
- Cookie authentication for the browser (`HttpOnly`, `Secure`, `SameSite=Lax`), with Google OpenID Connect as the
  challenge. Sessions last 30 days, sliding.
- The user is matched by the Google subject (`sub`) claim, stored on the user row together with the email, display
  name and picture URL. On the first sign-in the email must be on the invite list (case-insensitive); the invite is then
  marked used and linked to the new user.
- Unauthenticated browser requests to the SPA show a sign-in page; API requests get 401 (the SPA redirects to sign-in).
- API tokens (Settings → API tokens, `Authorization: Bearer`) keep working for the MCP endpoint and the API, scoped to the
  token's user — this is how the owner's Volvo sync tool and bot reach the hosted service. The sync tool gets a
  `--token` option (or reads it from its credential store) and an `--app https://….azurewebsites.net` URL.
- The existing CrossSiteGuard stays; it must accept the hosted origin.

### Modes
- `DriverBalance:Mode = Local` (default when running on the laptop): today's behaviour — auto sign-in as user 1 from
  loopback, local document folder, no Google needed.
- `DriverBalance:Mode = Hosted`: Google cookie auth + API tokens only; the LocalUser scheme is not registered at all.
  Hosted mode refuses to start if the Google client id/secret, the admin email or the blob connection are missing.

### Admin
- The admin is whoever signs in with the email in configuration `DriverBalance:AdminEmail` (an app setting in Azure,
  not stored in the database). The admin email is always allowed to sign in, invite or not.
- Admin page (only shown to the admin): invites (add/remove email, shows used/unused), users (list, delete with
  confirmation). Admin endpoints under `/api/admin`, 403 for everyone else.

### Data model changes
- `UserRow`: add `GoogleSubject` (unique, nullable for the local user until linked), `Email`, `DisplayName`,
  `PictureUrl`, `LastSignInAt`, `ParametersJson`.
- `InviteRow`: `Email` (unique, lower-case), `CreatedAt`, `UsedByUserId`, `UsedAt`.
- Migration moves parameters from the vehicle to the user: each user's `ParametersJson` = their default vehicle's
  parameters (or code defaults); `VehicleRow.ParametersJson` is dropped (or ignored, builder's call).
  The parameters endpoints move to `/api/me/parameters`; the analysis loader reads the owner's parameters.
- `/api/me`: who is signed in (name, email, picture, isAdmin) for the header (avatar + sign-out menu).

### Moving the owner's data
- The owner's laptop database is uploaded to the hosted storage once (manual step, documented).
- User 1 is linked to the owner's Google account on his first hosted sign-in: if the signing-in email equals `AdminEmail`
  and user 1 has no `GoogleSubject`, user 1 takes that subject instead of a new user being created.

### Documents
- `DocumentStore` gets two implementations behind one interface: the local folder (Local mode) and Azure Blob
  Storage (Hosted mode, one container, blob names prefixed by user id). Uploads, downloads and deletes go through the
  owner check as today.

### Deploy
- GitHub Actions workflow: on push to a `release` branch (and manual dispatch) build the web UI, run all tests,
  `dotnet publish`, deploy to the App Service with a publish profile stored as a GitHub secret.
- App settings in Azure: Mode, Google client id/secret, AdminEmail, blob connection string, database path.
- A short `docs/hosting.md`: create the Google OAuth client (redirect URI `https://<app>.azurewebsites.net/signin-google`),
  create the App Service F1 and the storage account, set the app settings, upload the database, first sign-in.
  The owner does the Google console and Azure portal steps himself; Claude never handles the secrets.

## Calls made by Claude (owner to confirm while reading)

1. **Access is checked on every request, not only at first sign-in.** The cookie's user is re-validated (exists, not
   blocked) at most every 5 minutes. Removing a *used* invite blocks that user: they are signed out and cannot sign in
   again, their data stays until the admin deletes it. Deleting a user (by the admin or by themselves) also removes
   their invite, so they can only come back if invited again — with an empty profile.
2. **The admin cannot delete his own account**, neither from the Admin page nor from Settings (the button is disabled
   with an explanation). His data can still be exported.
3. **Linux App Service F1**, .NET 10 runtime; the database lives at `/home/data/driverbalance.db`. `/home` is network
   storage, so SQLite runs in the default rollback-journal mode (no WAL) with a busy timeout; one instance only. The
   repo has no native image libraries, so Linux is safe. The one-off database upload and any restore go through the
   App Service's Kudu/SSH console, documented in `docs/hosting.md`.
4. **Limits per user on the hosted service:** documents up to 10 MB each (as today) and 200 MB in total per user;
   trip import files up to 20 MB. Clear error messages when exceeded.
5. **After the move the hosted service is the source of truth.** The laptop database becomes a development copy;
   Local mode keeps pointing at it. Nothing syncs back.
6. **API tokens are re-issued on the hosted service.** After his first hosted sign-in the owner creates a new token in
   Settings and gives it to the sync tool and the bot; tokens in the uploaded database are revoked as part of the move.
7. **Isolation audit is part of the plan:** every unique index, de-duplication key (trip keys, import file hashes,
   VINs), cache and file path is checked to be per user, with a two-user API test for each data type proving user B
   cannot read, change or collide with user A's data.

## Left to the builder
- Library choices within ASP.NET Core (e.g. `Microsoft.AspNetCore.Authentication.Google` vs generic OpenID Connect).
- Sign-in page and Admin page layout, following the existing UI style.
- Exact export ZIP layout.
- How the daily backup is triggered within the F1 limits.
- Whether `VehicleRow.ParametersJson` is dropped or left unused.
