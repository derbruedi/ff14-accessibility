# Controller-Kreuzleisten

Normale Kreuzleisten 1–8 lassen sich vorlesen und über ein gesprochenes Menü
belegen. Das Menü wird auch im Controller-Modus mit dem Nummernblock bedient.

- **Strg+F9** liest im Controller-Modus alle 16 Tasten der aktuellen normalen
  Kreuzleiste einschließlich leerer Plätze. Im Tastatur-Modus bleibt es bei Leiste 1.
- **`/acc crossread`** liest die normale Kreuzleiste unabhängig vom Eingabemodus.
- Ein Wechsel der normalen Kreuzleiste wird im Controller-Modus einmal angesagt.
- **`/acc crossbar`** oder **Strg+Nummernblock0** öffnet das Belegungsmenü.

## Eine Taste belegen

1. Mit **Nummernblock8/2** Kreuzleiste 1–8 oder Tastaturleisten wählen;
   **Nummernblock0** öffnet die Tasten. Der Kurzbefehl startet im Controller-Modus
   bei der aktuellen Kreuzleiste, sonst bei Tastaturleisten.
2. **Nummernblock8/2** liest die Tasten mit ihrer aktuellen Belegung.
   **Nummernblock4/6** wechselt die Leiste. **Nummernblock0** wählt die Zieltaste.
3. **Nummernblock4/6** wechselt zwischen Fähigkeiten, Gegenständen,
   Quest-Gegenständen, allgemeinen Aktionen, Reittieren und Mitstreiter-Kommandos.
   **Nummernblock8/2** blättert durch die Einträge.
4. **Nummernblock0** ersetzt die Belegung und kehrt zur Tastenliste zurück.

**Nummernblock Komma** geht einen Schritt zurück und schließt aus der
Leistenauswahl. **Strg+Nummernblock0** schließt ebenfalls. Bei geänderten
Plugin-Tasten gelten die eigenen Einstellungen.

Angesagt werden Leiste, L2/R2, Steuerkreuz- oder Aktionstastenposition und Inhalt.
Die Namen Quadrat/Dreieck/Kreis/Kreuz beziehen sich auf die Standardbelegung der
PlayStation; bei Xbox gelten LT/RT und die entsprechenden Tastenpositionen.
Eigene Controller-Umbindungen werden nicht ausgewertet. Geteilte Leisten werden
als solche angesagt: Änderungen betreffen auch andere Jobs.

PvP-Belegungen werden blockiert; nach Jobwechsel oder Abmeldung muss das Menü
erneut geöffnet werden. Erweiterte Haltebelegung, WXHB/Doppelkreuz und die
Begleiter-Kreuzleiste sind nicht abgedeckt. Gelesen wird die normale Leiste;
bei angezeigter Begleiterleiste wird sie als nicht verfügbar gemeldet.

Die Zuordnung aller 16 Tasten, tatsächliche Sprachausgabe, eigene Umbindungen und
das Speichern nach erneutem Anmelden müssen noch im Spiel geprüft werden.
Technische Prüfschritte stehen in [der englischen Dokumentation](../docs/controller-crossbar.md#verification-for-contributors).
