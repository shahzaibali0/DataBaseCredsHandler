using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;
using PgCredsHandler.Scanner;

namespace PgCredsHandler.Presentation;

/// <summary>
/// State and commands for every page: the first-run welcome, the scan with progress and cancel,
/// the databases grouped by project with search and filter, and the folders to scan.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Filter positions, in the order the segmented control shows them.</summary>
    public const int ShowAll = 0;
    public const int ShowOnThisPc = 1;
    public const int ShowOnNetwork = 2;

    private readonly ScanWorkflow _scanWorkflow;
    private readonly DataStores _stores;
    private readonly IDesktopShell _shell;
    private IReadOnlyList<ConnectionRowViewModel> _allRows = [];

    public MainViewModel(ScanWorkflow scanWorkflow, DataStores stores, IDesktopShell shell)
    {
        _scanWorkflow = scanWorkflow;
        _stores = stores;
        _shell = shell;
    }

    public ObservableCollection<ProjectGroupViewModel> Groups { get; } = [];

    public ObservableCollection<string> ChosenFolders { get; } = [];

    public string DataFolderPath => _shell.DataFolderPath;

    public bool ShowWelcome => !HasConnections && !IsScanning;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _hasConnections;

    [ObservableProperty]
    private int _databaseCount;

    [ObservableProperty]
    private int _projectCount;

    [ObservableProperty]
    private int _remoteCount;

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
    private string _scanScope = "";

    [ObservableProperty]
    private bool _hasChosenFolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(ChooseFolderCommand), nameof(AddFolderCommand),
        nameof(RemoveFolderCommand), nameof(UseAllDrivesCommand))]
    private bool _isDataUnreadable;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private int _hostFilterIndex = ShowAll;

    /// <summary>Loads the saved list and the scan scope. Called once at startup.</summary>
    public void Load() => RunGuarded(() =>
    {
        ShowConnections(_stores.Connections.GetAll());
        DescribeScope();
    });

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnHostFilterIndexChanged(int value) => ApplyFilter();

    private bool CanChangeData() => !IsDataUnreadable;

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanChangeData))]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StartProgress();
        var progress = new Progress<ScanProgress>(ShowProgress);
        try
        {
            var summary = await Task.Run(() => _scanWorkflow.ScanAndSave(progress, cancellationToken), cancellationToken);
            ShowConnections(_stores.Connections.GetAll());
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

    /// <summary>"Choose a folder instead": scan only the picked folder, now.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private async Task ChooseFolderAsync()
    {
        var folder = await _shell.PickFolderAsync();
        if (folder is null) return;

        SaveScanRoots([folder]);
        await ScanCommand.ExecuteAsync(null);
    }

    /// <summary>Settings: add a folder to the list of folders to scan.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private async Task AddFolderAsync()
    {
        var folder = await _shell.PickFolderAsync();
        if (folder is null || ChosenFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) return;

        SaveScanRoots([.. ChosenFolders, folder]);
    }

    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private void RemoveFolder(string folder) =>
        SaveScanRoots(ChosenFolders.Where(chosen => !chosen.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList());

    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private void UseAllDrives() => SaveScanRoots([]);

    [RelayCommand]
    private void OpenDataFolder() => _shell.OpenFolder(DataFolderPath);

    private void SaveScanRoots(IReadOnlyList<string> folders) => RunGuarded(() =>
    {
        var settings = _stores.Settings.Load();
        settings.ScanRoots = folders.ToList();
        _stores.Settings.Save(settings);
        DescribeScope();
    });

    private void RunGuarded(Action action)
    {
        try { action(); }
        catch (DataFileUnreadableException exception) { ShowUnreadableData(exception); }
    }

    private void ShowConnections(IEnumerable<SavedConnection> saved)
    {
        _allRows = saved.Select(ConnectionRowViewModel.From).ToList();
        HasConnections = _allRows.Count > 0;
        DatabaseCount = _allRows.Count;
        ProjectCount = _allRows.Select(row => row.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        RemoteCount = _allRows.Count(row => row.HostKind == HostKind.Remote);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var visibleGroups = _allRows
            .Where(row => row.Matches(SearchText) && MatchesHostFilter(row))
            .GroupBy(row => row.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(ToProjectGroup)
            .OrderBy(group => group.ProjectName, StringComparer.OrdinalIgnoreCase);

        Groups.Clear();
        foreach (var group in visibleGroups) Groups.Add(group);
        HasNoMatches = HasConnections && Groups.Count == 0;
    }

    private bool MatchesHostFilter(ConnectionRowViewModel row) => HostFilterIndex switch
    {
        ShowOnThisPc => row.IsOnThisPc,
        ShowOnNetwork => !row.IsOnThisPc,
        _ => true,
    };

    private static ProjectGroupViewModel ToProjectGroup(IGrouping<string, ConnectionRowViewModel> rows) =>
        new(rows.First().ProjectName, rows.Key, rows.OrderBy(row => row.Database, StringComparer.OrdinalIgnoreCase).ToList());

    private void DescribeScope()
    {
        var chosen = _stores.Settings.Load().ScanRoots;
        ChosenFolders.Clear();
        foreach (var folder in chosen) ChosenFolders.Add(folder);
        HasChosenFolders = chosen.Count > 0;

        var roots = string.Join(", ", _scanWorkflow.ListScanRoots());
        ScanScope = HasChosenFolders ? $"Scans the folders you chose: {roots}" : $"Scans all local drives ({roots})";
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

    private void ShowUnreadableData(DataFileUnreadableException exception)
    {
        IsDataUnreadable = true;
        ErrorMessage = $"{exception.Message} Nothing will be saved until you fix or delete that file "
                       + "(Privacy & data → Open data folder), then restart the app.";
    }

    private static string DescribeSummary(ScanSummary summary)
    {
        var databases = summary.Found == 1 ? "1 database" : $"{summary.Found} databases";
        return $"Last scan found {databases}: {summary.Added} new, {summary.PasswordsChanged} password changes, "
               + $"{summary.MarkedStale} no longer found.";
    }
}
