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
CONFIG=/usr/share/nginx/html/appsettings.json

# PLATFORM, when set, wins. Unset or empty, the Platform baked into the published
# appsettings.json is kept, so each branch's tracked file decides its hosted shell:
# Web on development, Mobile on development_mobileview. Before this, an unset
# PLATFORM meant Web on every branch, so a service created in the Render dashboard
# rather than from render.yaml served the web shell from this branch.
if [ -z "${PLATFORM:-}" ]; then
    PLATFORM="$(grep -o '"Platform"[[:space:]]*:[[:space:]]*"[A-Za-z]*"' "$CONFIG" 2>/dev/null \
                | head -n 1 | sed 's/.*"\([A-Za-z]*\)"$/\1/' || true)"
    PLATFORM="${PLATFORM:-Web}"
    echo "entrypoint: PLATFORM not set, using Platform=$PLATFORM from the published appsettings.json"
fi
export PORT API_PORT

# ---- SPA configuration -----------------------------------------------------
# The published wwwroot/appsettings.json points at the local-dev API
# (http://localhost:7050/api). Rewrite it on every start so the image can be
# repointed at a different API without a rebuild. The default is the relative
# path /api, which App.Web resolves against the page origin.
#
# Platform picks the shell the SPA renders: Web (the default) or Mobile, the
# phone shell — bottom navigation, the mobile skin, the mobile menu — served to
# a desktop browser so the MAUI app's UI can be reviewed without a device. It
# is the same switch as `Platform` in wwwroot/appsettings.json, applied here so
# no tracked file has to change: the development_mobileview branch sets
# PLATFORM=Mobile in its render.yaml and is otherwise identical to development.
# Mobile also moves every non-Admin user to the Mobile column of the access
# matrix, so it belongs on a dedicated preview service, never on the one users
# log in to. Any other value fails here, at start, rather than quietly rendering
# the web shell on a service with no shell access to go and check.
case "$PLATFORM" in
    Web|Mobile) ;;
    *) echo "entrypoint: PLATFORM must be Web or Mobile, got '$PLATFORM'" >&2; exit 1 ;;
esac

# One Google client id serves both halves: the API reads Google__ClientIds__0, and the SPA is
# handed the same value here unless GOOGLE_CLIENT_ID says otherwise. Empty hides the button.
GOOGLE_CLIENT_ID="${GOOGLE_CLIENT_ID:-${Google__ClientIds__0:-}}"

# Patch the published file in place instead of replacing it, so every other
# setting in the branch's tracked appsettings.json reaches the hosted app exactly
# as it runs locally. Only ApiBaseUrl has to differ: locally the API is a second
# origin (http://localhost:7050/api), here it is this origin's /api. Platform is
# rewritten too, but unset PLATFORM already equals the file's own value above.
# A file without both keys (never the tracked one) falls back to a minimal file.
if grep -q '"ApiBaseUrl"[[:space:]]*:' "$CONFIG" 2>/dev/null \
   && grep -q '"Platform"[[:space:]]*:' "$CONFIG" 2>/dev/null; then
    api_sed="$(printf '%s' "$API_BASE_URL" | sed 's/[\\|&]/\\&/g')"
    sed -i \
        -e "s|\(\"ApiBaseUrl\"[[:space:]]*:[[:space:]]*\"\)[^\"]*\"|\1${api_sed}\"|" \
        -e "s|\(\"Platform\"[[:space:]]*:[[:space:]]*\"\)[^\"]*\"|\1${PLATFORM}\"|" \
        "$CONFIG"
    # The tracked file already carries the Google client id; GOOGLE_CLIENT_ID, when set,
    # replaces it so a deployment can point at a different OAuth client without a rebuild.
    if [ -n "$GOOGLE_CLIENT_ID" ]; then
        sed -i -e "s|\(\"GoogleClientId\"[[:space:]]*:[[:space:]]*\"\)[^\"]*\"|\1${GOOGLE_CLIENT_ID}\"|" "$CONFIG"
    fi
else
    printf '{\n  "ApiBaseUrl": "%s",\n  "Platform": "%s",\n  "GoogleClientId": "%s"\n}\n' "$API_BASE_URL" "$PLATFORM" "$GOOGLE_CLIENT_ID" \
        > "$CONFIG"
fi

# Blazor publishes a precompressed sibling next to every static asset, and
# `gzip_static on` prefers it over the plain file whenever the client accepts
# gzip. Rewriting only the .json therefore changes nothing a browser can see:
# nginx keeps serving a .gz that still holds the build-time default, and the SPA
# goes on calling http://localhost:7050/api in production. Delete the siblings
# so the file just written is the only candidate — at a few dozen bytes there
# was nothing to gain by compressing it.
rm -f "$CONFIG.gz" "$CONFIG.br"

echo "entrypoint: ApiBaseUrl set to $API_BASE_URL, Platform set to $PLATFORM; GoogleClientId ${GOOGLE_CLIENT_ID:+set}${GOOGLE_CLIENT_ID:-empty (Google sign-in off)}"

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
