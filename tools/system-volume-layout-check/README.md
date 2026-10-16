# Проверка системных ползунков звука

Инструмент сверяет десять строк громкости в `ui/uld/configsystem.uld`
и их реальные подписи из таблицы Addon с тестовым снимком.
На каждой строке ползунок и подпись имеют общего родителя, подпись расположена
справа. Общие заголовки 111 и 141 сохранены для проверки ошибочного выбора имени.

```powershell
dotnet run --project tools/system-volume-layout-check/SystemVolumeLayoutCheck.csproj -- "D:\games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack" tests/Regression.Tests/Fixtures/system-volume-layout.json
```

При обновлении игры снимок можно заново записать с `--write` только после
проверки изменений `SystemVolumeNodes.Bindings`. Инструмент не подтверждает
живую озвучку, вызов штатного изменения ползунка или сохранение настроек игры.
