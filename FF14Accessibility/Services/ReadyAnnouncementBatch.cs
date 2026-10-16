namespace FF14Accessibility.Services;

/// <summary>Speak simultaneous cooldowns, procs and gauge messages together:
/// WarningVoiceService replaces its previous utterance on every Speak call.</summary>
public sealed class ReadyAnnouncementBatch
{
    private readonly List<string> _messages = [];
    public int Count => _messages.Count;
    public void Clear() => _messages.Clear();
    public void Add(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !_messages.Contains(text)) _messages.Add(text);
    }

    public void Speak(Func<string, bool> warningVoice, Action<string> screenReader)
    {
        if (_messages.Count == 0) return;
        var text = string.Join(" ", _messages);
        _messages.Clear();
        if (!warningVoice(text)) screenReader(text);
    }
}
