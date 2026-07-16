# Accessibility-Audit und Verbesserungsvorschläge

## Einleitung und Würdigung des Ist-Stands

FF14 Accessibility (Stand V4.61) ist ein außergewöhnlich weit entwickeltes Dalamud-Plugin. Es deckt bereits einen großen Teil des Spieler-Alltags ab: Charaktererstellung (teilweise), Login und Lobby, Bewegung und Zielerfassung über einen Objekt-Browser mit Kategorien, Auto-Lauf per vnavmesh, eine Gehhilfe mit Sound-Beacon, NPC-Dialoge, Quest-Annahme und -Verfolgung samt Zielsätzen, einfachen Solo-Kampf mit HP/MP- und Cast-Ansagen, Inventar und Gil, eine Aktionsleiste, das Bestiarium (Jagdtagebuch) inklusive Lebensraum-Ansage und Monster-Tracking, einen Emote-Browser sowie eine automatische Tastenkonflikt-Prüfung gegen die Spielbelegung.

Bemerkenswert ist die Qualität der Entwicklung selbst: Nahezu jede Verhaltensentscheidung im Code ist mit Datum, Nutzer-Feedback-Referenz oder Log-Beleg kommentiert. Das zeigt eine iterative, nutzergetriebene Vorgehensweise, die für ein Ein-Personen-Accessibility-Projekt sehr solide ist.

Gleichzeitig zeigt die Analyse aller 16 Services deutliche Lücken beim Gruppenspiel (Party-HP, Aggro, AoE-Flächen), bei den Wirtschafts-/Sozialfunktionen (Marktbrett, Handel, Retainer, Mail, Duty Finder, Gruppensuche), beim Handwerk sowie einige technische Altlasten (harte Kodierung, Ansage-Spam-Quellen, fehlende Konfigurierbarkeit über eine Einstellungs-Oberfläche). Die folgenden Vorschläge sind nach Themen gruppiert; am Ende steht eine Top-5-Priorisierung.

Grundsätzliche Realismus-Einschränkung vorab: Ein Dalamud-Plugin hat keinen Zugriff auf die Audio-Engine des Spiels und keine Möglichkeit, den Spielclient grafisch zu verändern. Alles, was vorgeschlagen wird, muss über Lesezugriff auf Spieldaten (ObjectTable, UI-Node-Bäume, Lumina-Sheets, native Spielstrukturen per Reflection) plus eigene TTS-Ausgabe (Tolk) und eigene Sound-Dateien (NAudio, wie das bestehende Beacon-System) umsetzbar sein.

## Kampf und Encounter

### Party-HP über gepannte Earcons

Was: Für jedes sichtbare Gruppenmitglied (`IPartyList`) einen kurzen, links/rechts gepannten Ton abspielen, dessen Tonhöhe oder Klangfarbe den HP-Zustand in 20-Prozent-Stufen kodiert, ähnlich dem bestehenden Beacon-Prinzip aus `BeaconService.cs`. Optional zusätzlich eine Taste für die textuelle Ansage "Gruppe: Alice 80 Prozent, Bob 45 Prozent, ...".
Warum wichtig: Aktuell liest `CombatService.AnnounceStatus()` ausschließlich `LocalPlayer` und das eigene Ziel; Gruppenkämpfe sind für den blinden Spieler faktisch unsichtbar. Ohne Party-Status ist aktive Unterstützung (Heilung, Rettung) kaum möglich.
Aufwand: mittel (IPartyList ist bereits über Dalamud verfügbar, die Panning-Logik existiert konzeptionell schon im BeaconService).
Priorität: hoch

### Aggro-/Enmity-Ansage

Was: Über die native Enmity-/Hate-Datenstruktur (ClientStructs `EnmityModule`) erkennen, ob der Spieler auf einem Gegner Platz 1 (Tank-Aggro) hat, und bei Wechsel kurz ansagen ("Aggro auf dich" / "Aggro verloren").
Warum wichtig: Für Tank-Spieler und zur allgemeinen Gefahreneinschätzung im Kampf ist Aggro-Information zentral und aktuell komplett unvorhanden.
Aufwand: mittel (erfordert Reflection auf eine bislang nicht genutzte native Struktur, ähnlich wie bereits bei RaptureHotbarModule/AgentEmote erfolgreich gemacht).
Priorität: mittel

### Cooldown-/GCD-Ansage ("bereit")

Was: Über `ActionManager.GetRecastTime`/`IsRecastActive` und `HotbarSlot.IsSlotUsable` erkennen, wann eine Fähigkeit wieder einsatzbereit ist, und dies per kurzem Erkennungston oder auf Abfrage-Taste ansagen.
Warum wichtig: Dies ist bereits in `STATUS.md` (Zeilen 824-827, 1907) als nächster geplanter Schritt vermerkt, aber technisch noch nicht verifiziert. Ohne Cooldown-Feedback ist eine effiziente Rotation für blinde Spieler kaum möglich.
Aufwand: mittel bis groß (native Struktur muss erst per ilspycmd verifiziert werden, wie es für andere Features bereits Praxis ist).
Priorität: hoch

### Laufende Cast-Zeit des Gegners statt Einmal-Ansage

Was: Die bereits vorhandene `IsCasting`/`CastActionId`-Erkennung in `CombatService.cs` um eine Restzeit-Ansage erweitern (z. B. kurze Zwischentöne, die schneller werden, oder eine Ansage bei 50 Prozent Restzeit), plus explizite Nennung ob der Cast unterbrechbar ist (`IsCastInterruptible` wird laut Analyse aktuell nur geloggt, nicht gesprochen).
Warum wichtig: Ob ein gefährlicher Cast unterbrechbar ist und wie viel Zeit bleibt, ist eine der wichtigsten Kampfinformationen für Sehende und fehlt blinden Spielern komplett.
Aufwand: klein bis mittel (Datenquelle ist bereits erschlossen, nur die Sprachausgabe fehlt).
Priorität: hoch

### AoE-Flächen-Erkennung (Bodeneffekte)

Was: Über `ObjectTable`-Einträge vom Typ Bodeneffekt/Omen (soweit über Dalamud/ClientStructs auslesbar) die Position und Fläche gefährlicher Bodenmarkierungen ermitteln und als gerichteten Warnton (z. B. "Fläche links, 3 Meter" oder ein kurzer, lauter werdender Ton bei Annäherung) ausgeben.
Warum wichtig: AoE-Ausweichen ist einer der zentralen Kampf-Skills in FF14 und für blinde Spieler ohne technische Hilfe praktisch unmöglich. Dies ist zugleich die größte technische Herausforderung des gesamten Projekts.
Aufwand: groß (unklar, ob und wie zuverlässig Bodeneffekt-Daten über Reflection zugänglich sind; erfordert eigene Forschungsphase, evtl. nur für einfache, kreisförmige Flächen machbar).
Priorität: mittel (hohe Wichtigkeit, aber hohes technisches Risiko - realistisch als mittelfristiges Forschungsprojekt einordnen, nicht als schnellen Gewinn)

### Buff-/Debuff-Ansage kontrolliert einführen

Was: Die Statuseffekt-Leiste (`_StatusCustom0`) nicht mehr nur als Spam-Quelle behandeln (aktuell laut `STATUS.md` Zeilen 96-98 ungefiltert und mit Sprint-Countdown-Spam), sondern gezielt: wichtige Debuffs (Bewegungsunfähigkeit, Silence, Vulnerability-Stacks) auf Auftreten/Wegfall ansagen, Countdown-Werte dagegen grundsätzlich unterdrücken.
Warum wichtig: Buffs/Debuffs sind kampfentscheidend, aktuell aber weder gefiltert noch gezielt genutzt - nur als bekanntes Ansage-Spam-Problem dokumentiert.
Aufwand: mittel (Aufbau einer kuratierten Ignorier-/Ansage-Liste je Status-ID).
Priorität: mittel

## Bestehendes verbessern: Ansage-Qualität und Spam-Reduktion

### Bekannte Spam-Quellen endlich filtern

Was: Die in `STATUS.md` (Zeilen 96-104) seit V4.60/61 dokumentierten, aber unbehobenen Spam-Quellen beheben: `_StatusCustom0` (Sprint-Countdown im Sekundentakt), `_FlyText` ("+Sprint", "700", "(+100%)"), sowie restliches Login-Geplapper ("INVENTAR", "SEITE AN SEITE", "Menü, 0 Einträge").
Warum wichtig: Diese Störquellen sind bereits identifiziert, aber laut Status-Dokumentation bewusst zurückgestellt. Sie erzeugen unnötige, ablenkende Sprachausgabe gerade in kampfnahen Situationen.
Aufwand: klein (Ignorierliste analog zu `HudNoiseAddons` in `UIReaderService.cs` erweitern).
Priorität: hoch

### Zentrale Konfigurations-Oberfläche statt reiner Code-Konfiguration

Was: Eine echte Dalamud-Settings-UI (oder zumindest ein textbasiertes Konfigurationsmenü, das per Tastatur bedienbar ist) für die bereits in `Configuration.cs` vorhandenen Schalter (Chat-Kanäle, Beacon-Lautstärke, Zielwechsel-Ansagen etc.), plus neue Schalter für Ansage-Ausführlichkeit (kurz/normal/ausführlich) je Themenbereich.
Warum wichtig: Aktuell lassen sich Einstellungen nur durch Bearbeiten der Konfigurationsdatei oder durch Neukompilieren ändern (die Analyse von `UIReaderService.cs` bestätigt: keine Laufzeit-Konfiguration, keine Einstellungs-Oberfläche). Für Endnutzer ohne Entwicklerkenntnisse ist das eine erhebliche Hürde.
Aufwand: mittel (Dalamud bietet Standard-Mechanismen für Plugin-Konfigurationsfenster, die selbst wieder screenreader-zugänglich gestaltet werden müssen - z. B. rein tastaturnavigierbare ImGui-Elemente).
Priorität: hoch

### Code-Architektur: Addon-Namen und Node-IDs zentralisieren

Was: Die derzeit über den gesamten `UIReaderService.cs` (4092 Zeilen) verteilten String-Literale für Addon-Namen und hartkodierte Node-IDs in eine zentrale Konfigurationsstruktur (Dictionary/Enum) auslegen; wiederkehrende Node-Traversierungs-Muster (`ReadFirstTextInComponent`, `ReadAllTexts`, `ScanAddonTexts` u. a.) zu einer gemeinsamen generischen Hilfsfunktion zusammenführen.
Warum wichtig: Dies ist keine funktionale Lücke, sondern eine Wartbarkeits-Investition: Bei über 4000 Zeilen in einer einzigen Datei mit vielfach dupliziertem Traversal-Code wird jede künftige Erweiterung (z. B. neue Addons für Markt/Retainer, siehe unten) zunehmend fehleranfälliger und aufwändiger.
Aufwand: groß (Refactoring ohne Funktionsänderung, hohes Regressionsrisiko bei einer derart zentralen Datei - sollte schrittweise und mit Testabdeckung je Addon erfolgen).
Priorität: niedrig (technische Schuld, aber kein Nutzer-Schmerzpunkt - erst angehen, wenn neue große Features im gleichen Bereich anstehen)

### Sprechgeschwindigkeit/-lautstärke aus dem Plugin steuerbar machen

Was: `Tolk_SetRate`/vergleichbare Tolk-Funktionen nutzen, um pro Ansage-Kategorie (z. B. Kampf-Ansagen schneller, Quest-Text normal) eine Sprechgeschwindigkeit vorzugeben, statt sich komplett auf die globale Screenreader-Einstellung zu verlassen.
Warum wichtig: In hektischen Kampfsituationen kann eine schnellere, aber knappere Sprachausgabe helfen; aktuell hat das Plugin darauf laut Analyse von `TolkService.cs` keinerlei Einfluss.
Aufwand: klein bis mittel (abhängig davon, ob die genutzte Tolk-Version diese Steuerung nativ unterstützt).
Priorität: niedrig

### Mehrsprachigkeit konsequent zu Ende führen

Was: Die in `UIReaderService.cs` an mehreren Stellen (z. B. `ConfirmButtonLabels`, Belohnungs-Label "Erfahrung"/"Gil") hart auf Deutsch kodierten Texte in die bereits vorhandene, aber nur teilweise genutzte `AccessibilityStrings`-Klasse überführen, die schon DE/EN unterscheidet.
Warum wichtig: Aktuell ist die deutsche Sprachausgabe an mehreren Stellen fest verdrahtet, obwohl das Projekt an anderer Stelle bereits eine zweisprachige Infrastruktur hat - das Plugin würde bei einem englischen Spielclient an diesen Stellen falsch oder gar nicht erkennen.
Aufwand: mittel
Priorität: niedrig

## Navigation und Bewegung

### Vorne/Hinten-Unterscheidung im Sound-Beacon verbessern

Was: Das bestehende Beacon-System (`BeaconService.cs`) kodiert Winkelabweichung aktuell nur über Tonhöhe (880 Hz vorne bis 220 Hz hinten) und Seite über Stereo-Pan; bei 0° und 180° liefert das Pan-Signal aber denselben (zentrierten) Wert, wodurch "leicht vorne links" und "leicht hinten links" schwerer unterscheidbar sind. Eine zusätzliche, dritte Klangeigenschaft (z. B. Klangfarbe/Timbre-Wechsel oder ein kurzer Echo-Effekt für "hinten") würde die Front/Rück-Unterscheidung eindeutiger machen - ähnlich dem Objekt-Signatur-Prinzip aus reinen Audiospielen wie Shades of Doom.
Warum wichtig: Präzise Richtungswahrnehmung ist die Grundlage der gesamten manuellen Navigation (Gehhilfe); eine Mehrdeutigkeit hier wirkt sich auf jede Nutzung aus.
Aufwand: klein bis mittel (Erweiterung der bestehenden `BeaconSampleProvider`-Klasse).
Priorität: mittel

### Objekt-Signatur-Sounds für Landmarken

Was: Für unterschiedliche Objektkategorien (NPC, Sammelpunkt, Quest-Ziel, Ätheryt, Schatzkiste) jeweils einen kurzen, charakteristischen Erkennungston definieren, der beim Durchblättern im Objekt-Browser (`NavigationService.CycleObject`) zusätzlich zur Sprachansage abgespielt wird.
Warum wichtig: Erleichtert schnelles Wiedererkennen von Objekttypen ohne den vollen Text abwarten zu müssen - ein Konzept, das in vergleichbaren Audio-Spielen (Shades of Doom) erfolgreich eingesetzt wird und sich hier eins zu eins auf die vorhandene Kategorien-Struktur übertragen lässt.
Aufwand: klein (kurze Sounddateien plus ein Mapping Kategorie→Sound im bestehenden Cue-/Beacon-System).
Priorität: niedrig

### Bekannte Auto-Lauf-Probleme systematisch nachverfolgen

Was: Die in `AutoWalkService.cs` als "DIAGNOSTIC (temporary)" markierten Log-Mechanismen (Wegpunkt-Logging für Zonenübergangs-Verkeilungen) in ein dauerhaftes, aber unauffälliges Diagnose-Log überführen, und den bekannten "Bridge-Trap"-Bug (Navmesh castet bei Brücken/Stegen fälschlich auf einen viel tieferen Boden) sowie den in `STATUS.md` erwähnten vnavmesh-eigenen Netz-Bug beim Zonenübergang "Tiefer Wald" dokumentiert an die vnavmesh-Entwickler zurückmelden, da diese Probleme außerhalb der Kontrolle dieses Plugins liegen.
Warum wichtig: Auto-Lauf ist eine Kernfunktion; ungelöste, nur notdürftig umgangene Navmesh-Probleme führen zu Vertrauensverlust bei blinden Nutzern, die sich beim Laufen ganz auf das System verlassen müssen.
Aufwand: klein (Rückmeldung an Drittprojekt) bis mittel (eigene weitere Umgehungslogik).
Priorität: mittel

### Vertikale Navigation und Etagenwechsel klarer kommunizieren

Was: Wenn "kein Weg gefunden" auf eine getrennte Mesh-Insel (z. B. Stadt-Ebenen, nur per Aufzug/Aethernet erreichbar) zurückzuführen ist, dies expliziter von einem echten Pfadfindungs-Fehler unterscheiden und - wo möglich - automatisch erkennen, ob ein Aufzug/eine Treppe in der Nähe ist, statt nur auf Aethernet zu verweisen.
Warum wichtig: Das bestehende System erkennt dieses Problem laut Code-Kommentar bereits konzeptionell ("walking can NEVER cross that gap") und bietet mit dem Aethernet-Tipp schon eine gute Grundlösung; eine noch genauere Unterscheidung (Aufzug vs. reine Distanz) würde Fehlinterpretationen weiter reduzieren.
Aufwand: mittel
Priorität: niedrig

### Party-Sammelpunkt / "Wo ist meine Gruppe?"

Was: Über `IPartyList`-Positionsdaten eine Richtungs-/Distanzansage zum nächsten oder zu allen Gruppenmitgliedern anbieten (analog zur bestehenden Objekt-Browser-Logik), besonders nützlich beim Dungeon-Betreten oder nach einem Tod/Rückzug.
Warum wichtig: In Gruppeninhalten (Dungeons, Trials) ist es für blinde Spieler entscheidend, den Anschluss an die Gruppe zu halten; aktuell existiert dafür kein Werkzeug.
Aufwand: klein bis mittel (Positionsdaten sind über Dalamud verfügbar, Richtungsberechnung existiert bereits in `NavigationService.cs`).
Priorität: mittel

## UI-Bereiche: Inventar, Markt, Retainer, Gruppensuche

### Marktbrett (Auktionshaus) barrierefrei machen

Was: Das Marktbrett-Fenster (Suche, Preisliste, Kaufen/Verkaufen) über den bestehenden universellen Addon-Reader oder einen dedizierten Handler zugänglich machen; dies ist in `STATUS.md` bereits als geplanter, aber noch nicht begonnener Punkt vermerkt.
Warum wichtig: Ohne Marktbrett-Zugang ist ein wesentlicher Teil der In-Game-Wirtschaft (Ausrüstung kaufen, Materialien verkaufen) für blinde Spieler nicht nutzbar - dies wurde in keiner der 16 Service-Dateien und nirgends im Statusdokument als umgesetzt gefunden.
Aufwand: groß (komplexes, listenbasiertes Fenster mit Sortier-/Filterfunktionen, ähnlich aufwändig wie das bereits gelöste Bestiarium, vermutlich aufwändiger wegen Preis-/Mengenfeldern).
Priorität: hoch

### Retainer (Gehilfe) bedienbar machen

Was: Retainer-Fenster (Beauftragen, Verkaufsliste einsehen/bearbeiten, Lagerung) über eigene oder universelle Addon-Handler zugänglich machen.
Warum wichtig: Aktuell existiert laut Analyse nur ein reines Objekt-Label ("Gehilfe") in der Zielkategorie-Ansage - keinerlei Fensterlogik. Retainer sind für viele Spieler zentral für Wirtschaft und Inventarverwaltung.
Aufwand: mittel bis groß
Priorität: mittel

### Gearset-/Ausrüstungs-Ansage

Was: Aktuelle Ausrüstung (angelegte Items pro Slot) über `IGameInventory`/`InventoryType.EquippedItems` auslesbar machen und auf Tastendruck ansagen; perspektivisch auch Gearset-Wechsel screenreader-lesbar gestalten.
Warum wichtig: `InventoryService.cs` deckt laut Analyse Taschen, Schlüsselgegenstände und Gil ab, aber keine Ausrüstung - ein Spieler kann aktuell nicht erfahren, was er trägt, ohne das Sehende-UI zu nutzen.
Aufwand: klein bis mittel (die Inventar-API-Struktur ist bereits im Projekt etabliert, `EquippedItems` ist ein regulärer `GameInventoryType`).
Priorität: hoch

### Handel (Trade-Fenster) zwischen Spielern

Was: Das Trade-Fenster (Gegenstände/Gil anbieten, Bestätigung) analog zum bereits gelösten NPC-Ablieferungsfenster (`Request`) zugänglich machen.
Warum wichtig: Handel zwischen Spielern ist eine grundlegende soziale/wirtschaftliche Interaktion, die aktuell nicht unterstützt wird, obwohl die technische Lösung für ein sehr ähnliches Fenster (NPC-Request) bereits existiert und wiederverwendet werden kann.
Aufwand: klein bis mittel (hohe Ähnlichkeit zum bereits gelösten Request-Fenster ist ein klarer Vorteil).
Priorität: mittel

### Duty Finder und Gruppensuche (Party Finder)

Was: Dungeon-/Trial-Auswahl im Duty Finder sowie Party-Finder-Listen (Beschreibung, Anforderungen, Beitreten) über den universellen oder einen dedizierten Handler lesbar machen.
Warum wichtig: Ohne Duty-Finder-Zugang können blinde Spieler nicht selbstständig in Gruppeninhalte (Dungeons, Trials, Raids) gelangen - eine der größten Zugangsbarrieren für "vollständig eigenständiges Spielen".
Aufwand: mittel (überwiegend listenbasierte Fenster, ähnliche Struktur wie bereits gelöste Listen-Addons).
Priorität: hoch

### Mail (Postfach)

Was: Postfach-Fenster (Absenderliste, Betreff, Anhänge, Annehmen) zugänglich machen.
Warum wichtig: Wird aktuell nirgends im Projekt erwähnt oder unterstützt; für Handel per Retainer-Verkauf und generelle Kommunikation notwendig.
Aufwand: klein bis mittel (einfaches Listenfenster)
Priorität: niedrig

### Handwerk/Crafting-Grundunterstützung

Was: Zumindest eine einfache Ansage des Synthese-Fortschritts (Fortschritt/Qualität/Haltbarkeit-Werte) während des Craftings sowie Vorlesen des Rezeptbuchs.
Warum wichtig: Handwerk wird im gesamten Projekt bislang an keiner Stelle erwähnt oder unterstützt; es ist ein umfangreicher eigenständiger Spielbereich, dessen komplette Erschließung (Rotation, Qualitäts-Feedback in Echtzeit) sehr aufwändig wäre, aber selbst eine einfache Fortschrittsansage wäre ein erster Zugang.
Aufwand: groß (vollständige Unterstützung), klein bis mittel (nur Basis-Statusansage)
Priorität: niedrig (im Vergleich zu Kampf/Markt/Duty-Finder-Lücken; als Langfrist-Ziel einordnen)

## Onboarding und Ersteinstieg für neue blinde Spieler

### Interaktives Ersteinrichtungs-Tutorial

Was: Ein geführter Ersteinstieg (z. B. über `/acc tutorial` oder automatisch beim allerersten Start) der Schritt für Schritt durch die wichtigsten Tasten (Objekt-Browser, Auto-Lauf, Hilfe-Taste, Kampfstatus) führt, mit kurzen Übungsaufgaben statt nur einer statischen Hilfe-Ansage.
Warum wichtig: Die aktuelle Hilfe (`Strg+F1`) liest eine lange, undifferenzierte Liste aller Tasten auf einmal vor - für komplette Neueinsteiger ohne Sehvermögen ist das eine hohe kognitive Hürde. Vergleichbare Projekte wie Hearthstone Access setzen bewusst auf ein eigenes, geführtes Tutorial für Screenreader-Nutzer.
Aufwand: mittel (Konzept und Texte, keine neue technische Infrastruktur nötig, nutzt bestehende TTS-Ausgabe).
Priorität: hoch

### Kontextsensitive "Wo bin ich"-Ansage konsolidieren und ausbauen

Was: Die bestehende `AnnounceActiveWindow`/`Strg+F2`-Funktion als zentrale Orientierungshilfe stärker bewerben und um eine kurze "was kann ich hier tun"-Zusatzinfo erweitern (z. B. bei einem neuen Fenstertyp automatisch die wichtigsten Bedienhinweise mitsprechen, ähnlich einer Kontexthilfe).
Warum wichtig: Gerade neue Nutzer wissen oft nicht, in welchem Menü/Fenster sie sich befinden oder welche Tasten dort gelten - eine konsolidierte, verständliche Ansage reduziert Verwirrung erheblich.
Aufwand: klein bis mittel
Priorität: mittel

### Übungsmodus / reduzierte Komplexität für Kampf-Neulinge

Was: Eine Art "Trainingshinweis" beim ersten Betreten des Kampfmodus, der die wichtigsten Kampf-Tasten (HP-Ansage, Hotbar, Zielwechsel) kurz erläutert, angelehnt an das Prinzip aus reinen Audio-Spielen (Shades of Doom, Hearthstone Access), Nutzer zuerst behutsam an das Sound-/Sprach-Vokabular heranzuführen.
Warum wichtig: Kampfsysteme sind für blinde Neueinsteiger die komplexeste Hürde; ein sanfter Einstieg erhöht die Wahrscheinlichkeit, dass neue Spieler nicht frühzeitig aufgeben.
Aufwand: klein (rein textuell/konzeptionell, keine neue Technik).
Priorität: mittel

### Zentrales Hilfe-System nach Themen statt einer langen Liste

Was: Die Hilfe-Ansage (`AnnounceHelp` in `Plugin.cs`) in thematische Unterabschnitte gliedern (z. B. "Strg+F1 für Navigation, Strg+F2 für Kampf, Strg+F3 für Menüs"), durch die man mit einer Taste blättern kann, statt eines einzigen langen Fließtexts.
Warum wichtig: Die aktuelle Hilfe ist bereits umfangreich (Navigation, Kampf, Inventar, Emotes gemischt) und wird mit jedem neuen Feature länger - eine flache Liste wird zunehmend unübersichtlich vorzulesen.
Aufwand: klein
Priorität: mittel

## Endgame: Raids, Extreme/Savage - realistische Einschätzung

Eine vollständige, eigenständige Bewältigung von Extreme- oder Savage-Raid-Mechaniken (komplexe, zeitkritische AoE-Muster, Stack-/Spread-Mechaniken, Rollen-spezifische Sonderaufgaben) ist mit den aktuell realistisch verfügbaren Dalamud-Mitteln nur sehr eingeschränkt erreichbar. Die technische Grundvoraussetzung dafür - zuverlässige, latenzarme Erkennung von Bodeneffekten/Telegraphen - ist selbst bei bestehenden Sehenden-Add-ons (Cactbot, ACT-Overlays) ein aufwändiges Feature, das auf externe Encounter-Datenbanken und ständige Pflege pro Kampf angewiesen ist.

Realistisch umsetzbar und sinnvoll ist dagegen:
- Party-HP und eigene HP/Cast-Informationen (siehe oben) als Grundlage für "Support"-Rollen (Heiler) auch in schwierigeren Inhalten.
- Eine allgemeine, nicht encounter-spezifische AoE-Erkennung für einfache, klar erkennbare Bodenmarkierungen (siehe Vorschlag oben), die auch in Extreme/Savage zumindest teilweise hilft.
- Die Zusammenarbeit mit einer sehenden Person in der Gruppe (Ansage per Discord/Sprachchat) bleibt für hochkomplexe Encounter-Mechaniken auf absehbare Zeit der praktikabelste Weg - das Plugin sollte diesen nicht ersetzen wollen, sondern wo möglich ergänzen (z. B. durch das oben erwähnte Cast-Interrupt-Feature).

Diese Einschätzung sollte transparent kommuniziert werden, damit Erwartungen realistisch bleiben: "Perfekt spielbar" ist im Solo- und Gruppen-Content bis einschließlich normaler Schwierigkeit ein erreichbares Ziel; bei Extreme/Savage ist eher von "mit Unterstützung spielbar" auszugehen.

## Sound-Design: Weiterentwicklung des bestehenden Beacon-Konzepts

Das bestehende Beacon-System ist bereits ein funktionales Richtungs-/Distanz-Feedback (Tonhöhe für Winkel, Stereo-Pan für Seite, Lautstärke für Distanz), aber kein echtes Sonar mit Umgebungsabtastung. Vergleichbare Projekte zeigen folgende übertragbare Konzepte:

### Mehrere gleichzeitige Signaturtöne (Objekt-Radar statt Einzelziel)

Was: Statt nur des einen aktiven Navigationsziels könnten mehrere nahe, relevante Objekte (z. B. die 2-3 nächsten Gegner oder Sammelpunkte) gleichzeitig als leise, unterscheidbare Earcons im Hintergrund hörbar gemacht werden, ähnlich einem Sonar-Sweep.
Warum wichtig: Aktuell muss der Objekt-Browser aktiv durchgeblättert werden, um zu erfahren, was in der Nähe ist; ein passives Hintergrund-Signal würde räumliches Bewusstsein ohne aktive Abfrage ermöglichen - ein Kernkonzept aus reinen Audio-Spielen wie Shades of Doom.
Aufwand: groß (mehrere gleichzeitige Audioquellen ohne gegenseitige Verwirrung zu gestalten ist eine größere Sounddesign-Herausforderung, technisch aber mit dem vorhandenen NAudio-Unterbau machbar).
Priorität: niedrig (spannendes Zukunftsfeature, aber kein akuter Schmerzpunkt gegenüber den oben genannten Lücken).

### Wärmer/Kälter-Prinzip für Wegpunkt-Navigation

Was: Analog zum ESO-Accessibility-Addon FCOAccessibility bei der Gehhilfe einen kontinuierlichen "wärmer/kälter"-Effekt (z. B. Tonhöhe steigt, je näher man dem direkten Pfad zum Ziel kommt, unabhängig vom reinen Zielwinkel) ergänzen.
Warum wichtig: Kann helfen, wenn der Spieler leicht vom optimalen Pfad abweicht, ohne dass er die exakte Gradzahl abschätzen muss - eine niedrigschwelligere Ergänzung zur bestehenden Winkel-/Distanz-Logik.
Aufwand: klein bis mittel (Erweiterung des bestehenden Beacon-Systems)
Priorität: niedrig

## Top-5-Empfehlung: Was zuerst angehen

1. Bekannte Ansage-Spam-Quellen filtern (`_StatusCustom0`-Sprint-Countdown, `_FlyText`, Login-Geplapper) - kleiner Aufwand, sofortige spürbare Verbesserung der täglichen Nutzungsqualität, seit Version 4.60/61 bereits identifiziert und nur noch nicht umgesetzt.
2. Party-HP-Ansage für Gruppeninhalte (gepannte Earcons plus Sammel-Taste) - schließt die größte funktionale Lücke für alles jenseits von Solo-Content und ist mit vorhandener Infrastruktur (IPartyList, BeaconService-Muster) machbar.
3. Gearset-/Ausrüstungs-Ansage im Inventar - kleiner bis mittlerer Aufwand, schließt eine überraschend grundlegende Lücke (der Spieler weiß aktuell nicht, was er anhat).
4. Cast-Interrupt-Information und laufende Cast-Zeit-Ansage des Gegners ergänzen - Datenquelle ist bereits erschlossen (`IsCastInterruptible` wird schon geloggt), es fehlt nur die Sprachausgabe; hoher Wirkungsgrad für Kampfsicherheit bei geringem technischem Risiko.
5. Duty Finder und Marktbrett grundlegend zugänglich machen - größere Aufwände, aber sie schließen die beiden Lücken, die dem Ziel "komplett ohne Sehen spielen" am stärksten entgegenstehen (kein eigenständiger Zugang zu Gruppeninhalten, keine eigenständige Marktteilnahme).
