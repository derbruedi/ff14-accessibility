[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
$fixture = Join-Path (Get-LocalRoot) ('artifacts\verification-gate\' + [Guid]::NewGuid().ToString('N'))
foreach ($dir in @('FF14Accessibility', 'tests', 'scripts', 'Installer\Russian', 'artifacts', 'docs\maintenance', 'tools\nav-route-check')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $fixture $dir) | Out-Null
}
foreach ($name in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'FF14Accessibility\sample.cs', 'tests\sample.cs', 'scripts\sample.ps1', 'Installer\Russian\sample.cs', 'docs\maintenance\AUTHOR_NAVIGATION.json', 'tools\nav-route-check\NavigationPathCheck.cs')) {
    Set-Content -LiteralPath (Join-Path $fixture $name) -Value 'original' -Encoding UTF8
}
function Expect-Rejection([scriptblock]$Action, [string]$Scenario) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (!$rejected) { throw "Verification guard accepted invalid state: $Scenario" }
}
$checks = 0
Expect-Rejection { Assert-LocalVerification $fixture } 'missing report'; $checks++
$report = @{ Passed = $true; Total = 10; Skipped = 0; VerificationFingerprint = Get-LocalVerificationFingerprint $fixture }
$path = Join-Path $fixture 'artifacts\test-info.json'
function Save-Report { $report | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding UTF8 }
Save-Report
Assert-LocalVerification $fixture; $checks++
foreach ($field in @('Passed', 'Total', 'Skipped')) {
    $previous = $report[$field]
    $report[$field] = switch ($field) { 'Passed' { $false } 'Total' { 0 } 'Skipped' { 1 } }
    Save-Report
    Expect-Rejection { Assert-LocalVerification $fixture } $field; $checks++
    $report[$field] = $previous
}
Save-Report
foreach ($file in @('FF14Accessibility\sample.cs', 'tests\sample.cs', 'scripts\sample.ps1', 'Installer\Russian\sample.cs', 'docs\maintenance\AUTHOR_NAVIGATION.json', 'tools\nav-route-check\NavigationPathCheck.cs')) {
    Set-Content -LiteralPath (Join-Path $fixture $file) -Value 'modified' -Encoding UTF8
    Expect-Rejection { Assert-LocalVerification $fixture } "changed $file"; $checks++
    Set-Content -LiteralPath (Join-Path $fixture $file) -Value 'original' -Encoding UTF8
}
Assert-LocalVerification $fixture; $checks++
Write-Host "Verification guard scenarios passed: $checks"
