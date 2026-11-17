---
name: wpf-mvvm-ui
description: "Spec and rules for the PgCredsHandler.App WPF UI: MVVM with CommunityToolkit.Mvvm and WPF-UI, composition root in App.xaml.cs, main window grouped by project with host-kind badges and row actions, UnlockService/ClipboardService/DialogService/ScanService, settings page, add/edit dialog. Use for any XAML, viewmodel, app service or startup work."
---

# UI

Status: spec, not built. `src/PgCredsHandler.App` today has a dark WPF-UI theme and a placeholder `MainWindow`.

## Structure

```
App.xaml(.cs)          composition root: create stores, repositories, services, viewmodels; show MainWindow
Views/                 MainWindow, SettingsPage, EditConnectionDialog  (XAML; code-behind only for view concerns)
ViewModels/            MainViewModel, ConnectionItemViewModel, SettingsViewModel, EditConnectionViewModel
Services/              UnlockService, ClipboardService, DialogService, ScanService
```

- Remove `StartupUri` from `App.xaml` and create `MainWindow` in `OnStartup` so its viewmodel can be constructed with its dependencies.
- Viewmodels: `partial class … : ObservableObject`, `[ObservableProperty]` fields, `[RelayCommand]` methods (async commands for scan, test, unlock). Constructor injection of interfaces.
- Viewmodels never touch `Clipboard`, `MessageBox`, `Process`, `File` or `WindowsCredentialStore` directly.
- A file over 400 lines splits along its seams: a section of the window becomes a `UserControl`, state and logic move to a viewmodel or service.

## Main window

- Toolbar: search box, **Scan now** (with progress and cancel), **Add**, **Settings**.
- List grouped by `ProjectName` (`CollectionViewSource` + `PropertyGroupDescription`), sorted by project then database. Search filters on project, host, database, user, never password.
- Row: database name, `user@host:port`, host-kind badge (`Local` neutral, `DockerService` blue, `Private` amber, `Remote` red), source kind, stale (dimmed + label), "password changed" marker, and actions: **Show**, **Copy password**, **Copy connection string ▾** (URL / JDBC / ADO.NET, with or without password), **Open in DBeaver**, **Export to pgAdmin**, **Test**, **Edit**, **Delete**.
- Empty state: no scan roots yet → button that opens Settings to add one.

## Services

| Service | Job |
| --- | --- |
| `UnlockService` | `Task<bool> EnsureUnlockedAsync()`: the Windows Hello flow from [credential-handling.md](../credential-safety/SKILL.md); tracks `unlockedUntil`. |
| `ClipboardService` | `CopySecret(text, clearAfter)`: copies, then clears only if the clipboard still holds that text. Retries on `COMException` (clipboard busy). |
| `DialogService` | Confirm, error, save-file dialogs, so viewmodels stay testable. |
| `ScanService` | Runs `ProjectScanner.Scan` on a background thread, merges, writes `PasswordWrites` then saves the list, returns counts for a status message ("3 added, 1 password changed, 2 stale"). |

## Settings page

Scan roots (add/remove folder), excluded folder names, DBeaver path (with "detect"), `RequireWindowsHello` (turning off needs an unlock), `UnlockMinutes`, `RevealSeconds`, `ClipboardClearSeconds`, `ScanOnStartup`, and a "Restore hidden connections" action that clears `HiddenFingerprints`.

## Add/edit dialog

Host, port, database, user, password (a `PasswordBox`, never bound in plain text), notes. Saving a scanned entry from this dialog sets `IsManual = true` so later scans leave it alone. Editing does not need an unlock; showing the existing password inside it does.

## Done means

Build passes, the app launches with title "PgCreds Handler", and the flows that can be exercised without a human (scan a temp folder, list, search, copy without password) have been tried. Windows Hello needs a human; say so.
