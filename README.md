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
   **On this branch (`development_mobileview`) that is the phone shell**, framed as a 412×915
   handset when the browser window is desktop-sized — see "The mobile app" below.
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

### The phone has its own home screen

The two hosts share every page except the one a seeker sees first. `Dashboard` (`/`) is laid out
for a desktop column — a 200px saffron hero, then a sadhana summary that wraps into three stacked
rows on a 375px screen — so the phone gets **`MobileDashboard`** (`/mobile-dashboard`) instead: a
one-line greeting, the same sadhana figures in a single four-cell row, then quick actions.

It is a **separate module**, not a variant of the dashboard, so an administrator can grant, reorder
or retire it on its own. Three things make that work, and all three are load-bearing:

| | |
|---|---|
| `ShowInMobile` | `mobileDashboard` is on, `dashboard` is **off** — otherwise the bar would carry two home tabs. |
| Landing route | `Dashboard` redirects to `/mobile-dashboard` when `AppConfig.IsMobile`. Login and Access-Denied both navigate to `""`, and the MAUI host cold-starts there, so the redirect — not the menu — is what moves those entry points. |
| `DisplayOrder` | Must stay ≤ 4. `MobileMenu.IsHome` only pins a `"/"` route, so ordering is the only thing keeping the home screen out of the **More** sheet. |

The endpoints it reads (`DashboardController`, plus the streak, chants and panchangam actions)
name **both** `dashboard` and `mobileDashboard`. `ModuleAccessFilter` resolves the module from the
endpoint's own attribute and never learns which page called it, so a seeker granted only the mobile
home would otherwise get a 403 on every widget. `SeedDataTests` pins the code, the flags and the
ordering, because `AccessRightsCatalog` compares codes **ordinally** — a row seeded as
`"MobileDashboard"` would look correct everywhere and refuse the form at run time.

### Configuration, in one place each

| Host | File | Notes |
|---|---|---|
| Mobile | `FrontEnd/App.Mobile/Resources/Raw/appsettings.json` | API URL per build configuration, app name, timeout. Read once at start-up by `MobileSettings.Load()`. Packaged, so changing it means a rebuild. |
| Web | `FrontEnd/App.Web/wwwroot/appsettings.json` | API URL and `Platform`. Docker rewrites `ApiBaseUrl` and `Platform` at container start. |
| Both | `FrontEnd/App.Core/Config/ApiRoutes.cs` | **Every REST path the clients call.** No URL string is spelled out anywhere else. |

Set `"Platform": "Mobile"` in the *web* `appsettings.json` to render the phone shell in a desktop
browser — the quickest way to review mobile UI without a device. **This branch
(`development_mobileview`) ships with it set**, so `RunFrontend.cmd` and `dotnet run` on `:7001`
are the phone preview, and its `render.yaml` sets `PLATFORM=Mobile` so the hosted service is too.
See `deploy/render/README.md`, "Hosting the phone shell as its own service".

### The phone shell in a desktop browser looks like a phone

A phone shell stretched across a 1920px window is not a preview of anything, so the web host
frames it. `App.Web/wwwroot/phoneFrame.js` runs before Blazor starts: when the host is the phone
shell (`Platform` is `Mobile`, or the origin is in `MobilePreviewOrigins`) and the window is
desktop-sized, the page becomes a handset bezel with an `<iframe>` of itself as the 412×915
screen, and the app boots inside that. An iframe is a real viewport — media queries, `dvh`,
`position: fixed` sheets, the scroll lock — so what you see is what a Pixel-class phone shows,
scaled to fit the window. The address bar and tab title follow the app inside, so deep links and
refresh keep working.

A phone-sized window (a real phone, or DevTools device emulation) gets the shell full-bleed, as
before, and `?frame=0` on the URL forces that at any size. The MAUI host never links the script.

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

### Media links expire

The four endpoints that stream bytes — deity images, chant audio, wallpapers and their download —
are `[AllowAnonymous]` because an `<img>`, an `<audio>` and a download link cannot carry a bearer
token. That made the URL alone a capability that never expired and could not be withdrawn.

They now require a ticket in the `t` query parameter, issued by `GET /api/media/ticket` to any
signed-in caller. The ticket is HMAC'd with a key derived from the JWT secret, so rotating that
secret invalidates every outstanding media URL, and it is valid for the current six-hour window and
the previous one.

**The window length is a trade, not a number to minimise.** The ticket is part of the URL and
therefore part of the HTTP cache key, so each rotation costs a full re-download of every visible
image — on phones. Six hours buys "bounded rather than forever" at four rotations a day. Set
`Media__RequireTicket=false` to turn the requirement off without a redeploy if a rollout goes wrong;
the client sends the ticket either way, so the URL shape does not change when you flip it.

One consequence worth knowing: an administrator previewing something they have just deactivated
sees a placeholder, because the endpoints serve published rows only and the ticket carries no
privilege bit. Giving it one would split the cache per role, which is a poor trade for a preview.

### Access Rights are enforced at the endpoint

The role-by-module matrix used to decide only which links appeared in the menu; every API endpoint
was gated by nothing finer than `[Authorize]`, so a role denied the Deities form could still read
`GET /api/deities` by asking for it. Writes were already closed by `[Authorize(Roles = "Admin")]` —
it is the reads this shuts.

Each endpoint now declares its form with `[RequiresModule(ModuleCodes.X)]`, or `[ModuleExempt]` for
the handful that belong to no form: the menu itself, the notification bell, and the caller's own
data (profile, favourites, changing your own password). A test fails the build if an endpoint
declares neither. **Default-deny**: an authenticated non-administrator reaching an endpoint that
names no module is refused, so a controller added tomorrow is closed until somebody maps it.
Administrators bypass the check entirely, so a forgotten attribute cannot brick administration.

A module denial answers 403 with `X-Access-Denied: module`, which is how the client tells it from
the role-based 403s that two screens already handle themselves; the app lands on `/access-denied`
rather than the app root, because the root is itself a gated form.

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

### Sign in with Google

A seeker can sign in with a Google account as well as with email-or-mobile + password. It is free:
Google charges nothing for Sign in with Google, the app needs only a Google Cloud project (no
billing account, no Firebase), and apps that request nothing beyond `openid email profile` do not
have to go through Google's OAuth verification.

**How it works.** The client obtains a Google ID token and posts it to `POST /api/auth/google`. The
API verifies the token's signature, issuer, expiry and, crucially, its audience against
`Google:ClientIds` (a token minted for some other site's OAuth client is refused), then answers
with one of three outcomes:

| Outcome | When | What the client does |
|---|---|---|
| `SignedIn` | the Google account is already connected | stores the session; done |
| `RegistrationRequired` | nobody has this email yet | opens the registration form with the email locked and the name pre-filled; the seeker adds mobile, seeker name, region and **a password, chosen once**; `POST /api/auth/google/register` creates the account, connects Google and signs the seeker in |
| `LinkRequired` | an account with this email exists but is not connected | asks for that account's password **once**; `POST /api/auth/google/link` connects the two and signs in |

After the first time, every Google sign-in is instant, and email + password keeps working for the
same account. Accounts are matched on Google's stable id (`Users.GoogleSubject`), never on the
address, once connected. The password step when linking is deliberate: plain registration never
verifies an email, so the account holding a Gmail address may have been created by somebody who
knew the address but not the inbox, and neither party should be able to walk into the other's
account. The register and link calls carry a ten-minute HMAC ticket (the same construction as the
media ticket) rather than the ID token, so nothing about the sign-in is stored between the two calls.

**Where the button appears.** The website (Google's own button, drawn by Google Identity Services,
which the login page loads on demand) and the Android app (the system account sheet, through
Credential Manager, because Google no longer lets a new Android app run the OAuth redirect through
a browser). **Not the iOS app**: App Store guideline 4.8 says an app that offers Google sign-in must
also offer an equivalent login that collects only name and email and lets the user hide their
address, and our registration asks for a mobile number, so it does not qualify. Adding Sign in with
Apple lifts that restriction and is the natural next step.

**Setting it up** (Google Cloud Console, all free):

1. *APIs & Services → OAuth consent screen*: External, app name, support and developer emails,
   scopes `openid email profile`, then **Publish to production** (otherwise it is capped at 100
   test users). Brand verification is optional and only affects whether your logo shows.
2. *Credentials → Create OAuth client ID → Web application*: authorised JavaScript origins
   `http://localhost:7001`, `http://localhost:7002` and your deployed origin (for example
   `https://sanathan-companion.onrender.com`). No redirect URIs are needed. **Its client id is the
   one value used everywhere.**
3. *Create OAuth client ID → Android*: package name `com.sanathana.companion` and the SHA-1 of the
   signing certificate: the debug keystore now
   (`keytool -list -v -keystore %USERPROFILE%\.android\debug.keystore -alias androiddebugkey -storepass android`),
   the Play App Signing key before release. Nothing from this client goes into the code; it is how
   Google knows this APK may ask.
4. Configure the id: API `Google__ClientIds__0` (Render, compose via `GOOGLE_CLIENT_ID`, or
   `appsettings.Development.json`); web `GoogleClientId` in `App.Web/wwwroot/appsettings.json`
   (Docker and Render write it from `GOOGLE_CLIENT_ID` at container start); Android
   `GoogleServerClientId` in `App.Mobile/Resources/Raw/appsettings.json` (a rebuild, like every
   mobile setting). Leaving any of them empty hides the button on that client, and the API logs a
   warning and answers 401 while its own list is empty.

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
| POST | `/api/auth/google` | anon | Sign in with a Google ID token → `SignedIn` (session), `RegistrationRequired` or `LinkRequired` (ticket) |
| POST | `/api/auth/google/register` | anon | Complete registration with the ticket (FullName, MobileNumber, Password, ConfirmPassword, SeekerName?, RegionId?) → session |
| POST | `/api/auth/google/link` | anon | Connect Google to the existing account with the ticket + its password → session |
| POST | `/api/auth/refresh` | anon | Exchange a refresh token for a fresh pair |
| POST | `/api/auth/logout` | anon | Revoke the refresh-token family (always 204) |
| PUT  | `/api/users/{id}/status` | Admin | Open or close an account |
| GET  | `/api/dashboard` | Bearer | Protected placeholder dashboard |

## Ads

The mobile apps can show Google AdMob ads; the web app never does. Which forms show one, and which
single format each shows, is configured at **Configuration → Ad Config** rather than in code.

The six formats the Google Mobile Ads SDK serves are seeded as master data in `AdFormats`, each
carrying Google's own placement rule:

| Format | Full screen | Rule that governs it |
|---|---|---|
| Banner | no | Keep clear of anything tappable — adjacent controls cause the accidental clicks that get ad serving disabled |
| Interstitial | yes | Natural breaks only. Never on launch or exit, never back-to-back, at most one per two actions |
| Native | no | Must be labelled as an ad and must not look like app content the seeker can act on |
| Rewarded | yes | The seeker opts in first and is told what they get |
| Rewarded Interstitial | yes | Needs an intro screen offering a way out |
| App Open | yes | The **only** format allowed at launch; an interstitial there is a policy breach |

They are seeded rather than an enum so the guidance is queryable and translatable, and they are not
creatable from the form — a seventh format would be one the SDK cannot render. Deactivating one
withdraws it from the picker.

`AdPlacement` holds one row per form with a **single** `AdFormatId`, so "only one type at a time" is
a property of the schema rather than a rule someone has to remember. Switching a form off clears its
format, so re-enabling never resurrects a forgotten choice.

Ads ship **off**, with `UseTestAds` **on**. Test mode substitutes Google's public test ad units for
whatever is configured — clicking your own live ads is the usual way an AdMob account gets
suspended, and the likeliest cause is somebody testing against production units. The app IDs and ad
unit IDs are not secrets: they ship inside the app binary. They live in the database so a placement
can be repointed without a store release.

`GET /api/ads/slot/{menuModuleId}?platform=Android|iOS` resolves the master switch, the placement
and the platform server-side and answers with one decision, so no client has to combine three
things and get it wrong.

## Building the mobile heads locally

`net10.0-ios` and `net10.0-windows10.0.19041.0` build with nothing extra on Windows.

**Android needs JDK 21 specifically** — not the newest one. The .NET 10 Android head refuses
anything else outright:

```
error XA0030: Building with JDK version `23.0.1` is not supported. Please install JDK version `21.0`.
```

Having a newer JDK on the machine does not help and is in fact the usual cause. Install Temurin 21
(the same distribution CI uses) and point the build at it explicitly rather than relying on `$PATH`,
which is likely to find whichever JDK is newest:

```bash
dotnet build FrontEnd/App.Mobile/App.Mobile.csproj -c Release -f net10.0-android -p:JavaSdkDirectory="$JDK21_HOME" -p:AndroidSdkDirectory="$ANDROID_SDK"
```

That produces a signed `.apk` and a Play Store `.aab` under
`FrontEnd/App.Mobile/bin/Release/net10.0-android/`. The current build is also kept at
`artifacts/android/SanathanCompanion-1.1.0-release.apk` (with the Play `.aab` beside it) so it is easy to find and hand to someone;
`artifacts/` is gitignored, because a 36 MB binary per rebuild would outweigh the whole source tree
in history.

> **That APK is signed with the Android debug certificate** (`CN=Android Debug`), which MSBuild
> generates automatically. It installs and runs on a device, and it is fine for testing — but Google
> Play will refuse it. Publishing needs an upload key you own, referenced through
> `AndroidSigningKeyStore` / `AndroidSigningKeyAlias` and their passwords supplied out of band, the
> same way `JwtSettings__Secret` is.

On Linux CI the framework has to be selected with `-p:TargetFrameworks=net10.0-android` rather than
`-f`: workload resolution reads the whole `TargetFrameworks` list during evaluation, so `-f` alone
still demands the iOS pack, which has no Linux build.

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
**Mobile (Android head)** and **Mobile (iOS head)** advisory until SDK provisioning on hosted
runners has proven itself. `global.json` pins the SDK so the runner and your machine cannot drift
apart.

**Mobile (iOS head)** runs on `macos-latest`, and it is the only place iOS is genuinely built —
Xcode ships the iOS SDK, the codesign toolchain and the simulator, and runs on nothing but macOS.
A Windows checkout can compile the shared UI for iOS and stop there. The job compiles the head and
uploads a **simulator `.app`** as a build artifact, which needs no Apple certificate and opens in
Xcode’s Simulator. A device `.ipa` is deliberately not built: it needs a Developer certificate and
a provisioning profile in the runner keychain, which belongs in a release workflow with real
secrets, not a CI check. The exact `dotnet publish` line for that is written out in a comment at
the end of the job.

The exact test counts are deliberately not written here — they were wrong within a month last
time. The workflow is the thing that knows.
