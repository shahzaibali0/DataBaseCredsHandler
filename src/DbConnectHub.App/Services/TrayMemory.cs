namespace DbConnectHub.App.Services;

/// <summary>Gives memory back to Windows when the window is hidden and the app waits in the tray.</summary>
public static class TrayMemory
{
    public static void Release()
    {
        // Whatever the hidden screen let go of is garbage now. .NET would collect it only when it next needs memory,
        // which in the tray may be never. An aggressive collection hands the freed memory back to Windows, which
        // throws it away. The working set is deliberately not trimmed: that would page memory still in use out to
        // pagefile.sys, and freed memory can still hold the bytes of a password that was shown, since .NET can't wipe
        // a string.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
