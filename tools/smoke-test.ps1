<#
.SYNOPSIS
  Pre-release smoke test: signs in as each role and checks that the pages each role relies on still load.

.DESCRIPTION
  Read-only: it only signs in and calls GET endpoints, so it is safe to run against a database with real test data.
  Run it with the API already started. Credentials come from environment variables so none are stored in the repo;
  a role without credentials is skipped (and reported as skipped).

    $env:SMOKE_SUPERADMIN_USER / $env:SMOKE_SUPERADMIN_PASS
    $env:SMOKE_CO_USER         / $env:SMOKE_CO_PASS        (Compliance Officer)
    $env:SMOKE_OWNER_USER      / $env:SMOKE_OWNER_PASS     (Document Owner)
    $env:SMOKE_DEPTADMIN_USER  / $env:SMOKE_DEPTADMIN_PASS (Department Admin)

  Exit code 0 when every check passed, 1 when anything failed. See SMOKE_TEST.md for the manual checklist
  that goes with it (the parts only a person clicking through can check).

.EXAMPLE
  $env:SMOKE_SUPERADMIN_USER = 'superadmin'; $env:SMOKE_SUPERADMIN_PASS = '...'
  .\tools\smoke-test.ps1 -ApiBase http://localhost:5101/api
#>
param(
    [string]$ApiBase = 'http://localhost:5101/api',
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$results = New-Object System.Collections.Generic.List[object]

function Add-Result([string]$Role, [string]$Check, [string]$Outcome, [string]$Detail = '') {
    $results.Add([pscustomobject]@{ Role = $Role; Check = $Check; Outcome = $Outcome; Detail = $Detail })
    $colour = @{ PASS = 'Green'; FAIL = 'Red'; SKIP = 'Yellow' }[$Outcome]
    Write-Host ("  [{0}] {1}{2}" -f $Outcome, $Check, $(if ($Detail) { " - $Detail" } else { '' })) -ForegroundColor $colour
}

function Invoke-Check([string]$Role, [string]$Path, [hashtable]$Headers) {
    try {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-RestMethod -Method Get -Uri "$ApiBase/$Path" -Headers $Headers -TimeoutSec $TimeoutSeconds | Out-Null
        Add-Result $Role "GET $Path" 'PASS' ("{0:N1}s" -f $watch.Elapsed.TotalSeconds)
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        Add-Result $Role "GET $Path" 'FAIL' $(if ($status) { "HTTP $status" } else { $_.Exception.Message })
    }
}

# What each role must be able to open. Paths containing {me} get the signed-in user's id.
$roles = [ordered]@{
    'Super Admin' = @{
        Prefix = 'SMOKE_SUPERADMIN'
        Paths  = @('user/me', 'notifications/unread-count', 'user/all', 'system-settings', 'AuditLog',
                   'compliance/dashboard', 'reports/monthly', 'reports/document-owners', 'reports/client-risk-rating',
                   'reports/system-audit', 'reports/document-owner-compliance', 'reports/institution-access-history',
                   'reports/expiring-documents', 'reports/outstanding-compliance', 'reports/ad-hoc/recent')
    }
    'Compliance Officer' = @{
        Prefix = 'SMOKE_CO'
        Paths  = @('user/me', 'notifications/unread-count', 'compliance/documents/pending-review', 'compliance/dashboard',
                   'reports/monthly', 'reports/document-owners')
    }
    'Document Owner' = @{
        Prefix = 'SMOKE_OWNER'
        Paths  = @('user/me', 'notifications/unread-count', 'documents', 'documents/flags', 'users/me/documents/required',
                   'document-access-requests/pending', 'document-access-requests/extensions',
                   'compliance/users/{me}', 'reports/activity/{me}')
    }
    'Department Admin' = @{
        Prefix = 'SMOKE_DEPTADMIN'
        Paths  = @('user/me', 'notifications/unread-count', 'department-access-requests/pending', 'documents')
    }
}

Write-Host "DocuVault smoke test against $ApiBase" -ForegroundColor Cyan

foreach ($role in $roles.Keys) {
    $config = $roles[$role]
    $user = [Environment]::GetEnvironmentVariable("$($config.Prefix)_USER")
    $pass = [Environment]::GetEnvironmentVariable("$($config.Prefix)_PASS")
    Write-Host ""
    Write-Host $role -ForegroundColor Cyan

    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($pass)) {
        Add-Result $role 'sign in' 'SKIP' "set $($config.Prefix)_USER and $($config.Prefix)_PASS to test this role"
        continue
    }

    try {
        $body = @{ username = $user; password = $pass } | ConvertTo-Json
        $login = Invoke-RestMethod -Method Post -Uri "$ApiBase/user/login" -ContentType 'application/json' -Body $body -TimeoutSec $TimeoutSeconds
        $token = $login.token
        if (-not $token) { throw 'No token in the sign-in response (is a one-time code required for this account?)' }
        Add-Result $role 'sign in' 'PASS'
    }
    catch {
        Add-Result $role 'sign in' 'FAIL' $_.Exception.Message
        continue
    }

    $headers = @{ Authorization = "Bearer $token" }
    $me = $null
    try {
        $account = Invoke-RestMethod -Method Get -Uri "$ApiBase/user/me" -Headers $headers -TimeoutSec $TimeoutSeconds
        $me = if ($account.userId) { $account.userId } else { $account.id }
    } catch { }

    foreach ($path in $config.Paths) {
        if ($path -like '*{me}*') {
            if (-not $me) { Add-Result $role "GET $path" 'SKIP' 'could not read the signed-in user id'; continue }
            $path = $path.Replace('{me}', $me)
        }
        Invoke-Check $role $path $headers
    }
}

$failed = @($results | Where-Object Outcome -eq 'FAIL')
$skipped = @($results | Where-Object Outcome -eq 'SKIP')
Write-Host ""
Write-Host ("{0} passed, {1} failed, {2} skipped" -f @($results | Where-Object Outcome -eq 'PASS').Count, $failed.Count, $skipped.Count) `
    -ForegroundColor $(if ($failed.Count) { 'Red' } else { 'Green' })
exit $(if ($failed.Count) { 1 } else { 0 })
