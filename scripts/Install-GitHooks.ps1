<#
.SYNOPSIS
    Installiert Git-Hooks fuer das Projekt.

.DESCRIPTION
    Kopiert den Pre-commit Hook, der versehentliche Commits von dekompiliertem
    Game-Code verhindert. Der Hook prueft:
    - Ob Dateien aus dem decompiled/-Ordner gestaged sind
    - Ob gestaged .cs-Dateien Decompiler-Header enthalten (ILSpy, dnSpy, dotPeek, IL2CPP)

.EXAMPLE
    .\Install-GitHooks.ps1
#>

$ErrorActionPreference = "Stop"

# Find git hooks directory
try {
    $gitDir = git rev-parse --git-dir 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FEHLER: Kein Git-Repository gefunden."
        Write-Host "Loesung: Fuehre dieses Script im Projektordner aus."
        exit 1
    }
} catch {
    Write-Host "FEHLER: Git ist nicht installiert oder nicht im PATH."
    exit 1
}

$hooksDir = Join-Path $gitDir "hooks"
$sourceHook = Join-Path (Join-Path $PSScriptRoot "hooks") "pre-commit"
$destHook = Join-Path $hooksDir "pre-commit"

# Verify source hook exists
if (-not (Test-Path $sourceHook)) {
    Write-Host "FEHLER: Hook-Datei nicht gefunden: $sourceHook"
    exit 1
}

# Create hooks dir if needed
if (-not (Test-Path $hooksDir)) {
    New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null
}

# Check for existing hook
if (Test-Path $destHook) {
    $existingContent = Get-Content $destHook -Raw
    $newContent = Get-Content $sourceHook -Raw
    if ($existingContent -eq $newContent) {
        Write-Host "OK: Pre-commit Hook ist bereits aktuell."
        exit 0
    }
    Write-Host "WARNUNG: Es existiert bereits ein Pre-commit Hook."
    Write-Host "Der bestehende Hook wird ueberschrieben."
    Write-Host ""
}

# Copy hook
Copy-Item -Path $sourceHook -Destination $destHook -Force

Write-Host "OK: Pre-commit Hook installiert."
Write-Host ""
Write-Host "Der Hook prueft bei jedem Commit:"
Write-Host "  - Keine Dateien aus decompiled/ gestaged"
Write-Host "  - Keine Decompiler-Header in .cs-Dateien (ILSpy, dnSpy, dotPeek, IL2CPP)"
Write-Host ""
Write-Host "Bypass (nur im Notfall): git commit --no-verify"
