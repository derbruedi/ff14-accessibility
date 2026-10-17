# Проверка файлов маршрутов подземелий

Пример для локального компьютера:

```powershell
dotnet run --project tools/dungeon-route-check/DungeonRouteCheck.csproj -c Release -- "D:\games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack" "$env:APPDATA\XIVLauncher\pluginConfigs\FF14Accessibility" .local/dungeon-report.json
```

Инструмент читает игровые таблицы и существующие файлы DungeonPaths, проверяет
маршруты, конечные координаты и номер механизмов. В JSON отмечаются территории
без позиционных маршрутов и наличие сохранённых сеток vnavmesh.

В отдельном процессе проверяется регистрация совместимости Дземаэль в настоящей
установленной vnavmesh и её повтор. Файлы, настройки и работающая игра не меняются.
Нужна та же проверенная DLL и её соседние библиотеки. Неизвестная DLL отклоняется.
Отчёт не доказывает связность пути или движение персонажа; эти поля всегда false.
