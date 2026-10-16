using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Alternate rows of the exact same physical map, not different
/// floors. Territory, resource ID, scale and offsets must all agree.</summary>
internal sealed class TravelMapIdentity(IDataManager data)
{
    private Dictionary<uint, uint>? _canonical;
    internal uint Canonical(uint id)
    {
        _canonical ??= data.GetExcelSheet<Map>().Where(m => m.RowId != 0 && m.TerritoryType.RowId != 0
                && !string.IsNullOrWhiteSpace(m.Id.ExtractText()))
            .GroupBy(m => (m.TerritoryType.RowId, Resource: m.Id.ExtractText(), m.SizeFactor, m.OffsetX, m.OffsetY))
            .SelectMany(g => g.Select(m => (m.RowId, Canonical: g.Min(row => row.RowId))))
            .ToDictionary(p => p.RowId, p => p.Canonical);
        return _canonical.GetValueOrDefault(id, id);
    }
}
