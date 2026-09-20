namespace FF14Accessibility.Services;

/// <summary>Maps cross-hotbar module IDs 10..17 to spoken sets 1..8.</summary>
internal sealed class CrossHotbarChangeTracker
{
    private int? previous;

    internal int? Update(int? moduleId)
    {
        if (moduleId is not (>= 10 and <= 17))
        {
            previous = null;
            return null;
        }
        var changed = previous.HasValue && previous != moduleId;
        previous = moduleId;
        return changed ? moduleId - 9 : null;
    }
}
