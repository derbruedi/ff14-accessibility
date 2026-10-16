$ErrorActionPreference = "Stop"

function Say($t) { Write-Host $t }

$src = Join-Path $PSScriptRoot "plugin"
if (-not (Test-Path -LiteralPath $src)) {
    $src = Join-Path $PSScriptRoot "artifacts\plugin"
}
if (-not (Test-Path $src)) {
    Say "ОШИБКА: нет готовой сборки в plugin или artifacts\plugin."
    Say "Распакуй архив целиком заново и запусти install.bat из распакованной папки."
    exit 1
}

$srcDll = Join-Path $src "FF14Accessibility.dll"
if (-not (Test-Path $srcDll)) {
    Say "ОШИБКА: в папке plugin нет FF14Accessibility.dll."
    exit 1
}
$sourceVersion = [System.Reflection.AssemblyName]::GetAssemblyName($srcDll).Version
$displayVersion = "{0}.{1:D2}.{2:D2}" -f $sourceVersion.Major, $sourceVersion.Minor, $sourceVersion.Build
Say ""
Say "FF14 Accessibility $displayVersion - русская сборка."
Say ""

$xl = Join-Path $env:APPDATA "XIVLauncher"
if (-not (Test-Path $xl)) {
    Say "ОШИБКА: не нашёл папку XIVLauncher:"
    Say "  $xl"
    exit 1
}

$running = Get-Process -Name ffxiv_dx11,ffxiv,ffxivboot,XIVLauncher -ErrorAction SilentlyContinue
if ($running) {
    Say "Игра или лаунчер сейчас запущены."
    Say "Закрой и XIVLauncher, и игру, потом запусти меня снова. Пока ничего не меняю."
    exit 1
}

Say "[1/4] Игра и лаунчер закрыты - можно работать."

$target = Join-Path $xl "devPlugins\FF14Accessibility"
$dll = Join-Path $target "FF14Accessibility.dll"

New-Item -ItemType Directory -Force -Path $target | Out-Null

if (Test-Path $dll) {
    Copy-Item $dll "$dll.bak" -Force
    Say "[2/4] Файлы копирую. Прежняя версия сохранена рядом как .bak"
} else {
    Say "[2/4] Файлы копирую в новую папку devPlugins\FF14Accessibility"
}
Copy-Item (Join-Path $src "*") $target -Recurse -Force

$installed = Join-Path $target "FF14Accessibility.dll"
$installedVersion = [System.Reflection.AssemblyName]::GetAssemblyName($installed).Version
if ($installedVersion -ne $sourceVersion) {
    Say "ОШИБКА: после копирования версия файла не совпала с версией в архиве."
    exit 1
}
$size = [Math]::Round((Get-Item $installed).Length / 1KB)
$count = (Get-ChildItem $target -Recurse -File).Count
Say "      Установлена версия $displayVersion ($size КБ), файлов: $count"

$cfg = Join-Path $xl "dalamudConfig.json"
if (-not (Test-Path $cfg)) {
    Say "ОШИБКА: не нашёл dalamudConfig.json в $xl"
    exit 1
}

$settings = [System.IO.File]::ReadAllText($cfg) | ConvertFrom-Json
$hasPath = [bool]($settings.DevPluginLoadLocations.'$values' |
    Where-Object { $_.Path -eq $dll -and $_.IsEnabled })
$hasDevMode = $settings.DevMode -eq $true
$copies = @($settings.DevPluginLoadLocations.'$values' |
    Where-Object { $_.Path -like '*\FF14Accessibility\FF14Accessibility.dll' }).Count

if ($hasPath -and $hasDevMode) {
    Say "[3/4] Dalamud уже знает этот плагин (режим разработчика включён) - трогать настройки не нужно."
} else {
    Say "[3/4] ВНИМАНИЕ: в настройках Dalamud нет записи об этой локальной версии."
    Say "      Сам я настройки не меняю. Напиши мне - допишу отдельно, это одна операция."
}
if ($copies -gt 1) {
    Say "      В настройках прописано несколько копий мода - напиши мне, разберёмся, чтобы не читалось дважды."
}

Say "[4/4] Готово."
Say ""
Say "Запускай игру как обычно."
Say "Ремесленные навыки, поставленные на панель прежней версией мода, назначь заново через Ctrl+Num0."
Say "Важно: пока правки не ушли автору, не жми «обновить» в установщике мода - он вернёт его версию без русского."
Say "Если в игре что-то не отзывается голосом - скажи, где именно, полезу искать в живом журнале."
Say ""
