# Sanathana Companion

A Hindu Dharma "spiritual companion" app. Two **standalone** applications that talk only over a REST API:

```
Application/
├── BackEnd/    Sanathana.Companion.sln            .NET 10 Clean Architecture Web API (repository + EF Core + PostgreSQL, JWT)
└── FrontEnd/   Sanathana.Companion.Frontend.sln   .NET 10 Blazor: shared UI rendered by a WASM web host and a MAUI mobile host
```

## Tech stack
- **API:** .NET 10, Clean Architecture (Domain / Application / Infrastructure / Api / Modules), repository + unit-of-work, EF Core + Npgsql, JWT bearer auth, FluentValidation, Serilog, Swagger.
- **Frontend:** .NET 10 Blazor. `App.Core` (models/services/auth), `App.UI.Shared` (shared Razor components + Hindu Dharma theme), `App.Web` (Blazor WebAssembly), `App.Mobile` (.NET MAUI Blazor Hybrid — Android + Windows), `App.Tests`.
- **Database:** PostgreSQL on `localhost:5432`, database `sanathana_companion` (created + seeded automatically on first API run).

## Prerequisites
- .NET 10 SDK, `dotnet-ef` 10.x (`dotnet tool update --global dotnet-ef --version 10.*`).
- A running **PostgreSQL** on `localhost:5432`. Adjust credentials in `BackEnd/src/Sanathana.Companion.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`).
- For the mobile app: MAUI workloads (`maui-android`, `maui-windows`). iOS/macOS require a Mac.

## Run
1. **Backend:** `RunBackend.cmd` → API on `http://localhost:7050`, Swagger at `/swagger`. Migrations + seed apply automatically on startup.
2. **Web:** `RunFrontend.cmd` → `http://localhost:7001` (calls the API at `:7050`).
3. **Mobile (Windows):** `dotnet build FrontEnd/App.Mobile -t:Run -f net10.0-windows10.0.19041.0`
   **Mobile (Android emulator):** `dotnet build FrontEnd/App.Mobile -t:Run -f net10.0-android` (API reached via `http://10.0.2.2:7050`).

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
- Default admin login → **credential `admin`, password `admin`**.
- New registrations are automatically assigned the **Sanathan** role.

## API endpoints
| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anon | Register (FullName, Email, MobileNumber, Password, ConfirmPassword, SeekerName?) |
| POST | `/api/auth/login` | anon | Login with email-or-mobile + password → JWT |
| GET  | `/api/dashboard` | Bearer | Protected placeholder dashboard |

## Tests
- Backend: `dotnet test BackEnd/Sanathana.Companion.slnx` (14 tests — seeding, register/login, JWT, BCrypt, validation).
- Frontend: `dotnet test FrontEnd/App.Tests` (6 tests — request validation).
