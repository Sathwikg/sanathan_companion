#!/bin/sh
# Runs from nginx's /docker-entrypoint.d before the server starts.
#
# The published wwwroot/appsettings.json points at the local-dev API
# (http://localhost:7050/api). In a container the SPA and the API share one
# origin behind this nginx, so the default is the relative path /api — which
# App.Web resolves against the page origin at startup.
set -eu

API_BASE_URL="${API_BASE_URL:-/api}"
CONFIG=/usr/share/nginx/html/appsettings.json

printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$API_BASE_URL" > "$CONFIG"

echo "20-api-base-url.sh: ApiBaseUrl set to $API_BASE_URL"
