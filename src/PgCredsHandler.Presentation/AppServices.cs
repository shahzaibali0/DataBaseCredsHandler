using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Scanner;

namespace PgCredsHandler.Presentation;

/// <summary>Starting the app with Windows (a per-user startup entry).</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}

/// <summary>Everything the main viewmodel works with, created once in the composition root.</summary>
public sealed record AppServices(
    ScanWorkflow ScanWorkflow,
    DataStores Stores,
    IDesktopShell Shell,
    IStartupRegistration Startup,
    DatabaseActions Databases,
    ILocalPortProbe Ports);
