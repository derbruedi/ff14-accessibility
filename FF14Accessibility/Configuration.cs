using Dalamud.Configuration;

namespace FF14Accessibility;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // Tastaturbelegung. Standard ab V4.21 kollisionsfrei laut Live-Keybind-Dump
    // (2026-07-10): N ist der einzige freie Buchstabe, Strg+F1..F12 sind frei.
    // Format: "Taste" oder "Strg+Umschalt+Taste" (Modifier: Strg, Umschalt, Alt).
    public string KeyHelp         = "Strg+F1";          // Kontextbezogene Hilfe
    public string KeyNextObject   = "N";                // Objekt-Browser: nächstes Objekt anvisieren
    public string KeyPrevObject   = "Umschalt+N";       // Objekt-Browser: vorheriges Objekt
    public string KeyCategory     = "Strg+N";           // Objekt-Browser: Kategorie vorwärts
    public string KeyCategoryPrev = "Strg+Umschalt+N";  // Objekt-Browser: Kategorie zurück (Umschalt = rückwärts, wie N/Umschalt+N; Strg+Alt+N war NVDA-Start-Hotkey!)
    public string KeyWalkGuide    = "Umschalt+Numpad3"; // Gehhilfe an/aus (neben Auto-Lauf Numpad3; Numpad3-Kombis laut Keybind-Dump frei)
    public string KeyAutoWalk     = "Numpad3";          // Auto-Lauf zum Ziel an/aus (braucht vnavmesh)
    public string KeyReadUI       = "Strg+F10";         // Aktuelles Menü vorlesen
    public string KeySilence      = "Strg+F11";         // Sprache stoppen
    public string KeyCombatStatus = "Strg+H";           // HP/MP ansagen (H=Health; bare H ist im Spiel MENU_CRAFT, Modifier+H laut Keybind-Dump frei)
    public string KeyDumpUI       = "Strg+F5";          // Node-Tree des aktuellen Addons auf Desktop speichern
    public string KeyWhereAmI     = "Strg+F2";          // Aktives Fenster ansagen + sichtbare Fenster ins Log
    public string KeyReadHotbar   = "Strg+F9";          // Aktionsleiste 1 vorlesen (was liegt auf Taste 1-0)
    public string KeyReadInventory = "Strg+F3";         // Inventar vorlesen (Tasche + Schlüsselgegenstände)
    public string KeyReadGil       = "Umschalt+F3";     // Nur den Gil-Stand ansagen (Umschalt+F1..F12 laut Keybind-Dump frei)
    public string KeyLevelExp      = "Strg+L";          // Stufe + fehlende EXP ansagen (L=Level; bare L ist im Spiel Linkshell)
    public string KeyEmoteNext     = "Umschalt+F5";     // Emote-Browser: nächstes Emote ansagen
    public string KeyEmotePrev     = "Umschalt+F4";     // Emote-Browser: vorheriges Emote ansagen
    public string KeyEmoteDo       = "Umschalt+F6";     // Gewähltes Emote ausführen
    public string KeyBestiary      = "Strg+F4";         // Bestiarium (Jagdtagebuch) komplett vorlesen (Strg+F4 laut Keybind-Dump frei)

    /// <summary>Resets all hotkeys to the current defaults (used by config migration).</summary>
    public void ResetKeysToDefaults()
    {
        var defaults = new Configuration();
        KeyHelp         = defaults.KeyHelp;
        KeyNextObject   = defaults.KeyNextObject;
        KeyPrevObject   = defaults.KeyPrevObject;
        KeyCategory     = defaults.KeyCategory;
        KeyCategoryPrev = defaults.KeyCategoryPrev;
        KeyWalkGuide    = defaults.KeyWalkGuide;
        KeyAutoWalk     = defaults.KeyAutoWalk;
        KeyReadUI       = defaults.KeyReadUI;
        KeySilence      = defaults.KeySilence;
        KeyCombatStatus = defaults.KeyCombatStatus;
        KeyDumpUI       = defaults.KeyDumpUI;
        KeyWhereAmI     = defaults.KeyWhereAmI;
        KeyReadHotbar   = defaults.KeyReadHotbar;
        KeyReadInventory = defaults.KeyReadInventory;
        KeyReadGil       = defaults.KeyReadGil;
        KeyLevelExp      = defaults.KeyLevelExp;
        KeyEmoteNext     = defaults.KeyEmoteNext;
        KeyEmotePrev     = defaults.KeyEmotePrev;
        KeyEmoteDo       = defaults.KeyEmoteDo;
        KeyBestiary      = defaults.KeyBestiary;
    }

    // Chat
    public bool ReadSayChat        = true;
    public bool ReadShoutChat      = true;
    public bool ReadPartyChat      = true;
    public bool ReadAllianceChat   = true;
    public bool ReadTellChat       = true;
    public bool ReadFCChat         = true;
    public bool ReadSystemMessages = true;
    public bool ReadCombatMessages = false;

    // Navigation
    public float NearbyDistance = 30f;
    public bool AnnounceTargetChanges = true;   // Zielwechsel (Tab/F1-F12) ansagen
    public float BeaconVolume = 0.35f;          // Gehhilfe-Ton: 0 = stumm, 1 = volle Lautstärke

    // Auto-Lauf: wie nah vnavmesh vor dem Ziel anhält (Meter)
    public float AutoWalkPlaceStopRange = 1.0f;      // Orte, Wegpunkte, Questziele: dicht dran
    public float AutoWalkTransitionStopRange = 0.5f; // Zonen-Übergänge: fast drauf, damit der Übergang auslöst

    // Kampf
    public bool AnnounceTargetHp = true;        // Ziel-HP in Stufen ansagen (im Kampf)
    public bool AnnounceEnemyCast = true;       // Ansage wenn das Ziel eine Aktion wirkt
    public bool EnableTargetTone = true;        // Kurzer Ton wenn ein Gegner anvisiert wird
    public float TargetToneVolume = 0.4f;       // Ziel-Ton: 0 = stumm, 1 = volle Lautstärke
}
