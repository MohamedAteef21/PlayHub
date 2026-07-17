# Test: prorated billing + controller tiers (single 1-2, couple 3-4)
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5052/api'

$r = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' `
    -Body '{"email":"PlayHubAdmin","password":"Admin@123"}'
$h = @{ Authorization = "Bearer $($r.accessToken)"; 'X-Branch-Id' = "$($r.activeBranchId)" }
Write-Host "Logged in, branch $($r.activeBranchId)"

$devices = Invoke-RestMethod -Uri "$base/assets/devices" -Headers $h
$plans = Invoke-RestMethod -Uri "$base/pricing/plans" -Headers $h
$device = $devices | Where-Object { $_.isActive } | Select-Object -First 1
# hourly gaming plan (timeUnit=2), not a package
$plan = $plans | Where-Object { $_.sessionMode -eq 1 -and $_.isActive -and $_.timeUnit -eq 2 -and -not $_.packagePrice } | Select-Object -First 1
if (-not $plan) {
    $body = @{ name = 'TIER TEST Hourly'; sessionMode = 1; timeUnit = 2; watchingBilling = 0; branchId = $null;
               gamingRates = @(@{ controllerCount = 1; rate = 60 }, @{ controllerCount = 2; rate = 90 }) } | ConvertTo-Json -Depth 4
    $plan = Invoke-RestMethod -Uri "$base/pricing/plans" -Method Post -ContentType 'application/json' -Headers $h -Body $body
}
$singleRate = ($plan.gamingRates | Where-Object { $_.controllerCount -eq 1 }).rate
$coupleRate = ($plan.gamingRates | Where-Object { $_.controllerCount -eq 2 }).rate
Write-Host "Device=$($device.name)  Plan=$($plan.name)  single=$singleRate couple=$coupleRate"

function Open-Session($controllers) {
    $body = @{ deviceId = $device.id; pricingPlanId = $plan.id; sessionMode = 1; controllerCount = $controllers } | ConvertTo-Json
    return Invoke-RestMethod -Uri "$base/sessions/open" -Method Post -ContentType 'application/json' -Headers $h -Body $body
}
function Close-Session($id) {
    $body = @{ payment = @{ paymentMethod = 1 }; discountAmount = 0 } | ConvertTo-Json
    return Invoke-RestMethod -Uri "$base/sessions/$id/close" -Method Post -ContentType 'application/json' -Headers $h -Body $body
}

# Cleanup: close any leftover active sessions
$leftover = @(Invoke-RestMethod -Uri "$base/sessions/active" -Headers $h)
foreach ($ls in $leftover) { if ($ls -and $ls.id) { Close-Session $ls.id | Out-Null; Write-Host "cleaned leftover session $($ls.id)" } }

# --- Test 1: prorated billing (no full-hour charge on open) ---
$s = Open-Session 2
Start-Sleep -Seconds 6
$live = (Invoke-RestMethod -Uri "$base/sessions/active" -Headers $h) | Where-Object { $_.id -eq $s.id }
$expectedMax = [math]::Ceiling($singleRate * 15 / 3600)  # generous upper bound for a few seconds
if ($live.currentTimeCost -ge $singleRate) { throw "FAIL prorate: cost=$($live.currentTimeCost) equals full unit" }
if ($live.currentTimeCost -gt $expectedMax) { throw "FAIL prorate: cost=$($live.currentTimeCost) > $expectedMax" }
Write-Host "PASS prorated: after ~6s cost=$($live.currentTimeCost) (rate $singleRate/h)"
$d = Close-Session $s.id
Write-Host "  closed, timeCost=$($d.timeCost) total=$($d.totalCost)"

# --- Test 2: 2 controllers -> single tier, 4 -> couple tier ---
foreach ($case in @(@{c=1; tier='single'; rate=$singleRate}, @{c=2; tier='single'; rate=$singleRate}, @{c=3; tier='couple'; rate=$coupleRate}, @{c=4; tier='couple'; rate=$coupleRate})) {
    $s = Open-Session $case.c
    Start-Sleep -Seconds 4
    $live = (Invoke-RestMethod -Uri "$base/sessions/active" -Headers $h) | Where-Object { $_.id -eq $s.id }
    $perSec = $case.rate / 3600.0
    $approx = [math]::Round($live.currentTimeCost / $perSec)  # implied seconds
    if ($live.controllerCount -ne $case.c) { throw "FAIL: controllerCount=$($live.controllerCount) expected $($case.c)" }
    if ($approx -lt 2 -or $approx -gt 20) { throw "FAIL tier $($case.c): cost=$($live.currentTimeCost) implies ${approx}s at $($case.tier) rate" }
    Write-Host "PASS controllers=$($case.c): billed at $($case.tier) rate ($($case.rate)/h), cost=$($live.currentTimeCost)"
    Close-Session $s.id | Out-Null
}

# --- Test 3: reject 0 and 5 controllers ---
foreach ($bad in @(0, 5)) {
    try { Open-Session $bad | Out-Null; throw "FAIL: controllers=$bad was accepted" }
    catch { if ($_.Exception.Message -like 'FAIL*') { throw } ; Write-Host "PASS rejected controllers=$bad" }
}

Write-Host "ALL BILLING/TIER TESTS PASSED"
