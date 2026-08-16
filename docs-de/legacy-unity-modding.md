# Modding bei alten Unity-Versionen

## Unity-Versionen und Mod-Loader-Kompatibilität

- **Unity 2019+**: MelonLoader, BepInEx, Doorstop - alle funktionieren
- **Unity 2017-2018**: MelonLoader, BepInEx, Doorstop - alle funktionieren
- **Unity 5.x**: Teilweise MelonLoader, teilweise BepInEx, evtl. Doorstop, Assembly-Patch als Fallback
- **Unity 4.x oder älter**: Nur Assembly-Patching funktioniert

## Vor dem Setup prüfen

### 1. Unity-Version ermitteln

Die Unity-Version steht in:
- `[Game]_Data/output_log.txt` (erste Zeile: "Initialize engine version: X.X.X")
- Oder im Crash-Log
- Oder im MelonLoader-Log nach erstem Start

### 2. Architektur prüfen

- `Mono/` Ordner = 32-bit, altes Mono
- `MonoBleedingEdge/` Ordner = 64-bit, neueres Mono
- Spiel in "Program Files (x86)" = oft 32-bit

### 3. Community-Recherche

**Immer zuerst prüfen:**
- Gibt es bereits Mods für das Spiel?
- Welches Framework nutzt die Community?
- Gibt es ein offizielles Mod-System?

**Suchbegriffe:**
- "[Spielname] modding guide"
- "[Spielname] BepInEx"
- "[Spielname] MelonLoader"
- "[Spielname] dll mod"

**Wo suchen:**
- Steam-Diskussionen
- Nexus Mods
- ModDB
- GitHub
- Offizielle Foren des Entwicklers

## Mod-Loader nach Priorität versuchen

### Bei Unity 2017+

1. **MelonLoader** - Einfachste Option für Unity-Spiele
2. **BepInEx** - Sehr verbreitet, gute Dokumentation
3. **Doorstop** - Falls andere nicht funktionieren

### Bei Unity 5.x

1. **BepInEx 5.x** mit net35-Kompatibilität
2. **Doorstop v3** (legacy branch)
3. **Assembly-Patching** als Fallback

### Bei Unity 4.x oder älter

1. **Community-Lösung suchen** - Vielleicht hat jemand etwas gefunden
2. **Assembly-Patching** - Meist die einzige funktionierende Option

## Assembly-Patching (Immer funktioniert)

### Was es ist

Den Mod-Code direkt in die Spiel-DLL (`Assembly-CSharp.dll`) einfügen.

### Vorteile

- Funktioniert mit jeder Unity-Version
- Keine externen Tools zur Laufzeit
- Keine Proxy-DLLs nötig

### Nachteile

- Ändert Original-Dateien (Backup machen!)
- Bei Spiel-Updates muss neu gepatcht werden
- Steam-Integritätsprüfung erkennt Änderung

### Tools

- **dnSpy** - GUI-basiert, kann editieren und speichern
- **ILSpy + Reflexil** - Alternative

### Vorgehen

1. Backup von `[Game]_Data/Managed/Assembly-CSharp.dll`
2. DLL in dnSpy öffnen
3. Geeignete Stelle finden (z.B. MainMenu.Start oder Awake)
4. Code einfügen (Methode editieren oder neue Klasse)
5. Speichern (Modul speichern)
6. Testen

### Geeignete Einstiegspunkte

- `Awake()` oder `Start()` einer früh geladenen Klasse
- Hauptmenü-Klasse (wird immer geladen)
- Singleton-Initialisierung

## Bekannte Probleme

### "mono.dll Access Violation"

- Tritt bei Unity 4.x mit BepInEx/Doorstop auf
- Mono-Runtime zu alt für moderne Tools
- Lösung: Assembly-Patching

### "Hooked into null"

- MelonLoader kann sich nicht einhaken
- Unity-Version nicht unterstützt
- Lösung: Anderes Framework oder Assembly-Patching

### Spiel startet nicht (0xc0000142)

- Proxy-DLL inkompatibel
- Lösung: Proxy-DLL entfernen, anderen Ansatz wählen

## C#-Sprachfeatures und alte Mono-Runtimes

Unity bringt seine eigene Mono-Runtime mit. Bei alten Unity-Versionen ist diese Runtime so alt, dass bestimmte C#-Features zwar **kompilieren**, aber zur **Laufzeit abstürzen**. Das ist besonders tückisch, weil kein Compiler-Fehler auftritt — der Build ist erfolgreich, und dann crasht das Spiel ohne klare Fehlermeldung.

### Wie erkennt man, welche Runtime man hat?

- `Mono/` Ordner im Spielverzeichnis = alte Runtime (Unity 5.x und früher)
- `MonoBleedingEdge/` Ordner = neuere Runtime (Unity 2017+, aber trotzdem eingeschränkt bis ~2019)
- Ab Unity 2021+ mit `MonoBleedingEdge/` sind praktisch alle modernen C#-Features sicher

### Bekannte Einschränkungen (Unity 2017 und älter)

**LINQ mit Lambdas** — Crash zur Laufzeit:
```csharp
// CRASHT bei alter Mono-Runtime:
var active = myList.Where(x => x.IsActive).ToList();
var names = myList.Select(x => x.Name).ToList();

// SICHER — klassische Schleife stattdessen:
var active = new List<MyType>();
foreach (var item in myList)
{
    if (item.IsActive) active.Add(item);
}
```

**string.Join mit List** — Crash zur Laufzeit:
```csharp
// CRASHT — alte Mono kennt nur die Array-Überladung:
string result = string.Join(", ", myList);

// SICHER:
string result = string.Join(", ", myList.ToArray());
```

**Switch Expressions** — Kompiliert nicht oder crasht:
```csharp
// CRASHT:
string label = state switch { State.Active => "an", State.Off => "aus", _ => "?" };

// SICHER:
string label;
switch (state)
{
    case State.Active: label = "an"; break;
    case State.Off: label = "aus"; break;
    default: label = "?"; break;
}
```

**Sort mit Lambda** — Crash zur Laufzeit:
```csharp
// CRASHT:
myList.Sort((a, b) => a.Distance.CompareTo(b.Distance));

// SICHER — eigene IComparer-Klasse:
private class DistanceComparer : IComparer<MyType>
{
    public int Compare(MyType a, MyType b)
    {
        return a.Distance.CompareTo(b.Distance);
    }
}
myList.Sort(new DistanceComparer());
```

**Reflection Null-Checks** — Vergleich funktioniert nicht:
```csharp
// FUNKTIONIERT NICHT bei alter Mono — gibt immer false:
if (fieldInfo == null) { ... }

// SICHER — expliziter Cast:
if ((object)fieldInfo == null) { ... }
```

### Welche Unity-Version braucht was?

- **Unity 4.x:** Stark eingeschränkt. Kein LINQ, keine Lambdas, kein `var` in manchen Kontexten. Wie C# 3.0 behandeln.
- **Unity 5.x:** LINQ-Grundfunktionen gehen (Where, Select ohne Lambda), aber Lambda-Syntax oft problematisch. Sort-Lambdas unsicher.
- **Unity 2017-2018:** LINQ mit Lambdas geht meistens, aber string.Join mit List und Switch Expressions nicht. Reflection-Null-Checks unsicher.
- **Unity 2019+:** Die meisten modernen C#-Features funktionieren. Trotzdem testen.
- **Unity 2021+:** Praktisch keine Einschränkungen mehr.

### Warum das funktioniert

Das ist kein Hack. Der C#-Compiler erzeugt für `list.Where(x => ...)` anderen Zwischencode (IL) als für eine `foreach`-Schleife. Die alte Mono-Runtime kann den einfacheren IL ausführen, den komplexeren nicht. Beide Varianten machen exakt dasselbe — die Schleife ist die ältere, kompatible Art, dasselbe Ergebnis zu bekommen.

Das betrifft nur die **Mono-Runtime des Spiels**, nicht den Mod-Loader. MelonLoader und BepInEx laufen auf dieser Runtime — sie können sie nicht ändern oder umgehen.

### Empfehlung

Bei Unity 2018 und älter: Die kompatiblen Varianten verwenden (Schleifen statt LINQ-Lambdas, Arrays statt Listen bei string.Join, switch-Blöcke statt switch-Expressions). Das ist kein Stilkompromiss, sondern technisch notwendig, weil die Spiel-Runtime die modernen IL-Instruktionen nicht versteht. Wenn ein Feature wider Erwarten doch funktioniert, im Code kommentieren, dass es getestet wurde — das spart dem nächsten Entwickler die gleiche Recherche.

---

## Besonderheiten bei der UI-Analyse (alte Unity-Versionen)

Bei älteren Unity-Versionen können zusätzliche Herausforderungen auftreten:

- **Ältere UI-Systeme:** Unity 4.x nutzt oft noch das alte `OnGUI`-System statt uGUI/Canvas
- **Andere Komponenten-Namen:** `GUIText` statt `Text`, `GUITexture` statt `Image`
- **Fehlende Features:** TextMeshPro existiert möglicherweise nicht
- **Reflection-Unterschiede:** Private Fields können andere Naming-Conventions haben

### OnGUI-System (Unity 4.x und früher)

Das alte OnGUI-System funktioniert komplett anders als moderne Unity UI:

```csharp
void OnGUI() {
    if (GUI.Button(new Rect(10, 10, 100, 50), "Click me")) {
        // Button wurde geklickt
    }
    GUI.Label(new Rect(10, 70, 100, 20), "Some text");
}
```

**Herausforderungen:**
- UI wird jeden Frame neu gezeichnet (Immediate Mode)
- Keine persistenten GameObjects für UI-Elemente
- Schwieriger zu hoooken als moderne UI
- Text ist oft nur im OnGUI-Aufruf bekannt

**Mögliche Lösungen:**
- Harmony-Patch auf die OnGUI-Methode
- GUI.skin und GUIStyle analysieren
- Eigene Tracking-Logik für UI-Zustand

## Checkliste für alte Spiele

- [ ] Unity-Version ermittelt
- [ ] Architektur geprüft (32/64-bit)
- [ ] Community-Lösungen recherchiert
- [ ] Offizielles Mod-System geprüft
- [ ] Mod-Loader in Reihenfolge versucht
- [ ] Bei Fehlschlag: Assembly-Patching vorbereitet
- [ ] Backup der Original-DLLs erstellt
