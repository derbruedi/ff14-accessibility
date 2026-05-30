using Dalamud.Configuration;

namespace FF14Accessibility;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Tastaturbelegung
    public string KeyNavigation   = "F6";   // Richtung zum Ziel ansagen
    public string KeySetTarget    = "F7";   // Aktuelles Spielziel verfolgen
    public string KeyClearTarget  = "F8";   // Zielverfolgung beenden
    public string KeyNearby       = "F9";   // Objekte in der Nähe auflisten
    public string KeyReadUI       = "F10";  // Aktuelles Menü vorlesen
    public string KeySilence      = "F11";  // Sprache stoppen
    public string KeyCombatStatus = "F12";  // HP/MP-Status ansagen
    public string KeyDumpUI       = "F5";   // Node-Tree des aktuellen Addons auf Desktop speichern

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
}
