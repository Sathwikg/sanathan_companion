#!/bin/bash
# Sanathana Companion — container init for Render.
#
# Compose supervises two containers; here one container supervises two
# processes. This script does what compose did for us: render the nginx config,
# point the SPA at the API, start both, and make the container exit as soon as
# either process dies so Render restarts the whole thing rather than leaving a
# half-dead instance serving 502s.
set -euo pipefail

# Render injects PORT; the fallbacks keep `docker run` working locally.
PORT="${PORT:-10000}"
API_PORT="${API_PORT:-8080}"
API_BASE_URL="${API_BASE_URL:-/api}"
export PORT API_PORT

# ---- SPA configuration -----------------------------------------------------
# The published wwwroot/appsettings.json points at the local-dev API
# (http://localhost:7050/api). Rewrite it on every start so the image can be
# repointed at a different API without a rebuild. The default is the relative
# path /api, which App.Web resolves against the page origin.
printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$API_BASE_URL" \
    > /usr/share/nginx/html/appsettings.json

# Blazor publishes a precompressed sibling next to every static asset, and
# `gzip_static on` prefers it over the plain file whenever the client accepts
# gzip. Rewriting only the .json therefore changes nothing a browser can see:
# nginx keeps serving a .gz that still holds the build-time default, and the SPA
# goes on calling http://localhost:7050/api in production. Delete the siblings
# so the file just written is the only candidate — at a few dozen bytes there
# was nothing to gain by compressing it.
rm -f /usr/share/nginx/html/appsettings.json.gz \
      /usr/share/nginx/html/appsettings.json.br

echo "entrypoint: ApiBaseUrl set to $API_BASE_URL"

# ---- nginx configuration ---------------------------------------------------
# Restrict envsubst to these two names so nginx's own $variables are left alone.
envsubst '${PORT} ${API_PORT}' \
    < /etc/nginx/templates/default.conf.template \
    > /etc/nginx/conf.d/default.conf
nginx -t
echo "entrypoint: nginx will listen on $PORT, proxying /api to 127.0.0.1:$API_PORT"

# ---- processes -------------------------------------------------------------
# Kestrel first. It is not waited on: nginx answers immediately and returns 502
# on /api until migrations finish, which is exactly what Render's health check
# on /api/regions/options should see — the instance goes live only once the
# database is actually reachable.
cd /app/api
dotnet Sanathana.Companion.Api.dll &
api_pid=$!

nginx -g 'daemon off;' &
nginx_pid=$!

shutdown() {
    trap - TERM INT
    echo "entrypoint: shutting down"
    kill -TERM "$api_pid" "$nginx_pid" 2>/dev/null || true
}
trap shutdown TERM INT

# Wait for whichever process exits first, then take the other one down with it.
set +e
wait -n
status=$?
echo "entrypoint: a supervised process exited with status $status"
shutdown
wait
exit "$status"
