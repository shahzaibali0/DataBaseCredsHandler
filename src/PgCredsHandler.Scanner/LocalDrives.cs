namespace PgCredsHandler.Scanner;

/// <summary>What the scanner needs to know about one drive.</summary>
public sealed record DriveSnapshot(string RootPath, DriveType Type, bool IsReady);

/// <summary>The default scan roots: every fixed local drive (C:\, D:\…).</summary>
public static class LocalDrives
{
    /// <summary>Root folders of the fixed drives on this PC that are ready to read.</summary>
    public static IReadOnlyList<string> ListFixedDriveRoots() =>
        SelectScanRoots(DriveInfo.GetDrives().Select(ToSnapshot));

    /// <summary>
    /// Keeps fixed, ready drives only. USB sticks, network shares and CD drives are left out:
    /// they are slow, may belong to someone else, and the user can still pick them by hand.
    /// </summary>
    public static IReadOnlyList<string> SelectScanRoots(IEnumerable<DriveSnapshot> drives) =>
        drives
            .Where(drive => drive.Type == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootPath)
            .ToList();

    private static DriveSnapshot ToSnapshot(DriveInfo drive)
    {
        try { return new DriveSnapshot(drive.RootDirectory.FullName, drive.DriveType, drive.IsReady); }
        catch (IOException) { return new DriveSnapshot(drive.Name, drive.DriveType, false); } // drive vanished mid-listing
    }
}
