using System.Text.RegularExpressions;

namespace FF14Accessibility.Services;

/// <summary>Let fields settle; countdown ticks alone never re-read a whole page.</summary>
internal sealed class SquadronAnnouncement
{
    private readonly SelectOkAnnouncement _state = new();
    internal string Update(string page, bool visible, long now, bool repeat = false)
    {
        var stable = Regex.Replace(page, @"\b\d{1,2}:\d{2}(?::\d{2})?\b", "--:--");
        return _state.Update(stable, visible, now, repeat).Length > 0 ? page : "";
    }
}
