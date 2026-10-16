using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Opens each bag item's game context menu and selects the game's own Armoury
/// Chest action. The game chooses the destination slot. One action is outstanding
/// at a time, and the expected section must confirm the result before continuing.
/// </summary>
public sealed unsafe class ArmouryTransferService
{
    private static readonly InventoryType[] BagPages =
    {
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
    };

    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IGameGui _gameGui;
    private readonly UIReaderService _uiReader;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;
    private readonly Queue<Transfer> _remaining = new();

    private Transfer? _pending;
    private bool _menuOpening;
    private int _armouryCountBefore;
    private uint _ownerAddonId;
    private DateTime _requestedAt;
    private DateTime _nextRequestAt;
    private int _found;
    private int _moved;
    private int _skipped;
    private bool _active;

    private readonly record struct Transfer(InventoryType Source, ushort SourceSlot,
                                             InventoryType Destination, uint ItemId);

    public ArmouryTransferService(IDataManager data, IClientState clientState,
                                  IGameGui gameGui, UIReaderService uiReader,
                                  TolkService tolk, IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _gameGui = gameGui;
        _uiReader = uiReader;
        _tolk = tolk;
        _log = log;
    }

    public void Start()
    {
        if (_active)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkInProgress);
            return;
        }

        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnavailable);
            return;
        }

        try
        {
            _remaining.Clear();
            var manager = InventoryManager.Instance();
            if (manager == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnavailable);
                return;
            }

            var context = AgentInventoryContext.Instance();
            var contextPtr = _gameGui.GetAddonByName("ContextMenu");
            var menuVisible = !contextPtr.IsNull && ((AtkUnitBase*)(nint)contextPtr)->IsVisible;
            if (menuVisible &&
                (context == null || Array.IndexOf(BagPages, context->TargetInventoryId) < 0))
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnavailable);
                return;
            }
            _ownerAddonId = menuVisible && context != null && context->OwnerAddonId != 0
                ? context->OwnerAddonId
                : FindVisibleInventoryAddonId();
            if (_ownerAddonId == 0)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkOpenInventory);
                return;
            }
            CloseOwnContextMenu();

            var items = _data.GetExcelSheet<LuminaItem>();
            foreach (var page in BagPages)
            {
                var bag = manager->GetInventoryContainer(page);
                if (bag == null || !bag->IsLoaded || bag->Items == null || bag->Size is <= 0 or > 140)
                {
                    _remaining.Clear();
                    _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnavailable);
                    return;
                }

                for (ushort slot = 0; slot < bag->Size; slot++)
                {
                    var item = bag->Items[slot];
                    if (item.ItemId == 0 || !items.TryGetRow(item.ItemId, out var row)) continue;
                    var destination = DestinationFor(row);
                    if (destination == InventoryType.Invalid) continue;
                    _remaining.Enqueue(new Transfer(page, slot, destination, item.GetItemId()));
                }
            }

            _found = _remaining.Count;
            _moved = 0;
            _skipped = 0;
            _pending = null;
            _menuOpening = false;
            if (_found == 0)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkEmpty);
                return;
            }

            _active = true;
            _nextRequestAt = DateTime.UtcNow.AddMilliseconds(100);
            _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkStarted(_found));
            _log.Info($"[ArmouryBulk] Gestartet: {_found} Ausrüstungsteile im Inventar.");
            PluginFileLog.Write($"[ArmouryBulk] Начато: {_found} предметов.");
        }
        catch (Exception ex)
        {
            _remaining.Clear();
            _active = false;
            _log.Error($"[ArmouryBulk] Inventar konnte nicht gelesen werden: {ex}");
            _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnavailable);
        }
    }

    public void Update(bool allowRequests = true)
    {
        if (!_active) return;
        if (!_clientState.IsLoggedIn)
        {
            Reset();
            return;
        }

        try
        {
            // Keep supervision alive while another mode owns input. Cancel
            // instead of pausing a queue that could resume against stale UI.
            if (!allowRequests || FindVisibleInventoryAddonId(_ownerAddonId) == 0)
            {
                StopUnconfirmed();
                return;
            }
            var manager = InventoryManager.Instance();
            if (manager == null)
            {
                StopUnconfirmed();
                return;
            }

            var now = DateTime.UtcNow;
            if (_pending is { } pending)
            {
                if (_menuOpening)
                {
                    var action = _uiReader.TryPlaceContextItemInArmoury(
                        pending.Source, pending.SourceSlot, pending.ItemId, _ownerAddonId,
                        now - _requestedAt >= TimeSpan.FromMilliseconds(750));
                    if (action == ArmouryMenuActionResult.Requested)
                    {
                        _menuOpening = false;
                        _requestedAt = now;
                        return;
                    }
                    if (action == ArmouryMenuActionResult.Unavailable)
                    {
                        PluginFileLog.Write($"[ArmouryBulk] Пропущено: item={pending.ItemId}, игра не предложила перенос в сундук.");
                        CloseOwnContextMenu();
                        _skipped++;
                        _pending = null;
                        _nextRequestAt = now.AddMilliseconds(200);
                        return;
                    }
                    if (action == ArmouryMenuActionResult.WrongItem ||
                        now - _requestedAt > TimeSpan.FromSeconds(2))
                    {
                        PluginFileLog.Write($"[ArmouryBulk] Меню не совпало с item={pending.ItemId}: {action}.");
                        StopUnconfirmed();
                    }
                    return;
                }

                var source = manager->GetInventorySlot(pending.Source, pending.SourceSlot);
                var destinationCount = CountInArmourySection(manager, pending.Destination, pending.ItemId);
                // The first inventory update can be optimistic. Give the game
                // time to settle the action before reporting it as completed.
                if (ArmouryTransferRules.IsConfirmed(pending.ItemId,
                    source == null ? null : source->GetItemId(),
                    _armouryCountBefore, destinationCount, now - _requestedAt))
                {
                    _moved++;
                    _pending = null;
                    _nextRequestAt = now.AddMilliseconds(200);
                    _log.Info($"[ArmouryBulk] Bestätigt: {pending.ItemId} {pending.Source}/{pending.SourceSlot} -> {pending.Destination}.");
                    PluginFileLog.Write($"[ArmouryBulk] Подтверждено: item={pending.ItemId} из {pending.Source}/{pending.SourceSlot} в {pending.Destination}.");
                }
                else if (now - _requestedAt > TimeSpan.FromSeconds(3))
                {
                    StopUnconfirmed();
                    return;
                }
                else return;
            }

            if (now < _nextRequestAt) return;
            while (_remaining.TryDequeue(out var item))
            {
                var source = manager->GetInventorySlot(item.Source, item.SourceSlot);
                if (source == null || source->GetItemId() != item.ItemId)
                {
                    _skipped++;
                    continue;
                }

                var countBefore = CountInArmourySection(manager, item.Destination, item.ItemId);
                if (countBefore < 0)
                {
                    StopUnconfirmed();
                    return;
                }

                _pending = item;
                _armouryCountBefore = countBefore;
                _menuOpening = true;
                _requestedAt = now;
                var context = AgentInventoryContext.Instance();
                if (context == null)
                {
                    StopUnconfirmed();
                    return;
                }
                context->OpenForItemSlot(item.Source, item.SourceSlot, 0, _ownerAddonId);
                _log.Info($"[ArmouryBulk] Kontextmenü angefordert: {item.ItemId} {item.Source}/{item.SourceSlot}, erwartete Kategorie {item.Destination}.");
                PluginFileLog.Write($"[ArmouryBulk] Меню: item={item.ItemId} из {item.Source}/{item.SourceSlot}, ожидается {item.Destination}.");
                return;
            }

            Finish();
        }
        catch (Exception ex)
        {
            _log.Error($"[ArmouryBulk] Transfer fehlgeschlagen: {ex}");
            PluginFileLog.Write($"[ArmouryBulk] Ошибка переноса: {ex.Message}");
            StopUnconfirmed();
        }
    }

    private void Finish()
    {
        _log.Info($"[ArmouryBulk] Fertig: gefunden={_found}, verschoben={_moved}, übersprungen={_skipped}.");
        PluginFileLog.Write($"[ArmouryBulk] Готово: найдено={_found}, перенесено={_moved}, пропущено={_skipped}.");
        _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkDone(_moved, _skipped));
        Reset();
    }

    private void StopUnconfirmed()
    {
        _log.Warning($"[ArmouryBulk] Abgebrochen: gefunden={_found}, bestätigt={_moved}, übersprungen={_skipped}, offen={_pending?.ItemId}.");
        PluginFileLog.Write($"[ArmouryBulk] Остановлено: найдено={_found}, подтверждено={_moved}, пропущено={_skipped}, текущий={_pending?.ItemId}.");
        if (_menuOpening && _pending != null) CloseOwnContextMenu();
        _tolk.SpeakInterrupt(AccessibilityStrings.ArmouryBulkUnconfirmed(_moved));
        Reset();
    }

    private void Reset()
    {
        _active = false;
        _remaining.Clear();
        _pending = null;
        _menuOpening = false;
    }

    private static int CountInArmourySection(InventoryManager* manager, InventoryType section, uint itemId)
    {
        var chest = manager->GetInventoryContainer(section);
        if (chest == null || !chest->IsLoaded || chest->Items == null || chest->Size is <= 0 or > 140)
            return -1;
        var count = 0;
        for (var i = 0; i < chest->Size; i++)
            if (chest->Items[i].GetItemId() == itemId) count++;
        return count;
    }

    private void CloseOwnContextMenu()
    {
        var ptr = _gameGui.GetAddonByName("ContextMenu");
        if (ptr.IsNull) return;
        var addon = (AtkUnitBase*)(nint)ptr;
        var agent = AgentInventoryContext.Instance();
        if (addon == null || !addon->IsVisible || agent == null) return;
        if (agent->OwnerAddonId != _ownerAddonId) return;
        if (_pending is { } item)
        {
            if (agent->TargetInventoryId != item.Source ||
                agent->TargetInventorySlotId != item.SourceSlot) return;
        }
        else if (Array.IndexOf(BagPages, agent->TargetInventoryId) < 0) return;
        agent->AgentInterface.Hide();
        addon->Hide(false, true, 0);
    }

    private uint FindVisibleInventoryAddonId(uint requiredOwner = 0)
    {
        foreach (var name in new[] { "Inventory", "InventoryLarge", "InventoryExpansion" })
        {
            var ptr = _gameGui.GetAddonByName(name);
            if (ptr.IsNull) continue;
            var addon = (AtkUnitBase*)(nint)ptr;
            if (addon != null && addon->IsVisible && (requiredOwner == 0 || addon->Id == requiredOwner))
                return addon->Id;
        }
        return 0;
    }

    private static InventoryType DestinationFor(LuminaItem row)
    {
        if (row.EquipSlotCategory.ValueNullable is not { } category) return InventoryType.Invalid;
        if (category.MainHand > 0) return InventoryType.ArmoryMainHand;
        if (category.OffHand > 0) return InventoryType.ArmoryOffHand;
        if (category.Body > 0) return InventoryType.ArmoryBody;
        if (category.Head > 0) return InventoryType.ArmoryHead;
        if (category.Gloves > 0) return InventoryType.ArmoryHands;
        if (category.Legs > 0) return InventoryType.ArmoryLegs;
        if (category.Feet > 0) return InventoryType.ArmoryFeets;
        if (category.Ears > 0) return InventoryType.ArmoryEar;
        if (category.Neck > 0) return InventoryType.ArmoryNeck;
        if (category.Wrists > 0) return InventoryType.ArmoryWrist;
        if (category.FingerL > 0 || category.FingerR > 0) return InventoryType.ArmoryRings;
        if (category.SoulCrystal > 0) return InventoryType.ArmorySoulCrystal;
        return InventoryType.Invalid;
    }
}
