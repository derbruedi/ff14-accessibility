# Проверка окон отрядов

Сверяет девять файлов ULD установленной игры с тестовым снимком: окна миссий,
бойцов, карточки бойца, тренировок, результатов, совместных походов, смены
класса, совместимости и тактики. В снимке сохранены номера, типы, родители,
координаты и постоянные подписи; личных данных игрока в нём нет.
Значения в автоматических тестах задаются отдельно.

```powershell
dotnet run --project tools/squadron-layout-check/SquadronLayoutCheck.csproj -c Release -- "D:\games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack" tests/Regression.Tests/Fixtures/squadron-layout.json
```

При несовпадении проверка завершается ошибкой. Параметр `--write` допустим
после изучения изменений окон и соответствующих правил `SquadronReader`.
Имена окон и файлов отличаются: `GcArmyExpedition` использует
`gcarmyexpeditionselect.uld`, а `GcArmyMenberProfile` — `gcarmymemberprofile.uld`.
Проверка файлов не подтверждает живое управление и речь FFXIV/NVDA/Tolk.
