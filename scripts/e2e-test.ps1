# PlayHub end-to-end API smoke test (run against local dev API).
# Creates a TEST customer + one short session, then cleans up what it can.
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5052/api'
$results = @()

function Step($name, $script) {
    [Console]::Out.WriteLine("RUNNING: $name")
    try {
        $out = & $script
        $script:results += [pscustomobject]@{ Step = $name; Result = 'PASS'; Info = "$out" }
        [Console]::Out.WriteLine("  PASS: $out")
    } catch {
        $script:results += [pscustomobject]@{ Step = $name; Result = 'FAIL'; Info = $_.Exception.Message }
        [Console]::Out.WriteLine("  FAIL: $($_.Exception.Message)")
    }
}

# 1. Login
$r = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' `
    -Body '{"email":"PlayHubAdmin","password":"Admin@123"}'
$h = @{ Authorization = "Bearer $($r.accessToken)"; 'X-Branch-Id' = "$($r.activeBranchId)" }
Step 'Login SuperAdmin' { if ($r.user.role -ne 2) { throw "role=$($r.user.role)" }; "role=2, branch=$($r.activeBranchId)" }

# 2. Me endpoint
Step 'GET /auth/me' {
    $me = Invoke-RestMethod -Uri "$base/auth/me" -Headers $h
    "isMaster=$($me.isMaster) role=$($me.role)"
}

# 3. Users list
Step 'GET /users' {
    $u = Invoke-RestMethod -Uri "$base/users?page=1&pageSize=50" -Headers $h
    "count=$($u.totalCount); ma21 role=$(($u.items | Where-Object { $_.username -eq 'ma21' }).role)"
}

# 4. Devices + plans
$devices = Invoke-RestMethod -Uri "$base/assets/devices" -Headers $h
$plans = Invoke-RestMethod -Uri "$base/pricing/plans" -Headers $h
Step 'GET devices + pricing plans' { "devices=$($devices.Count) plans=$($plans.Count)" }

$freeDevice = $devices | Where-Object { $_.isActive -and $_.status -eq 1 } | Select-Object -First 1
if (-not $freeDevice) { $freeDevice = $devices | Where-Object { $_.isActive } | Select-Object -First 1 }
$gamingPlan = $plans | Where-Object { $_.sessionMode -eq 1 -and $_.isActive } | Select-Object -First 1
if (-not $gamingPlan) {
    Step 'Create gaming pricing plan (none existed)' {
        $body = @{ name = 'E2E Hourly'; sessionMode = 1; timeUnit = 2; watchingBilling = 0; branchId = $null;
                   gamingRates = @(@{ controllerCount = 1; rate = 20 }, @{ controllerCount = 2; rate = 30 }) } | ConvertTo-Json -Depth 4
        $script:gamingPlan = Invoke-RestMethod -Uri "$base/pricing/plans" -Method Post -ContentType 'application/json' -Headers $h -Body $body
        "planId=$($script:gamingPlan.id)"
    }
}

# 5. Customer + wallet
$phone = "010$(Get-Random -Minimum 10000000 -Maximum 99999999)"
$cust = Invoke-RestMethod -Uri "$base/customers" -Method Post -ContentType 'application/json' -Headers $h `
    -Body (@{ name = 'TEST AUTO CLEANUP'; phone = $phone } | ConvertTo-Json)
Step 'Create customer' { "id=$($cust.id) code=$($cust.code)" }

Step 'Wallet top-up 50 + bonus 5' {
    $c2 = Invoke-RestMethod -Uri "$base/customers/$($cust.id)/wallet/topup" -Method Post -ContentType 'application/json' -Headers $h `
        -Body '{"amount":50,"bonusAmount":5,"note":"e2e test"}'
    if ($c2.walletBalance -ne 55) { throw "balance=$($c2.walletBalance) expected 55" }
    "balance=55 OK"
}

Step 'Wallet transactions listed' {
    $tx = Invoke-RestMethod -Uri "$base/customers/$($cust.id)/wallet?page=1&pageSize=10" -Headers $h
    if ($tx.totalCount -lt 2) { throw "expected >=2 tx, got $($tx.totalCount)" }
    "txCount=$($tx.totalCount)"
}

# 6. Session lifecycle
$session = $null
Step 'Open session (planned 60 min, with customer)' {
    $body = @{ deviceId = $freeDevice.id; pricingPlanId = $gamingPlan.id; sessionMode = 1;
               controllerCount = 1; plannedDurationMinutes = 60; customerId = $cust.id } | ConvertTo-Json
    $script:session = Invoke-RestMethod -Uri "$base/sessions/open" -Method Post -ContentType 'application/json' -Headers $h -Body $body
    "sessionId=$($script:session.id) device=$($script:session.deviceName)"
}

Step 'Pause session' {
    $s = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/pause" -Method Post -Headers $h
    if ($s.status -ne 2) { throw "status=$($s.status)" }
    "paused OK"
}

Start-Sleep -Seconds 3

Step 'Resume session (paused time excluded)' {
    $s = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/resume" -Method Post -Headers $h
    if ($s.status -ne 1) { throw "status=$($s.status)" }
    if ($s.totalPausedSeconds -lt 2) { throw "totalPausedSeconds=$($s.totalPausedSeconds)" }
    "totalPausedSeconds=$($s.totalPausedSeconds)"
}

# 7. Cafeteria on session
$items = Invoke-RestMethod -Uri "$base/cafeteria/items" -Headers $h
$item = $items | Where-Object { $_.isActive -and $_.currentQuantity -gt 0 } | Select-Object -First 1
Step 'Add cafeteria item to session' {
    if (-not $item) { throw 'no active item with stock' }
    $s = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/cafeteria" -Method Post -ContentType 'application/json' -Headers $h `
        -Body (@{ cafeteriaItemId = $item.id; quantity = 1; unit = 0 } | ConvertTo-Json)
    if ($s.cafeteriaLines.Count -lt 1) { throw 'no cafeteria lines' }
    "item=$($item.name) cafCost=$($s.cafeteriaCost)"
}

Step 'Extend session +30 min' {
    $s = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/extend" -Method Post -ContentType 'application/json' -Headers $h `
        -Body '{"additionalMinutes":30}'
    if ($s.plannedDurationMinutes -ne 90) { throw "planned=$($s.plannedDurationMinutes)" }
    "planned=90 OK"
}

Step 'Convert to open time' {
    $s = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/extend" -Method Post -ContentType 'application/json' -Headers $h `
        -Body '{"additionalMinutes":null}'
    if ($null -ne $s.plannedDurationMinutes) { throw "planned=$($s.plannedDurationMinutes)" }
    "open timer OK"
}

# 8. Close with split payment (wallet 10 + cash rest)
$closed = $null
Step 'Close session (wallet 10 + cash)' {
    $body = @{ payment = @{ paymentMethod = 1; walletAmount = 10 }; discountAmount = 0 } | ConvertTo-Json
    $script:closed = Invoke-RestMethod -Uri "$base/sessions/$($session.id)/close" -Method Post -ContentType 'application/json' -Headers $h -Body $body
    "invoice=$($script:closed.invoiceNumber) total=$($script:closed.totalCost)"
}

Step 'Wallet balance reduced by 10' {
    $c3 = Invoke-RestMethod -Uri "$base/customers/$($cust.id)" -Headers $h
    if ($c3.walletBalance -ne 45) { throw "balance=$($c3.walletBalance) expected 45" }
    "balance=45 OK"
}

# 9. Cash drawer
$today = (Get-Date).ToString('yyyy-MM-dd')
$drawer1 = Invoke-RestMethod -Uri "$base/reports/cash-drawer?date=$today&tzOffsetMinutes=180" -Headers $h
Step 'Cash drawer reflects wallet top-up + cash session' {
    "balance=$($drawer1.drawerBalance) cashIn(day)=$($drawer1.totalCashIn) topups=$($drawer1.cashWalletTopUps)"
}

Step 'Collect 10 (partial)' {
    $d2 = Invoke-RestMethod -Uri "$base/reports/cash-drawer/collect" -Method Post -ContentType 'application/json' -Headers $h `
        -Body (@{ amount = 10; note = 'E2E TEST COLLECTION'; date = $today; tzOffsetMinutes = 180 } | ConvertTo-Json)
    $diff = $drawer1.drawerBalance - $d2.drawerBalance
    if ($diff -ne 10) { throw "balance dropped by $diff, expected 10" }
    "balance $($drawer1.drawerBalance) -> $($d2.drawerBalance) OK; dayCollections=$($d2.dayCollections.Count)"
}

Step 'Collect more than balance rejected' {
    try {
        Invoke-RestMethod -Uri "$base/reports/cash-drawer/collect" -Method Post -ContentType 'application/json' -Headers $h `
            -Body (@{ amount = 999999; date = $today; tzOffsetMinutes = 180 } | ConvertTo-Json) | Out-Null
        throw 'was accepted!'
    } catch {
        if ($_.Exception.Message -eq 'was accepted!') { throw $_ }
        'correctly rejected'
    }
}

# 10. Reports + audit
Step 'Revenue report' {
    $rev = Invoke-RestMethod -Uri "$base/reports/revenue?from=$today&to=$today" -Headers $h
    "total=$($rev.totalRevenue)"
}

Step 'Audit log contains session close + collection' {
    $logs = Invoke-RestMethod -Uri "$base/audit?page=1&pageSize=20" -Headers $h
    $actions = $logs.items | Select-Object -ExpandProperty actionType
    if ($actions -notcontains 'Session.Closed') { throw 'Session.Closed missing' }
    if ($actions -notcontains 'CashDrawer.Collected') { throw 'CashDrawer.Collected missing' }
    "total=$($logs.totalCount) recent OK"
}

Step 'Receivables endpoint' {
    $rec = Invoke-RestMethod -Uri "$base/receivables" -Headers $h
    "count=$($rec.Count)"
}

Step 'Invoice PDF downloads' {
    $pdf = Invoke-WebRequest -Uri "$base/alerts/invoices/$($session.id)/pdf" -Headers $h -UseBasicParsing
    if ($pdf.Content.Length -lt 500) { throw "pdf too small: $($pdf.Content.Length)" }
    "pdfBytes=$($pdf.Content.Length)"
}

# 10b. More read endpoints
Step 'Accounting dashboard' {
    $a = Invoke-RestMethod -Uri "$base/accounting/dashboard?from=$today&to=$today" -Headers $h
    'OK'
}
Step 'Inventory movements' {
    $m = Invoke-RestMethod -Uri "$base/inventory/movements?page=1&pageSize=5" -Headers $h
    "count=$($m.totalCount)"
}
Step 'Offers list' {
    $o = Invoke-RestMethod -Uri "$base/offers" -Headers $h
    "count=$($o.Count)"
}
Step 'Branches list' {
    $b = Invoke-RestMethod -Uri "$base/branches" -Headers $h
    "count=$($b.Count)"
}
Step 'Notifications' {
    $n = Invoke-RestMethod -Uri "$base/notifications/unread-count" -Headers $h
    "unread=$($n.count)"
}
Step 'Session history' {
    $sh = Invoke-RestMethod -Uri "$base/sessions/history?page=1&pageSize=5" -Headers $h
    "count=$($sh.totalCount)"
}
Step 'Best sellers report' {
    $bs = Invoke-RestMethod -Uri "$base/reports/best-sellers?from=$today&to=$today" -Headers $h
    "count=$($bs.Count)"
}
Step 'Device usage report' {
    $du = Invoke-RestMethod -Uri "$base/reports/device-usage?from=$today&to=$today" -Headers $h
    "count=$($du.Count)"
}

# 11. Cleanup test customer
Step 'Cleanup: deactivate test customer' {
    Invoke-RestMethod -Uri "$base/customers/$($cust.id)" -Method Delete -Headers $h | Out-Null
    'deleted'
}

''
$results | Format-Table -AutoSize | Out-String -Width 200
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
"TOTAL: $($results.Count) steps, FAILED: $failed"
