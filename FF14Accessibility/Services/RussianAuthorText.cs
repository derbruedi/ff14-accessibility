using System.Text;
using Lumina.Excel.Sheets;
using Lumina.Excel;

namespace FF14Accessibility.Services;

/// <summary>Speech-only translation of measured author additions; raw search keys stay intact.</summary>
internal static class RussianAuthorText
{
    private static readonly Lazy<RussianDescriptionCatalog?> Catalog = new(() =>
    {
        try { return RussianDescriptionCatalog.Load("RussianAuthorText"); }
        catch { return null; }
    });

    internal static string Translate(string sheet, uint id, ReadOnlySpan<byte> english, string fallback)
    {
        // Name/description fields can contain identical English bytes at
        // different offsets but different translated grammar. Bind the known
        // field using its original Lumina memory before considering byte aliases.
        var exact = sheet switch {
            "ENpcResidentName" => GameDisplayText.Name<ENpcResident>(id, x => x.Singular),
            "PetName" => GameDisplayText.Name<Pet>(id, x => x.Name),
            "XBMPetDescription" => GameDisplayText.Name<RawRow>(id, x => x.ReadStringColumn(8), "XBMPet"),
            "FateName" => GameDisplayText.Name<Fate>(id, x => x.Name),
            "AddonText" => GameDisplayText.Name<Addon>(id, x => x.Text), _ => null };
        if (exact != null) return exact;
        var actualSheet = sheet switch { "ENpcResidentName" => "ENpcResident", "PetName" => "Pet",
            "XBMPetDescription" => "XBMPet", "FateName" => "Fate", "AddonText" => "Addon", _ => sheet };
        if (GameDisplayText.Find(actualSheet, id, english) is { } displayed) return displayed.ExtractText();
        var translated = Catalog.Value?.Find(sheet, id, english, Loc.IsRussian);
        return translated == null ? fallback : Encoding.UTF8.GetString(translated);
    }

    internal static string PlaceName(uint id, string fallback)
        => GameDisplayText.Name<PlaceName>(id, x => x.Name)
            ?? (Loc.IsRussian && RussianPlaceNames.PlaceName(id) is { } russian ? russian : fallback);
}
