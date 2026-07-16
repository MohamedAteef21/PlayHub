# Kill orphaned Puppeteer Chrome that locks .wwebjs_auth (Windows).
# Run when: EBUSY lockfile, "browser is already running", or no QR after crash.

$ErrorActionPreference = 'Continue'
$sessionRoot = Join-Path $PSScriptRoot '.wwebjs_auth'
$marker = '.wwebjs_auth'

Write-Host 'Stopping orphaned Puppeteer Chrome for PlayHub WhatsApp sessions...'

$killed = 0
Get-CimInstance Win32_Process -Filter "Name = 'chrome.exe'" -ErrorAction SilentlyContinue |
  Where-Object {
    $_.CommandLine -and (
      $_.CommandLine -like "*$marker*" -or
      ($_.CommandLine -like '*\puppeteer\chrome\*' -and $_.CommandLine -like '*whatsapp-gateway*')
    )
  } |
  ForEach-Object {
    try {
      Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
      Write-Host "  Killed chrome PID $($_.ProcessId)"
      $killed++
    } catch {
      Write-Host "  Could not kill PID $($_.ProcessId): $_"
    }
  }

Get-CimInstance Win32_Process -Filter "Name = 'chromium.exe'" -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -and $_.CommandLine -like "*$marker*" } |
  ForEach-Object {
    try {
      Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
      Write-Host "  Killed chromium PID $($_.ProcessId)"
      $killed++
    } catch { }
  }

Write-Host "Stopped $killed process(es)."

Get-ChildItem -Path $sessionRoot -Recurse -Filter 'lockfile' -ErrorAction SilentlyContinue |
  ForEach-Object {
    try {
      Remove-Item $_.FullName -Force
      Write-Host "Removed $($_.FullName)"
    } catch {
      Write-Host "Could not remove $($_.FullName): $_"
    }
  }

Write-Host 'Done. Restart: npm start'
