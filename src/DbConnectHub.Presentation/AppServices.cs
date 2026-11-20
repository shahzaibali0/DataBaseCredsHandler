using DbConnectHub.Core.Abstractions;
using DbConnectHub.Scanner;

namespace DbConnectHub.Presentation;

/// <summary>Starting the app with Windows (a per-user startup entry).</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}

/// <summary>Starts a dev server in its own console window (implemented with DbConnectHub.Integrations in the app).</summary>
public interface IServerRunner
{
    ToolLaunch Start(ServerCommand command);
}

/// <summary>Everything the main viewmodel works with, created once in the composition root.</summary>
public sealed record AppServices(
    ScanWorkflow ScanWorkflow,
    DataStores Stores,
    IDesktopShell Shell,
    IStartupRegistration Startup,
    DatabaseActions Databases,
    ILocalPortProbe Ports,
    IServerRunner Servers);
