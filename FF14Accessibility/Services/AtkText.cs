using System;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Guarded reader for game UI text. Every text the plugin speaks comes from a
/// node the game owns, and reading one that is not built yet crashes the whole
/// game - this is the single place where that is checked.
///
/// WHY THIS EXISTS (crash 2026-07-20, four times in 25 minutes):
/// FFXIVClientStructs' <c>Utf8String.ToString()</c> is unguarded. Decompiled
/// (ilspycmd, FFXIVClientStructs 2026-07-17):
/// <code>
/// public readonly int Length => Math.Max(0, (int)(BufUsed - 1));
/// public readonly ReadOnlySpan&lt;byte&gt; AsSpan() =&gt; new((byte*)StringPtr, Length);
/// public override string ToString() =&gt; !AsSpan().IsEmpty
///     ? Encoding.UTF8.GetString(AsSpan()) : string.Empty;
/// </code>
/// There is no null check and no bounds check. On a node the game has
/// allocated but not filled, <c>StringPtr</c> and <c>BufUsed</c> hold whatever
/// was in that memory before, so <c>GetString</c> reads a garbage pointer over
/// a garbage length and walks off the page. That is an
/// <see cref="AccessViolationException"/>, and .NET treats it as a corrupted
/// state exception: a try-catch around the call does NOT stop it. The check
/// has to happen BEFORE the read, which is what this class does.
///
/// A null pointer check alone is not enough - that was the guard in place when
/// the game crashed (UIReaderService, social window tab label). The node
/// pointer was set; only the string behind it was still uninitialised.
/// </summary>
internal static unsafe class AtkText
{
    /// <summary>
    /// Upper bound for a plausible UI string, in bytes. This is a sanity limit,
    /// not a game constant: the real invariant checked below is
    /// <c>BufUsed &lt;= BufSize</c>, which the game itself maintains. This bound
    /// only catches the case where BOTH fields hold garbage that happens to be
    /// self-consistent. No UI label in this game comes near 64 KB - the longest
    /// real strings are quest descriptions at a few hundred bytes.
    /// </summary>
    private const long MaxPlausibleLength = 64 * 1024;

    /// <summary>
    /// Reads a text node's text, or an empty string when the node is not in a
    /// readable state. Never throws and never returns null.
    /// </summary>
    public static string Read(AtkTextNode* node)
        => node == null ? string.Empty : Read(&node->NodeText);

    /// <summary>
    /// Reads a <see cref="Utf8String"/> as RAW UTF-8, or an empty string when it
    /// is not in a readable state. Fast, but keeps any SeString payload markers
    /// verbatim - use <see cref="ReadClean(Utf8String*)"/> for text that may
    /// carry item links / auto-translate payloads.
    /// </summary>
    public static string Read(Utf8String* str)
    {
        if (!TryValidate(str, out var start, out var length)) return string.Empty;
        return Encoding.UTF8.GetString(start, length);
    }

    /// <summary>
    /// Reads a text node and drops SeString payloads (item links, auto-translate)
    /// so only the human-readable text is returned. Item-link node text otherwise
    /// leaks its raw marker bytes ("H?%I?&amp;Ahorn-Holzscheit...IH"); parsing it
    /// through Dalamud's SeString reader yields "Ahorn-Holzscheit".
    /// </summary>
    public static string ReadClean(AtkTextNode* node)
        => node == null ? string.Empty : ReadClean(&node->NodeText);

    /// <inheritdoc cref="ReadClean(AtkTextNode*)"/>
    public static string ReadClean(Utf8String* str)
    {
        // Same guard as Read: the buffer must be mapped and self-consistent
        // before Dalamud is allowed to walk it. The address+length overload is
        // used (not the Utf8String* one, which is deprecated) - both parse the
        // FFXIV SeString payloads; TextValue then yields only the readable text.
        if (!TryValidate(str, out var start, out var length)) return string.Empty;
        var se = Dalamud.Memory.MemoryHelper.ReadSeString((nint)start, length);
        return TolkService.Sanitize(se.TextValue);
    }

    /// <summary>
    /// The shared guard: verifies the string struct and its buffer are mapped and
    /// the game's own length invariant holds, returning the readable span. This
    /// is the single place the crash from an unfilled node (see class remarks) is
    /// prevented; both read paths funnel through it.
    /// </summary>
    private static bool TryValidate(Utf8String* str, out byte* start, out int length)
    {
        start = null;
        length = 0;

        // 1. The struct itself must be mapped before any field is touched.
        if (!IsReadable(str)) return false;

        // 2. Trust the game's own emptiness flag first - an empty string is a
        //    normal state (a label the game deliberately blanked), not a fault.
        length = str->Length;
        if (length <= 0) return false;

        // 3. The struct's own invariant: the used portion cannot exceed the
        //    allocated buffer. Garbage almost always violates this.
        if (str->BufUsed > str->BufSize) return false;
        if (str->BufSize is <= 0 or > MaxPlausibleLength) return false;

        // 4. The buffer must be mapped at both ends. Checking only the start
        //    would still let a long read run off the end of the page.
        start = (byte*)str->StringPtr;
        if (!IsReadable(start)) return false;
        if (!IsReadable(start + length - 1)) return false;

        return true;
    }

    /// <summary>
    /// Whether a pointer refers to committed, readable memory. Asks the OS via
    /// VirtualQuery rather than guessing.
    ///
    /// TRAP (fixed 2026-07-09, cost a day): MEMORY_BASIC_INFORMATION is 48
    /// bytes on x64, not 44. With the wrong size VirtualQuery fails with
    /// ERROR_BAD_LENGTH on every call, so this returns false everywhere and all
    /// guarded reads silently degrade to empty. If UI text ever goes blank
    /// across the board, verify this struct first.
    /// </summary>
    public static bool IsReadable(void* ptr)
    {
        if (ptr == null) return false;
        if (VirtualQuery(ptr, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0) return false;
        const uint MEM_COMMIT = 0x1000;
        const uint PAGE_NOACCESS = 0x01;
        const uint PAGE_GUARD = 0x100;
        return mbi.State == MEM_COMMIT && (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) == 0;
    }

    /// <summary>
    /// Diagnostic companion to <see cref="IsReadable"/>: pointer value plus the
    /// VirtualQuery verdict, so a rejected read is explainable from the log
    /// instead of just being silent.
    /// </summary>
    public static string DescribeMemory(void* ptr)
    {
        if (ptr == null) return "null";
        if (VirtualQuery(ptr, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            return $"0x{(nint)ptr:X}: VirtualQuery failed";
        return $"0x{(nint)ptr:X}: State=0x{mbi.State:X} Protect=0x{mbi.Protect:X} RegionSize=0x{(ulong)mbi.RegionSize:X}";
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct MEMORY_BASIC_INFORMATION
    {
        [FieldOffset(0)]  public nuint BaseAddress;
        [FieldOffset(8)]  public nuint AllocationBase;
        [FieldOffset(16)] public uint  AllocationProtect;
        [FieldOffset(20)] public ushort PartitionId;
        [FieldOffset(24)] public nuint RegionSize;
        [FieldOffset(32)] public uint  State;
        [FieldOffset(36)] public uint  Protect;
        [FieldOffset(40)] public uint  Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nuint VirtualQuery(void* lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, nuint dwLength);
}
