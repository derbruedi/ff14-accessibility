using System;

namespace FF14Accessibility.Services;

public readonly record struct ArmouryMenuRow(uint LabelId, bool Disabled, string Text, string EnglishText);

/// <summary>Decisions shared by the live transfer and tests using recorded game menus.</summary>
public static class ArmouryTransferRules
{
    public static ArmouryMenuActionResult SelectCommand(ReadOnlySpan<ArmouryMenuRow> rows,
        string gameLabel, bool russian, bool finishWaiting, out int index)
    {
        index = -1;
        if (rows.IsEmpty) return ArmouryMenuActionResult.NotReady;
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row.Disabled || !(row.LabelId == 1387 ||
                Matches(row.Text, gameLabel, russian) || Matches(row.EnglishText, gameLabel, russian))) continue;
            if (index >= 0)
            {
                index = -1;
                return ArmouryMenuActionResult.Unavailable;
            }
            index = i;
        }
        return index >= 0 ? ArmouryMenuActionResult.Requested :
            finishWaiting ? ArmouryMenuActionResult.Unavailable : ArmouryMenuActionResult.NotReady;
    }

    private static bool Matches(string text, string gameLabel, bool russian)
    {
        text = text.Trim();
        return (!string.IsNullOrWhiteSpace(gameLabel) && text.Equals(gameLabel.Trim(), StringComparison.OrdinalIgnoreCase)) ||
            text.Equals("Place in Armoury Chest", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("Place in Armory Chest", StringComparison.OrdinalIgnoreCase) ||
            (russian && (text.Equals("Поместить в оружейный сундук", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("Положить в арсенал", StringComparison.OrdinalIgnoreCase)));
    }

    public static bool IsConfirmed(uint expectedItem, uint? sourceItem, int before, int after, TimeSpan elapsed) =>
        expectedItem != 0 && sourceItem.HasValue && sourceItem.Value != expectedItem &&
        before >= 0 && after > before && elapsed >= TimeSpan.FromMilliseconds(750);
}
