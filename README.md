# Sanathana Companion

A Hindu Dharma "spiritual companion" app. Two **standalone** applications that talk only over a REST API:

```
Application/
├── BackEnd/    Sanathana.Companion.sln            .NET 10 Clean Architecture Web API (repository + EF Core + PostgreSQL, JWT)
└── FrontEnd/   Sanathana.Companion.Frontend.sln   .NET 10 Blazor: shared UI rendered by a WASM web host and a MAUI mobile host
```

## Tech stack
- **API:** .NET 10, Clean Architecture (Domain / Application / Infrastructure / Api / Modules), repository + unit-of-work, EF Core + Npgsql, JWT bearer auth, FluentValidation, Serilog, Swagger.
- **Frontend:** .NET 10 Blazor. `App.Core` (models/services/auth), `App.UI.Shared` (shared Razor components + Hindu Dharma theme), `App.Web` (Blazor WebAssembly), `App.Mobile` (.NET MAUI Blazor Hybrid — **Android, iOS, Mac Catalyst** + Windows), `App.Tests`.
- **Database:** PostgreSQL on `localhost:5432`, database `sanathana_companion` (created + seeded automatically on first API run).

## Prerequisites
- .NET 10 SDK, `dotnet-ef` 10.x (`dotnet tool update --global dotnet-ef --version 10.*`).
- A running **PostgreSQL** on `localhost:5432`. Adjust credentials in `BackEnd/src/Sanathana.Companion.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`).
- For the mobile app: `dotnet workload install maui-android maui-ios maui-maccatalyst maui-windows`.
- **Android additionally needs JDK 21** — the .NET 10 Android SDK rejects anything else (`error XA0030`). Android Studio ships one at `%LOCALAPPDATA%\Android\jdk`; point the build at it rather than changing `JAVA_HOME`:

  ```bash
  dotnet build FrontEnd/App.Mobile -f net10.0-android -p:JavaSdkDirectory="$LOCALAPPDATA/Android/jdk"
  ```
- iOS/Mac Catalyst **compile** on Windows, but producing a runnable app, an `.ipa` or a store upload needs a Mac (Pair to Mac, or a macOS build agent).

## Run
1. **Backend:** `RunBackend.cmd` → API on `http://localhost:7050`, Swagger at `/swagger`. Migrations + seed apply automatically on startup.
2. **Web:** `RunFrontend.cmd` → `http://localhost:7001` (calls the API at `:7050`).
3. **Mobile (Android emulator):** `dotnet build FrontEnd/App.Mobile -t:Run -f net10.0-android` (API reached via `http://10.0.2.2:7050`).
   **Mobile (iOS simulator, on a Mac):** `dotnet build FrontEnd/App.Mobile -t:Run -f net10.0-ios`
   **Mobile (Windows):** `dotnet build FrontEnd/App.Mobile -t:Run -f net10.0-windows10.0.19041.0`

## The mobile app

`App.Mobile` is a MAUI Blazor Hybrid host: iOS and Android run the *same* Razor pages as the web,
so every feature exists on both without being written twice. What differs is the shell.

| | Web | Mobile |
|---|---|---|
| Layout | `MainLayout` — top bar + left drawer | `MobileLayout` — top bar + **bottom navigation** |
| Chosen by | `Routes.razor`, from `AppConfig.Platform` | same |
| Skin | `theme.css` + `app.css` | the same, plus `mobile.css` (frosted chrome, gradients, safe-area insets) |
| Navigation | full menu tree in the rail | first four leaves of the menu as tabs; the rest behind **More** |
| Notifications | a page | a **bell in the top bar** with a live "active right now" panel |
| Profile | `/profile` | `/profile`, plus an account sheet on the avatar (streak, region, language, theme) |

The bottom bar is **not hardcoded**. It is built from `GET /api/menumodules/menu?platform=Mobile`,
so publishing a form to mobile (Modules → *Show in mobile*) or reordering it changes the tabs with
no app release. A form appears on a phone only when its **`ShowInMobile`** flag is set — including
for Admin — and, for every other role, only when Access Rights also grants it the *Mobile* column.

### Configuration, in one place each

| Host | File | Notes |
|---|---|---|
| Mobile | `FrontEnd/App.Mobile/Resources/Raw/appsettings.json` | API URL per build configuration, app name, timeout. Read once at start-up by `MobileSettings.Load()`. Packaged, so changing it means a rebuild. |
| Web | `FrontEnd/App.Web/wwwroot/appsettings.json` | API URL and `Platform`. Docker rewrites `ApiBaseUrl` at container start. |
| Both | `FrontEnd/App.Core/Config/ApiRoutes.cs` | **Every REST path the clients call.** No URL string is spelled out anywhere else. |

Set `"Platform": "Mobile"` in the *web* `appsettings.json` to render the phone shell in a desktop
browser's device emulation — the quickest way to review mobile UI without a device.

## Run with Docker (single server, API under `/api`)

`docker-compose.yml` brings up the whole stack. nginx is the only thing exposed; it
serves the Blazor WASM files and reverse-proxies `/api` to the API container, so
the app and the API share one origin (no CORS involved).

```
browser ──► web (nginx) :8080 ──┬─ /      static Blazor WebAssembly
                                └─ /api/  ──► api (.NET 10) ──► Supabase PostgreSQL
```

```bash
cp .env.example .env      # then edit it — CONNECTION_STRING and JWT_SECRET are required
docker compose up -d --build
```

Then open **http://localhost:8080**. The API is at **http://localhost:8080/api**.

### Supabase: use the session pooler, not the direct host

The direct host `db.<project-ref>.supabase.co` resolves to an **IPv6 address only**.
Docker's default bridge network is IPv4-only, so a container using it fails with
`Network is unreachable`. Take the **Session pooler** string instead —
*Dashboard → Project Settings → Database → Connection string → Session pooler* — which
is IPv4-reachable:

| | Direct (won't work in Docker) | Session pooler (use this) |
|---|---|---|
| Host | `db.<ref>.supabase.co` | `aws-<n>-<region>.pooler.supabase.com` |
| Port | 5432 | 5432 |
| Username | `postgres` | `postgres.<ref>` |

Stay on the **session** pooler (5432), not the transaction pooler (6543): EF Core runs
migrations on startup and Npgsql's prepared statements don't survive transaction mode.

The alternative fixes are Supabase's paid IPv4 add-on, or enabling IPv6 on the Docker
network — which also requires working IPv6 egress on the host.

No extra NuGet package is needed for this; `Microsoft.Extensions.Configuration.Json` is
already part of the ASP.NET Core host, and the connection string is supplied as the
`ConnectionStrings__DefaultConnection` environment variable rather than a config file.

### Notes

- `JWT_SECRET` must be random and at least 32 bytes; the API refuses to start on a
  placeholder containing `change-me`. Generate one with `openssl rand -base64 48`
  (or `[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))` in PowerShell).
- Migrations and seeding run on API startup against the Supabase database, so the first
  boot takes longer than later ones.
- **`SSL Mode=Require` is not the safe option.** In Npgsql it means "refuse to connect
  without TLS" and nothing more: no certificate chain check and no hostname check, so it
  stops eavesdropping on the hop to Supabase but not impersonation. Only `VerifyCA` and
  `VerifyFull` validate. (`Trust Server Certificate` is inert in Npgsql 10 — the property
  is obsolete and documented as doing nothing, so removing it changes no behaviour.)
  Download the CA from your Supabase project settings, put it where `SUPABASE_CA_PATH`
  points, and use `SSL Mode=VerifyFull;Root Certificate=/etc/ssl/supabase/prod-ca.crt`.
  Compose mounts it read-only; on Render add it as a Secret File. For a first-boot smoke
  test, plain `SSL Mode=Require` with no `Root Certificate` will connect to anything —
  the API logs a warning outside Development when the string does not validate.
- Change the published port with `WEB_PORT` in `.env`.
- Swagger is proxied at `/swagger` but only responds if you set the API's
  `ASPNETCORE_ENVIRONMENT` to `Development` in `docker-compose.yml`.
- The SPA's API URL comes from `API_BASE_URL` (default `/api`), written into
  `wwwroot/appsettings.json` at container start — no rebuild needed to repoint it.

```bash
docker compose logs -f api     # follow API logs
docker compose down            # stop
```

## Seeded data
- Roles: **Admin**, **Sanathan**.
- New registrations are automatically assigned the **Sanathan** role.
- An administrator account is seeded with the credential `admin`, but it ships **locked** — its
  stored hash verifies against nothing, so it cannot be signed in to until you open it.

### Opening the administrator account

Set `Admin__InitialPassword` in the environment and start the API. It must satisfy the same
password policy as everyone else (at least 10 characters, and not an obvious one). It is
applied **once**, and only while the account is still locked, so leaving the variable set cannot
reset a password you later change, and removing it cannot lock you out.

```bash
Admin__InitialPassword='<a strong passphrase>'
```

After that, rotate it in the app: `POST /api/auth/change-password` with a bearer token
(`currentPassword`, `newPassword`, `confirmNewPassword`).

> This account previously shipped with the password `admin`, documented here, on a public
> repository — one unauthenticated request to full administrator. The `LockSeededAdminAccount`
> migration overwrites that hash on every existing database, **including production**, the next
> time the API starts. If you were relying on `admin`/`admin`, set `Admin__InitialPassword`
> **before** deploying, or you will have no way in.

### Sessions

The access token is short-lived and the refresh token is not. A refresh token is stored only as a
SHA-256 hash, is single-use, and is rotated on every exchange; presenting a spent one revokes the
whole family it belongs to, because a replay and a theft look identical from the server's side.

Two things end a session before its tokens expire, both enforced per request against
`Users.TokensValidFromUtc`:

- **Changing a password** revokes every token the account holds, then hands the device that asked a
  fresh pair — so the seeker who took the precaution is not signed out by it, and every other
  device is.
- **Closing an account** (`PUT /api/users/{id}/status`) does the same and refuses sign-in. An
  administrator cannot close their own account or the built-in one.

`JwtSettings__RefreshTokenDays` sets how long a refresh token lives (30 by default). Shortening
`JwtSettings__ExpiryMinutes` is now safe — the client renews in the background — and is worth doing:
the shorter it is, the less a stolen access token is worth.

### Before deploying `NormalizeUserCredentials`

This migration lower-cases every email and reduces every mobile number to its digits, then makes
the mobile number unique. Where two accounts would collide it changes nothing and **aborts**, and
because `Migrate()` runs outside the start-up try/catch an abort is a failed boot, not a skipped
step. Check first:

```sql
SELECT lower(btrim("Email")) AS credential, count(*) FROM "Users"
 GROUP BY 1 HAVING count(*) > 1
UNION ALL
SELECT regexp_replace("MobileNumber", '[^0-9]', '', 'g'), count(*) FROM "Users"
 GROUP BY 1 HAVING count(*) > 1;
```

Anything this returns has to be merged or removed by hand — the migration will not pick a winner
between two real accounts. The reverse migration drops the constraint but cannot restore the
original casing or punctuation.

## API endpoints
| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anon | Register (FullName, Email, MobileNumber, Password, ConfirmPassword, SeekerName?) |
| POST | `/api/auth/login` | anon | Login with email-or-mobile + password → access token + refresh token |
| POST | `/api/auth/refresh` | anon | Exchange a refresh token for a fresh pair |
| POST | `/api/auth/logout` | anon | Revoke the refresh-token family (always 204) |
| PUT  | `/api/users/{id}/status` | Admin | Open or close an account |
| GET  | `/api/dashboard` | Bearer | Protected placeholder dashboard |

## Tests and CI
- Backend: `dotnet test BackEnd/Sanathana.Companion.slnx` — seeding, register/login, JWT, BCrypt,
  the password and credential policy, the HTML sanitizer, menu/platform filtering, localization.
- Frontend: `dotnet test FrontEnd/App.Tests` — request validation, the API route table, the auth
  pipeline, geolocation precision, the mobile bottom-navigation rule.

Stop the API before running the backend suite: `dotnet test` cannot overwrite the DLLs a running
`Sanathana.Companion.Api` holds open.

`.github/workflows/ci.yml` runs both suites, publishes App.Web the way the deploy image does, and
compiles the Android head, on every push and pull request to `main` and `development`. It exists
because `render.yaml` deploys straight off a commit, so before it the first thing that built a
pushed branch was production. Make **Backend**, **Web** and **Security checks** required; leave
**Mobile (Android head)** advisory until Android SDK provisioning on hosted runners has proven
itself. `global.json` pins the SDK so the runner and your machine cannot drift apart.

The exact test counts are deliberately not written here — they were wrong within a month last
time. The workflow is the thing that knows.
