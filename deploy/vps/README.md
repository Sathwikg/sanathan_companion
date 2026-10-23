# Deploying to the VPS

`deploy.ps1` (repository root) builds the app on your Windows PC, copies it to the VPS and
starts it there with Docker Compose. The app then runs at **http://187.127.169.147:8081**.

```
browser ──► VPS :8081 ──► web (nginx) ──┬─ /      static Blazor WebAssembly
                                        └─ /api/  ──► api (.NET 10) ──► PostgreSQL on the VPS :5433
```

It shares the VPS with the grocery app, which owns ports 80, 443 and 5000. This stack uses only
**8081**, lives in its own folder (`~/sanathana_deploy`) and its own Compose project, so deploying
one never touches the other.

| File | What it is | In git? |
|---|---|---|
| `deploy.ps1` | The deploy script. Run it from the repository root. | Yes |
| `docker-compose.remote.yml` | The stack that runs on the VPS. No secrets in it. | Yes |
| `.env.vps.example` | Template for your settings. | Yes |
| `.env.vps` | Your settings: database password, JWT secret, admin password. | **No** (git-ignored, never commit it) |

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
| `GOOGLE_CLIENT_ID` | Leave empty. Google sign-in needs HTTPS. |

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
6. Uploads the images, the compose file and your settings (the longest step)
7. Loads the images, starts the stack and waits until the API answers a database query

It ends with `Open http://187.127.169.147:8081`. The first deploy takes longer, because the API
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
| Google sign-in or the browser's location prompt does not work | The site is served over plain HTTP. | Expected until HTTPS is added (below). |

## Adding HTTPS later

Plain `http://IP:8081` is fine for testing, but browsers only allow Google sign-in, location access
and some other features on HTTPS. To add it:

1. Point a domain (for example `app.example.com`) at 187.127.169.147.
2. The grocery stack's Caddy already owns ports 80 and 443. Give its `caddy` service
   `extra_hosts: ["host.docker.internal:host-gateway"]` in the grocery compose file, and add a site
   block to the grocery `Caddyfile`:

   ```
   app.example.com {
       reverse_proxy host.docker.internal:8081
   }
   ```

3. Redeploy the grocery stack so Caddy picks it up. Caddy fetches the certificate automatically.
4. Optionally close 8081 in the firewall so the app is only reachable through the domain.
5. Set `GOOGLE_CLIENT_ID` in `.env.vps`, register the `https://` origin in Google Cloud Console,
   and run `deploy.ps1` again.
