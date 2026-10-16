using System;
using System.IO;

namespace FF14Accessibility.Services;

/// <summary>
/// A small plain-text log next to the plugin, written in addition to the
/// Dalamud log.
/// <para>
/// Why it exists: the Dalamud log is a growing ring buffer (4.7 MB in our own
/// test run), so the one line a player has to send us can be rotated out
/// between two game starts. For a blind player there is no way to scroll a
/// console window and find it again — a file in the plugin folder can be
/// attached to a chat message as it is.
/// </para>
/// <para>
/// Deliberately tiny: append-only, never throws into the caller (logging must
/// not be able to break the mod), and silently a no-op when
/// <see cref="Open"/> was never called — a constructor that runs before
/// <c>Plugin</c> reaches it must not crash.
/// </para>
/// </summary>
internal static class PluginFileLog
{
    private static string? _path;
    private static readonly object Gate = new();

    /// <summary>Points the log at <paramref name="directory"/> and starts a
    /// fresh file. Called once from the plugin constructor.
    /// <paramref name="version"/> is written into the header line on purpose:
    /// a blind player has no way to read the running plugin version inside the
    /// game (there is no version command), so without it neither they nor we
    /// can tell whether a freshly installed build actually took over
    /// (reported 2026-09-26, after a round-trip over exactly that question).</summary>
    internal static void Open(string directory, string version)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                _path = Path.Combine(directory, "FF14Accessibility.log");
                // Preserve the previous session: a restart after a failure must
                // not erase the only detailed transfer evidence.
                if (File.Exists(_path))
                {
                    var archive = Path.Combine(directory, "logs");
                    Directory.CreateDirectory(archive);
                    File.Copy(_path, Path.Combine(archive,
                        $"FF14Accessibility-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log"));
                }
                File.WriteAllText(_path,
                    $"FF14 Accessibility {version} - Diagnoseprotokoll, gestartet {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
            }
        }
        catch
        {
            // No writable folder (locked install, no rights): the Dalamud log
            // stays as the only channel. Not worth an announcement - the player
            // cannot act on it and the mod itself is unaffected.
            _path = null;
        }
    }

    /// <summary>Appends one line with a timestamp.</summary>
    internal static void Write(string line)
    {
        if (_path == null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch
        {
            // Full disk or a locked file: never let a diagnostic break the game.
        }
    }

    /// <summary>
    /// Copies the collected log to the desktop and returns the full path, or
    /// null when that failed.
    /// <para>
    /// Why a copy instead of logging there directly: a player who reports a bug
    /// should not have to navigate to an install folder — the desktop is the one
    /// place a screen reader user reaches with a single keystroke (Windows+D),
    /// and the file can be attached to a chat message right away. The working
    /// copy stays in the plugin folder so a second report does not need the game
    /// restarted.
    /// </para>
    /// </summary>
    internal static string? CopyToDesktop(string fileName)
    {
        if (_path == null) return null;
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(desktop)) return null;
            var target = Path.Combine(desktop, fileName);
            lock (Gate)
                File.Copy(_path, target, overwrite: true);
            return target;
        }
        catch
        {
            // No desktop (rare, but a locked-down profile exists) or the file is
            // busy: the caller announces the failure, nothing is lost - the
            // working copy in the plugin folder is untouched.
            return null;
        }
    }
}
