using System.Diagnostics;

namespace DbConnectHub.Integrations;

/// <summary>The database clients DB Connect Hub can open a connection in.</summary>
public enum DbTool
{
    DBeaver,
    PgAdmin,
    DbVisualizer,
}

/// <summary>What a client needs to connect. The password is passed in at the moment of launching, never kept.</summary>
public sealed record DbConnectionInfo(string Name, string Host, int Port, string Database, string Username, string? Password)
{
    public override string ToString() => $"{Username}@{Host}:{Port}/{Database}"; // never the password
}

/// <summary>An expected outcome of opening a client, with a message the user can act on.</summary>
public sealed record LaunchResult(bool Success, string Message);

/// <summary>Starts programs. Behind an interface so tests can check the exact arguments without starting anything.</summary>
public interface IProcessRunner
{
    void Start(string executablePath, IReadOnlyList<string> arguments);

    /// <summary>Runs a program to completion and returns its exit code (-1 if it timed out).</summary>
    int Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout);
}

public sealed class ProcessRunner : IProcessRunner
{
    public void Start(string executablePath, IReadOnlyList<string> arguments) =>
        Process.Start(CreateStartInfo(executablePath, arguments))?.Dispose();

    public int Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        using var process = Process.Start(CreateStartInfo(executablePath, arguments));
        if (process is null) return -1;
        if (process.WaitForExit(timeout)) return process.ExitCode;

        process.Kill(entireProcessTree: true);
        return -1;
    }

    // ArgumentList quotes each value correctly; a command line is never built by joining strings.
    private static ProcessStartInfo CreateStartInfo(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "",
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}
