# Проверка заданий с Harmonia / Project Prima

Из корня проекта, с установленной игрой и уже установленным пакетом Prima:

```powershell
dotnet run --project tools/harmonia-quest-check/HarmoniaQuestCheck.csproj -c Release -- 'ПУТЬ_К_GAME\sqpack' "$env:APPDATA\XIVLauncher\pluginConfigs\HarmoniaEngine\packs" 'artifacts/logs/harmonia-quest-check.json'
```

Инструмент читает только текущие HPK, указанные в `installed.json`. Проверяет
SHA-256 пакета и совпадение исходного английского имени каждой строки Quest
с защитной отметкой перевода. Затем использует настоящий поиск
`QuestMarkerService`: все переведённые названия должны сохранять номера своих
заданий и категории из игровых таблиц. Совпадение названий разных категорий
остаётся неопределённым. Проверяются сюжетные, побочные и классовые задания,
а также переключение русского и английского языка с обновлением поиска.

Отдельно проверяются три названия из журнала 6 октября 2026:
«Уровень угрозы повышен», «Слушатель часто опаздывает», «Кочующие мародёры».
Они должны определяться как сюжетные задания. Для другого пакета, в котором
эти названия изменились, данная проверка потребует обновления примеров.

Файлы игры и Harmonia не меняются. Переводы Prima не включаются в исходники
или архив плагина. Это проверка установленных данных и поиска заданий;
реальное окно, нажатия и речь NVDA/Tolk здесь не проверяются.

Первичные описания: [Harmonia](https://github.com/AngelicaProject/Harmonia),
[чтение HPK](https://github.com/AngelicaProject/Harmonia/blob/main/Harmonia/Packs/Hpk/HpkFile.cs),
[защита исходного текста](https://github.com/AngelicaProject/Harmonia/blob/main/Harmonia/Packs/Hpk/SourceGuard.cs).
