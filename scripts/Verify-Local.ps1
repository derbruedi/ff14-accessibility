[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-Local.ps1')
& (Join-Path $PSScriptRoot 'Test-Local.ps1')
& (Join-Path $PSScriptRoot 'Package-Local.ps1')
