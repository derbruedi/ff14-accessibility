using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the running craft (the game's Synthesis window) - the piece a blind
/// player needs to make HQ materials, and the reason Einfg had nothing to be
/// tested against: since patch 6.0 HQ items come from crafting alone, and
/// crafting HQ means filling the quality bar with Touch actions while watching
/// the bars move.
///
/// The window is read through the named fields of FFXIVClientStructs'
/// <c>AddonSynthesis</c> (verified with a reflection probe of the referenced
/// DLL, 2026-09-12): CurrentQuality/MaxQuality, HQPercentage, CurrentProgress/
/// MaxProgress, CurrentDurability/StartingDurability, StepNumber, Condition,
/// ItemName and nine craft-effect slots with name and remaining steps. Nothing
/// is recomputed - every number is the one the game itself displays; in
/// particular the HQ chance is the game's own figure, never a formula of ours.
///
/// The user's dump of the window (msg 9367/9365, 2026-09-12) is the proof that
/// these are the numbers on the bars: quality 63 of 80 with 64% HQ, then a fresh
/// craft with 0 of 80, 1%, durability 40, step 1. Fields are used instead of the
/// node ids from that dump, because the typed fields cannot drift with the
/// window layout.
///
/// Announcement: the generic focus reader already speaks a window when the FOCUS
/// moves into it - during a craft the focus never moves (the hands are on the
/// hotbar), so the bars changed in silence. That was the real gap, and this
/// service closes it: it watches the read state and speaks one short line
/// whenever it changes.
/// </summary>
public sealed class SynthesisService
{
    /// <summary>The addon name as the dump of 2026-09-12 reports it ("DUMP:
    /// Synthesis", 101 nodes).</summary>
    private const string AddonName = "Synthesis";

    /// <summary>How long a changed state has to stand still before it is spoken.
    /// The bars are animated: the numbers can be written more than once while an
    /// action resolves, and this keeps one action from producing a burst of
    /// lines. Purely a display concern of ours - not a game rule; if her crafts
    /// show a line arriving late, this is the number to lower.</summary>
    private const int SettleMs = 350;

    private readonly IGameGui _gameGui;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    /// <summary>The state last spoken, as a signature string. A change against
    /// it is what earns the next line.</summary>
    private string _spoken = string.Empty;
    private string _pending = string.Empty;
    private long _pendingSince;

    /// <summary>The condition spoken last, so a change of it is audible while a
    /// repeated "Normal" is not repeated at every step.</summary>
    private string _lastCondition = string.Empty;

    /// <summary>False until the first line of this craft was spoken: the opening
    /// line is the full one (it names the item), the ones after it are short.</summary>
    private bool _spokeOnce;

    public SynthesisService(IGameGui gameGui, TolkService tolk, IPluginLog log)
    {
        _gameGui = gameGui;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>Everything the window shows about the running craft.</summary>
    private struct CraftState
    {
        public bool Read;
        public string Item;
        public string Condition;
        public string Quality, MaxQuality;
        public string HqPercent;
        public string Progress, MaxProgress;
        public string Durability, MaxDurability;
        public string Step;
        public List<string> Effects;

        /// <summary>Every readable number, in one string. Comparing it is how a
        /// change is detected - the individual values are what gets spoken.</summary>
        public string Signature =>
            $"{Quality}/{MaxQuality}|{HqPercent}|{Progress}/{MaxProgress}|"
            + $"{Durability}/{MaxDurability}|{Step}|{Condition}|{string.Join(",", Effects)}";

        /// <summary>The line spoken after every action.</summary>
        public string Short => AccessibilityStrings.SynthesisProgress(
            Quality, MaxQuality, HqPercent, Progress, MaxProgress, Durability, MaxDurability);

        /// <summary>The line spoken when the window opens and on demand: it also
        /// names the item, the step and the running effects.</summary>
        public string Full => AccessibilityStrings.SynthesisOpened(
            Item, Quality, MaxQuality, HqPercent, Progress, MaxProgress, Durability, MaxDurability,
            Step, string.Join(", ", Effects));
    }

    public unsafe bool IsWindowOpen()
    {
        var handle = _gameGui.GetAddonByName(AddonName);
        if (handle.IsNull) return false;
        return ((AddonSynthesis*)(nint)handle)->AtkUnitBase.IsVisible;
    }

    /// <summary>
    /// Per-frame watch. Speaks the opening line once per craft and a short line
    /// on every later change. Does nothing while the window is closed.
    /// </summary>
    public unsafe void Update()
    {
        var handle = _gameGui.GetAddonByName(AddonName);
        if (handle.IsNull || !((AddonSynthesis*)(nint)handle)->AtkUnitBase.IsVisible)
        {
            // Window gone: the next craft starts over with its own opening line.
            _spoken = string.Empty;
            _pending = string.Empty;
            _lastCondition = string.Empty;
            _spokeOnce = false;
            return;
        }

        var state = Read((AddonSynthesis*)(nint)handle);
        if (!state.Read) return;

        var signature = state.Signature;
        if (signature == _spoken) { _pending = string.Empty; return; }

        // Not spoken yet: the state has to stand still for SettleMs first, so the
        // animation of one action cannot produce several lines.
        if (signature != _pending)
        {
            _pending = signature;
            _pendingSince = Environment.TickCount64;
            return;
        }
        if (Environment.TickCount64 - _pendingSince < SettleMs) return;

        _spoken = signature;
        _pending = string.Empty;
        var line = _spokeOnce ? state.Short : state.Full;
        _spokeOnce = true;

        // The condition is spoken only when it CHANGES, not on every line: it is
        // the game's own word, and on Good/Excellent a quality action does more -
        // that is a decision the player makes, so the change has to be audible.
        // Comparing the previous value (instead of the word "Normal") keeps this
        // independent of the client language.
        if (state.Condition.Length > 0 && state.Condition != _lastCondition)
            line += " " + AccessibilityStrings.SynthesisCondition(state.Condition);
        _lastCondition = state.Condition;

        _log.Info($"[Synthese] {line}");
        _tolk.Speak(line);
    }

    /// <summary>
    /// The on-demand read (chat command). Speaks the full state of the open
    /// window; outside a craft it says so instead of staying silent, so the user
    /// can tell "no window" from "the mod did not answer".
    /// </summary>
    public unsafe string DescribeNow()
    {
        var handle = _gameGui.GetAddonByName(AddonName);
        if (handle.IsNull || !((AddonSynthesis*)(nint)handle)->AtkUnitBase.IsVisible)
            return AccessibilityStrings.SynthesisNoWindow;

        var state = Read((AddonSynthesis*)(nint)handle);
        if (!state.Read) return AccessibilityStrings.SynthesisNoWindow;
        return state.Full;
    }

    private static unsafe CraftState Read(AddonSynthesis* addon)
    {
        var state = new CraftState { Read = true };
        state.Item = AtkText.ReadClean(addon->ItemName).Trim();
        state.Condition = AtkText.ReadClean(addon->Condition).Trim();
        state.Quality = AtkText.ReadClean(addon->CurrentQuality).Trim();
        state.MaxQuality = AtkText.ReadClean(addon->MaxQuality).Trim();
        state.HqPercent = AtkText.ReadClean(addon->HQPercentage).Trim();
        state.Progress = AtkText.ReadClean(addon->CurrentProgress).Trim();
        state.MaxProgress = AtkText.ReadClean(addon->MaxProgress).Trim();
        state.Durability = AtkText.ReadClean(addon->CurrentDurability).Trim();
        state.MaxDurability = AtkText.ReadClean(addon->StartingDurability).Trim();
        state.Step = AtkText.ReadClean(addon->StepNumber).Trim();

        state.Effects = new List<string>();
        AddEffect(state.Effects, addon->CraftEffect1.Name, addon->CraftEffect1.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect2.Name, addon->CraftEffect2.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect3.Name, addon->CraftEffect3.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect4.Name, addon->CraftEffect4.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect5.Name, addon->CraftEffect5.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect6.Name, addon->CraftEffect6.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect7.Name, addon->CraftEffect7.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect8.Name, addon->CraftEffect8.StepsRemaining);
        AddEffect(state.Effects, addon->CraftEffect9.Name, addon->CraftEffect9.StepsRemaining);

        // A window that yields nothing readable is not spoken about: reporting a
        // craft with empty numbers would be worse than a moment of silence.
        var anyNumber = state.Quality.Length + state.MaxQuality.Length + state.Progress.Length
                      + state.MaxProgress.Length + state.Durability.Length + state.Step.Length > 0;
        state.Read = anyNumber;
        return state;
    }

    /// <summary>A craft effect slot: name plus the steps it still runs. Empty
    /// slots are skipped - the window keeps nine of them, most unused.</summary>
    private static unsafe void AddEffect(List<string> into, AtkTextNode* name, AtkTextNode* steps)
    {
        var label = AtkText.ReadClean(name).Trim();
        if (label.Length == 0) return;
        var rest = AtkText.ReadClean(steps).Trim();
        into.Add(rest.Length == 0 || rest == "0" ? label : $"{label} {rest}");
    }
}
