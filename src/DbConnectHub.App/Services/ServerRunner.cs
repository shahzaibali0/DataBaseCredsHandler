using DbConnectHub.Integrations;
using DbConnectHub.Presentation;
using DbConnectHub.Scanner;

namespace DbConnectHub.App.Services;

/// <summary>Connects the Run buttons to the console launcher in Integrations.</summary>
public sealed class ServerRunner : IServerRunner
{
    public ToolLaunch Start(ServerCommand command)
    {
        try
        {
            var launch = ServerLauncher.Start(command.Program, command.Arguments, command.Folder);
            return new ToolLaunch(launch.Success, launch.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new ToolLaunch(false, $"The server couldn't be started: {exception.Message}");
        }
    }
}
