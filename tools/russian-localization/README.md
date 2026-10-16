# Русский перевод речи и игровых названий

Справочники версии 6.08.70 встроены в DLL, работают без сети и применяются только
при `Loc.IsRussian`. Переводы привязаны к таблице, номеру строки и точным английским
байтам. После изменения строки патчем остаётся актуальный текст игры. Имена игроков,
сообщения чата, команды и ключи поиска в игровых окнах не заменяются.

## Проверенный охват

Для игры `2026.09.15.0000.0000` переведены все доступные непустые строки
30 захваченных таблиц: 50 880 названий предметов, 3 531 квестовый предмет,
45 400 обычных действий, 433 ремесленных действия, 43 общих действия,
6 команд чокобо, 41 действие питомцев, 682 пассивных умения, 369 маунтов,
590 спутников, 4 780 названий и 4 760 описаний эффектов, 305 эмоций.
Остальные таблицы охватывают профессии, характеристики, категории предметов,
команды меню, фильтры чата и предметы/эффекты глубоких подземелий.

В Action остаются 118 серверных ключей `_rsv_`, в Status — 11 названий и
11 описаний. В файлах игры у них нет текста; они перечислены отдельно и
не считаются переведёнными. Для всех остальных строк пропусков нет.
Номера старых и служебных строк включены в счётчики: это не количество
умений, доступных одному персонажу. Полный отчёт —
`docs/maintenance/russian-game-text-coverage.json`.

Переведены все 3 813 разные фразы внешности: 2 650 описаний значков и
1 163 фразы измерений формы. Короткие и полные варианты сохраняют различия,
числа и порядок признаков. Отчёт — `russian-character-coverage.json`.
Сверка четырёх групп `russian-character-details-coverage.json` относится
к более ранней версии 6.08.69 и сохранена как история проверки.

## Источники и воспроизведение

### Обновления автора 6.08.32–6.08.35

`AuthorText.cs` снимает 50 записей `XBMPet`/`Pet`, 68 имён выдающих ливквесты
NPC через `Leve.LevelLevemete`/`Level`/`ENpcResident`, тексты интерфейса лотерей
и название FATE 1409. Измеренный снимок находится в
`tests/Regression.Tests/Fixtures/author-text-sources.json` и входит в обязательные
тесты и отпечаток проверки. Версия игры — `2026.09.15.0000.0000`.

Переводы зверей написаны в `author-beast-translations.json`. Имена NPC находятся
в `author-giver-translations.json`: 27 из
[ENpcResident XIV Rus](https://github.com/xivrus/xiv_ru_weblate/blob/6a2733af6df0f86b133bcbf61c553dfacf80ec2d/exd/ENpcResident/ru.xlf),
41 дополнено вручную; версия источника и SHA-256 сохранены рядом с переводами.
У импортированных имён исходный английский текст совпал с байтами игры.

```powershell
dotnet run --project tools/russian-localization/AuthorText.csproj -c Release -- 'ПУТЬ_К_GAME\sqpack' 'tests/Regression.Tests/Fixtures/author-text-sources.json'
python tools/russian-localization/generate_author_text.py
```

Генератор выпускает `RussianAuthorText.json.gz` и `RussianLotteryLabels.json`.
Описания зверей и имена NPC в этом снимке не содержат управляющих вставок;
генератор проверяет это побайтно. Подписи лотерей переводятся только в их окнах.
Динамическая подсказка сохраняет число открываемых клеток; неизвестный вариант
или изменённый исходник читается на языке игры. Параметры остальных диалогов,
имена игроков и чат не заменяются. `AuthorUpdateTests` проверяет полный охват,
смену языка, исходные поисковые имена, устаревший источник и числа лотерей.

`game-text-sources.json.gz` содержит снимок выбранных английских строк игры.
`game-text-imported.json.gz` содержит только совпавшие по исходным байтам переводы
из закреплённого снимка XIV Rus `6a2733af6df0f86b133bcbf61c553dfacf80ec2d`
с хешами исходных файлов. Наши дополнения находятся в JSON/TSV рядом с генераторами;
они не выдаются за переводы, проверенные командой XIV Rus.

Нужны Python 3.10+ и .NET с зависимостями Dalamud для C#-проверок. Из корня проекта:

```powershell
python -X utf8 tools/russian-localization/generate.py
python -X utf8 tools/russian-localization/build_action_glossary.py --output tools/russian-localization/complete-action-translations.json
python -X utf8 tools/russian-localization/item_templates.py
python -X utf8 tools/russian-localization/build_item_supplements.py
python -X utf8 tools/russian-localization/generate_game_text.py --require-complete
python -X utf8 -m unittest discover -s tools/russian-localization -p 'test_*.py'
```

Этот порядок использует сохранённые входные данные и не требует сети или заранее
подготовленной `.local`. Генераторы создают её для дополнительных отчётов.
Семейства названий состоят из ограниченного словаря проверенных компонентов;
неизвестное слово не угадывается. Категория предмета защищает рыбу и еду от
ошибочного перевода как материала. Точный перевод предмета имеет приоритет
над одинаковым названием действия. Исправления смысла конкретной строки находятся
в `game-text-corrections.json`: боевое Reflect — «Отражение», ремесленное — «Размышление».

Для нового снимка игры сначала заново захватить источники, затем обновить импорт:

```powershell
dotnet run --project tools/russian-localization/GameText.csproj -c Release -- 'ПУТЬ_К_GAME\sqpack' 'tools/russian-localization/game-text-sources.json.gz'
python -X utf8 tools/russian-localization/prepare_game_text.py --download
```

После этого пересмотреть изменённые строки, дополнить переводы и повторить генерацию.
`generate_names.py` сохраняет старый набор 124 названий в `.local`; производственный
ресурс он больше не перезаписывает. `translate_offline.py` может создавать только
черновики в `.local`; результаты машинного перевода не являются входом сборки.

## Подключение и проверки

`GameDescriptionService` и `RussianGameText` предоставляют названия инвентарю,
наградам, магазинам, рецептам, меню назначения и чтению панелей, предупреждениям,
эффектам и поддерживаемым окнам. Поиск по сырому тексту окна выполняется до перевода.
Типы `CraftAction` и `Action` сохраняются раздельно. Формулы описаний вычисляет
`ISeStringEvaluator`, затем `RussianDescriptionTerms` переводит известные английские
названия внутри готовой русской фразы с сохранением границ слов.

```powershell
dotnet run --project tools/russian-localization/GameText.csproj -c Release -- 'ПУТЬ_К_GAME\sqpack' '.local/verified-game-text-sources.json.gz' 'FF14Accessibility/Resources/RussianActionNames.json.gz' 'artifacts/logs/russian-game-text-live-source-check.json'
dotnet run --project tools/russian-localization/Audit.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Local.ps1
```

Сверка проверяет каждую принятую пару по установленной игре и формат управляющих
вставок. `--require-complete` и тесты отдельно требуют отсутствия доступных пропусков.
Проверка собственных сообщений рассматривает Release и диагностические блоки DEBUG;
она проверяет аргументы локализации и прямые строки речи, но не доказывает перевод
всех динамических значений любого окна. Сборка, счётчики и тесты не заменяют
проверку фактической речи, переключения языка и выполнения умений в FFXIV.
