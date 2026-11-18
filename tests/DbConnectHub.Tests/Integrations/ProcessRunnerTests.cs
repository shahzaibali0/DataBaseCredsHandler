using DbConnectHub.Integrations;

namespace DbConnectHub.Tests.Integrations;

public sealed class ProcessRunnerTests
{
    private const string ElectronAsNode = "ELECTRON_RUN_AS_NODE";

    private static readonly string Shell = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    // VS Code sets this for everything it starts; an Electron app such as pgAdmin that inherits it quits at once.
    [Fact]
    public async Task RunAsync_AppStartedFromVsCode_DoesNotPassElectronRunAsNodeOn()
    {
        var previous = Environment.GetEnvironmentVariable(ElectronAsNode);
        Environment.SetEnvironmentVariable(ElectronAsNode, "1");
        try
        {
            var run = await new ProcessRunner().RunAsync(
                new ProcessCommand(Shell, ["/c", $"echo [%{ElectronAsNode}%]"]), TimeSpan.FromSeconds(30), CancellationToken.None);

            Assert.Equal($"[%{ElectronAsNode}%]", run.Output.Trim());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ElectronAsNode, previous);
        }
    }

    [Fact]
    public async Task StartAsync_ProgramEndsWithoutAWindow_ReportsItWasHandedOff()
    {
        var outcome = await new ProcessRunner().StartAsync(
            new ProcessCommand(Shell, ["/c", "exit 0"]), TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(StartOutcome.HandedOff, outcome);
    }
}
