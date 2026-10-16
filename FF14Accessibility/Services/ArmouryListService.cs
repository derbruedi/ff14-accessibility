using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>Owns browsing keys while the Armoury Chest is focused.
/// Context commands and their confirmations remain native game interactions.</summary>
public sealed class ArmouryListService
{
    internal static readonly int[] Keys = [0x68, 0x26, 0x62, 0x28, 0x65, 0x0C, 0x66, 0x27,
        0x60, 0x64, 0x25, 0x6E, 0x2E, 0x67, 0x24, 0x69, 0x21, 0x23];
    private readonly IArmouryListAccess _access;
    private readonly Func<bool> _loggedIn;
    private readonly Func<uint> _territoryId;
    private readonly Func<uint, string> _description;
    private readonly Action<string> _speak;
    private readonly Func<DateTime> _clock;
    private readonly IKeyState _keys;
    private readonly IPluginLog _log;
    private readonly MenuInput _input;
    private readonly ArmouryListModel _model = new();
    private ArmouryListWindow _owner;
    private uint _territory;
    private DateTime _refreshAt;
    private DateTime _contextDeadline;
    private ArmouryListItem? _requested;
    private bool _sawContext;
    private bool _ready;
    private bool _announced;
    private bool _wasBrowsing;
    private bool _confirmHeld;
    private bool _waiting;
    private bool _selectionInvalidated;

    public ArmouryListService(IGameGui gui, IClientState client, IKeyState keys,
        InventoryService inventory, GearInfoService gear, TolkService tolk, IPluginLog log)
        : this(new ArmouryListAccess(gui, inventory, gear), keys, log, () => client.IsLoggedIn,
            () => client.TerritoryType, id => string.Join(". ", new[] { gear.DescribeItemBasics(id),
                gear.DescribeOwnClasses(id), inventory.ResolveItemDescription(id) }
                .Where(text => !string.IsNullOrWhiteSpace(text))),
            text => tolk.SpeakInterrupt(text), () => DateTime.UtcNow) { }

    internal ArmouryListService(IArmouryListAccess access, IKeyState keys, IPluginLog log,
        Func<bool> loggedIn, Func<uint> territory, Func<uint, string> description,
        Action<string> speak, Func<DateTime> clock)
    {
        _access = access; _keys = keys; _log = log; _loggedIn = loggedIn; _territoryId = territory;
        _description = description; _speak = speak; _clock = clock;
        _input = new MenuInput(keys, log, Keys);
    }

    public bool IsOpen => _owner.Address != 0;
    public bool IsBrowsing { get; private set; }
    public bool OwnsInput => IsBrowsing || _waiting;

    public void Update(bool active, bool typing, bool otherMenu, string? focusedAddon)
    {
        _input.Poll();
        // The opening Num0 belongs to us through key-up, including the next
        // native dialog. Holding it must never choose the first item command.
        if (_confirmHeld)
        {
            if (!_input.Down(0x60)) _confirmHeld = false;
            else _keys[0x60] = false;
        }
        var board = _access.Window;
        if (!_loggedIn() || board.Address == 0) { Reset(); return; }
        if (_owner != board || _territory != _territoryId())
        {
            Reset(); _owner = board; _territory = _territoryId();
        }
        var now = _clock();
        var context = _access.ContextVisible;
        var failure = false;
        if (_requested != null)
        {
            if (!context && _sawContext) { _requested = null; _sawContext = false; _refreshAt = default; }
            else if (!_access.ContextMatches(_owner, _requested))
            {
                _access.CloseContext(_owner, _requested); _requested = null; _refreshAt = default; failure = true;
            }
            else if (context) _sawContext = true;
            else if (now >= _contextDeadline) { _requested = null; _refreshAt = default; failure = true; }
        }
        var available = active && !typing && !otherMenu && focusedAddon == "ArmouryBoard";
        IsBrowsing = available && _requested == null && !context;
        _waiting = available && _requested != null && !context;
        var changed = false;
        _selectionInvalidated = false;
        if (now >= _refreshAt || (IsBrowsing && _input.JustAny(Keys)))
        {
            _refreshAt = now.AddMilliseconds(250);
            var previous = _model.Selected;
            var wasReady = _ready;
            if (_access.TryCollect(out var items)) { changed = _model.Replace(items); _ready = true; }
            else _ready = false;
            // A temporary sort/load must not erase the physical selection or
            // silently send the next Num0 to the first item after recovery.
            changed |= wasReady != _ready;
            _selectionInvalidated = previous != null && !ArmouryListModel.CanAct(previous, _model.Selected);
        }
        if (IsBrowsing && (!_wasBrowsing || !_announced || changed))
        {
            if (!_announced && _ready)
            {
                _announced = true;
                _speak(AccessibilityStrings.ArmouryListTitle(_model.Items.Count) + ". "
                    + SelectionText());
            }
            else if (!_wasBrowsing || changed) ReadSelected();
        }
        if (failure && active) _speak(AccessibilityStrings.ArmouryListMenuFailed);
        _wasBrowsing = IsBrowsing;
    }

    public bool HandleKeys(bool modifiers)
    {
        if (!OwnsInput || modifiers) return false;
        _input.ConsumeAll();
        if (_waiting) return true;
        if (_input.JustAny(0x6E, 0x2E))
        {
            _access.Close(_owner); Reset(); _speak(AccessibilityStrings.MenuClosed);
        }
        else if (!_ready) { if (_input.JustAny(Keys)) ReadSelected(); }
        else if (_input.JustAny(0x68, 0x26)) { _model.MoveRow(-1); ReadSelected(); }
        else if (_input.JustAny(0x62, 0x28)) { _model.MoveRow(1); ReadSelected(); }
        else if (_input.JustAny(0x64, 0x25)) { _model.Move(-1); ReadSelected(); }
        else if (_input.JustAny(0x66, 0x27)) { _model.Move(1); ReadSelected(); }
        else if (_input.JustAny(0x67, 0x24)) SwitchCategory(-1);
        else if (_input.JustAny(0x69, 0x21)) SwitchCategory(1);
        else if (_input.Just(0x23)) { _model.End(true); ReadSelected(); }
        else if (_input.JustAny(0x65, 0x0C)) ReadSelected(description: true);
        else if (_input.Just(0x60)) { _confirmHeld = true; OpenSelectedMenu(); }
        return true;
    }

    public void ReadSelected(bool description = false)
    {
        if (description)
        {
            // A manual read can happen between periodic refreshes. Read the
            // live item again rather than describing the row that disappeared.
            if (_access.TryCollect(out var items)) { _model.Replace(items); _ready = true; }
            else _ready = false;
        }
        var text = SelectionText();
        if (description && _ready && _model.Selected is { } item)
        {
            var detail = _description(item.BaseItemId);
            if (!string.IsNullOrWhiteSpace(detail)) text += ". " + detail;
            if (!string.IsNullOrWhiteSpace(item.Detail)) text += ". " + item.Detail;
            _log.Info($"[ArmouryList] Description: {item.Container}/{item.Slot}, item={item.ItemId}, base={item.BaseItemId}, HQ={item.HighQuality}; {text}");
        }
        _speak(text);
    }

    private string SelectionText() => !_ready ? AccessibilityStrings.ArmouryListUpdating
        : _model.Selected is not { } item ? AccessibilityStrings.MenuEmpty
        : AccessibilityStrings.MenuEntry(item.Label, _model.Cursor + 1, _model.Items.Count);

    private void SwitchCategory(int direction)
    {
        _model.MoveCategory(direction);
        var text = SelectionText();
        if (_model.Selected is { } item)
            text = AccessibilityStrings.ArmouryListCategory(ArmouryListModel.CategoryName(item.Container), text);
        _speak(text);
    }

    private void OpenSelectedMenu()
    {
        if (_selectionInvalidated) { _speak(AccessibilityStrings.ArmouryListItemChanged); return; }
        if (!_ready || _model.Selected is not { } expected) { ReadSelected(); return; }
        if (!_access.OpenContext(_owner, expected))
        {
            _access.CloseContext(_owner, expected);
            _refreshAt = default; _speak(AccessibilityStrings.ArmouryListItemChanged); return;
        }
        _requested = expected; _sawContext = false; _contextDeadline = _clock().AddSeconds(2);
        _log.Info($"[ArmouryList] Native item menu requested: {expected.Container}/{expected.Slot}, item={expected.ItemId}.");
    }

    private void Reset()
    {
        if (_requested != null) _access.CloseContext(_owner, _requested);
        _owner = default; _model.Clear(); _requested = null; _sawContext = false;
        _ready = false; _announced = false; _wasBrowsing = false; _waiting = false;
        IsBrowsing = false; _refreshAt = default;
    }
}
