# FF14 Accessibility Plugin - Projektstatus

## Ziel
Dalamud-Plugin für FF14 das blinden Spielern via NVDA/TOLK ermöglicht das Spiel vollständig per Tastatur zu spielen.

## Was bereits fertig ist

### Code (H:\ff14\FF14Accessibility\)
| Datei | Status | Beschreibung |
|---|---|---|
| `Plugin.cs` | ✅ fertig | Einstiegspunkt, `/acc`-Befehle, F6-F11 Tasten, Controller D-Pad |
| `Configuration.cs` | ✅ fertig | Einstellungen, Tastenbelegung |
| `Native/TolkNative.cs` | ✅ fertig | P/Invoke zu Tolk.dll, DllImportResolver (pfadunabhängig) |
| `Services/TolkService.cs` | ✅ fertig | NVDA-Anbindung, Warteschlange |
| `Services/NavigationService.cs` | ✅ fertig | Richtung/Distanz zu Zielen |
| `Services/ChatReaderService.cs` | ✅ fertig | Chat vorlesen |
| `Services/UIReaderService.cs` | ✅ fertig | Menüs vorlesen — universelles System (PostSetup/Update/ReceiveEvent) |
| `Services/CombatService.cs`   | ✅ fertig | HP/MP ansagen, Kampfbeginn/-ende, HP-Schwellenwerte (75/50/25/10%) |
| `FF14Accessibility.csproj` | ✅ fertig | net10.0-windows, DalamudPackager 14.0.2, ApiLevel 14 |
| `FF14Accessibility.json` | ✅ fertig | DalamudApiLevel: 14 |

### Build-Status
- **Letzter Build: ERFOLGREICH** (2026-04-30)
- Output: `H:\ff14\FF14Accessibility\bin\Debug\net10.0-windows\FF14Accessibility.dll`
- 1 harmlose Warnung: ImGui.NET.dll nicht gefunden (wird nicht genutzt)

### Umgebung
- `DALAMUD_HOME` = `C:\Users\brued\AppData\Roaming\XIVLauncher\addon\Hooks\dev`
- `Tolk.dll` + `nvdaControllerClient64.dll` im Output-Verzeichnis (werden automatisch gefunden)
- .NET 10 SDK aktiv
- Plugin wird von Dalamud geladen und läuft ✅

## UIReaderService – Architektur (2026-04-30)

### Menü-Stack
- `_menuStack` (Stack) statt einzelner `_lastListAddon`-Variable
- **PostSetup**: Addon öffnet + Liste gefunden → Stack-Push, alle Einträge vorlesen
- **PreFinalize**: Addon schließt → Stack-Pop (Elternmenü wird automatisch wieder aktiv)
- **PostUpdate**: Nur für das oberste Stack-Element — SelectedItemIndex-Änderung → Eintrag ansagen
- **PostReceiveEvent**: Fallback für Nicht-Listen-Addons (HasFocus-Flag)

### Spezial-Addons
- **Talk, SelectYesno**: eigene Handler, kein Stack-Eintrag
- **SelectString, SelectIconString**: eigener PostSetup-Handler (liest Fragtext + Liste vor), dann Stack-Eintrag + Update läuft universal
- **Benachrichtigungen**: eigene Handler

### Navigation (Plugin.cs)
- Pfeiltasten: nur bei `HasActiveMenu`, Spiel navigiert nativ, Update-Hook kündigt Änderungen an
- Enter/Escape: unterdrückt (SuppressKey) + FireCallback — kein Doppel-Trigger
- Controller: D-Pad Links/Rechts → NavigateGamepad(±1) für SelectYesno

## Weitere Features (geplant)
- [ ] Cooldowns ansagen (ActionManager.GetRecastGroupDetail via FFXIVClientStructs)
- [ ] Audio-Beacon (Stereo-Panning zum Ziel)
- [ ] Auktionshaus/Marktplatz vollständig lesbar machen
- [ ] Alle Untermenüs (Ausrüstung, Inventar) — testen ob universeller Handler reicht
- [ ] Zielverfolgung per Name über Chat-Befehl

## Chat-Befehle
```
/acc set    → aktuell anvisiertes Ziel verfolgen
/acc nav    → Richtung + Distanz ansagen
/acc near   → Objekte in der Nähe auflisten
/acc ui     → aktuelles Menü vorlesen
/acc stop   → Sprache stoppen
/acc help   → Hilfe
```

## Tastenbelegung (F6-F12)
| Taste | Funktion |
|-------|---------|
| F6 | Richtung + Distanz zum Ziel |
| F7 | Aktuelles Spielziel verfolgen |
| F8 | Zielverfolgung beenden |
| F9 | Objekte in der Nähe |
| F10 | Aktuelles Menü vorlesen |
| F11 | Sprache stoppen |
| F12 | HP/MP-Status ansagen |
