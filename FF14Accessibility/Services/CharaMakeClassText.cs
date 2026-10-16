using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Resolve both words from the same preview weapon, never the shared help pane.</summary>
internal static class CharaMakeClassText
{
    internal const ulong ModelMask = 0x0000_FFFF_FFFF_FFFFul;
    internal readonly record struct Entry(uint ClassId, string Name, string Description);
    internal readonly record struct Equipment(ulong Weapon, uint ClassId);
    internal static readonly uint[] StartingClasses = [1, 2, 3, 4, 5, 6, 7, 26];

    internal static uint ResolveName(string label, IEnumerable<(uint Id, string Name)> names)
    {
        label = TolkService.Sanitize(label).Trim();
        uint found = 0;
        foreach (var (id, name) in names)
        {
            if (LobbyRow(id) == 0 || !HeadingMatches(label, name)) continue;
            if (found != 0 && found != id) return 0;
            found = id;
        }
        return found;
    }

    internal static uint ResolveName(IDataManager data, string label)
        => ResolveName(label, ClassNames(data));

    private static IEnumerable<(uint Id, string Name)> ClassNames(IDataManager data)
    {
        foreach (var id in StartingClasses)
        {
            if (!data.GetExcelSheet<ClassJob>().TryGetRow(id, out var job)) continue;
            yield return (id, job.Name.ExtractText());
            yield return (id, RussianGameText.Name(data, job, row => row.Name));
            if (Loc.IsRussian)
                foreach (var alias in AccessibilityStrings.CharaMakeClassAliases(id)) yield return (id, alias);
        }
    }

    // Checked against installed CharaMakeClassEquip, ClassJob and Lobby rows.
    // Lobby.Unknown1 is the description column; Text is the class heading.
    internal static uint LobbyRow(uint classId) => classId switch
    {
        1 => 178, 2 => 180, 3 => 182, 4 => 184,
        5 => 186, 6 => 188, 7 => 190, 26 => 192,
        _ => 0,
    };

    internal static uint ResolveClass(ulong weapon, IEnumerable<Equipment> equipment)
    {
        weapon &= ModelMask;
        if (weapon == 0) return 0;
        uint found = 0;
        foreach (var row in equipment)
        {
            if ((row.Weapon & ModelMask) != weapon) continue;
            if (row.ClassId == 0 || (found != 0 && found != row.ClassId)) return 0;
            found = row.ClassId;
        }
        return found;
    }

    internal static Entry? Read(IDataManager data, ulong weapon)
    {
        var id = ResolveClass(weapon, PreviewWeapons(data));
        return ReadById(data, id);
    }

    internal static Entry? ReadById(IDataManager data, uint id)
    {
        if (LobbyRow(id) == 0 || !data.GetExcelSheet<ClassJob>().TryGetRow(id, out var job)) return null;
        var name = RussianGameText.Name(data, job, row => row.Name).Trim();
        if (name.Length == 0) return null;
        name = char.ToUpperInvariant(name[0]) + name[1..];
        var description = string.Empty;
        var lobbyId = LobbyRow(id);
        // A changed game table must not silently pair a class with somebody else's text.
        if (lobbyId != 0
            && data.GetExcelSheet<ClassJob>(ClientLanguage.English).TryGetRow(id, out var englishJob)
            && data.GetExcelSheet<Lobby>(ClientLanguage.English).TryGetRow(lobbyId, out var englishLobby)
            && HeadingMatches(englishJob.Name.ExtractText(), englishLobby.Text.ExtractText())
            && data.GetExcelSheet<Lobby>().TryGetRow(lobbyId, out var lobby))
            description = GameDisplayText.Find(data, lobby, x => x.Unknown1) is { } displayed
                ? displayed.ExtractText().Trim()
                : AccessibilityStrings.CharaMakeClassDescription(id,
                    englishLobby.Unknown1.ExtractText().Trim(), lobby.Unknown1.ExtractText().Trim());
        return new Entry(id, name, description);
    }

    private static IEnumerable<Equipment> PreviewWeapons(IDataManager data)
    {
        foreach (var row in data.GetExcelSheet<CharaMakeClassEquip>())
        {
            if (LobbyRow(row.Class.RowId) == 0) continue;
            yield return new Equipment(row.Weapon, row.Class.RowId);
            // The live preview also wears the class's actual starting item.
            // Its full model differs from the showcase gear in CharaMakeClassEquip.
            if (data.GetExcelSheet<ClassJob>().TryGetRow(row.Class.RowId, out var job)
                && data.GetExcelSheet<Item>().TryGetRow(job.ItemStartingWeaponMainHand.RowId, out var item))
                yield return new Equipment(item.ModelMain, row.Class.RowId);
        }
    }

    internal static bool HeadingMatches(string job, string lobby)
        => !string.IsNullOrWhiteSpace(job)
           && string.Equals(job.Trim(), lobby.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>The selected class owns the delayed description and its cancellation.</summary>
internal sealed class CharaMakeClassSpeech
{
    private CharaMakeClassText.Entry? _selection;
    private long _changedAt;
    private bool _described;

    internal (string Headline, string Description) Update(CharaMakeClassText.Entry? selection, long now,
        bool repeat = false)
    {
        if (selection == null)
        {
            _selection = null;
            _described = false;
            return (string.Empty, string.Empty);
        }
        var changed = selection != _selection;
        if (changed)
        {
            _selection = selection;
            _changedAt = now;
            _described = false;
        }
        var value = selection.Value;
        if (repeat)
        {
            _described = true;
            return (value.Name, value.Description);
        }
        if (changed) return (value.Name, string.Empty);
        if (_described || now - _changedAt < 350) return (string.Empty, string.Empty);
        _described = true;
        return (string.Empty, value.Description);
    }
}
