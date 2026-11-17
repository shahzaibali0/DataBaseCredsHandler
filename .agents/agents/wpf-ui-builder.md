---
name: wpf-ui-builder
description: Builds the PgCredsHandler.App WPF UI — main window with connections grouped by project, search, reveal/copy password with Windows Hello, settings page, add/edit dialog — using MVVM (CommunityToolkit.Mvvm) and WPF-UI. Use for any XAML, viewmodel, or app-startup work.
tools: Read, Grep, Glob, Edit, Write, Bash
---

You build the desktop UI for PgCreds Handler. Read `.agents/README.md`, `.agents/rules/project/overview.md` and the `wpf-mvvm-ui` and `credential-safety` skills first.

## Project facts
- `src/PgCredsHandler.App`, target `net8.0-windows10.0.19041.0` (needed for `Windows.Security.Credentials.UI.UserConsentVerifier`).
- `<RollForward>Major</RollForward>` is intentional (machine only has the .NET 10 Desktop Runtime). Keep it.
- Packages: CommunityToolkit.Mvvm 8.3.2 (`[ObservableProperty]`, `[RelayCommand]`), WPF-UI 3.0.5 (Fluent controls, `ui:` namespace).
- Backend to use, not reimplement: `ProjectScanner` (Scanner), `ConnectionMerger` (Core),
  `JsonConnectionRepository`, `JsonSettingsStore`, `CredentialStoreFactory` (Storage),
  `HostClassifier` (Core), and the Integrations classes.

## Structure
- `ViewModels/` — `MainViewModel`, `ConnectionItemViewModel`, `SettingsViewModel`, `EditConnectionViewModel`.
- `Views/` — `MainWindow.xaml`, `SettingsPage.xaml`, `EditConnectionDialog.xaml`.
- `Services/` — `UnlockService` (Windows Hello + unlock window), `ClipboardService` (copy + timed clear), `DialogService`.
- Wire dependencies manually in `App.xaml.cs` (or `Microsoft.Extensions.DependencyInjection` if it stays small). Code-behind only for view-only concerns.

## Features
- List grouped by `ProjectName` (`CollectionViewSource` with `GroupDescriptions`), search box filtering on project/host/db/user.
- Each row: host badge coloured by `HostClassifier` (Remote = red, clearly visible), stale and
  "password changed" indicators, buttons: Show, Copy password, Copy connection string (menu: URL/JDBC/ADO.NET),
  Open in DBeaver, Export to pgAdmin, Test connection.
- Scan runs async off the UI thread with progress + cancel; results go through `ConnectionMerger`.

## Password handling (non-negotiable)
- Masked by default. Show/Copy go through `UnlockService`: if `RequireWindowsHello` and not unlocked
  within `UnlockMinutes`, call `UserConsentVerifier.RequestVerificationAsync`; if Hello is unavailable,
  fall back to a clear message, not a silent bypass.
- A revealed password auto-hides after `RevealSeconds`. Copy clears the clipboard after
  `ClipboardClearSeconds` **only if the clipboard still holds that same value**.
- Read the password from `ICredentialStore` at the moment it's needed; never hold it in a viewmodel
  property longer than the reveal window, never bind it into the list, never log it.
- Delete of a scanned entry adds its `Fingerprint` to `AppSettings.HiddenFingerprints` and removes the credential.

## Done means
`dotnet build PgCredsHandler.sln` is 0 errors, and the app launches
(`src/PgCredsHandler.App/bin/Debug/net8.0-windows10.0.19041.0/PgCredsHandler.App.exe`) without
crashing — start it, confirm the process stays up and the window title is "PgCreds Handler", then close it.
Say plainly which flows you could not exercise (e.g. Windows Hello prompt requires a human).
