namespace FF14Accessibility.Services;

public sealed record ModsSwitchResult(bool Answered, bool NothingToDo, int ModCount, int Changed, int Skipped, int Remembered);
