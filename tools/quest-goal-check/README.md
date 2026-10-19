# Проверка целей двух квестов по данным игры

Инструмент вызывает действующий выбор цели на настоящих Quest/Level установленной игры.
Проверяет этап с киркой (66484 → EObj2002185/Level4298909) и спасение исследователя
(66485 → EventRange4298945), высоту, радиус, сохранение исходного маркера и отказы
при неизвестном этапе, другой карте и объекте вне круга. Ответ сетки для нижнего
уступа воспроизводится по журналу. Движение, речь и прохождение квестов не проверяет.

```powershell
dotnet run --project tools/quest-goal-check/QuestGoalCheck.csproj -c Release -- "D:\games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack" ".local/quest-audit/game-check.json"
```
