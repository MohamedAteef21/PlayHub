# Test: users page scoping + staff inherit master's subscription expiry
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5052/api'

$sa = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' `
    -Body '{"email":"PlayHubAdmin","password":"Admin@123"}'
$saH = @{ Authorization = "Bearer $($sa.accessToken)"; 'X-Branch-Id' = "$($sa.activeBranchId)" }
Write-Host "SuperAdmin logged in"

$stamp = Get-Random -Minimum 1000 -Maximum 9999
$expiry = (Get-Date).AddDays(30).ToString('yyyy-MM-dd')

# 1. SuperAdmin creates a master with expiry in 30 days
$masterBody = @{ username = "master$stamp"; password = 'Pass@123'; firstName = 'Test'; lastName = "Master$stamp";
                 subscriptionExpiresAt = $expiry; branchIds = @() } | ConvertTo-Json
$master = Invoke-RestMethod -Uri "$base/users" -Method Post -ContentType 'application/json' -Headers $saH -Body $masterBody
if ($master.role -ne 1) { throw "master role=$($master.role)" }
Write-Host "Created master$stamp (expiry $($master.subscriptionExpiresAt))"

# 2. Login as master, create a branch, then staff
$m = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' `
    -Body (@{ email = "master$stamp"; password = 'Pass@123' } | ConvertTo-Json)
$mH = @{ Authorization = "Bearer $($m.accessToken)" }
$branch = Invoke-RestMethod -Uri "$base/branches" -Method Post -ContentType 'application/json' -Headers $mH `
    -Body (@{ name = "Branch$stamp" } | ConvertTo-Json)
$mH['X-Branch-Id'] = "$($branch.id)"
Write-Host "Master created branch $($branch.name)"

$staffBody = @{ username = "staff$stamp"; password = 'Pass@123'; firstName = 'Test'; lastName = "Staff$stamp";
                branchIds = @($branch.id); permissionCodes = @() } | ConvertTo-Json
$staff = Invoke-RestMethod -Uri "$base/users" -Method Post -ContentType 'application/json' -Headers $mH -Body $staffBody

# 3. Staff must inherit master's expiry
if (-not $staff.subscriptionExpiresAt) { throw "FAIL: staff has no expiry" }
if ($staff.subscriptionExpiresAt.Substring(0,10) -ne $expiry) { throw "FAIL: staff expiry=$($staff.subscriptionExpiresAt) master=$expiry" }
Write-Host "PASS: staff inherited master expiry $($staff.subscriptionExpiresAt.Substring(0,10))"

# 4. Master sees only themselves + their staff
$mUsers = Invoke-RestMethod -Uri "$base/users?page=1&pageSize=100" -Headers $mH
$ids = $mUsers.items | ForEach-Object { $_.id }
$bad = $mUsers.items | Where-Object { $_.id -ne $master.id -and $_.parentUserId -ne $master.id }
if ($bad) { throw "FAIL: master sees foreign users: $($bad | ForEach-Object { $_.username })" }
if ($ids -notcontains $master.id) { throw "FAIL: master does not see himself" }
if ($ids -notcontains $staff.id) { throw "FAIL: master does not see his staff" }
Write-Host "PASS: master sees only self + own staff (count=$($mUsers.totalCount))"

# 5. SuperAdmin sees everyone (at least master + staff)
$saUsers = Invoke-RestMethod -Uri "$base/users?page=1&pageSize=100" -Headers $saH
$saIds = $saUsers.items | ForEach-Object { $_.id }
if ($saIds -notcontains $master.id -or $saIds -notcontains $staff.id) { throw "FAIL: superadmin missing users" }
Write-Host "PASS: superadmin sees all (count=$($saUsers.totalCount))"

# 6. SuperAdmin renews master's subscription -> staff expiry follows
$newExpiry = (Get-Date).AddDays(90).ToString('yyyy-MM-dd')
$updBody = @{ firstName = 'Test'; lastName = "Master$stamp"; isActive = $true;
              subscriptionExpiresAt = $newExpiry; branchIds = @() } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/users/$($master.id)" -Method Put -ContentType 'application/json' -Headers $saH -Body $updBody | Out-Null
$staff2 = Invoke-RestMethod -Uri "$base/users/$($staff.id)" -Headers $saH
if ($staff2.subscriptionExpiresAt.Substring(0,10) -ne $newExpiry) { throw "FAIL: staff expiry not cascaded: $($staff2.subscriptionExpiresAt)" }
Write-Host "PASS: renewing master cascaded expiry $newExpiry to staff"

Write-Host "ALL USER-SCOPE TESTS PASSED"
