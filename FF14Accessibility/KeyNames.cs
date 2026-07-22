using System.Collections.Generic;

namespace FF14Accessibility;

/// <summary>
/// Maps between the key names used in <see cref="Configuration"/> ("Strg+F5")
/// and Windows virtual key codes, plus spoken names for the settings menu.
///
/// The names of keys that already appear in saved configurations are FIXED -
/// renaming "Up" to "Pfeil hoch" here would silently invalidate every existing
/// binding, because <see cref="Plugin.ParseKeySpec"/> looks the config string up
/// in this table. Spoken names live in a separate table for that reason.
///
/// Deliberately absent: the German umlaut keys. docs/game-api.md lists VK
/// 136-140 as umlauts, but that mapping was DERIVED from a manual comparison,
/// not verified - binding a hotkey to a guessed VK would produce a key that
/// silently never fires.
/// </summary>
public static class KeyNames
{
    /// <summary>Config key name to virtual key code.</summary>
    public static readonly Dictionary<string, int> NameToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"]  = 0x70, ["F2"]  = 0x71, ["F3"]  = 0x72, ["F4"]  = 0x73,
        ["F5"]  = 0x74, ["F6"]  = 0x75, ["F7"]  = 0x76, ["F8"]  = 0x77,
        ["F9"]  = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,

        ["Escape"] = 0x1B, ["Return"] = 0x0D, ["Tab"] = 0x09,
        ["Leertaste"] = 0x20, ["Rücktaste"] = 0x08,
        ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,

        ["Einfg"] = 0x2D, ["Entf"] = 0x2E,
        ["Pos1"] = 0x24, ["Ende"] = 0x23,
        ["BildAuf"] = 0x21, ["BildAb"] = 0x22,

        [","] = 0xBC, ["."] = 0xBE, ["-"] = 0xBD, ["+"] = 0xBB,
    };

    /// <summary>Spoken form for the settings menu; falls back to the config name.</summary>
    private static readonly Dictionary<string, string> SpokenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Up"] = "Pfeil hoch", ["Down"] = "Pfeil runter",
        ["Left"] = "Pfeil links", ["Right"] = "Pfeil rechts",
        ["Return"] = "Eingabe", ["Escape"] = "Escape",
        [","] = "Komma", ["."] = "Punkt", ["-"] = "Minus", ["+"] = "Plus",
    };

    private static readonly Dictionary<int, string> VkToNameMap = new();

    static KeyNames()
    {
        // Letters, digits and the numeric keypad are generated: they are their
        // own names ("N", "7", "Numpad3") and listing 46 one-liners by hand only
        // invites typos. N/H/L/Numpad2-8 were already in use before this table
        // moved out of Plugin.cs - generating them keeps those names identical.
        for (var c = 'A'; c <= 'Z'; c++) NameToVk[c.ToString()] = c;
        for (var d = '0'; d <= '9'; d++) NameToVk[d.ToString()] = d;
        for (var n = 0; n <= 9; n++)     NameToVk[$"Numpad{n}"] = 0x60 + n;

        // Reverse map for key recording. Built from NameToVk so the two can
        // never drift apart. First name wins where several map to one VK.
        foreach (var (name, vk) in NameToVk)
            if (!VkToNameMap.ContainsKey(vk))
                VkToNameMap[vk] = name;
    }

    /// <summary>Config name for a virtual key, or null if the key is not bindable.</summary>
    public static string? VkToName(int vk) => VkToNameMap.GetValueOrDefault(vk);

    /// <summary>
    /// True for the modifier keys themselves. They carry the "Strg+"/"Umschalt+"
    /// prefix of a binding and can never be the main key of one - during key
    /// recording they must be ignored, otherwise holding Ctrl to reach a
    /// combination would immediately be recorded as the binding "Strg".
    /// </summary>
    public static bool IsModifierVk(int vk) =>
        vk is 0x10 or 0x11 or 0x12          // SHIFT, CONTROL, MENU (Alt)
           or 0xA0 or 0xA1 or 0xA2 or 0xA3  // L/R SHIFT, L/R CONTROL
           or 0xA4 or 0xA5;                 // L/R MENU

    /// <summary>
    /// A key spec ("Strg+Umschalt+N") as it should be read aloud
    /// ("Strg plus Umschalt plus N"), so the screen reader does not run the
    /// parts together.
    /// </summary>
    public static string Speak(string keySpec)
    {
        if (string.IsNullOrWhiteSpace(keySpec)) return "keine Taste";
        var parts = keySpec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length; i++)
            parts[i] = SpokenNames.GetValueOrDefault(parts[i], parts[i]);
        return string.Join(" plus ", parts);
    }
}
