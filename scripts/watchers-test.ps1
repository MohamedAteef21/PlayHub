# Test: change watcher count on an open watching session (flat + per-screen billing).
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5052/api'

$r = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' `
    -Body '{"email":"PlayHubAdmin","password":"Admin@123"}'
$h = @{ Authorization = "Bearer $($r.accessToken)"; 'X-Branch-Id' = "$($r.activeBranchId)" }
"login OK"

$devices = Invoke-RestMethod -Uri "$base/assets/devices" -Headers $h
$device = $devices | Where-Object { $_.isActive } | Select-Object -First 1
"device: $($device.name)"

# per-screen (time billed) watching plan
$plans = Invoke-RestMethod -Uri "$base/pricing/plans" -Headers $h
$plan = $plans | Where-Object { $_.sessionMode -eq 2 -and $_.watchingBilling -eq 2 -and $_.isActive } | Select-Object -First 1
if (-not $plan) {
    $body = @{ name = 'E2E Watch PerScreen'; sessionMode = 2; timeUnit = 2; watchingBilling = 2; branchId = $null;
               watchingRates = @(@{ ratePerPerson = 10 }) } | ConvertTo-Json -Depth 4
    $plan = Invoke-RestMethod -Uri "$base/pricing/plans" -Method Post -ContentType 'application/json' -Headers $h -Body $body
    "created per-screen plan"
}

$s = Invoke-RestMethod -Uri "$base/sessions/open" -Method Post -ContentType 'application/json' -Headers $h `
    -Body (@{ deviceId = $device.id; pricingPlanId = $plan.id; sessionMode = 2; watcherCount = 2 } | ConvertTo-Json)
"opened watching session watchers=$($s.watcherCount) accrued=$($s.accruedTimeCost)"

Start-Sleep -Seconds 3

# increase watchers 2 -> 4
$s2 = Invoke-RestMethod -Uri "$base/sessions/$($s.id)/watchers" -Method Post -ContentType 'application/json' -Headers $h `
    -Body '{"watcherCount":4}'
"after +2: watchers=$($s2.watcherCount) accrued=$($s2.accruedTimeCost) elapsed=$($s2.elapsedSeconds)"
if ($s2.watcherCount -ne 4) { throw "watcherCount=$($s2.watcherCount) expected 4" }

Start-Sleep -Seconds 2

# decrease watchers 4 -> 1
$s3 = Invoke-RestMethod -Uri "$base/sessions/$($s.id)/watchers" -Method Post -ContentType 'application/json' -Headers $h `
    -Body '{"watcherCount":1}'
"after -3: watchers=$($s3.watcherCount) accrued=$($s3.accruedTimeCost)"
if ($s3.watcherCount -ne 1) { throw "watcherCount=$($s3.watcherCount) expected 1" }

# over capacity rejected
try {
    Invoke-RestMethod -Uri "$base/sessions/$($s.id)/watchers" -Method Post -ContentType 'application/json' -Headers $h `
        -Body '{"watcherCount":999}' | Out-Null
    throw 'over-capacity accepted!'
} catch {
    if ($_.Exception.Message -eq 'over-capacity accepted!') { throw }
    "over-capacity correctly rejected"
}

# active sessions list still works
$active = Invoke-RestMethod -Uri "$base/sessions/active" -Headers $h
"active list OK count=$($active.Count)"

# close (cash) to clean up
$closed = Invoke-RestMethod -Uri "$base/sessions/$($s.id)/close" -Method Post -ContentType 'application/json' -Headers $h `
    -Body (@{ payment = @{ paymentMethod = 1 }; discountAmount = 0 } | ConvertTo-Json)
"closed total=$($closed.totalCost) timeCost=$($closed.timeCost)"
"ALL WATCHERS TESTS PASSED"
