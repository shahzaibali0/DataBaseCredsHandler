using PgCredsHandler.Scanner;

namespace PgCredsHandler.Tests.Scanner;

public sealed class LocalDrivesTests
{
    [Fact]
    public void SelectScanRoots_KeepsOnlyReadyFixedDrives()
    {
        DriveSnapshot[] drives =
        [
            new(@"C:\", DriveType.Fixed, IsReady: true),
            new(@"D:\", DriveType.Fixed, IsReady: true),
            new(@"E:\", DriveType.Removable, IsReady: true),
            new(@"F:\", DriveType.Network, IsReady: true),
            new(@"G:\", DriveType.CDRom, IsReady: false),
            new(@"H:\", DriveType.Fixed, IsReady: false),
        ];

        Assert.Equal([@"C:\", @"D:\"], LocalDrives.SelectScanRoots(drives));
    }

    [Fact]
    public void ListFixedDriveRoots_OnThisPc_IncludesTheSystemDrive()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)!;

        Assert.Contains(systemDrive, LocalDrives.ListFixedDriveRoots(), StringComparer.OrdinalIgnoreCase);
    }
}
