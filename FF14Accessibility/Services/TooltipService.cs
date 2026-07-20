using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Live map of node pointer to tooltip text, kept in step with the game.
///
/// WHY THIS EXISTS: icon buttons carry no text node at all - only Collision and
/// Image (dump 2026-07-20: all nine buttons in Character). Three earlier routes
/// were ruled out with data: text nodes (none exist), the visible tooltip (the
/// game opens that WINDOW on mouse hover only, never on keyboard focus - all 20
/// silent cases logged tooltip=[]), and event parameters (the same fixed series
/// 256..260 appears on every button in every window, so sheet lookups returned
/// identical nonsense regardless of which button was focused).
///
/// What those attempts missed: AttachTooltip and ShowTooltip are separate. The
/// window is one thing, the BINDING another - and the binding is created while
/// the addon is built. Confirmed by probe (log 2026-07-20 09:31, 241 calls):
/// opening Character alone produced every label in the user's language, from
/// "Ausruestung optimieren" to "Aktualisieren", plus all 22 attribute
/// descriptions in the stats window.
///
/// WHY A LIVE MAP AND NOT A TABLE: the pointers change on every rebuild of a
/// window - the probe log holds two different sets for the same nine buttons.
/// A fixed pointer-to-name table would serve wrong labels after the first
/// reopen. Detach is therefore hooked as well, so a released pointer leaves the
/// map at once and a recycled one can never inherit its predecessor's name -
/// which for a blind player is the difference between silence and being sent to
/// the wrong window.
///
/// Read-only towards the game: every detour records and hands over to the
/// original. No synthetic mouse events, no tooltip forced open.
/// </summary>
public sealed unsafe class TooltipService : IDisposable
{
    private delegate void AttachTooltipDelegate(
        AtkTooltipManager* self, AtkTooltipType type, ushort parentId,
        AtkResNode* targetNode, AtkTooltipManager.AtkTooltipArgs* args);

    private delegate void DetachTooltipDelegate(AtkTooltipManager* self, AtkResNode* targetNode);

    private delegate void DetachByAddonDelegate(AtkTooltipManager* self, ushort addonId, bool removeEvents);

    private readonly IPluginLog _log;
    private readonly Hook<AttachTooltipDelegate>? _attachHook;
    private readonly Hook<DetachTooltipDelegate>? _detachHook;
    private readonly Hook<DetachByAddonDelegate>? _detachByAddonHook;

    /// <summary>Node pointer to label. Also keyed by addon id so a closing window clears in one go.</summary>
    private readonly Dictionary<nint, string> _byNode   = new();
    private readonly Dictionary<nint, ushort> _addonOf  = new();

    /// <summary>True when the hooks are installed and the map is being maintained.</summary>
    public bool IsActive => _attachHook != null;

    /// <summary>Installs the hooks. A failure disables tooltip lookup only - the plugin keeps running.</summary>
    public TooltipService(IGameInteropProvider interop, IPluginLog log)
    {
        _log = log;

        // Hooking reaches into game memory: if a signature ever stops matching,
        // this must not take the whole plugin down with it.
        try
        {
            _attachHook = interop.HookFromAddress<AttachTooltipDelegate>(
                AtkTooltipManager.Addresses.AttachTooltip.Value, OnAttach);
            _detachHook = interop.HookFromAddress<DetachTooltipDelegate>(
                AtkTooltipManager.Addresses.DetachTooltip.Value, OnDetach);
            _detachByAddonHook = interop.HookFromAddress<DetachByAddonDelegate>(
                AtkTooltipManager.Addresses.DetachTooltipByAddonId.Value, OnDetachByAddon);

            _attachHook.Enable();
            _detachHook.Enable();
            _detachByAddonHook.Enable();
            _log.Info("[Tooltip] Hooks aktiv (Attach/Detach/DetachByAddon).");
        }
        catch (Exception ex)
        {
            _log.Error($"[Tooltip] Hooks fehlgeschlagen, Tooltip-Namen nicht verfuegbar: {ex}");
        }
    }

    /// <summary>
    /// Label the game bound to this node, or null. Callers must treat null as
    /// "nothing known" and stay silent rather than substitute a guess.
    /// </summary>
    public string? TryGetTooltip(AtkResNode* node)
    {
        if (node == null) return null;
        return _byNode.TryGetValue((nint)node, out var text) && text.Length > 0 ? text : null;
    }

    /// <summary>
    /// Like <see cref="TryGetTooltip(AtkResNode*)"/>, but also checks the node's
    /// parents. The keyboard focus often sits on a component's Collision child
    /// while the tooltip hangs on the component itself - the probe showed both
    /// shapes in the same window.
    /// </summary>
    public string? TryGetTooltipDeep(AtkResNode* node, int maxDepth = 3)
    {
        for (var i = 0; node != null && i <= maxDepth; i++, node = node->ParentNode)
        {
            var text = TryGetTooltip(node);
            if (text != null) return text;
        }
        return null;
    }

    private void OnAttach(
        AtkTooltipManager* self, AtkTooltipType type, ushort parentId,
        AtkResNode* targetNode, AtkTooltipManager.AtkTooltipArgs* args)
    {
        try
        {
            // AtkTooltipArgs is a union - every variant sits at offset 0. Reading
            // TextArgs on an Item or Action tooltip would reinterpret an id as a
            // pointer, so the text is only touched when the Text flag is set.
            if (targetNode != null && (type & AtkTooltipType.Text) != 0)
            {
                var ptr = args->TextArgs.Text;
                if (ptr.HasValue)
                {
                    var text = ptr.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _byNode[(nint)targetNode]  = text;
                        _addonOf[(nint)targetNode] = parentId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"[Tooltip] Attach-Lesefehler: {ex.Message}");
        }

        _attachHook!.Original(self, type, parentId, targetNode, args);
    }

    private void OnDetach(AtkTooltipManager* self, AtkResNode* targetNode)
    {
        if (targetNode != null)
        {
            _byNode.Remove((nint)targetNode);
            _addonOf.Remove((nint)targetNode);
        }

        _detachHook!.Original(self, targetNode);
    }

    private void OnDetachByAddon(AtkTooltipManager* self, ushort addonId, bool removeEvents)
    {
        // A closing window releases all its nodes at once. Without this the map
        // would keep stale pointers, and the memory behind them gets reused.
        var stale = new List<nint>();
        foreach (var pair in _addonOf)
            if (pair.Value == addonId) stale.Add(pair.Key);

        foreach (var key in stale)
        {
            _byNode.Remove(key);
            _addonOf.Remove(key);
        }

        _detachByAddonHook!.Original(self, addonId, removeEvents);
    }

    /// <summary>Removes the hooks and drops the map.</summary>
    public void Dispose()
    {
        _attachHook?.Disable();
        _attachHook?.Dispose();
        _detachHook?.Disable();
        _detachHook?.Dispose();
        _detachByAddonHook?.Disable();
        _detachByAddonHook?.Dispose();
        _byNode.Clear();
        _addonOf.Clear();
    }
}
