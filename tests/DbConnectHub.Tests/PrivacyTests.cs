using System.Reflection;
using DbConnectHub.Core.Services;
using DbConnectHub.Scanner;
using DbConnectHub.Storage;

namespace DbConnectHub.Tests;

/// <summary>
/// The privacy promise, checked on every build: the code that scans, stores and shows your data has no way
/// to send it anywhere. Only DbConnectHub.Integrations may use the network, to connect to a database the
/// user explicitly asks to test or open. The WinUI App project holds only XAML views and a small shell
/// service; every decision and all data handling is in the assemblies checked here.
/// </summary>
public sealed class PrivacyTests
{
    private static readonly string[] NetworkingAssemblies =
    [
        "System.Net.Http", "System.Net.Sockets", "System.Net.Requests", "System.Net.WebClient",
        "System.Net.WebSockets", "System.Net.WebSockets.Client", "System.Net.Mail", "System.Net.Ping",
        "System.Net.NetworkInformation", "Npgsql",
    ];

    public static TheoryData<string> AppAssemblies() => new()
    {
        typeof(ConnectionMerger).Assembly.GetName().Name!,
        typeof(ProjectScanner).Assembly.GetName().Name!,
        typeof(JsonConnectionRepository).Assembly.GetName().Name!,
        typeof(DbConnectHub.Presentation.MainViewModel).Assembly.GetName().Name!,
    };

    [Theory]
    [MemberData(nameof(AppAssemblies))]
    public void Assembly_DoesNotUseAnyNetworkingLibrary(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.Empty(references.Intersect(NetworkingAssemblies, StringComparer.OrdinalIgnoreCase));
    }
}
