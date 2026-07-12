$old = @"
    private unsafe void AnnounceConfigSystemFocusIfChanged(AtkUnitBase* addon)
    {
        // Diagnose-Dump im Frame nach InputReceived
        if (_dumpOnNextConfigSystemUpdate)
        {
            _dumpOnNextConfigSystemUpdate = false;
            DumpConfigSystemNodes(addon);
        }

        // Tab-Fokus prüfen
        var (tabIdx, tabLabel) = GetFocusedTabInfo(addon);
        if (tabIdx != _csLastTabIndex || tabLabel != _csLastTabText)
        {
            _csLastTabIndex = tabIdx;
            _csLastTabText  = tabLabel;

            if (!string.IsNullOrEmpty(tabLabel) && _csTabs.Count > 0)
            {
                _log.Info($"[CS] Tab-Wechsel ? '{tabLabel}' [{tabIdx + 1}/{_csTabs.Count}]");
                _tolk.SpeakInterrupt(AccessibilityStrings.TabPosition(tabLabel, tabIdx + 1, _csTabs.Count));  
            }

            // Cache zurücksetzen: neue Texte des Tabs als Ersterscheinung behandeln
            _configSystemLastTexts.Clear();
            _csOptionFlags.Clear();
            ScanConfigSystemTexts(addon); // Cache befüllen, nichts ansagen
            LogConfigOptionFlags(addon);  // [CS-OPT] Ausgangszustand protokollieren
            return;
        }

        // [CS-OPT] Flags-Änderungen an Option-Nodes erkennen (Diag2: klärt Fokus-Indikator)
        LogConfigOptionFlagChanges(addon);

        // Wert-Änderungen in Text-Nodes scannen und ansagen
        ScanConfigSystemTexts(addon);
    }
"@
$new = @"
    private unsafe void AnnounceConfigSystemFocusIfChanged(AtkUnitBase* addon)
    {
        // Diagnose-Dump im Frame nach InputReceived
        if (_dumpOnNextConfigSystemUpdate)
        {
            _dumpOnNextConfigSystemUpdate = false;
            DumpConfigSystemNodes(addon);
        }

        // Tab-Fokus prüfen
        var (tabIdx, tabLabel) = GetFocusedTabInfo(addon);
        if (tabIdx != _csLastTabIndex || tabLabel != _csLastTabText)
        {
            _csLastTabIndex = tabIdx;
            _csLastTabText  = tabLabel;

            if (!string.IsNullOrEmpty(tabLabel) && _csTabs.Count > 0)
            {
                _log.Info($"[CS] Tab-Wechsel -> '{tabLabel}' [{tabIdx + 1}/{_csTabs.Count}]");
                _tolk.SpeakInterrupt(AccessibilityStrings.TabPosition(tabLabel, tabIdx + 1, _csTabs.Count));  
            }

            // Cache zurücksetzen: neue Texte des Tabs als Ersterscheinung behandeln
            _configSystemLastTexts.Clear();
            _csOptionFlags.Clear();
            ScanConfigSystemTexts(addon); // Cache befüllen, nichts ansagen
            LogConfigOptionFlags(addon);  // [CS-OPT] Ausgangszustand protokollieren
            return;
        }

        // Suche die aktuell fokussierte Einstellung
        // Wir iterieren rückwärts, da FFXIV UIs oft von hinten nach vorne geschichtet sind.
        string? focusedText = null;
        uint focusedId = 0;

        for (var i = addon->UldManager.NodeListCount - 1; i >= 0; i--)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;

            var ct = (int)comp->GetComponentType();
            // CheckBox(3), RadioButton(4), Slider(6), NumericInput(8), DropDownList(10)
            if (ct != 3 && ct != 4 && ct != 6 && ct != 8 && ct != 10) continue;

            var hasFocus = false;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                // Wir suchen das Focus-Bit (0x10) bei einem der Kinder (meistens das Collision-Node oder der Fokus-Rahmen)
                if (child != null && child->IsVisible() && ((ushort)child->NodeFlags & HasFocusBit) != 0)
                {
                    hasFocus = true;
                    break;
                }
            }

            if (hasFocus)
            {
                // Versuche den Text direkt aus dem Komponenten-Node zu lesen (z.B. Checkbox-Label)
                focusedText = GetTextFromNodeTree(n, 0);
                
                if (string.IsNullOrWhiteSpace(focusedText) || focusedText.Length < 3)
                {
                    // Für Slider und Dropdowns steht der beschreibende Text oft in einem separaten Text-Node kurz davor (höherer Index)
                    for (var k = i + 1; k < Math.Min(i + 15, addon->UldManager.NodeListCount); k++)
                    {
                        var sibling = addon->UldManager.NodeList[k];
                        if (sibling != null && sibling->Type == NodeType.Text && sibling->IsVisible())
                        {
                            focusedText = GetTextFromNodeTree(sibling, 0);
                            if (!string.IsNullOrWhiteSpace(focusedText)) break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(focusedText))
                {
                    focusedId = n->NodeId;
                    break; // Fokus gefunden
                }
            }
        }

        if (!string.IsNullOrEmpty(focusedText) && (focusedText != _lastFocused || focusedId != _lastFocusedNodeId))
        {
            _lastFocusedNodeId = focusedId;
            _lastFocused = focusedText;
            _log.Info($"[CS] Fokus-Wechsel erkannt -> '{focusedText}'");
            _tolk.SpeakInterrupt(focusedText);
        }

        // [CS-OPT] Flags-Änderungen an Option-Nodes erkennen (Diag2: klärt Fokus-Indikator)
        LogConfigOptionFlagChanges(addon);

        // Wert-Änderungen in Text-Nodes scannen und ansagen
        ScanConfigSystemTexts(addon);
    }
"@
$content = Get-Content -Path "FF14Accessibility\Services\UIReaderService.cs" -Raw
$content = $content.Replace($old, $new)
Set-Content -Path "FF14Accessibility\Services\UIReaderService.cs" -Value $content -NoNewline
