# Starts the WhatsApp API for IIS reverse-proxy (HTTP on loopback only).
# HTTPS is terminated by IIS — see README-IIS.md and iis\web.config

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$env:PORT = if ($env:PORT) { $env:PORT } else { '3000' }
$env:HOST = if ($env:HOST) { $env:HOST } else { '127.0.0.1' }
$env:QUIET_QR_TERMINAL = if ($env:QUIET_QR_TERMINAL) { $env:QUIET_QR_TERMINAL } else { '1' }

Write-Host "Starting WhatsApp API on http://$($env:HOST):$($env:PORT)"
Write-Host "Point IIS HTTPS site to this address via iis\web.config"
node index.js
