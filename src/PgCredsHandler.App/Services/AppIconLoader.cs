using System.Runtime.InteropServices;

namespace PgCredsHandler.App.Services;

/// <summary>
/// The app icon, read from the program file itself (set by ApplicationIcon in the project). Reading it from the
/// exe, rather than from an .ico next to it, also works for the single-file download.
/// </summary>
public static class AppIconLoader
{
    /// <summary>A handle to the icon at the requested size, or zero when it can't be read.</summary>
    public static IntPtr Load(bool large)
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return IntPtr.Zero;

        var largeIcons = new IntPtr[1];
        var smallIcons = new IntPtr[1];
        return ExtractIconEx(exePath, 0, largeIcons, smallIcons, 1) > 0
            ? (large ? largeIcons[0] : smallIcons[0])
            : IntPtr.Zero;
    }

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int iconIndex, IntPtr[] largeIcons, IntPtr[] smallIcons, uint icons);
}
