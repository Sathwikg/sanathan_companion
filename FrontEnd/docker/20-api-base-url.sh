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
PLATFORM="${PLATFORM:-Web}"
CONFIG=/usr/share/nginx/html/appsettings.json

case "$PLATFORM" in
    Web|Mobile) ;;
    *) echo "20-api-base-url.sh: PLATFORM must be Web or Mobile, got '$PLATFORM'" >&2; exit 1 ;;
esac

printf '{\n  "ApiBaseUrl": "%s",\n  "Platform": "%s"\n}\n' "$API_BASE_URL" "$PLATFORM" > "$CONFIG"

# Blazor publishes a precompressed sibling next to every static asset, and the
# `gzip_static on` in nginx.conf prefers it over the plain file whenever the
# client accepts gzip. Rewriting only the .json therefore changes nothing a
# browser can see: nginx keeps serving a .gz that still holds the build-time
# default, and the SPA goes on calling http://localhost:7050/api. Delete the
# siblings so the file just written is the only candidate — at a few dozen
# bytes there was nothing to gain by compressing it.
rm -f "$CONFIG.gz" "$CONFIG.br"

echo "20-api-base-url.sh: ApiBaseUrl set to $API_BASE_URL, Platform set to $PLATFORM"
