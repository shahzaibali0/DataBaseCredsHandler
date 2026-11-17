---
name: desktop-ui
description: "Spec and rules for the DB Connect Hub desktop UI: WinUI 3 (Windows App SDK) with CommunityToolkit.WinUI and WinUIEx, viewmodels in DbConnectHub.Presentation, Mica window with NavigationView, project cards with app files and database files, and complete database credentials. Use for any XAML, page, viewmodel, app service or startup work."
---

# Desktop UI

The general WinUI rules are in `.agents/rules/dotnet/winui.md`. This page is the project's own screens.

## Where things are

```
src/DbConnectHub.Presentation/   (net8.0, testable, no UI framework)
  MainViewModel.cs                 state and commands for every page
  ProjectGroupViewModel.cs         one project card
  ConnectionRowViewModel.cs        one database file with complete connection details
  IDesktopShell.cs                 data folder, open folder, folder picker
src/DbConnectHub.App/            (WinUI 3, views only)
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

**Projects page (current design, 2026-11-17).** Modelled on a reference design the owner provided (square case cards that open a detail window):
- A grid of square project cards (`ItemsRepeater` + `UniformGridLayout`, min 250×250): accent strip, initials badge, name, app/database counts, stack summary, one line per local server (`Backend :3001`), path footer.
- Clicking a card opens the project window over the grid (backdrop, Esc or click outside closes): `Ui/ProjectDetailPanel.xaml` with **Ports** (role, URL, where the port came from, Open), **Database connections** (host, port, database, user, password masked with eye + Copy, source; Open in DBeaver / pgAdmin / DbVisualizer, enabled only when installed; Copy connection URL without password), **Stack** and **Key packages** by purpose.
- Closing the window, leaving the page, hiding to the tray or rescanning masks every shown password (`MainViewModel.HideAllPasswords`, `WorkspaceViewModel.HidePasswords`).

Older notes below may describe earlier layouts.


- **Window**: Mica, custom title bar with app mark and a "Local only" pill, min 900×620, starts 1240×820 centred. Sidebar: **Databases**, **Privacy & data**, footer **Settings**.
- **Databases, first run**: gradient hero card (pill "PRIVATE · OFFLINE · OPEN SOURCE", headline, one-line explanation, **Scan my PC for databases** accent button, **Choose a folder instead**, the scope line), then three feature cards (stays on this PC, passwords encrypted, only your projects).
- **Databases, scanning**: card with `ProgressRing`, files checked, databases found, current file, **Cancel**. Cancel or failure saves nothing.
- **Databases, list**: each Project contains clickable Frontend/Backend application files and separate Database files. An application file expands to framework, local URL/port and project folder. A Database file expands to host, port, database, username, full unmasked password, source and host type. The page has Projects/Apps/Databases stat cards, Ctrl+F search, **Choose folder**, and **Rescan** (F5).
- **Privacy & data**: hero "Your data never leaves this PC"; data folder with **Open data folder**; passwords in Credential Manager; damaged files never overwritten; links to `PrivacyTests.cs` and the repo; how to start over.
- **Settings**: folders to scan (**Add folder**, **Remove**, **Use all local drives**), always-skipped folders, start with Windows, close to tray, theme follows Windows, about.
- **Background**: single instance, optional start with Windows, quiet startup rescan, and close-to-tray with an Exit tray command.
- **Errors**: an `InfoBar` at the top of Databases for an unreadable data file or a failed scan; scanning is disabled while a data file is unreadable.

## Still to build

| Feature | Notes |
| --- | --- |
| Connect | Row actions: Open in DBeaver, Export to pgAdmin, Test, Copy connection string (URL / JDBC / ADO.NET, with or without password). Confirm before testing a Remote host. Spec in `db-client-integrations`. |
| Add / edit / delete | Edit dialog (`ContentDialog`, `PasswordBox`); saving a scanned entry sets `IsManual`; delete of a scanned entry adds its fingerprint to `HiddenFingerprints`; confirm before delete. |
| Settings | DBeaver path and restore hidden connections. |

## Done means

`dotnet build` 0 warnings, `dotnet test` green (viewmodel behaviour is tested in `tests/…/Presentation/`), the app launches with title "DB Connect Hub", and the changed screens were looked at in both light and dark mode. Say which flows needed a human.
