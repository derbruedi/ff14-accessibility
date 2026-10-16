using System.Threading;
using Dalamud.Game.ClientState.Keys;
using FF14Accessibility.Services;

namespace FF14Accessibility;

// Funktionen aus der lokalen 6.08.19, die im 6.08.56-Zweig fehlten.
public sealed partial class Plugin
{
    private int _modsRunning;

    private static bool TrySwitchWord(string text, out bool enabled)
    {
        enabled = false;
        switch (text.Trim().ToLowerInvariant())
        {
            case "выкл": case "выкл.": case "выключить": case "выключи":
            case "отключить": case "отключи": case "убрать":
            case "off": case "disable": case "aus": case "ausschalten":
                return true;
            case "вкл": case "вкл.": case "включить": case "включи":
            case "вернуть": case "on": case "enable": case "ein": case "einschalten":
                enabled = true;
                return true;
            default:
                return false;
        }
    }

    private bool TryHandleModsCommand(string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 ||
            (!parts[0].Equals("mods", StringComparison.OrdinalIgnoreCase) &&
             !parts[0].Equals("моды", StringComparison.OrdinalIgnoreCase))) return false;

        if (parts.Length > 1 && TrySwitchWord(parts[1].Split(' ', 2)[0], out var enabled))
            BeginModsSwitch(restore: enabled);
        else
            BeginModsRead();
        return true;
    }

    private void BeginModsRead()
    {
        if (Interlocked.CompareExchange(ref _modsRunning, 1, 0) != 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ModsBusy);
            return;
        }
        if (!_mods.PenumbraReady)
        {
            Interlocked.Exchange(ref _modsRunning, 0);
            _tolk.SpeakInterrupt(AccessibilityStrings.ModsPenumbraSilent);
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var overview = _mods.Read();
                if (!_shutdown.IsCancellationRequested)
                    Framework.RunOnFrameworkThread(() =>
                    {
                        if (!_shutdown.IsCancellationRequested) AnnounceModsRead(overview);
                    });
            }
            catch (Exception ex) { Log.Error($"[Mods] Lesen fehlgeschlagen: {ex}"); }
            finally { Interlocked.Exchange(ref _modsRunning, 0); }
        });
    }

    private void AnnounceModsRead(ModsOverview overview)
    {
        if (!overview.Answered) _tolk.Speak(AccessibilityStrings.ModsPenumbraSilent);
        else if (overview.ModCount == 0) _tolk.Speak(AccessibilityStrings.ModsNone);
        else if (overview.Unreadable > 0) _tolk.Speak(AccessibilityStrings.ModsReadPartlyFailed);
        else _tolk.Speak(AccessibilityStrings.ModsOverview(
            overview.ModCount, overview.CollectionCount, overview.EnabledMods, overview.Names));
    }

    private void BeginModsSwitch(bool restore)
    {
        if (Interlocked.CompareExchange(ref _modsRunning, 1, 0) != 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ModsBusy);
            return;
        }
        if (!_mods.PenumbraReady)
        {
            Interlocked.Exchange(ref _modsRunning, 0);
            _tolk.SpeakInterrupt(AccessibilityStrings.ModsPenumbraSilent);
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var result = restore ? _mods.Restore() : _mods.AllOff();
                if (!_shutdown.IsCancellationRequested)
                    Framework.RunOnFrameworkThread(() =>
                    {
                        if (_shutdown.IsCancellationRequested) return;
                        PluginInterface.SavePluginConfig(_config);
                        AnnounceModsSwitch(result, restore);
                    });
            }
            catch (Exception ex) { Log.Error($"[Mods] Umschalten fehlgeschlagen: {ex}"); }
            finally { Interlocked.Exchange(ref _modsRunning, 0); }
        });
    }

    private void AnnounceModsSwitch(ModsSwitchResult result, bool restore)
    {
        if (!result.Answered) { _tolk.Speak(AccessibilityStrings.ModsPenumbraSilent); return; }
        if (restore)
            _tolk.Speak(result.NothingToDo
                ? AccessibilityStrings.ModsNothingRemembered
                : AccessibilityStrings.ModsRestored(result.Changed, result.Skipped));
        else if (result.NothingToDo)
            _tolk.Speak(result.ModCount == 0 ? AccessibilityStrings.ModsNone
                : result.Skipped > 0 ? AccessibilityStrings.ModsCouldNotRead(result.Skipped)
                : AccessibilityStrings.ModsAlreadyOff(result.Remembered));
        else
            _tolk.Speak(AccessibilityStrings.ModsSwitchedOff(result.Changed, result.Skipped));
    }

    private void AnnounceFrameRate() =>
        _tolk.SpeakInterrupt(FocusTimingProbe.CurrentFrameRate(out var fps, out var meanMs, out var maxMs)
            ? AccessibilityStrings.FrameRate(fps, meanMs, maxMs)
            : AccessibilityStrings.FrameRateTooEarly);

    private void ToggleSpeechTrace()
    {
        if (!SpeechTrace.Armed)
        {
            SpeechTrace.Arm();
            _tolk.SpeakInterrupt(AccessibilityStrings.SpeechTraceArmed);
        }
        else
        {
            var lines = SpeechTrace.Write(Log, PluginVersion, withHeader: true);
            SpeechTrace.Disarm();
            _tolk.SpeakInterrupt(lines < 0 ? AccessibilityStrings.SpeechTraceFailed
                : AccessibilityStrings.SpeechTraceWritten(lines, SpeechTrace.FileName));
        }
    }

    private void HandlePlayerMenuKey()
    {
        if (!IsJustPressed(_config.KeyPlayerMenu)) return;
        var blockingAddon = _uiReader.BlockingFocusedAddonForPlayerMenu();
        if (_menu.IsOpen || _hotbar.IsSkillMenuOpen || _uiReader.HasActiveMenu || blockingAddon != null)
        {
            Log.Info($"[PlayerMenu] Num0: anderes Menü aktiv (Sprachmenü={_menu.IsOpen}, " +
                     $"Fertigkeiten={_hotbar.IsSkillMenuOpen}, Spielmenü={_uiReader.HasActiveMenu}, " +
                     $"Fokus={blockingAddon ?? "keiner"})");
            return;
        }
        // Browser selection sets the hard target; a temporary keyboard target
        // is useful only when no hard target exists.
        var target = TargetManager.Target ?? TargetManager.SoftTarget;
        if (target == null || !PlayerInfo.IsPlayer(target) || target.Address == nint.Zero)
        {
            Log.Info($"[PlayerMenu] Num0: kein Spielerziel (Ziel={target?.ObjectKind.ToString() ?? "keins"})");
            return;
        }
        Log.Info($"[PlayerMenu] Num0: öffne Menü für '{target.Name.TextValue}'");
        if (!_uiReader.OpenPlayerContextMenu(target.Address))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerMenuNotOpened);
            return;
        }
        // Das Spiel darf denselben Numpad-Druck nicht danach als Menue-OK lesen.
        var key = (VirtualKey)96;
        if (KeyState.IsVirtualKeyValid(96) && KeyState[key]) KeyState[key] = false;
    }
}
