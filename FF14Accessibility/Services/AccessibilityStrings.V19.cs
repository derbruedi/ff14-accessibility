using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;

namespace FF14Accessibility.Services;

// Aus der Version 6.08.19 zurueckgeholte Ansagen.
public static partial class AccessibilityStrings
{
	public static string OptAttacker => L("Wer hat angegriffen", "Who attacked", "Кто напал");

	public static string OptStrongerEnemy => L("Warnung vor stärkeren Gegnern", "Warning about stronger enemies", "Предупреждение о сильных врагах");

	public static string OptQuestObjective => L("Auftragstext im Quest-Tracker", "Quest objective text", "Текст задания в списке заданий");

	public static string SupplyPaneEmpty => L("Zurzeit nichts angefordert.", "Nothing requested right now.", "Сейчас запросов нет.");

	public static string YesNoStillOpen => L("Das Fenster ist noch offen.", "The window is still open.", "Окно всё ещё открыто.");

	public static string GatherProbeSaved => L("Datei auf dem Desktop.", "File on the desktop.", "Файл на рабочем столе.");

	public static string GatherProbeFailed => L("Sammel-Sonde konnte die Datei nicht schreiben.", "Gather probe could not write its file.", "Зонд сбора не смог записать файл.");

	public static string FoodProbeFailed => L("Nahrungs-Sonde konnte die Datei nicht schreiben.", "Food probe could not write its file.", "Зонд еды не смог записать файл.");

	public static string SpawnProbeFailed => L("Spawn-Sonde konnte die Datei nicht schreiben.", "Spawn probe could not write its file.", "Зонд спавна не смог записать файл.");

	public static string QuestProbeFailed => L("Quest-Sonde konnte die Datei nicht schreiben.", "Quest probe could not write its file.", "Зонд заданий не смог записать файл.");

	public static string FocusTimingProbeFailed => L("Fokus-Zeit-Sonde konnte die Datei nicht schreiben.", "Focus timing probe could not write its file.", "Зонд времени фокуса не смог записать файл.");

	public static string SpeechTraceArmed => L("Sprach-Mitschnitt laeuft. Fenster oeffnen, durchgehen, kurz stehen bleiben - dann derselbe Befehl.", "Speech trace is running. Open the window, walk through it, pause briefly - then the same command again.", "Запись речи включена. Открой окно, пройди по нему, постой немного — потом та же команда.");

	public static string SpeechTraceFailed => L("Sprach-Mitschnitt konnte die Datei nicht schreiben.", "Speech trace could not write its file.", "Запись речи не смогла записать файл.");

	public static string QuestKindProbeCyrillic => L("kyrillisch", "cyrillic", "кириллица");

	public static string QuestKindProbeLatin => L("lateinisch", "latin", "латиница");

	public static string QuestKindProbeHit => L("Art erkannt", "kind recognised", "тип распознан");

	public static string QuestKindProbeMiss => L("NICHT gefunden", "NOT found", "НЕ найдена");

	public static string QuestKindProbeLoose => L("erst nach Normalisierung gefunden", "found only after normalising", "найдена только после нормализации");

	public static string QuestKindProbeNoTarget => L("keine Quest gewählt", "no quest selected", "цель не выбрана");

	public static string QuestKindProbeNameUnknown => L("Name nicht in der Tabelle", "name not in the table", "имя не найдено в таблице");

	public static string PlayerMenuNotOpened => L("Spielermenü ließ sich nicht öffnen.", "Could not open the player menu.", "Меню игрока не открылось.");

	public static string PlayerMenuNoList => L("Spielermenü offen, die Punkte sind nicht als Liste lesbar.", "Player menu open, the entries are not readable as a list.", "Меню игрока открылось, но список не читается.");

	public static string CompanionOpenFailed => L("Kompanon-Fenster ist nicht aufgegangen.", "The companion window did not open.", "Окно спутника не открылось.");

	public static string CompanionProbeSpoken => L("Kompanon-Sonde auf dem Desktop: Fensterbefund, Nummerblock 0 und der letzte Spielchat.", "Companion probe on the desktop: window state, numpad 0 and the last game chat.", "Зонд спутника записан на рабочий стол: состояние окон, ноль и последний игровой чат.");

	public static string CompanionProbeFailed => L("Kompanon-Sonde konnte die Datei nicht schreiben.", "Companion probe could not write its file.", "Зонд спутника не смог записать файл.");

	public static string SlotEmpty => L("leer", "empty", "пусто");

	public static string FeetBlocked => L("Schuhe können dabei nicht getragen werden", "boots cannot be worn with it", "обувь при ней не надевается");

	public static string PlaceKindTransition => L("Übergang", "Transition", "Переход");

	public static string PlaceKindAetheryte => L("Ätheryt", "Aetheryte", "Эфирит");

	public static string PlaceKindAethernet => L("Aethernet", "Aethernet", "Эфирная сеть");

	public static string PlaceKindPlace => L("Ort", "Place", "Место");

	public static string ModsPenumbraSilent => L("Penumbra antwortet nicht. Sage mir Bescheid, dann sehe ich nach.", "Penumbra is not answering. Tell me and I will look into it.", "Penumbra не отвечает. Скажи мне, и я посмотрю.");

	public static string ModsNone => L("In Penumbra liegt kein einziger Mod.", "There is not a single mod in Penumbra.", "В Penumbra нет ни одного мода.");
	public static string ModsNoNames => L("keine", "none", "нет");
	public static string ModsMoreNames(int count) => L($"und {count} weitere", $"and {count} more", $"и ещё {count}");

	public static string ModsReadPartlyFailed => L("Penumbra hat geantwortet, aber den Zustand nicht aller Mods freigegeben. Eine Zahl waere geraten - sage mir Bescheid, dann sehe ich nach.", "Penumbra answered, but it did not give away the state of every mod. A number would be a guess - tell me and I will look into it.", "Penumbra ответила, но не про все моды отдала состояние. Число было бы наугад — скажи мне, и я посмотрю.");

	public static string ModsNothingRemembered => L("Ich habe nichts gemerkt, was ich zurueckschalten koennte - es wurde noch nichts ausgeschaltet. Zuerst: /acc mods off", "I have nothing remembered to put back - nothing was switched off yet. First: /acc mods off", "Мне нечего возвращать — моды ещё не выключали. Сначала: /acc моды выкл");

	public static string ModsBusy => L("Der Mod arbeitet noch an Penumbra. Ich melde mich, wenn er fertig ist.", "The mod is still working on Penumbra. I will speak up when it is done.", "Мод ещё занят в Penumbra. Скажу, когда закончит.");

	public static string FrameRateTooEarly => L("Dafuer sind noch zu wenige Bilder durchgelaufen. Warte ein paar Sekunden und frage noch einmal.", "Too few frames have gone by for that. Wait a few seconds and ask again.", "Кадров ещё слишком мало для счёта. Подожди пару секунд и спроси ещё раз.");

	public static string PerformanceDumpNoAddon => L("Kein Auftrittsfenster offen. Oeffne den Auftritt und versuche es noch einmal.", "No performance window open. Open it and try again.", "Окно выступления не открыто. Открой выступление и повтори.");

	public static string PerformanceDumpFailed => L("Die Auftrittsdatei konnte nicht geschrieben werden.", "The performance file could not be written.", "Не удалось записать файл выступления.");

	public static string SupplyRow(string name, string requested)
	{
		return L(name + ", angefordert " + requested, name + ", requested " + requested, name + ", запрошено " + requested);
	}

	public static string SupplyRowNoCount(string name)
	{
		return L(name + ", ohne Mengenangabe", name + ", no amount shown", name + ", количество не указано");
	}

	public static string SupplyPaneIntro(string rows)
	{
		return L("Versorgungs-Missionen: " + rows, "Supply missions: " + rows, "Снабжение и обеспечение: " + rows);
	}

	public static string SupplySection(string label, string rows)
	{
		return L(label + ": " + rows, label + ": " + rows, label + ": " + rows);
	}

	public static string HuntingSpotCoords(float mapX, float mapY)
	{
		return L($"Auf der Karte {mapX:0.0} und {mapY:0.0}", $"On the map {mapX:0.0} and {mapY:0.0}", $"На карте {mapX:0.0} и {mapY:0.0}");
	}

	public static string DistanceFromMap(string distance)
	{
		return L("laut Karte " + distance, distance + " on the map", "по карте " + distance);
	}

	public static string GatherProbeResult(int kindCount, int kindTargetable, int otherKindCount, int filterPassed, uint jobId, uint territoryId)
	{
		return L($"Sammel-Sonde: {kindCount} Objekte der Art Sammelpunkt, davon {kindTargetable} arbeitsbar. {otherKindCount} Objekte passen auf eine Sammelpunkt-Zeile, haben aber eine andere Art. Der eigene Filter laesst {filterPassed} durch. Klasse {jobId}, Gebiet {territoryId}.", $"Gather probe: {kindCount} objects of kind gathering point, {kindTargetable} of them workable. {otherKindCount} objects match a gathering point row but have a different kind. Our own filter passes {filterPassed}. Class {jobId}, area {territoryId}.", $"Зонд сбора: {kindCount} объектов вида точка сбора, из них {kindTargetable} рабочих. {otherKindCount} объектов подходят под строку точки сбора, но вид у них другой. Свой фильтр пропускает {filterPassed}. Класс {jobId}, зона {territoryId}.");
	}

	public static string FoodProbeResult(int lines)
	{
		return L($"Nahrungs-Sonde: {lines} Zeilen auf dem Desktop.", $"Food probe: {lines} lines on the desktop.", $"Зонд еды: {lines} строк на рабочем столе.");
	}

	public static string SpawnProbeResult(int lines)
	{
		return L($"Spawn-Sonde: {lines} Zeilen auf dem Desktop.", $"Spawn probe: {lines} lines on the desktop.", $"Зонд спавна: {lines} строк на рабочем столе.");
	}

	public static string QuestProbeResult(int lines)
	{
		return L($"Quest-Sonde: {lines} Zeilen auf dem Desktop.", $"Quest probe: {lines} lines on the desktop.", $"Зонд заданий: {lines} строк на рабочем столе.");
	}

	public static string FocusTimingProbeResult(int lines, string worst)
	{
		return L($"Fokus-Zeit: {lines} Zeilen auf dem Desktop. Langsamste Stelle: {worst}.", $"Focus timing: {lines} lines on the desktop. Slowest part: {worst}.", $"Время фокуса: {lines} строк на рабочем столе. Самое медленное место: {worst}.");
	}

	public static string SpeechTraceWritten(int lines, string fileName)
	{
		return L($"Sprach-Mitschnitt: {lines} Zeilen in {fileName} auf dem Desktop.", $"Speech trace: {lines} lines in {fileName} on the desktop.", $"Запись речи: {lines} строк в файле {fileName} на рабочем столе.");
	}

	public static string QuestKindProbe(int total, int main, int job, int side, string sample, string script, string selected, string verdict)
	{
		return L($"Quest-Tabelle: {total} Namen (Hauptszenario {main}, Job {job}, Nebenauftrag {side}). Beispiel: '{sample}' ({script}). Ziel: '{selected}' -> {verdict}.", $"Quest table: {total} names (main {main}, job {job}, side {side}). Sample: '{sample}' ({script}). Target: '{selected}' -> {verdict}.", $"Таблица заданий: {total} названий — главных {main}, классовых {job}, побочных {side}. Пример: «{sample}» ({script}). Цель: «{selected}» — {verdict}.");
	}

	public static string QuestKindWord(QuestKind kind)
	{
		return kind switch
		{
			QuestKind.MainStory => L("Story", "story", "сюжет"),
			QuestKind.Job => L("Job", "job", "класс"),
			QuestKind.BeastTribe => L("Freundesvolk", "beast tribe", "племя"),
			QuestKind.Chronicle => L("Chronik", "chronicle", "хроника"),
			QuestKind.SideQuest => L("Nebenauftrag", "side quest", "побочное"),
			QuestKind.Other => L("Sonstiges", "other", "прочее"),
			_ => L("unbekannt", "unknown", "неизвестно"),
		};
	}

	public static string QuestKindProbeNoSelection(string markers)
	{
		return L("Keine Quest gewählt. Aktuelle Markierungen: " + markers, "No quest selected. Current markers: " + markers, "Цель не выбрана. Текущие метки: " + markers);
	}

	public static string QuestKindProbeMarker(string label, string kind, string nameIds)
	{
		return L($"'{label}': {kind}, Id aus dem Namen: {nameIds}", $"'{label}': {kind}, id from the name: {nameIds}", $"«{label}»: {kind}, Id по имени: {nameIds}");
	}

	public static string QuestKindProbeSeveralIds(int count, string ids)
	{
		return L($"{count} Zeilen ({ids})", $"{count} rows ({ids})", $"{count} строк ({ids})");
	}

	public static string PlayerMenuWindowsOpened(int count, string names)
	{
		return L($"Spielermenü kam nicht, aber {count} Fenster gingen auf: {names}", $"Player menu did not appear, but {count} windows opened: {names}", "Меню игрока не открылось, но открылись окна: " + names);
	}

	public static string PlayerMenuEntries(int count, string entries)
	{
		return L($"Spielermenü, {count} Punkte: {entries}", $"Player menu, {count} entries: {entries}", $"Меню игрока, пунктов {count}: {entries}");
	}

	public static string AttackedBy(string name, int level, string levelRelation, int others)
	{
		if (others > 0)
		{
			return L($"Angegriffen von {name}, Stufe {level}, {levelRelation}, und {MoreEnemies(others)}.", $"Attacked by {name}, level {level}, {levelRelation}, and {MoreEnemies(others)}.", $"Напал {name}, уровень {level}, {levelRelation}, и ещё {MoreEnemies(others)}.");
		}
		return L($"Angegriffen von {name}, Stufe {level}, {levelRelation}.", $"Attacked by {name}, level {level}, {levelRelation}.", $"Напал {name}, уровень {level}, {levelRelation}.");
	}

	private static string MoreEnemies(int count)
	{
		if (Loc.IsRussian)
		{
			return count switch
			{
				1 => "один",
				2 => "двое",
				3 => "трое",
				_ => count.ToString(),
			};
		}
		if (Loc.IsGerman) return count == 1 ? "einem weiteren Gegner" : $"{count} weiteren Gegnern";
		return (count == 1) ? "one more" : $"{count} more";
	}

	public static string LevelRelation(int difference)
	{
		if (difference <= 0)
		{
			if (difference >= 0)
			{
				return L("genau dein Level", "exactly your level", "твой уровень");
			}
			return L("unter deinem Level", "below your level", "ниже тебя");
		}
		return L("über deinem Level", "above your level", "выше тебя");
	}

	public static string StrongerEnemyNearby(string name, int level, string distance, string compass)
	{
		return L($"Achtung, Gegner über deinem Level: {name}, Stufe {level}, {distance} {compass}.", $"Careful, enemy above your level: {name}, level {level}, {distance} {compass}.", $"Осторожно, враг выше тебя: {name}, уровень {level}, {distance} {compass}.");
	}

	public static string CompanionWindow(string name, string rank, string xp, string hp, string time, string tab)
	{
		return L($"{name}. Rang {rank}. Erfahrung {xp}. HP {hp}. Zeit {time}. Reiter: {tab}.", $"{name}. Rank {rank}. Experience {xp}. HP {hp}. Time {time}. Tab: {tab}.", $"{name}. Ранг {rank}. Опыт {xp}. ОЗ {hp}. Время {time}. Вкладка: {tab}.");
	}

	public static string CompanionTab(string tab)
	{
		return L("Reiter: " + tab + ".", "Tab: " + tab + ".", "Вкладка: " + tab + ".");
	}

	public static string CompanionSkillWindow(string points, string branches)
	{
		return L($"Fertigkeiten. {points}. Zweige: {branches}.", $"Skills. {points}. Branches: {branches}.", $"Умения. {points}. Ветки: {branches}.");
	}

	public static string CompanionSkillWindowNoPoints(string branches)
	{
		return L("Fertigkeiten. Zweige: " + branches + ".", "Skills. Branches: " + branches + ".", "Умения. Ветки: " + branches + ".");
	}

	public static string CompanionSkillSlot(string number)
	{
		return L("Fähigkeit " + number + ".", "Skill " + number + ".", "Навык " + number + ".");
	}

	public static string CompanionSkillWithBranch(string text, string branch, string level)
	{
		return L($"{text}. Zweig {branch}, {level}.", $"{text}. Branch {branch}, {level}.", $"{text}. Ветка {branch}, {level}.");
	}

	public static string OccupiedSlots(string slots)
	{
		return L("Belegt: " + slots, "occupies: " + slots, "занимает: " + slots);
	}

	public static string OccupiesAlso(string slots)
	{
		return L("belegt auch: " + slots, "also occupies: " + slots, "занимает также: " + slots);
	}

	public static string OnlyForJobs(string jobs)
	{
		return L("nur für: " + jobs, "only for: " + jobs, "только для: " + jobs);
	}

	public static string SheetTerm(string sheetName, string? russian)
	{
		if (!Loc.IsRussian || string.IsNullOrWhiteSpace(russian))
		{
			return sheetName;
		}
		return russian;
	}

	public static string PlaceKindLabel(string typeLabel)
	{
		return typeLabel switch
		{
			"Übergang" => PlaceKindTransition,
			"Ätheryt" => PlaceKindAetheryte,
			"Aethernet" => PlaceKindAethernet,
			"Ort" => PlaceKindPlace,
			"Markierung" => FlagName,
			_ => typeLabel,
		};
	}

	public static string TargetEffectsHeader(int count)
	{
		return L($"Wirkungen auf dem Ziel ({count})", $"Effects on the target ({count})", $"Эффекты на цели ({count})");
	}

	public static string GatherNoteListPosition(int index, int total)
	{
		return L($"Liste {index} von {total}", $"List {index} of {total}", $"Список {index} из {total}");
	}

	public static string GatherNotePane(int pane)
	{
		return pane switch
		{
			0 => L("Kategorien", "Categories", "Категории"),
			1 => L("Stufen", "Levels", "Уровни"),
			2 => L("Orte", "Areas", "Места"),
			3 => L("Gegenstände", "Items", "Предметы"),
			_ => string.Empty,
		};
	}

	public static string ModsOverview(int mods, int collections, int enabled, string names)
	{
		return L($"Penumbra: {mods} Mod(s), {collections} Sammlung(en), davon an: {enabled}. Namen: {names}", $"Penumbra: {mods} mod(s), {collections} collection(s), of those on: {enabled}. Names: {names}", $"Penumbra: модов {mods}, наборов {collections}, включено {enabled}. Названия: {names}");
	}

	public static string ModsCouldNotRead(int skipped)
	{
		return L($"Ich konnte den Zustand von {skipped} Mod(s) in Penumbra nicht lesen und habe deshalb " + "NICHTS ausgeschaltet - ein Mod, den ich nicht sehe, bleibt lieber an. Sage mir Bescheid.", $"I could not read the state of {skipped} mod(s) in Penumbra, so I switched NOTHING off - " + "a mod I cannot see is better left on. Tell me and I will look into it.", $"Не смогла прочитать состояние {skipped} модов в Penumbra, поэтому НИЧЕГО не выключала — " + "мод, которого я не вижу, лучше оставить включённым. Скажи мне, и я посмотрю.");
	}

	public static string ModsSwitchedOff(int turnedOff, int skipped)
	{
		return L($"Alle Mods in Penumbra ausgeschaltet: {turnedOff}. " + "Der Mod, der dir das Spiel vorliest, liegt nicht in Penumbra und ist davon nicht betroffen. Starte das Spiel neu, dann wirkt es. Zurueck wie vorher: /acc mods on" + ((skipped > 0) ? $" Bei {skipped} Mod(s) war der Zustand nicht lesbar - die habe ich stehen lassen." : string.Empty), $"Switched every mod in Penumbra off: {turnedOff}. " + "The mod that reads the game to you does not live in Penumbra and is not affected by this. Restart the game for it to take effect. Back as before: /acc mods on" + ((skipped > 0) ? $" For {skipped} mod(s) the state was unreadable - I left those alone." : string.Empty), $"Выключила все моды в Penumbra: {turnedOff}. " + "Мод, который читает тебе игру, в Penumbra не лежит — его это не касается. Перезапусти игру, чтобы подействовало. Вернуть как было: /acc моды вкл" + ((skipped > 0) ? $" У {skipped} модов не смогла прочитать состояние — их не трогала." : string.Empty));
	}

	public static string ModsAlreadyOff(int remembered)
	{
		if (remembered <= 0)
		{
			return L("In Penumbra ist schon alles aus, und es gibt nichts zurueckzunehmen.", "Everything in Penumbra is already off, and there is nothing to put back.", "В Penumbra всё уже выключено, и возвращать нечего.");
		}
		return L($"In Penumbra ist schon alles aus. Gemerkt zum Zuruecknehmen: {remembered}. /acc mods on", $"Everything in Penumbra is already off. Remembered to put back: {remembered}. /acc mods on", $"В Penumbra всё уже выключено. Помню, что вернуть: {remembered}. /acc моды вкл");
	}

	public static string ModsRestored(int restored, int failed)
	{
		return L($"Mods wieder eingeschaltet wie vorher: {restored}. Starte das Spiel neu, dann sind sie wieder da." + ((failed > 0) ? $" Nicht zurueckgekommen: {failed} - sage mir Bescheid, dann sehe ich nach." : string.Empty), $"Mods switched back on as before: {restored}. Restart the game and they are back." + ((failed > 0) ? $" Did not come back: {failed} - tell me and I will look into it." : string.Empty), $"Вернула моды как было: {restored}. Перезапусти игру, и они снова заработают." + ((failed > 0) ? $" Не вернулось: {failed} — скажи мне, и я посмотрю." : string.Empty));
	}

	public static string FrameRate(int fps, int meanMs, int maxMs)
	{
		return L($"Das Spiel laeuft mit {fps} Bildern je Sekunde. Im Mittel {meanMs} Millisekunden je Bild, " + "das laengste " + $"{maxMs}.", $"The game is running at {fps} frames per second. {meanMs} milliseconds per frame on average, the longest {maxMs}.", $"Игра идёт {fps} кадров в секунду. В среднем {meanMs} миллисекунд на кадр, самый долгий {maxMs}.");
	}

	public static string PerformanceKeyboard(string pairs)
	{
		return L("Auftrittsfenster. Tastatur: " + pairs, "Performance window. Keyboard: " + pairs, "Окно выступления. Клавиши: " + pairs);
	}

	public static string PerformanceHelpKeys(string pairs)
	{
		return L("Oktaven und Halbtoene: " + pairs, "Octaves and semitones: " + pairs, "Октавы и полутоны: " + pairs);
	}

	public static string PerformanceSwitches(int boxes, int on, int plain)
	{
		return L($"Schalter ohne Beschriftung: {boxes} Kaestchen, davon {on} an, dazu {plain} Knoepfe.", $"Switches without labels: {boxes} checkboxes, {on} of them on, plus {plain} buttons.", $"Переключатели без подписей: флажков — {boxes}, из них включено — {on}, кнопок — {plain}.");
	}

	public static string PerformanceDumpSaved(int addons, int nodes)
	{
		return L($"Auftrittsdatei auf dem Desktop: {addons} Fenster, {nodes} Knoten.", $"Performance file on the desktop: {addons} windows, {nodes} nodes.", $"Файл выступления на рабочем столе: окон — {addons}, узлов — {nodes}.");
	}

	public static string PerformanceNote(string note)
	{
		string text = (note ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return text;
		}
		string text2 = text;
		string text3 = string.Empty;
		int num = text.IndexOf('+');
		if (num > 0 && int.TryParse(text.Substring(num + 1), out var result) && result > 0)
		{
			text2 = text.Substring(0, num).Trim();
			text3 = ((result == 1) ? L(", eine Oktave hoeher", ", one octave up", ", октавой выше") : L($", {result} Oktaven hoeher", $", {result} octaves up", $", на {result} октавы выше"));
		}
		bool flag = text2.EndsWith("♯") || text2.EndsWith("#");
		bool flag2 = text2.EndsWith("♭") || (text2.Length > 1 && text2.EndsWith("b"));
		string text4 = text2.TrimEnd(new char[4] { '♯', '#', '♭', 'b' }).Trim().ToUpperInvariant();
		string text5;
		if (text4 != null)
		{
			int length = text4.Length;
			if (length == 1)
			{
				switch (text4[0])
				{
				case 'C':
					break;
				case 'D':
					goto IL_01ec;
				case 'E':
					goto IL_0204;
				case 'F':
					goto IL_021c;
				case 'G':
					goto IL_0234;
				case 'A':
					goto IL_024c;
				case 'B':
				case 'H':
					goto IL_0264;
				default:
					goto IL_027c;
				}
				text5 = L("C", "C", "до");
				goto IL_0283;
			}
		}
		goto IL_027c;
		IL_0283:
		string text6 = text5;
		if (text6.Length == 0)
		{
			return text;
		}
		if (flag)
		{
			text6 += L("-is", "-sharp", "-диез");
		}
		if (flag2)
		{
			text6 += L("-es", "-flat", "-бемоль");
		}
		return text6 + text3;
		IL_0234:
		text5 = L("G", "G", "соль");
		goto IL_0283;
		IL_024c:
		text5 = L("A", "A", "ля");
		goto IL_0283;
		IL_021c:
		text5 = L("F", "F", "фа");
		goto IL_0283;
		IL_0204:
		text5 = L("E", "E", "ми");
		goto IL_0283;
		IL_0264:
		text5 = L("H", "H", "си");
		goto IL_0283;
		IL_027c:
		text5 = string.Empty;
		goto IL_0283;
		IL_01ec:
		text5 = L("D", "D", "ре");
		goto IL_0283;
	}
}
