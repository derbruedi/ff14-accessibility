using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

internal static class BrowserTargetSelection
{
    internal readonly record struct InteractionRequest(bool Requested, bool TargetAccepted, ulong NativeResult);

    internal static bool IsWorldObject(IGameObject? obj) => obj?.ObjectKind
        is ObjectKind.EventObj or ObjectKind.Treasure or ObjectKind.GatheringPoint or ObjectKind.Aetheryte;

    internal static IGameObject? FindExact(IEnumerable<IGameObject> objects, ulong id)
        => id == 0 ? null : objects.FirstOrDefault(o => o.GameObjectId == id);

    internal static InteractionRequest ConfirmWorldObject(ITargetManager targets, IGameObject obj,
        Func<IGameObject, ulong> nativeConfirm)
        => ConfirmQuestOrWorldObject(targets, obj, false, nativeConfirm);

    internal static InteractionRequest ConfirmQuestOrWorldObject(ITargetManager targets, IGameObject obj,
        bool questSelected, Func<IGameObject, ulong> nativeConfirm)
    {
        if (!(IsWorldObject(obj) || questSelected && obj.ObjectKind == ObjectKind.EventNpc)
            || !obj.IsTargetable || obj.Address == 0) return default;
        var targetAccepted = Select(targets, obj);
        // SetHardTarget applies targeting filters. Its refusal does not mean
        // this live, targetable prop cannot be interacted with. Let the native
        // interaction check range, line of sight and event availability for the
        // exact object; never issue a generic Confirm against the old enemy.
        var nativeResult = nativeConfirm(obj);
        // A dispatched request is not proof of an opened UI or completed quest.
        // InteractWithObject's ulong return is kept only as diagnostic data.
        return new(true, targetAccepted, nativeResult);
    }

    internal static bool Select(ITargetManager targets, IGameObject obj)
    {
        // Keep the native game checks. An explicit browser choice replaces an
        // older temporary selection only after the hard target was accepted.
        targets.Target = obj;
        if (targets.Target?.GameObjectId != obj.GameObjectId) return false;
        if (targets.SoftTarget != null) targets.SoftTarget = null;
        return targets.Target?.GameObjectId == obj.GameObjectId;
    }

    internal static int NextIndex(int current, int direction, int count)
    {
        if (count <= 0) return -1;
        if (current < 0 || current >= count) return direction < 0 ? count - 1 : 0;
        return (int)(((long)current + direction % count + count) % count);
    }
}
