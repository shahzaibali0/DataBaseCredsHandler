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

/// <summary>A program to run to completion. <paramref name="ExtraSearchFolder"/> goes first on its PATH.</summary>
public sealed record ProcessCommand(string ExecutablePath, IReadOnlyList<string> Arguments, string? ExtraSearchFolder = null);

/// <summary>How a run ended (-1 when it timed out) and what it printed.</summary>
public sealed record ProcessRun(int ExitCode, string Output);

/// <summary>How starting a client ended.</summary>
public enum StartOutcome
{
    /// <summary>Its window is on screen.</summary>
    WindowShown,

    /// <summary>It closed without a window: it passed the request to a copy that was already open.</summary>
    HandedOff,

    /// <summary>No window yet when the wait ran out; it may still appear.</summary>
    StillStarting,
}

/// <summary>Starts programs. Behind an interface so tests can check the exact arguments without starting anything.</summary>
public interface IProcessRunner
{
    /// <summary>Starts a program and waits until its window is on screen, it exits, or the wait runs out.</summary>
    Task<StartOutcome> StartAsync(ProcessCommand command, TimeSpan windowTimeout, CancellationToken cancellationToken);

    Task<ProcessRun> RunAsync(ProcessCommand command, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan WindowCheckInterval = TimeSpan.FromMilliseconds(250);

    // The started program keeps running after this returns: disposing the Process object only lets go of it.
    public async Task<StartOutcome> StartAsync(ProcessCommand command, TimeSpan windowTimeout, CancellationToken cancellationToken)
    {
        using var process = Process.Start(CreateStartInfo(command));
        if (process is null) return StartOutcome.HandedOff;

        try
        {
            return await WaitForWindowAsync(process, windowTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The program started but Windows won't let us watch it (it raised itself to admin, say). Waiting is only
            // for the progress line, so stop waiting instead of failing a launch that worked.
            return StartOutcome.StillStarting;
        }
    }

    private static async Task<StartOutcome> WaitForWindowAsync(Process process, TimeSpan windowTimeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + windowTimeout;
        using var timer = new PeriodicTimer(WindowCheckInterval);
        while (DateTimeOffset.UtcNow < deadline && await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            process.Refresh();
            if (process.HasExited) return StartOutcome.HandedOff;
            if (process.MainWindowHandle != IntPtr.Zero) return StartOutcome.WindowShown;
        }
        return StartOutcome.StillStarting;
    }

    public async Task<ProcessRun> RunAsync(ProcessCommand command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(command);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = Process.Start(startInfo);
        if (process is null) return new ProcessRun(-1, "");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new ProcessRun(process.ExitCode, await output.ConfigureAwait(false) + await errors.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); // release its files first
            if (cancellationToken.IsCancellationRequested) throw;
            return new ProcessRun(-1, "");
        }
    }

    // ArgumentList quotes each value correctly; a command line is never built by joining strings.
    private static ProcessStartInfo CreateStartInfo(ProcessCommand command)
    {
        var startInfo = new ProcessStartInfo(command.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath) ?? "",
        };
        foreach (var argument in command.Arguments) startInfo.ArgumentList.Add(argument);
        // VS Code sets this for everything started from it. An Electron app (pgAdmin) that inherits it runs as plain
        // Node and quits at once with no window, so the clients never get it, however DB Connect Hub was started.
        startInfo.Environment.Remove("ELECTRON_RUN_AS_NODE");
        if (command.ExtraSearchFolder is not null)
            startInfo.Environment["PATH"] = command.ExtraSearchFolder + Path.PathSeparator + startInfo.Environment["PATH"];
        return startInfo;
    }
}
