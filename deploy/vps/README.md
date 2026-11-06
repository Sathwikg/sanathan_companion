# Deploying to the VPS

`deploy.ps1` (repository root) builds the app on your Windows PC, copies it to the VPS and
starts it there with Docker Compose. The app then runs at **https://enityasannidhi.com** (and,
for a direct check that bypasses Caddy, at http://187.127.169.147:8081).

```
browser ──► https://enityasannidhi.com ──► caddy ──► web (nginx) ──┬─ /      static Blazor WebAssembly
                                             │                     └─ /api/  ──► api (.NET 10) ──► PostgreSQL on the VPS :5433
        ──► https://gippslandfresh.au  ──────┘──► grocery-frontend / grocery-backend (the grocery stack)
```

The VPS also runs the grocery app (Gippsland Fresh) as a separate Compose project. Since
2026-11-06 **this stack's Caddy is the only one on the host**: it owns ports 80 and 443, gets the
certificates for both domains and routes the grocery domain to that stack's containers over their
Docker network. The grocery deploy must therefore no longer start a Caddy of its own (see
[The grocery stack](#the-grocery-stack)). Everything else stays separate: own folder
(`~/sanathana_deploy`), own Compose project, own images.

| File | What it is | In git? |
|---|---|---|
| `deploy.ps1` | The deploy script. Run it from the repository root. | Yes |
| `docker-compose.remote.yml` | The stack that runs on the VPS. No secrets in it. | Yes |
| `.env.vps.example` | Template for your settings. | Yes |
| `.env.vps` | Your settings: database password, JWT secret, admin password. | **No** (git-ignored, never commit it) |
| `deploy/vps/Caddyfile` | Routes both domains. Uploaded and reloaded on every deploy. | Yes |

## What you need on your PC

- **Docker Desktop**, installed and running (the images are built locally).
- **OpenSSH** (`ssh` and `scp`), built into Windows 10 and 11.
- **The VPS's SSH key** at `C:\Users\<you>\.ssh\id_rsa`. If it is somewhere else, pass
  `-KeyFile <path>` to the script.
- **This repository**, checked out, including the three files above.

## First-time setup

Run everything in PowerShell from the repository root.

### 1. Create `.env.vps`

```powershell
Copy-Item .env.vps.example .env.vps

# Generate a JWT secret and copy the output
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)

notepad .env.vps
```

Fill in the following values and save:

| Setting | What to put |
|---|---|
| `CONNECTION_STRING` | Replace `YOUR-PASSWORD` with the Postgres password. The port (5433) and host are already set. `sanathan` is the database name; it already exists and holds the app's data, and the API applies any new migrations to it at startup. |
| `JWT_SECRET` | The string generated above. **Keep it the same on every deploy**: changing it signs every user out. |
| `ADMIN_INITIAL_PASSWORD` | At least 10 characters. The seeded `admin` account is locked on a new database until this is set. |
| `GOOGLE_CLIENT_ID` | Optional. Leave empty to hide the Google button; see [Afterwards](#afterwards) to enable it. |

If a value contains a `$`, write it as `$$`.

### 2. Check the VPS

```powershell
ssh root@187.127.169.147 "ss -ltnp | grep ':5433 '; ss -ltn | grep -q ':8081 ' && echo '8081 IN USE' || echo '8081 free'; ufw status | head -1"
```

- **Postgres on 5433**: the first line must show `0.0.0.0:5433`, `*:5433` or a `docker-proxy`
  process. If it shows only `127.0.0.1:5433`, Postgres accepts local connections only and the API
  container cannot reach it. Fix this before deploying (see [Troubleshooting](#troubleshooting)).
- **Port 8081**: must say `8081 free` on the very first deploy. On later deploys it is in use by this
  app, which is fine.
- **Firewall**: if it says `Status: active`, open the port once:

  ```powershell
  ssh root@187.127.169.147 "ufw allow 8081/tcp"
  ```

  If the hosting provider has its own firewall in its control panel, allow 8081 there too.

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy.ps1
```

Run this for every deploy, the first one and every update after it. It takes several minutes and
reports each step:

1. Checks `.env.vps` (fails within seconds if a secret is missing or still a placeholder)
2. Builds the API image
3. Builds the web image
4. Saves both images to one file
5. Creates `~/sanathana_deploy` on the VPS
6. Uploads the images, the compose file, the Caddyfile and your settings (the longest step)
7. Validates the Caddyfile, loads the images, starts the stack, waits until the API answers a
   database query, reloads Caddy and checks both domains answer over https

It ends with `Open https://enityasannidhi.com`. The first deploy takes longer, because the API
creates the database and loads the seed data before it reports healthy.

If step 7 fails, the script prints the last 80 lines of the API's log. The
[Troubleshooting](#troubleshooting) table below covers the usual causes.

Options, if the defaults ever need to change:

```powershell
.\deploy.ps1 -WebPort 8082 -RemoteUser root -RemoteHost 187.127.169.147 -KeyFile C:\path\to\key
```

### First sign-in

Sign in with the username **`admin`** and the `ADMIN_INITIAL_PASSWORD` you set, then change the
password from the app. The admin password is set from that value only once, while the account is
still locked, so leaving it in `.env.vps` never resets a password you changed later.

## Day-to-day commands

```powershell
# Are both containers up and healthy?
ssh root@187.127.169.147 "cd ~/sanathana_deploy && docker compose -f docker-compose.remote.yml ps"

# Follow the API log (Ctrl+C to stop)
ssh root@187.127.169.147 "docker logs -f --tail 100 sanathana-api"

# Restart without redeploying
ssh root@187.127.169.147 "cd ~/sanathana_deploy && docker compose -f docker-compose.remote.yml restart"

# Stop the app (the grocery app keeps running)
ssh root@187.127.169.147 "cd ~/sanathana_deploy && docker compose -f docker-compose.remote.yml down"
```

### Roll back to the previous version

Every deploy tags its images with the date and time, and the older ones stay on the VPS:

```powershell
# List the versions on the server
ssh root@187.127.169.147 "docker images sanathana-api --format '{{.Tag}}  {{.CreatedSince}}'"

# Switch to one of them, e.g. 20261023-141500
ssh root@187.127.169.147 "cd ~/sanathana_deploy && sed -i 's/^IMAGE_TAG=.*/IMAGE_TAG=20261023-141500/' .env && docker compose -f docker-compose.remote.yml up -d"
```

Migrations only move forward. Rolling back the app does not undo a migration the newer version
already applied to the database.

### Free disk space

Old versions pile up, roughly a few hundred MB per deploy. Remove the ones you no longer need,
one tag at a time:

```powershell
ssh root@187.127.169.147 "docker rmi sanathana-api:20261023-141500 sanathana-web:20261023-141500"
```

Do not use `docker image prune -a` on this server: it also removes the grocery app's unused
images.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `.env.vps not found`, or a `JWT_SECRET` / `CONNECTION_STRING` error | The settings file is missing or unfilled. | Step 1 of the first-time setup. |
| `docker : The term 'docker' is not recognized`, or `API docker build failed` | Docker Desktop is not installed or not running. | Start Docker Desktop and wait until it says it is running. |
| `Permission denied (publickey)` | The SSH key is missing or not the right one. | Put the key at `~\.ssh\id_rsa` or pass `-KeyFile`. |
| API log: `Failed to connect to ...:5433` or `Connection refused` | Postgres listens on localhost only. | In `postgresql.conf` set `listen_addresses = '*'`, restart Postgres, deploy again. |
| API log: `no pg_hba.conf entry for host "172.x.x.x"` | Postgres rejects connections from Docker networks. | Add `host all all 172.16.0.0/12 scram-sha-256` to `pg_hba.conf`, reload Postgres, deploy again. |
| API log: `password authentication failed` | Wrong password in `CONNECTION_STRING`. | Fix `.env.vps`, deploy again. |
| API log: `JwtSettings:Secret` error | The secret is too short or a placeholder. | Generate a new one (step 1). |
| `port is already allocated` / `address already in use` | Something else holds 8081. | Free it, or deploy with `-WebPort <other port>` (and open that port in the firewall). |
| Containers healthy, but the page will not load in the browser | The firewall blocks 8081. | `ufw allow 8081/tcp`, and the provider's panel firewall. |
| Google sign-in or the browser's location prompt does not work | The site was opened over plain http://IP:8081. | Use https://enityasannidhi.com; set `GOOGLE_CLIENT_ID` (see [Afterwards](#afterwards)). |

## The domains

Both domains point at the VPS (A records for `@` and `www` of each, at the registrar) and the
Caddyfile in `deploy/vps/Caddyfile` says where each one goes:

```
enityasannidhi.com      -> web:80          (this stack's nginx, then /api -> api)
www.enityasannidhi.com  -> 301 to enityasannidhi.com
gippslandfresh.au, www  -> /api* -> grocery-backend:5000, everything else -> grocery-frontend:80
```

Edit that file to add or change a site and run `deploy.ps1`: it validates the file before anything
is touched, uploads it and reloads Caddy in place. Let's Encrypt issues a new certificate within a
minute of the first deploy of a domain; the certificates live in the `caddy_data` volume and
survive deploys.

### The grocery stack

Caddy reaches `grocery-frontend` and `grocery-backend` by joining the grocery project's network
(`grocery_deploy_default`), which Compose treats as external: the grocery stack must be running
when this one deploys. On the first deploy after the switch, `deploy_step.sh` removes the grocery
stack's old `caddy` container (only one carrying the grocery compose project label) so the ports
are free.

Two consequences for the **grocery repository**, whose `deploy.ps1` generates the compose file and
uploads a Caddyfile:

- Remove the `caddy` service (and the `caddy_data` / `caddy_config` volumes) from the compose text
  it generates, and stop uploading its Caddyfile. Until that is done, the next grocery deploy
  tries to start a second Caddy, fails on ports 80/443 being in use, and the grocery deploy
  reports a failure - its frontend and backend containers still update, and this stack's Caddy
  keeps serving the site.
- `docker compose down` on the grocery side cannot remove its network while Caddy is attached. Use
  `up -d` to update it, as its deploy script already does.

### Afterwards

- Google sign-in now works: set `GOOGLE_CLIENT_ID` in `.env.vps`, register
  `https://enityasannidhi.com` as an authorised JavaScript origin in Google Cloud Console, and run
  `deploy.ps1` again.
- Port 8081 can stay open (plain http, bypassing Caddy) or be closed in the hosting provider's
  firewall. The web image's nginx trusts `X-Forwarded-For` / `X-Forwarded-Proto` only from
  Docker-network addresses (that is, from Caddy), so the API sees each seeker's own address for
  rate limiting and the audit log, and knows the request came in over HTTPS; a request straight to
  :8081 cannot impersonate Caddy.

| Symptom | Cause | Fix |
|---|---|---|
| Deploy stops at `Validating the Caddyfile` | Syntax error in `deploy/vps/Caddyfile`. | Fix it; nothing on the server changed. |
| `grocery_deploy_default does not exist` | The grocery stack is not running. | Start it (`cd ~/grocery_deploy && docker compose -f docker-compose.remote.yml up -d`), deploy again. |
| `WARNING: https://... answers '000'` and the Caddy log shows ACME errors | Let's Encrypt could not validate the domain - DNS pointing elsewhere or port 80 blocked. | Check the A records and the provider firewall; Caddy keeps retrying on its own. |
| `port is already allocated` for 80 or 443 | Another Caddy (the grocery one) is running. | `docker rm -f caddy`, deploy again, and fix the grocery deploy script as above. |
