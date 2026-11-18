using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DbConnectHub.Core.Models;
using DbConnectHub.Core.Util;

namespace DbConnectHub.Presentation;

/// <summary>
/// One database connection with every detail. The password stays masked until the eye is clicked; it is read from
/// the credential store at that moment and dropped again by <see cref="Hide"/> (project closed, window hidden, rescan).
/// </summary>
public sealed partial class ConnectionRowViewModel : ObservableObject
{
    public const string Mask = "••••••••••••";

    private readonly DatabaseActions _actions;
    private string _shownPassword = "";

    private ConnectionRowViewModel(SavedConnection connection, DatabaseActions actions)
    {
        _actions = actions;
        Id = connection.Id;
        ProjectName = connection.ProjectName;
        ProjectPath = connection.ProjectPath;
        Database = connection.Database;
        Username = connection.Username;
        Host = connection.Host;
        Port = connection.Port;
        HostKind = HostClassifier.Classify(connection.Host);
        Source = string.IsNullOrEmpty(connection.SourceKey) ? connection.SourceKind : $"{connection.SourceKind} · {connection.SourceKey}";
        SourceFile = connection.SourceFile;
        IsStale = connection.IsStale;
        HasPassword = connection.HasPassword;
    }

    public string Id { get; }

    public string ProjectName { get; }

    /// <summary>The project on the card (the repository), not the app folder; "ShopRepo" rather than "backend".</summary>
    public string WorkspaceName { get; private init; } = "";

    /// <summary>The folder the config file belongs to; usually the app folder.</summary>
    public string ProjectPath { get; }

    public string Database { get; }

    public string Username { get; }

    public string Host { get; }

    public int Port { get; }

    public string PortText => Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary><c>user@host:port</c>, as people write it.</summary>
    public string Endpoint => $"{Username}@{Host}:{Port}";

    /// <summary>For pasting into any client; never includes the password.</summary>
    public string ConnectionUrl => PostgresUrl.ToUrl(Host, Port, Database, Username, password: null);

    public string JdbcUrl => PostgresUrl.ToJdbc(Host, Port, Database);

    public HostKind HostKind { get; }

    public string HostKindLabel => HostKind switch
    {
        HostKind.Local => "Local",
        HostKind.DockerService => "Docker",
        HostKind.Private => "Private network",
        _ => "Remote",
    };

    public string Source { get; }

    public string SourceFile { get; }

    public bool IsStale { get; }

    public bool HasPassword { get; }

    /// <summary>Only a database on this PC (localhost) is checked; a remote server is never contacted.</summary>
    public bool IsOnThisPc => HostKind == HostKind.Local;

    /// <summary>Whether the database is accepting connections right now.</summary>
    [ObservableProperty]
    private RunState _runState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPassword), nameof(RevealLabel))]
    private bool _isRevealed;

    public string DisplayPassword => IsRevealed ? _shownPassword : HasPassword ? Mask : "No password found";

    public string RevealLabel => IsRevealed ? "Hide password" : "Show password";

    /// <summary>What is happening while a client is being opened for this database; empty otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpening))]
    private string _openingText = "";

    public bool IsOpening => OpeningText.Length > 0;

    public bool CanOpenInDbeaver => _actions.IsInstalled(DatabaseTool.DBeaver);

    public bool CanOpenInPgAdmin => _actions.IsInstalled(DatabaseTool.PgAdmin);

    public bool CanOpenInDbVisualizer => _actions.IsInstalled(DatabaseTool.DbVisualizer);

    /// <param name="workspaceName">The project the card shows (the repository), used to name it in database clients.</param>
    public static ConnectionRowViewModel From(SavedConnection connection, DatabaseActions actions, string workspaceName) =>
        new(connection, actions) { WorkspaceName = workspaceName };

    /// <summary>Masks the password again and forgets it.</summary>
    public void Hide()
    {
        _shownPassword = "";
        IsRevealed = false;
        OnPropertyChanged(nameof(DisplayPassword));
    }

    /// <summary>True when the search text appears in the project, host, database or user. Never the password.</summary>
    public bool Matches(string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
        || new[] { ProjectName, Host, Database, Username }
            .Any(field => field.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));

    private bool CanReveal() => HasPassword;

    [RelayCommand(CanExecute = nameof(CanReveal))]
    private void ToggleReveal()
    {
        if (IsRevealed)
        {
            Hide();
            return;
        }

        var password = _actions.ReadPassword(Id);
        if (password is null) return;
        _shownPassword = password;
        IsRevealed = true;
    }

    [RelayCommand(CanExecute = nameof(CanReveal))]
    private void CopyPassword() => _actions.CopyPassword(Id);

    [RelayCommand]
    private void CopyConnectionUrl() => _actions.CopyText("Connection URL", ConnectionUrl);

    // Async commands: the button stays disabled while its client is being prepared, so a second click can't add twice.
    [RelayCommand(CanExecute = nameof(CanOpenInDbeaver))]
    private Task OpenInDbeaverAsync() => OpenInToolAsync(DatabaseTool.DBeaver);

    [RelayCommand(CanExecute = nameof(CanOpenInPgAdmin))]
    private Task OpenInPgAdminAsync() => OpenInToolAsync(DatabaseTool.PgAdmin);

    [RelayCommand(CanExecute = nameof(CanOpenInDbVisualizer))]
    private Task OpenInDbVisualizerAsync() => OpenInToolAsync(DatabaseTool.DbVisualizer);

    // The progress line stays until the client's window is up (or the launch gave up), whatever happened.
    private async Task OpenInToolAsync(DatabaseTool tool)
    {
        OpeningText = DatabaseActions.OpeningText(tool);
        try
        {
            await _actions.OpenAsync(tool, this);
        }
        finally
        {
            OpeningText = "";
        }
    }
}
