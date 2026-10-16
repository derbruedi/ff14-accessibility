namespace FF14Accessibility.Services;

public sealed record ModsOverview(bool Answered, int ModCount, int CollectionCount, int EnabledMods, string Names, int Unreadable);
