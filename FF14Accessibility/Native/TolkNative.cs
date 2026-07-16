using System.Runtime.InteropServices;

namespace FF14Accessibility.Native;

internal static class TolkNative
{
    private const string TolkDll = "Tolk";

    /// <summary>
    /// True when nvdaControllerClient64.dll was preloaded from the plugin directory.
    /// Tolk.dll locates NVDA by calling LoadLibrary("nvdaControllerClient64.dll")
    /// in NATIVE code - the managed DllImportResolver below cannot intercept that,
    /// and the Windows loader only searches the game directory, system dirs and
    /// PATH (not the plugin folder). Preloading the DLL by full path puts the
    /// module into the process; LoadLibrary by base name then returns the already
    /// loaded module, so NVDA detection works without copying DLLs into the game
    /// directory (which a reinstall wipes - that broke speech on 2026-07-16).
    /// </summary>
    internal static bool NvdaClientPreloaded { get; private set; }

    internal static void Initialize(string pluginDir)
    {
        NvdaClientPreloaded = NativeLibrary.TryLoad(
            Path.Combine(pluginDir, "nvdaControllerClient64.dll"), out _);

        NativeLibrary.SetDllImportResolver(typeof(TolkNative).Assembly, (name, assembly, searchPath) =>
        {
            if (name.Equals("Tolk", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(pluginDir, "Tolk.dll");
                return NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
            }
            if (name.Equals("nvdaControllerClient64", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(pluginDir, "nvdaControllerClient64.dll");
                return NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
            }
            return IntPtr.Zero;
        });
    }

    [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Tolk_Load();

    [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Tolk_Unload();

    [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Tolk_IsLoaded();

    [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Tolk_DetectScreenReader();

    [DllImport(TolkDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string str, [MarshalAs(UnmanagedType.I1)] bool interrupt);

    [DllImport(TolkDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Tolk_Speak([MarshalAs(UnmanagedType.LPWStr)] string str, [MarshalAs(UnmanagedType.I1)] bool interrupt);

    [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Tolk_Silence();

    internal static string? GetScreenReaderName()
    {
        var ptr = Tolk_DetectScreenReader();
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) : null;
    }
}
