# Sync the latest React build into the API wwwroot (for local + MonsterASP publish).
# Usage (from repo root):
#   powershell -File scripts/sync-spa.ps1
# Optional skip build if web/dist already fresh:
#   powershell -File scripts/sync-spa.ps1 -SkipBuild

param(
  [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root 'web'
$dist = Join-Path $web 'dist'
$wwwroot = Join-Path $root 'src/PlayHub.Api/wwwroot'

if (-not $SkipBuild) {
  Write-Host 'Building frontend (web)...'
  Push-Location $web
  try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit $LASTEXITCODE" }
  } finally {
    Pop-Location
  }
}

if (-not (Test-Path (Join-Path $dist 'index.html'))) {
  throw "Missing $dist/index.html — run npm run build in web/ first."
}

Write-Host "Syncing $dist -> $wwwroot"
if (Test-Path $wwwroot) {
  Remove-Item -Recurse -Force $wwwroot
}
New-Item -ItemType Directory -Path $wwwroot | Out-Null
Copy-Item -Path (Join-Path $dist '*') -Destination $wwwroot -Recurse -Force
Write-Host 'Done. Restart the API (or hard-refresh the browser) to load the new UI.'
