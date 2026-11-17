namespace PgCredsHandler.Presentation;

/// <summary>One project card: its name, folder and the databases found in it.</summary>
public sealed class ProjectGroupViewModel
{
    public ProjectGroupViewModel(string projectName, string projectPath, IReadOnlyList<ConnectionRowViewModel> connections)
    {
        ProjectName = projectName;
        ProjectPath = projectPath;
        Connections = connections;
    }

    public string ProjectName { get; }

    public string ProjectPath { get; }

    public IReadOnlyList<ConnectionRowViewModel> Connections { get; }

    public string CountLabel => Connections.Count == 1 ? "1 database" : $"{Connections.Count} databases";
}
