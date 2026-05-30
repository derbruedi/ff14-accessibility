using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FF14Accessibility.Native;
using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] private IDalamudPluginInterface PluginInterface { get; init; } = null!;
    [PluginService] private ICommandManager         CommandManager  { get; init; } = null!;
    [PluginService] private IClientState            ClientState     { get; init; } = null!;
    [PluginService] private IObjectTable            ObjectTable     { get; init; } = null!;
    [PluginService] private IChatGui                ChatGui         { get; init; } = null!;
    [PluginService] private IGameGui                GameGui         { get; init; } = null!;
    [PluginService] private IAddonLifecycle         AddonLifecycle  { get; init; } = null!;
    [PluginService] private IPluginLog              Log             { get; init; } = null!;
    [PluginService] private IKeyState               KeyState        { get; init; } = null!;
    [PluginService] private IFramework              Framework       { get; init; } = null!;
    [PluginService] private IGamepadState           GamepadState    { get; init; } = null!;

    private readonly Configuration      _config;
    private readonly TolkService        _tolk;
    private readonly NavigationService  _navigation;
    private readonly UIReaderService    _uiReader;
    private readonly ChatReaderService  _chatReader;
    private readonly CombatService      _combat;

    public Plugin()
    {
        _config     = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        TolkNative.Initialize(PluginInterface.AssemblyLocation.DirectoryName!);
        _tolk       = new TolkService(Log);
        _navigation = new NavigationService(ClientState, ObjectTable, _tolk, Log);
        _uiReader   = new UIReaderService(AddonLifecycle, GameGui, _tolk, Log);
        _chatReader = new ChatReaderService(ChatGui, _tolk, _config);
        _combat     = new CombatService(ObjectTable, _tolk, Log);

        RegisterCommands();
        Framework.Update += OnFrameworkUpdate;

        Log.Info("FF14 Accessibility Plugin V3.4 geladen.");
        _tolk.Speak("FF14 Accessibility Version 3 Punkt 4 bereit. Stabilitäts Fix aktiv.");
    }

    private void RegisterCommands()
    {
        // /acc nav  → Richtung zum Ziel
        // /acc set  → Aktuelles Spielziel verfolgen
        // /acc near → Objekte in der Nähe
        // /acc stop → Sprache stoppen
        CommandManager.AddHandler("/acc", new CommandInfo(OnCommand)
        {
            HelpMessage = "FF14 Accessibility: nav, set, near, stop, help"
        });
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();

        // "dump" nimmt einen optionalen Addon-Namen — muss vor dem switch geprüft werden
        if (trimmed.StartsWith("dump", StringComparison.OrdinalIgnoreCase))
        {
            var dumpArg = trimmed.Length > 4 ? trimmed[4..].Trim() : string.Empty;
            _uiReader.DumpAddon(dumpArg);
            return;
        }

        switch (trimmed.ToLower())
        {
            case "nav":
                _navigation.AnnounceDirection();
                break;
            case "set":
                _navigation.SetTargetFromGameTarget();
                break;
            case "clear":
                _navigation.ClearTarget();
                break;
            case "near":
                _navigation.AnnounceNearbyObjects(_config.NearbyDistance);
                break;
            case "stop":
                _tolk.Silence();
                break;
            case "status":
                _combat.AnnounceStatus();
                break;
            case "ui":
                _uiReader.ReadCurrentFocus();
                break;
            case "help":
                AnnounceHelp();
                break;
            default:
                _tolk.SpeakInterrupt("Unbekannter Befehl. Tippe /acc help für Hilfe.");
                break;
        }
    }

    private static readonly Dictionary<string, int> KeyNameToVK = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"]  = 0x70, ["F2"]  = 0x71, ["F3"]  = 0x72, ["F4"]  = 0x73,
        ["F5"]  = 0x74, ["F6"]  = 0x75, ["F7"]  = 0x76, ["F8"]  = 0x77,
        ["F9"]  = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        ["Up"]     = 0x26, ["Down"]   = 0x28,
        ["Left"]   = 0x25, ["Right"]  = 0x27,
        ["Return"] = 0x0D,
        // Nummernblock — TitleDCWorldMap Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        ["Numpad2"] = 0x62, ["Numpad4"] = 0x64,
        ["Numpad6"] = 0x66, ["Numpad8"] = 0x68,
    };

    private readonly bool[] _keyWasDown = new bool[256];

    private bool IsJustPressed(string keyName)
    {
        if (!KeyNameToVK.TryGetValue(keyName, out var vk)) return false;
        var down = KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)vk];
        var justPressed = down && !_keyWasDown[vk];
        _keyWasDown[vk] = down;
        return justPressed;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (IsJustPressed(_config.KeyNavigation))    _navigation.AnnounceDirection();
        if (IsJustPressed(_config.KeySetTarget))     _navigation.SetTargetFromGameTarget();
        if (IsJustPressed(_config.KeyClearTarget))   _navigation.ClearTarget();
        if (IsJustPressed(_config.KeyNearby))        _navigation.AnnounceNearbyObjects(_config.NearbyDistance);
        if (IsJustPressed(_config.KeyReadUI))        _uiReader.ReadCurrentFocus();
        if (IsJustPressed(_config.KeySilence))       _tolk.Silence();
        if (IsJustPressed(_config.KeyCombatStatus))  _combat.AnnounceStatus();
        // F5 — UI-Dump des aktuell aktiven Addons auf den Desktop schreiben
        // (kein Chat-Fenster nötig, funktioniert auch auf dem Titelbildschirm)
        if (IsJustPressed(_config.KeyDumpUI))        _uiReader.DumpFocusedAddon();

        _combat.Update();

        // DC-Auswahl: Nummernblock-Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        // Nummernblock-Tasten werden vom Spiel intern verarbeitet und feuern keine
        // AddonReceiveEvent-Hooks — deshalb hier abfangen und ForceDCMapRead() aufrufen.
        if (_uiReader.IsDCMapOpen)
        {
            var np2 = IsJustPressed("Numpad2");
            var np4 = IsJustPressed("Numpad4");
            var np6 = IsJustPressed("Numpad6");
            var np8 = IsJustPressed("Numpad8");
            if (np2 || np4 || np6 || np8)
                _uiReader.ForceDCMapRead();
        }

        // Menü-Navigation: nur wenn ein Menü aktiv ist
        if (_uiReader.HasActiveMenu)
        {
            if (IsJustPressed("Up"))     _uiReader.Navigate(-1, false);
            if (IsJustPressed("Down"))   _uiReader.Navigate(+1, false);
            if (IsJustPressed("Left"))   _uiReader.Navigate(-1, true);
            if (IsJustPressed("Right"))  _uiReader.Navigate(+1, true);
            // SelectYesno hat keine native Enter-Bindung — muss per FireCallback bestätigt werden
            if (IsJustPressed("Return")) _uiReader.ConfirmYesNo();
        }

        // Controller D-Pad Links/Rechts: SelectYesno Ja↔Nein
        if (GamepadState.Pressed(GamepadButtons.DpadLeft)  > 0) _uiReader.NavigateGamepad(-1);
        if (GamepadState.Pressed(GamepadButtons.DpadRight) > 0) _uiReader.NavigateGamepad(+1);
    }

    private void AnnounceHelp()
    {
        _tolk.SpeakInterrupt(
            "Befehle: " +
            "/acc nav, Richtung zum Ziel. " +
            "/acc set, Aktuelles Ziel verfolgen. " +
            "/acc clear, Ziel aufheben. " +
            "/acc near, Objekte in der Nähe. " +
            "/acc status, HP und MP ansagen. " +
            "/acc ui, Menü vorlesen. " +
            "/acc stop, Sprache stoppen."
        );
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler("/acc");
        _chatReader.Dispose();
        _uiReader.Dispose();
        _tolk.Dispose();
    }
}
