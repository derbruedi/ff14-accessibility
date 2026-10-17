using System.Runtime.InteropServices;

namespace FF14Accessibility.Services;

/// <summary>One native confirm per physical press in manual dialogs. The first
/// frame belongs to the game; later frames stay blocked even after page/window
/// replacement, until the physical key is released.</summary>
internal sealed class DialogueConfirmGuard
{
    private readonly HashSet<int> _down = [];
    private readonly HashSet<int> _blocked = [];

    internal bool Update(int key, bool down, bool manualDialog, bool nativeHold)
    {
        if (!down) { _down.Remove(key); _blocked.Remove(key); return false; }
        var wasDown = !_down.Add(key);
        if (_blocked.Contains(key)) return true;
        if (!manualDialog || nativeHold) return false;
        _blocked.Add(key);
        return wasDown;
    }

    internal static bool PhysicallyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    // Only retain keys actually observed on the keyboard. Inputs supplied by
    // another device must keep their ordinary game state and edge detection.
    internal static bool ReadKey(bool gameDown, bool physicalDown, ref bool tracked)
    {
        if (tracked)
        {
            tracked = physicalDown;
            return physicalDown;
        }
        tracked = gameDown && physicalDown;
        return gameDown;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
