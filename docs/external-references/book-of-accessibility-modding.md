# BOOK_OF_ACCESSIBILITY — External Reference

> **Nicht Teil dieses Templates.** Dieses Dokument ist eine **Referenz auf ein externes Werk**, das von einer anderen Person geschrieben wurde. Wir haben es nicht verfasst und wir hosten den Inhalt nicht mit. Nur hier verlinkt, damit du es bei Bedarf findest.

## Quelle

- **Titel:** The Book of Accessibility Modding
- **Autor / GitHub-Handle:** `blal1` (GitHub-Profil ohne Klarnamen, signiert sich im Text als „Accessibility Modding Community")
- **Repo:** https://github.com/blal1/BOOK_OF_ACCESSIBILITY_MODING
- **Direktlink (EN):** https://github.com/blal1/BOOK_OF_ACCESSIBILITY_MODING/blob/main/BOOK_OF_ACCESSIBILITY.md
- **Direktlink (FR):** https://github.com/blal1/BOOK_OF_ACCESSIBILITY_MODING/blob/main/BOOK_OF_ACCESSIBILITY_FR.md
- **Gesichtet:** 2026-04-19
- **Umfang:** ca. 314 KB Markdown, 16 Kapitel, 7 Teile

## Lizenz-Status

- **Keine Lizenz angegeben** (keine LICENSE-Datei, kein Lizenz-Text im Dokument).
- Damit gilt Standard-Urheberrecht: **nicht kopieren, nicht re-hosten, nicht in eigene Projekte einchecken**.
- Lesen und Verlinken ist in Ordnung. Zitate nur mit klarer Quellenangabe.

## Worum geht es

Lehrbuch-Format, nicht Scaffold. Führt von Grund auf durch das Modden von Unity-Spielen für blinde Spieler.

**Abgedeckte Technologien:**
- Unity (Mono und IL2CPP)
- BepInEx, MelonLoader, Harmony / HarmonyX
- Tolk (Windows), speech-dispatcher (Linux), `say` (macOS)
- Dekompilierung: ilspycmd, dnSpyEx, Il2CppDumper, Il2CppInspector
- .NET 8+, PowerShell, Visual Studio / VS Code

**Struktur in Kurzform:**
- Teil 1: Modding-Ökosystem und Setup
- Teil 2: Architektur (Speech, Input-Virtualisierung, State-Extraktion)
- Teil 3: Reverse Engineering, Dekompilierung
- Teil 4: Menü-Patterns, räumliche Navigation, fortgeschrittenes Patching
- Teil 5: Lokalisierung, native Localization-Hooks
- Teil 6: Deployment, Community, Social/Meta
- Teil 7: Case Study zum `ATSAccessibility`-Mod für *Against the Storm* (nicht vom Buch-Autor, sondern von `rashadnaqeeb/ats-accessibility-mod`, hier nur analysiert)

**Zielgruppe:** Mittelfortgeschrittene bis erfahrene C#-Entwickler mit etwas Modding-Erfahrung.

## Wann ist es für uns nützlich

- **IL2CPP-Themen** — unser Template ist Mono-lastig; das Buch geht tiefer auf IL2CPP-Dumping und MelonLoader ein.
- **Harmony-Transpiler-Patches** — falls ein Projekt über Prefix/Postfix hinausgehen muss.
- **Architektur-Philosophie** — Kapitel 3 („Nervous System") und Kapitel 4 („Input Virtualization") haben gute Begründungen für zentrale Input-Authority.
- **Cross-Platform-Screen-Reader-Abstraktion** — falls je Linux/macOS-Support anfällt.

## Bekannte Lücken im Buch

- Wenig zu Echtzeit-/Action-Games (Souls-like, FPS, Plattformer)
- Controller/Gamepad nur knapp
- Kein Abschnitt zu automatisiertem Testing / CI gegen Spiel-Updates
- Keine Behandlung von Multiplayer / Anti-Cheat
- Kapitel 5 endet laut Analyse mitten in der Implementierung abrupt
- Keine Vertiefung zu rechtlichen Fragen (ToS, DMCA)

## Verhältnis zu diesem Template

- **Buch:** Lehrbuch + Referenz. Liest man.
- **Dieses Template:** Ausführbares Scaffold mit Claude-Führung. Klont man, `CLAUDE.md` fragt dich durch den Setup.
- Sie überschneiden sich konzeptionell, ersetzen sich aber nicht gegenseitig.
