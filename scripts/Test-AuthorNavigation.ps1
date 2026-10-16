[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskManifest = Get-Content -LiteralPath (Join-Path $taskRoot 'docs/maintenance/AUTHOR_NAVIGATION.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($taskFile in $taskManifest.Files) {
    $taskPath = Join-Path $taskRoot $taskFile.Path
    $taskOverride = @($taskManifest.LocalOverrides | Where-Object { $_.Path -eq $taskFile.Path })
    if ($taskOverride.Count -gt 1) { throw "Повторное исключение навигации: $($taskFile.Path)" }
    $taskExpected = if ($taskOverride.Count -eq 1) { $taskOverride[0].Sha256 } else { $taskFile.Sha256 }
    if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskExpected) {
        throw "Файл автонавигации отличается от версии автора: $($taskFile.Path)"
    }
    if ($taskOverride.Count -eq 1) { Write-Host "Проверено ограниченное изменение: $($taskOverride[0].Reason)" }
}
foreach ($taskMethod in @($taskManifest.Resolver, $taskManifest.HuntSearch)) {
    $taskSource = [IO.File]::ReadAllText((Join-Path $taskRoot $taskMethod.Path)).Replace("`r`n", "`n")
    $taskStart = $taskSource.IndexOf([string]$taskMethod.Signature, [StringComparison]::Ordinal)
    if ($taskStart -lt 0) { throw "Не найден метод автора: $($taskMethod.Signature)" }
    $taskEnd = $taskSource.IndexOf("`n    }", $taskStart, [StringComparison]::Ordinal)
    if ($taskEnd -lt 0) { throw "Не найден конец метода автора: $($taskMethod.Signature)" }
    $taskBytes = [Text.Encoding]::UTF8.GetBytes($taskSource.Substring($taskStart, $taskEnd + 6 - $taskStart))
    $taskSha = [Security.Cryptography.SHA256]::Create()
    try { $taskHash = [BitConverter]::ToString($taskSha.ComputeHash($taskBytes)).Replace('-', '').ToLowerInvariant() }
    finally { $taskSha.Dispose() }
    if ($taskHash -ne $taskMethod.Sha256) { throw "Маршрутизация отличается от версии автора: $($taskMethod.Signature)" }
}
foreach ($taskRemoved in $taskManifest.RemovedFiles) {
    if (Test-Path -LiteralPath (Join-Path $taskRoot $taskRemoved)) {
        throw "Возвращён удалённый дополнительный код навигации: $taskRemoved"
    }
}
Write-Host "Навигация проверена: $($taskManifest.Files.Count) файлов, два метода; исходный коммит $($taskManifest.Commit); исключений $(@($taskManifest.LocalOverrides).Count)."
