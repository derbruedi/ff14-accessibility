[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
$root = Get-LocalRoot
$logs = Join-Path $root 'artifacts\logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$fingerprint = Get-LocalVerificationFingerprint $root
$reportPath = Join-Path $root 'artifacts\test-info.json'
@{ Passed = $false; Total = 0; Skipped = 0; VerificationFingerprint = $fingerprint } |
    ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
$projects = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests') -Filter '*.csproj' -File -Recurse)
if (!$projects.Count) { throw 'No test projects found.' }
$requiredJson = Get-Content -LiteralPath (Join-Path $root 'tests\required-projects.json') -Raw | ConvertFrom-Json
# Windows PowerShell 5.1 emits a JSON array as one pipeline object.
$required = @($requiredJson | ForEach-Object { [string]$_ })
$actual = @($projects | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') })
if (Compare-Object $required $actual) { throw 'Test projects differ from tests/required-projects.json. Account for every suite.' }
$total = 0
& (Join-Path $PSScriptRoot 'Test-AuthorNavigation.ps1')
& (Join-Path $PSScriptRoot 'Test-VerificationGate.ps1')
foreach ($project in $projects) {
    $results = Join-Path $root ("artifacts\test-results\$stamp\" + $project.BaseName)
    & dotnet test $project.FullName -c Release -warnaserror --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=results.trx' --results-directory $results 2>&1 |
        Tee-Object -FilePath (Join-Path $logs ("test-$stamp-" + $project.BaseName + '.txt')) | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $($project.Name), exit $LASTEXITCODE" }
    [xml]$trx = Get-Content -LiteralPath (Join-Path $results 'results.trx') -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if (!$counters -or [int]$counters.total -le 0 -or [int]$counters.passed -ne [int]$counters.total -or
        [int]$counters.executed -ne [int]$counters.total) { throw "Failed, skipped or missing tests: $($project.Name)" }
    $total += [int]$counters.total
}
if ((Get-LocalVerificationFingerprint $root) -ne $fingerprint) { throw 'Inputs changed during testing. Run Test-Local.ps1 again.' }
[ordered]@{ Passed = $true; Total = $total; Skipped = 0; Projects = $actual;
    VerificationFingerprint = $fingerprint; TestedAt = (Get-Date).ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "Passed test projects: $($projects.Count). This does not verify live game behavior."
