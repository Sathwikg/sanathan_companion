<#
Deploys Nitya Sannidhi to the VPS (Windows PowerShell).

What it does:
- Builds the API and web images locally (BackEnd/Dockerfile, FrontEnd/Dockerfile)
- Saves both images to one tarball
- Copies it, docker-compose.remote.yml, deploy/vps/Caddyfile and the settings from .env.vps to
  the host via scp
- SSHes into the host, loads the images, starts the stack with docker compose, waits for the
  API to report healthy - printing the API's log if it does not - then reloads Caddy and checks
  both domains answer over https

The app is then served at https://enityasannidhi.com (and plain http://<RemoteHost>:<WebPort>).
The stack's Caddy is the only one on the host: it also serves the grocery domain, reaching
that stack's containers over their network, so the grocery deploy must not start a Caddy of
its own. Otherwise the two stacks stay separate: own folder, own compose project.

Usage (from any folder; paths resolve against this script's folder):
  Copy .env.vps.example to .env.vps and fill it in once, then:
    ./deploy.ps1
    ./deploy.ps1 -RemoteUser deploy -KeyFile C:\Users\Me\.ssh\id_rsa -WebPort 8081
    ./deploy.ps1 -BuildOnServer      # no Docker on this PC: the VPS builds the images itself

Requirements on local Windows machine:
- Docker Desktop installed and running - or pass -BuildOnServer, which needs only git here
- `ssh` and `scp` available (Windows 10+ OpenSSH or Git Bash)
- SSH key auth set up for the remote host
#>

param(
  [string]$RemoteHost = "187.127.169.147",
  [string]$RemoteUser = "root",
  [string]$KeyFile = "$env:USERPROFILE\.ssh\id_rsa",
  [string]$RemoteDir = "~/sanathana_deploy",
  # Host port the app is published on. Only the web container gets one; the API is reachable
  # only through the web container's /api proxy.
  [int]$WebPort = 8081,
  # Secrets and per-server settings. Git-ignored; see .env.vps.example.
  [string]$EnvFile = "",
  # No Docker on this PC: upload the committed source instead (git archive of HEAD, so uncommitted
  # edits and local secrets never leave the machine) and let the VPS build both images. Slower on
  # a small VPS - the .NET SDK image and a Release publish of both apps - but needs nothing here.
  [switch]$BuildOnServer
)

$ErrorActionPreference = "Stop"

if (-not $EnvFile) { $EnvFile = Join-Path $PSScriptRoot ".env.vps" }

function Fail([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

# ---- Progress reporting -----------------------------------------------------
# Each step announces itself with a counter and how long the previous one took, so a slow step
# (the upload especially) can be told apart from a hang.
$script:DeployStart  = Get-Date
$script:StepStart    = Get-Date
$script:StepNo       = 0
$script:StepTotal    = if ($BuildOnServer) { 5 } else { 7 }
$script:StepReported = $false

function Step([string]$name) {
    # Skip the automatic timing line when the step already reported its own result.
    if ($script:StepNo -gt 0 -and -not $script:StepReported) {
        $took = [math]::Round(((Get-Date) - $script:StepStart).TotalSeconds, 1)
        Write-Host ("      done in {0}s" -f $took) -ForegroundColor DarkGray
    }
    $script:StepNo++
    $script:StepStart = Get-Date
    $script:StepReported = $false
    $elapsed = [math]::Round(((Get-Date) - $script:DeployStart).TotalSeconds, 1)
    Write-Host ("[{0}/{1}] {2}   (+{3}s)" -f $script:StepNo, $script:StepTotal, $name, $elapsed) -ForegroundColor Cyan
}

function StepDone([string]$summary) {
    $took = [math]::Round(((Get-Date) - $script:StepStart).TotalSeconds, 1)
    Write-Host ("      {0} in {1}s" -f $summary, $took) -ForegroundColor DarkGray
    $script:StepStart = Get-Date
    $script:StepReported = $true
}

# ---- Settings ---------------------------------------------------------------
# Checked before anything is built, so a missing secret fails in a second rather than after
# several minutes of building and uploading - or worse, as an API that refuses to start.
Step "Checking settings in $EnvFile"
if (-not (Test-Path $EnvFile)) {
    Fail @"
$EnvFile not found. It holds the database connection string and the JWT secret, and is
git-ignored so they never reach the repository. Create it once:

  Copy-Item .env.vps.example .env.vps

then fill it in and re-run this script.
"@
}

$settings = [ordered]@{}
foreach ($line in Get-Content $EnvFile) {
    $t = $line.Trim()
    if ($t -eq "" -or $t.StartsWith("#")) { continue }
    $eq = $t.IndexOf("=")
    if ($eq -lt 1) { continue }
    $settings[$t.Substring(0, $eq).Trim()] = $t.Substring($eq + 1).Trim()
}

$conn = [string]$settings["CONNECTION_STRING"]
if ([string]::IsNullOrWhiteSpace($conn) -or $conn -match "YOUR-PASSWORD") {
    Fail "CONNECTION_STRING in $EnvFile is missing or still has the placeholder password."
}
$jwt = [string]$settings["JWT_SECRET"]
if ($jwt.Length -lt 32 -or $jwt -match "change-me") {
    Fail "JWT_SECRET in $EnvFile must be a random string of 32+ characters without 'change-me' - the API refuses to start otherwise."
}
if ([string]::IsNullOrWhiteSpace([string]$settings["ADMIN_INITIAL_PASSWORD"])) {
    Write-Host "      ADMIN_INITIAL_PASSWORD is empty: on a fresh database nobody can sign in as admin until it is set." -ForegroundColor Yellow
}

$ImageTag = Get-Date -Format "yyyyMMdd-HHmmss"
$ApiImage = "sanathana-api:$ImageTag"
$WebImage = "sanathana-web:$ImageTag"

# Everything uploaded is staged in one temp folder and sent with one scp from inside it, so no
# Windows drive-letter path is ever handed to scp. The folder holds the secrets, so it is
# removed in the finally block whether the deploy succeeds or not.
$Stage = Join-Path $env:TEMP "sanathana-deploy"
if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Path $Stage | Out-Null

$sshArgs = @()
if (Test-Path $KeyFile) { $sshArgs += "-i"; $sshArgs += $KeyFile }

try {
    if ($BuildOnServer) {
        Step "Packing the committed source (git archive of HEAD)"
        $Payload = "sanathana-src.tar.gz"
        $head = (& git -C $PSScriptRoot rev-parse --short HEAD).Trim()
        $branch = (& git -C $PSScriptRoot rev-parse --abbrev-ref HEAD).Trim()
        $dirty = @(& git -C $PSScriptRoot status --porcelain --untracked-files=no)
        if ($dirty.Count -gt 0) {
            Write-Host "      Note: uncommitted changes are not deployed - the commit at HEAD is ($branch $head)." -ForegroundColor Yellow
        }
        # Only the two build contexts; .dockerignore applies on the server as it would here.
        & git -C $PSScriptRoot archive --format=tar.gz -o (Join-Path $Stage $Payload) HEAD BackEnd FrontEnd
        if ($LASTEXITCODE -ne 0) { Fail "git archive failed" }
        $PayloadMb = [math]::Round((Get-Item (Join-Path $Stage $Payload)).Length / 1MB, 1)
        StepDone "packed $branch $head, $PayloadMb MB"

        $remoteImages = @"
echo "Unpacking the source..."
rm -rf src && mkdir src
tar -xzf $Payload -C src
rm -f $Payload
export DOCKER_BUILDKIT=1
echo "Building the API image (the first build pulls the .NET SDK image - several minutes)..."
docker build -t $ApiImage src/BackEnd
echo "Building the web image..."
docker build -t $WebImage src/FrontEnd
"@
    }
    else {
        Step "Building API image  $ApiImage"
        docker build -t $ApiImage (Join-Path $PSScriptRoot "BackEnd")
        if ($LASTEXITCODE -ne 0) { Fail "API docker build failed (is Docker Desktop running?)" }

        Step "Building web image  $WebImage"
        docker build -t $WebImage (Join-Path $PSScriptRoot "FrontEnd")
        if ($LASTEXITCODE -ne 0) { Fail "web docker build failed" }

        $Payload = "sanathana-images.tar"
        Step "Saving both images to $Payload"
        docker save $ApiImage $WebImage -o (Join-Path $Stage $Payload)
        if ($LASTEXITCODE -ne 0) { Fail "docker save failed" }
        $PayloadMb = [math]::Round((Get-Item (Join-Path $Stage $Payload)).Length / 1MB, 0)
        StepDone "saved $PayloadMb MB"

        $remoteImages = @"
echo "Loading images..."
docker load -i $Payload
rm -f $Payload
"@
    }

    # The server's .env: the local settings plus the two values this deploy decides. Written
    # without a BOM and with LF endings, which Compose and bash both expect.
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $envLines = @()
    foreach ($key in $settings.Keys) {
        if ($key -in @("IMAGE_TAG", "WEB_PORT")) { continue }
        $envLines += "$key=$($settings[$key])"
    }
    $envLines += "IMAGE_TAG=$ImageTag"
    $envLines += "WEB_PORT=$WebPort"
    [System.IO.File]::WriteAllText((Join-Path $Stage ".env"), (($envLines -join "`n") + "`n"), $utf8NoBom)

    Copy-Item (Join-Path $PSScriptRoot "docker-compose.remote.yml") $Stage
    # Caddy reads the file as-is, so make sure a Windows checkout has not given it CRLF endings.
    $caddyfile = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "deploy/vps/Caddyfile")).Replace("`r", "")
    [System.IO.File]::WriteAllText((Join-Path $Stage "Caddyfile"), $caddyfile, $utf8NoBom)

    # Run on the host by name rather than piped to `ssh bash -s`: PowerShell prefixes a piped
    # stream with a UTF-8 BOM, which bash reads as a command on line 1. A file also stays on
    # the host to inspect when a remote step misbehaves.
    $remoteScript = @'
set -euo pipefail
cd __REMOTE_DIR__
chmod 600 .env

__IMAGES__

if docker compose version >/dev/null 2>&1; then DC="docker compose"; else DC="docker-compose"; fi

# The Caddyfile is checked before anything is touched: a typo must not take both domains down.
echo "Validating the Caddyfile..."
docker run --rm -v "$PWD/Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2 caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile

# Caddy serves the grocery domain over that stack's network, which compose expects to exist.
if ! docker network inspect grocery_deploy_default >/dev/null 2>&1; then
  echo "The grocery stack's network (grocery_deploy_default) does not exist - is the grocery stack running? Caddy needs it to serve gippslandfresh.au."
  exit 1
fi

# One Caddy per host. The grocery stack's own Caddy (container "caddy") held ports 80/443 until
# 2026-11-06; this stack's Caddy serves its domain now, so it is removed before ours starts.
# Only a container the grocery compose project created is touched.
if [ "$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' caddy 2>/dev/null)" = grocery_deploy ]; then
  echo "Retiring the grocery stack's Caddy (sanathana-caddy serves gippslandfresh.au from now on)..."
  docker rm -f caddy >/dev/null
fi

echo "Starting the stack..."
$DC -f docker-compose.remote.yml up -d --remove-orphans

# The health check queries the database, so healthy means migrations ran and Postgres answers.
echo "Waiting for the API to report healthy (the first start runs the migrations)..."
status=starting
for i in $(seq 1 48); do
  status=$(docker inspect --format '{{.State.Health.Status}}' sanathana-api 2>/dev/null || echo missing)
  restarts=$(docker inspect --format '{{.RestartCount}}' sanathana-api 2>/dev/null || echo 0)
  if [ "$status" = healthy ]; then
    echo "API is healthy."
    # `up -d` does not restart Caddy for a changed bind-mounted Caddyfile, so apply it explicitly.
    docker exec sanathana-caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile
    $DC -f docker-compose.remote.yml ps
    # Both domains over https. The first deploy waits for Let's Encrypt to issue the
    # certificates; a miss here is reported, not fatal - the stack itself is up.
    for site in enityasannidhi.com gippslandfresh.au; do
      code=000
      for j in $(seq 1 18); do
        code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$site/" || true)
        case "$code" in 2*|3*) break;; esac
        sleep 5
      done
      case "$code" in
        2*|3*) echo "https://$site answers $code";;
        *) echo "WARNING: https://$site answers '$code' - check DNS and: docker logs --tail 50 sanathana-caddy";;
      esac
    done
    exit 0
  fi
  if [ "$status" = unhealthy ] || [ "$status" = missing ] || [ "$restarts" -gt 2 ]; then
    break
  fi
  sleep 5
done

echo "The API did not become healthy (status: $status, restarts: $restarts). Last 80 lines of its log:"
docker logs --tail 80 sanathana-api || true
exit 1
'@
    $remoteScript = $remoteScript.Replace("__REMOTE_DIR__", $RemoteDir).Replace("__IMAGES__", $remoteImages).Replace("`r", "")
    [System.IO.File]::WriteAllText((Join-Path $Stage "deploy_step.sh"), $remoteScript, $utf8NoBom)

    Step "Preparing $RemoteDir on $RemoteHost"
    & ssh @sshArgs "$RemoteUser@$RemoteHost" "mkdir -p $RemoteDir"
    if ($LASTEXITCODE -ne 0) { Fail "ssh to $RemoteUser@$RemoteHost failed" }

    Step "Uploading $PayloadMb MB plus compose file and settings  (largest step - usually a few minutes)"
    Push-Location $Stage
    # -C compresses in transit: image layers are uncompressed tar, so this cuts the upload a lot.
    & scp -C @sshArgs $Payload "docker-compose.remote.yml" "Caddyfile" ".env" "deploy_step.sh" "$RemoteUser@${RemoteHost}:$RemoteDir/"
    $scpExit = $LASTEXITCODE
    Pop-Location
    if ($scpExit -ne 0) { Fail "scp failed" }

    Step $(if ($BuildOnServer) { "Building the images on the host and starting the stack  (slowest step - expect 5-15 minutes)" } else { "Loading images and starting the stack on the host" })
    & ssh @sshArgs "$RemoteUser@$RemoteHost" "bash $RemoteDir/deploy_step.sh"
    if ($LASTEXITCODE -ne 0) { Fail "remote deploy failed - see the API log above" }
    StepDone "running"
}
finally {
    Remove-Item -Recurse -Force $Stage -ErrorAction SilentlyContinue
}

Write-Host ("Deployment finished in {0}s." -f [math]::Round(((Get-Date) - $script:DeployStart).TotalSeconds, 1)) -ForegroundColor Green
Write-Host "Open https://enityasannidhi.com  (or http://${RemoteHost}:$WebPort directly, bypassing Caddy)."
