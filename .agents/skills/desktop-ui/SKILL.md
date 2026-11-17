---
name: desktop-ui
description: "Spec and rules for the PgCreds Handler desktop UI: WinUI 3 (Windows App SDK) with CommunityToolkit.WinUI and WinUIEx, viewmodels in PgCredsHandler.Presentation, Mica window with NavigationView (Databases, Privacy & data, Settings), hero welcome, scan progress card, stat cards, search and filter, project cards (SettingsExpander), and what is still to build (show/copy with Windows Hello, connect, add/edit). Use for any XAML, page, viewmodel, app service or startup work."
---

# Desktop UI

The general WinUI rules are in `.agents/rules/dotnet/winui.md`. This page is the project's own screens.

## Where things are

```
src/PgCredsHandler.Presentation/   (net8.0, testable, no UI framework)
  MainViewModel.cs                 state and commands for every page
  ProjectGroupViewModel.cs         one project card
  ConnectionRowViewModel.cs        one database row (no password, only HasPassword)
  IDesktopShell.cs                 data folder, open folder, folder picker
src/PgCredsHandler.App/            (WinUI 3, views only)
  App.xaml(.cs)                    composition root (OnLaunched)
  MainWindow.xaml(.cs)             Mica, custom title bar, NavigationView, Frame
  Pages/DatabasesPage.xaml         welcome, scanning, databases
  Pages/PrivacyPage.xaml           privacy promise, data locations, how to verify, start over
  Pages/SettingsPage.xaml          folders to scan, appearance, about
  Services/DesktopShell.cs         FolderPicker (with the window handle), Explorer
  Themes/Styles.xaml               every app colour and shared style
  Ui/Badges.cs                     x:Bind helper functions (host badge brush, number format)
```

## Built

- **Window**: Mica, custom title bar with app mark and a "Local only" pill, min 900×620, starts 1240×820 centred. Sidebar: **Databases**, **Privacy & data**, footer **Settings**.
- **Databases, first run**: gradient hero card (pill "PRIVATE · OFFLINE · OPEN SOURCE", headline, one-line explanation, **Scan my PC for databases** accent button, **Choose a folder instead**, the scope line), then three feature cards (stays on this PC, passwords encrypted, only your projects).
- **Databases, scanning**: card with `ProgressRing`, files checked, databases found, current file, **Cancel**. Cancel or failure saves nothing.
- **Databases, list**: title with last-scan summary, **Choose folder** and **Rescan** (F5); stat cards (Projects, Databases, On remote servers); search box (Ctrl+F) and `Segmented` filter (All / On this PC / On a network); scope line; a "no matches" card; one `SettingsExpander` per project (name, path, count) with a `SettingsCard` per database (name, `user@host:port · source`, tags: No longer found, Password saved, host badge: Local grey, Docker blue, Private amber, Remote red).
- **Privacy & data**: hero "Your data never leaves this PC"; data folder with **Open data folder**; passwords in Credential Manager; damaged files never overwritten; links to `PrivacyTests.cs` and the repo; how to start over.
- **Settings**: folders to scan (**Add folder**, **Remove**, **Use all local drives**), always-skipped folders, theme follows Windows, about.
- **Errors**: an `InfoBar` at the top of Databases for an unreadable data file or a failed scan; scanning is disabled while a data file is unreadable.

## Still to build

| Feature | Notes |
| --- | --- |
| Show / copy password | `UnlockService` (Windows Hello via `UserConsentVerifier`, `UnlockMinutes`), reveal auto-hides after `RevealSeconds`, `ClipboardService` clears after `ClipboardClearSeconds` only if unchanged. Flow in the `credential-safety` skill. |
| Connect | Row actions: Open in DBeaver, Export to pgAdmin, Test, Copy connection string (URL / JDBC / ADO.NET, with or without password). Confirm before testing a Remote host. Spec in `db-client-integrations`. |
| Add / edit / delete | Edit dialog (`ContentDialog`, `PasswordBox`); saving a scanned entry sets `IsManual`; delete of a scanned entry adds its fingerprint to `HiddenFingerprints`; confirm before delete. |
| Settings | Timers (`UnlockMinutes`, `RevealSeconds`, `ClipboardClearSeconds`), `RequireWindowsHello` (turning off needs an unlock), DBeaver path, restore hidden connections. |

## Done means

`dotnet build` 0 warnings, `dotnet test` green (viewmodel behaviour is tested in `tests/…/Presentation/`), the app launches with title "PgCreds Handler", and the changed screens were looked at in both light and dark mode. Say which flows needed a human.
