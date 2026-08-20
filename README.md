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
- `Trust Server Certificate=true` encrypts the connection but skips CA validation. To
  validate properly, download Supabase's CA certificate and use
  `SSL Mode=VerifyFull;Root Certificate=/path/to/prod-ca.crt`.
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

Set `Admin__InitialPassword` (minimum 8 characters) in the environment and start the API. It is
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

## API endpoints
| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anon | Register (FullName, Email, MobileNumber, Password, ConfirmPassword, SeekerName?) |
| POST | `/api/auth/login` | anon | Login with email-or-mobile + password → JWT |
| GET  | `/api/dashboard` | Bearer | Protected placeholder dashboard |

## Tests
- Backend: `dotnet test BackEnd/Sanathana.Companion.slnx` (279 tests — seeding, register/login, JWT, BCrypt, validation, menu/platform filtering, localization).
- Frontend: `dotnet test FrontEnd/App.Tests` (73 tests — request validation, the API route table, the mobile bottom-navigation rule).

Stop the API before running the backend suite: `dotnet test` cannot overwrite the DLLs a running
`Sanathana.Companion.Api` holds open.
