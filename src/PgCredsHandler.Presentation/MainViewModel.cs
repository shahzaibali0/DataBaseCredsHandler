using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Scanner;

namespace PgCredsHandler.Presentation;

/// <summary>
/// The Databases page: first-run welcome, the scan with progress and cancel, and everything found arranged as
/// Project → Frontend/Backend files and Database files, with search and complete connection details.
/// Settings live in MainViewModel.Settings.cs.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private IReadOnlyList<SavedApp> _apps = [];
    private IReadOnlyList<ConnectionRowViewModel> _databases = [];

    public MainViewModel(AppServices services)
    {
        _services = services;
        _services.Databases.Notice += ShowNotice;
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    public string DataFolderPath => _services.Shell.DataFolderPath;

    public bool ShowWelcome => !HasData && !IsScanning;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _hasData;

    [ObservableProperty]
    private int _projectCount;

    [ObservableProperty]
    private int _appCount;

    [ObservableProperty]
    private int _databaseCount;

    [ObservableProperty]
    private bool _hasNoMatches;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _isScanning;

    [ObservableProperty]
    private int _filesChecked;

    [ObservableProperty]
    private int _databasesFound;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(ChooseFolderCommand), nameof(AddFolderCommand),
        nameof(RemoveFolderCommand), nameof(UseAllDrivesCommand))]
    private bool _isDataUnreadable;

    [ObservableProperty]
    private string _searchText = "";

    /// <summary>The project whose detail window is open, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectOpen))]
    private WorkspaceViewModel? _selectedProject;

    public bool IsProjectOpen => SelectedProject is not null;

    /// <summary>Loads what is saved and the settings. Called once at startup.</summary>
    public void Load() => RunGuarded(() =>
    {
        ShowSaved();
        LoadSettings();
    });

    /// <summary>Masks and forgets every shown password; used when the window hides and before a rescan replaces rows.</summary>
    public void HideAllPasswords()
    {
        foreach (var database in _databases) database.Hide();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private bool CanChangeData() => !IsDataUnreadable;

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanChangeData))]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StartProgress();
        var progress = new Progress<ScanProgress>(ShowProgress);
        try
        {
            var summary = await Task.Run(() => _services.ScanWorkflow.ScanAndSave(progress, cancellationToken), cancellationToken);
            ShowSaved();
            Summary = DescribeSummary(summary);
        }
        catch (OperationCanceledException)
        {
            Summary = "Scan cancelled. Nothing was changed.";
        }
        catch (DataFileUnreadableException exception)
        {
            ShowUnreadableData(exception);
        }
        catch (Exception exception) // the command is an entry point: an unhandled error would close the app
        {
            ErrorMessage = $"The scan stopped: {exception.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => _services.Shell.OpenFolder(DataFolderPath);

    [RelayCommand]
    private void OpenLocalUrl(string url) => _services.Shell.OpenUrl(url);

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    [RelayCommand]
    private void OpenProject(WorkspaceViewModel project)
    {
        _services.Databases.RefreshInstalledTools();
        SelectedProject = project;
    }

    [RelayCommand]
    private void CloseProject()
    {
        SelectedProject?.HidePasswords();
        SelectedProject = null;
    }

    private void RunGuarded(Action action)
    {
        try { action(); }
        catch (DataFileUnreadableException exception) { ShowUnreadableData(exception); }
    }

    private void ShowSaved()
    {
        HideAllPasswords();
        var openProjectPath = SelectedProject?.Path;
        var inventory = _services.Stores.Inventory.Load();
        _apps = inventory.Apps;
        _databases = _services.Stores.Connections.GetAll()
            .Select(saved => ConnectionRowViewModel.From(saved, _services.Databases)).ToList();
        HasData = _apps.Count + _databases.Count > 0;
        ApplyFilter();
        CountEverything();
        SelectedProject = Workspaces.FirstOrDefault(project => project.Path == openProjectPath);
    }

    private void ApplyFilter()
    {
        var workspaces = WorkspaceBuilder.Build(
            new WorkspaceSources(_apps, _databases), new WorkspaceFilter(SearchText));
        Workspaces.Clear();
        foreach (var workspace in workspaces) Workspaces.Add(workspace);
        HasNoMatches = HasData && Workspaces.Count == 0;
    }

    private void CountEverything()
    {
        var everything = WorkspaceBuilder.Build(new WorkspaceSources(_apps, _databases), new WorkspaceFilter(""));
        ProjectCount = everything.Count;
        AppCount = everything.Sum(workspace => workspace.Apps.Count);
        DatabaseCount = _databases.Count;
    }

    private void StartProgress()
    {
        FilesChecked = 0;
        DatabasesFound = 0;
        CurrentFile = "Starting…";
        IsScanning = true;
    }

    private void ShowProgress(ScanProgress progress)
    {
        FilesChecked = progress.FilesScanned;
        DatabasesFound = progress.ConnectionsFound;
        CurrentFile = progress.CurrentFile;
    }

    private void ShowNotice(string message, bool isProblem)
    {
        if (isProblem) ErrorMessage = message;
        else StatusMessage = message;
    }

    private void ShowUnreadableData(DataFileUnreadableException exception)
    {
        IsDataUnreadable = true;
        ErrorMessage = $"{exception.Message} Nothing will be saved until you fix or delete that file "
                       + "(Privacy & data → Open data folder), then restart the app.";
    }

    private static string DescribeSummary(ScanSummary summary)
    {
        var databases = summary.Found == 1 ? "1 database" : $"{summary.Found} databases";
        return $"Last scan found {summary.AppsFound} apps and {databases}: "
               + $"{summary.Added} new, {summary.PasswordsChanged} password changes, "
               + $"{summary.MarkedStale} no longer found.";
    }
}
