#!/usr/bin/env bash
# Sync the latest React build into the API wwwroot (for local + publish).
# Usage (from repo root):
#   bash scripts/sync-spa.sh
#   bash scripts/sync-spa.sh --skip-build

set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEB="$ROOT/web"
DIST="$WEB/dist"
WWWROOT="$ROOT/src/PlayHub.Api/wwwroot"
SKIP_BUILD=0

if [[ "${1:-}" == "--skip-build" ]]; then
  SKIP_BUILD=1
fi

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  echo "Building frontend (web)..."
  (cd "$WEB" && npm run build)
fi

if [[ ! -f "$DIST/index.html" ]]; then
  echo "Missing $DIST/index.html — run npm run build in web/ first." >&2
  exit 1
fi

echo "Syncing $DIST -> $WWWROOT"
rm -rf "$WWWROOT"
mkdir -p "$WWWROOT"
cp -a "$DIST"/. "$WWWROOT"/
echo "Done. Restart the API (or hard-refresh the browser) to load the new UI."
