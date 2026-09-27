#!/bin/sh
# Runs from nginx's /docker-entrypoint.d before the server starts.
#
# The published wwwroot/appsettings.json points at the local-dev API
# (http://localhost:7050/api). In a container the SPA and the API share one
# origin behind this nginx, so the default is the relative path /api — which
# App.Web resolves against the page origin at startup.
set -eu

API_BASE_URL="${API_BASE_URL:-/api}"
# Web or Mobile: the shell the SPA renders. The same switch as `Platform` in
# wwwroot/appsettings.json; deploy/render/entrypoint.sh has the full story.
# Unset or empty keeps the Platform baked into the published appsettings.json, so
# each branch's tracked file decides: Web on development, Mobile on this branch.
CONFIG=/usr/share/nginx/html/appsettings.json
if [ -z "${PLATFORM:-}" ]; then
    PLATFORM="$(grep -o '"Platform"[[:space:]]*:[[:space:]]*"[A-Za-z]*"' "$CONFIG" 2>/dev/null \
                | head -n 1 | sed 's/.*"\([A-Za-z]*\)"$/\1/' || true)"
    PLATFORM="${PLATFORM:-Web}"
fi

case "$PLATFORM" in
    Web|Mobile) ;;
    *) echo "20-api-base-url.sh: PLATFORM must be Web or Mobile, got '$PLATFORM'" >&2; exit 1 ;;
esac

# Empty hides the Sign in with Google button; the API side is Google__ClientIds__0.
GOOGLE_CLIENT_ID="${GOOGLE_CLIENT_ID:-}"

# Patch the published file in place instead of replacing it, so every other
# setting in the branch's tracked appsettings.json reaches the container exactly
# as it runs locally. Only ApiBaseUrl has to differ; deploy/render/entrypoint.sh
# has the full story.
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
    printf '{\n  "ApiBaseUrl": "%s",\n  "Platform": "%s",\n  "GoogleClientId": "%s"\n}\n' "$API_BASE_URL" "$PLATFORM" "$GOOGLE_CLIENT_ID" > "$CONFIG"
fi

# Blazor publishes a precompressed sibling next to every static asset, and the
# `gzip_static on` in nginx.conf prefers it over the plain file whenever the
# client accepts gzip. Rewriting only the .json therefore changes nothing a
# browser can see: nginx keeps serving a .gz that still holds the build-time
# default, and the SPA goes on calling http://localhost:7050/api. Delete the
# siblings so the file just written is the only candidate — at a few dozen
# bytes there was nothing to gain by compressing it.
rm -f "$CONFIG.gz" "$CONFIG.br"

echo "20-api-base-url.sh: ApiBaseUrl set to $API_BASE_URL, Platform set to $PLATFORM"
