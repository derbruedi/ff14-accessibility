# Проверка окна характеристик

Сравнивает структуру `ui/uld/characterstatus.uld` установленной игры с данными,
на которых выполняются `CharacterStatusTableTests`. Проверяются номера,
родители, типы и координаты всех элементов. Файл не содержит игровых значений:
в тестах числа и русские подписи взяты из журнала 30 сентября 2026, 18:03–18:05.
Видимость в тестах задаётся явно, поскольку игра меняет её после загрузки окна.

```powershell
dotnet run --project tools/table-layout-check/TableLayoutCheck.csproj -c Release -- "D:\games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack" tests/Regression.Tests/Fixtures/character-status-layout.json
```

Ошибка останавливает проверку. После осознанного пересмотра структуры параметр
`--write` обновляет файл теста. Самостоятельно применять его при несовпадении
нельзя: сначала нужно проверить смысл изменений и код `CharacterStatusTable`.
Разбор ULD использует [Lumina](https://github.com/NotAdam/Lumina/blob/master/src/Lumina/Data/Files/UldFile.cs).
Эта проверка не воспроизводит речь, живой фокус и смену профессии в FFXIV.
