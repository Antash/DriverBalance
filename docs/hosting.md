# Hosting DriverBalance (invite-only, Google sign-in)

Status: **sign-in is built; deployment is not.** The app can run in `Hosted` mode (Google sign-in, invite list, admin page,
per-user data, account export and delete). Not done yet: Azure Blob storage for documents, the daily backup, per-user analysis
parameters, and the GitHub Actions deploy (see `docs/superpowers/specs/2026-11-09-public-service-design.md`). Until then a hosted
copy keeps documents on the app's disk (`DriverBalance__DocumentsPath`), which is fine for a few people but limited by the plan's disk.

You do the Google and Azure steps yourself; nothing here needs a secret to be pasted into a chat or committed to git.

## 1. Google sign-in (free)

1. <https://console.cloud.google.com> → create a project (or use the one you already have for the family assistant).
2. **APIs & Services → OAuth consent screen**: user type *External*, app name `DriverBalance`, your email as support/developer
   contact. Scopes: the defaults `openid`, `email`, `profile` are enough (no sensitive scopes, so no Google review is needed).
   Publishing status: **In production**, otherwise only listed test users can sign in and Google expires their grants after 7 days.
3. **Credentials → Create credentials → OAuth client ID** → type *Web application*.
   Authorised redirect URI: `https://<your-app>.azurewebsites.net/signin-google` (exactly this path; for a local try-out also
   `https://localhost:<port>/signin-google`).
4. Copy the client id and secret into the app settings below. Never commit them.

## 2. The app (Azure App Service F1, free)

Create an App Service on the **F1** plan, Linux, .NET 10 runtime, **HTTPS only** on. One instance only (SQLite is a single-writer database).
F1 sleeps after about 20 minutes idle (first request after that takes 10 to 20 seconds) and has 60 CPU-minutes a day.

App settings (Azure: Configuration → Application settings; the double underscore is the `:` of the setting name):

| Setting | Value |
|---|---|
| `DriverBalance__Mode` | `Hosted` |
| `DriverBalance__AdminEmail` | your Google address (you are always allowed in, no invite needed) |
| `DriverBalance__Google__ClientId` | from step 1 |
| `DriverBalance__Google__ClientSecret` | from step 1 |
| `DriverBalance__DatabasePath` | `/home/data/driverbalance.db` (`/home` is persistent) |
| `DriverBalance__DocumentsPath` | `/home/data/documents` |
| `AllowedHosts` | `<your-app>.azurewebsites.net` (the default only allows localhost, so without this every request is refused) |

Hosted mode **refuses to start** if the Google client id, secret or the admin email is missing; the log names what is missing.
Session cookies are protected with keys stored next to the database (`/home/data/keys`, or `DriverBalance__KeysPath`), so sign-ins survive restarts and redeploys.
Deleting that folder signs everybody out.

## 3. First sign-in and moving your data

1. Deploy (publish output of `src/DriverBalance.Web`; the web UI is built into it).
2. Back up your laptop database first. Then, with the app stopped, copy it to `/home/data/driverbalance.db` through the App Service's Kudu/SSH console
   (and the laptop `documents` folder to `/home/data/documents`). The app applies the database migrations on start.
3. Open `https://<your-app>.azurewebsites.net` and **Sign in with Google** using the admin address. Your first sign-in takes over user 1,
   so all your cars, trips and expenses are yours on the hosted copy. Names and addresses of other people are never needed.
4. **Settings → API tokens**: create new tokens for the Volvo sync tool and the family assistant. Tokens from the uploaded database should be deleted (they would work, but you should re-issue them on purpose).

## 4. Inviting people

Sign in as admin, open **Admin**, type their Google address under **Invites**, and send them the app's link yourself.
They sign in with that Google account and get an empty profile (they add a car, then import their trip export).
On the first-car screen they can also try a demo car with made-up data, then delete it or add their real car.
Anyone else who signs in with Google sees "This service is invite-only for now" and nothing about them is stored.

- **Remove an invite that was not used**: it just disappears.
- **Remove a used invite ("Block")**: the person is signed out within 5 minutes and cannot sign in again; their data stays until you delete the user.
- **Delete a user**: removes their cars, trips, expenses, documents and invite. You cannot delete the admin account (neither here nor under **Account**).
- People can export their data and delete their own account under **Account**, and read the **Privacy** page.

## 5. Notes

- API tokens (`Authorization: Bearer dbk_...`) work for `/mcp` and `/api` and belong to one user; they are never admin.
- The local setup (`DriverBalance__Mode` unset or `Local`) is unchanged: loopback requests are user 1, no Google needed.
- Behind the App Service proxy the app trusts `X-Forwarded-Proto`/`For` so that redirect addresses and `Secure` cookies use `https`.
