namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string EquipMainHand => L("Haupthand", "Main hand", "Основная рука");
    public static string EquipEitherRing => L("Ring links oder rechts", "Left or right ring", "Кольцо на правой или левой руке");
    public static string EquipLeftRing => L("Linker Ring", "Left ring", "Кольцо на левой руке");
    public static string EquipRightRing => L("Rechter Ring", "Right ring", "Кольцо на правой руке");
    public static string EquipmentSlots(string slots) => L($"Anlegen: {slots}", $"Equip to: {slots}", $"Надевать: {slots}");
    public static string EquipmentBlockedSlots(string slots) => L($"Blockiert: {slots}", $"Blocks: {slots}", $"Занимает также: {slots}");
    public static string EquipmentClasses(string classes) => L($"Klassen: {classes}", $"Classes: {classes}", $"Классы: {classes}");
    public static string ComparedEquipmentSlot(string slot) => L($"Vergleichsplatz: {slot}", $"Compared slot: {slot}", $"Сравниваемое место: {slot}");

    // Exact English ClassJobCategory labels from the game. Abbreviation lists
    // are expanded separately from the current ClassJob sheet and verified names.
    public static string? RussianEquipmentClassGroup(string source) => !Loc.IsRussian ? null : source switch
    {
        "All Classes" => "Все классы",
        "Disciple of War" => "Боевые классы",
        "Disciple of Magic" => "Магические классы",
        "Disciple of the Land" => "Собиратели",
        "Disciple of the Hand" => "Ремесленники",
        "Disciples of War or Magic" => "Боевые и магические классы",
        "Disciples of the Land or Hand" => "Собиратели и ремесленники",
        "Any Disciple of War (excluding gladiators)" => "Боевые классы, кроме гладиатора",
        "Any Disciple of the Hand (excluding culinarians)" => "Ремесленники, кроме кулинара",
        "Jobs of the Disciples of War or Magic" => "Боевые и магические профессии",
        "All classes and jobs (excluding limited jobs)" => "Все классы и профессии, кроме ограниченных профессий",
        "Any Disciple of War or Magic (excluding limited jobs)" => "Боевые и магические классы, кроме ограниченных профессий",
        "Disciples of War (excluding limited jobs)" => "Боевые классы, кроме ограниченных профессий",
        "Any Disciple of Magic (excluding limited jobs)" => "Магические классы, кроме ограниченных профессий",
        "Any job of the Disciples of War or Magic (excluding limited jobs)" => "Боевые и магические профессии, кроме ограниченных профессий",
        "Tank (excluding limited jobs)" => "Танки, кроме ограниченных профессий",
        "Healer (excluding limited jobs)" => "Целители, кроме ограниченных профессий",
        "Physical DPS (excluding limited jobs)" => "Бойцы с физическим уроном, кроме ограниченных профессий",
        "Melee DPS (excluding limited jobs)" => "Бойцы ближнего боя, кроме ограниченных профессий",
        "Physical Ranged DPS (excluding limited jobs)" => "Бойцы дальнего боя с физическим уроном, кроме ограниченных профессий",
        "Magical Ranged DPS (excluding limited jobs)" => "Бойцы дальнего боя с магическим уроном, кроме ограниченных профессий",
        _ => null,
    };
}
