using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DbConnectHub.Presentation;

/// <summary>The Settings page: where to scan and how the app runs in the background.</summary>
public sealed partial class MainViewModel
{
    private bool _isLoadingSettings;

    public ObservableCollection<string> ChosenFolders { get; } = [];

    [ObservableProperty]
    private string _scanScope = "";

    [ObservableProperty]
    private bool _hasChosenFolders;

    /// <summary>Start in the tray when Windows starts.</summary>
    [ObservableProperty]
    private bool _startWithWindows;

    /// <summary>Closing the window keeps the app in the tray instead of quitting.</summary>
    [ObservableProperty]
    private bool _keepRunningInTray = true;

    /// <summary>Rescan quietly when the app starts, once the user has scanned at least once.</summary>
    public bool ShouldScanOnStartup => HasData && !IsDataUnreadable && _services.Stores.Settings.Load().ScanOnStartup;

    /// <summary>"Choose a folder instead": scan only the picked folder, now.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private async Task ChooseFolderAsync()
    {
        var folder = await _services.Shell.PickFolderAsync();
        if (folder is null) return;

        SaveScanRoots([folder]);
        await ScanCommand.ExecuteAsync(null);
    }

    /// <summary>Settings: add a folder to the list of folders to scan.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private async Task AddFolderAsync()
    {
        var folder = await _services.Shell.PickFolderAsync();
        if (folder is null || ChosenFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) return;

        SaveScanRoots([.. ChosenFolders, folder]);
    }

    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private void RemoveFolder(string folder) =>
        SaveScanRoots(ChosenFolders.Where(chosen => !chosen.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList());

    [RelayCommand(CanExecute = nameof(CanChangeData))]
    private void UseAllDrives() => SaveScanRoots([]);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SaveSetting(settings => settings.StartWithWindows = value);
        _services.Startup.SetEnabled(value);
    }

    partial void OnKeepRunningInTrayChanged(bool value)
    {
        if (_isLoadingSettings) return;
        SaveSetting(settings => settings.KeepRunningInTray = value);
    }

    private void LoadSettings()
    {
        var settings = _services.Stores.Settings.Load();
        _isLoadingSettings = true;
        KeepRunningInTray = settings.KeepRunningInTray;
        StartWithWindows = settings.StartWithWindows;
        _isLoadingSettings = false;

        // Keep the Windows startup entry in line with the setting (it points at wherever the app now lives).
        _services.Startup.SetEnabled(settings.StartWithWindows);
        DescribeScope();
    }

    private void SaveScanRoots(IReadOnlyList<string> folders) => SaveSetting(settings => settings.ScanRoots = folders.ToList());

    private void SaveSetting(Action<Core.Models.AppSettings> change) => RunGuarded(() =>
    {
        var settings = _services.Stores.Settings.Load();
        change(settings);
        _services.Stores.Settings.Save(settings);
        DescribeScope();
    });

    private void DescribeScope()
    {
        var chosen = _services.Stores.Settings.Load().ScanRoots;
        ChosenFolders.Clear();
        foreach (var folder in chosen) ChosenFolders.Add(folder);
        HasChosenFolders = chosen.Count > 0;

        var roots = string.Join(", ", _services.ScanWorkflow.ListScanRoots());
        ScanScope = HasChosenFolders ? $"Scans the folders you chose: {roots}" : $"Scans all local drives ({roots})";
    }
}
