---
name: ui-builder
description: Builds the PgCreds Handler desktop UI — WinUI 3 pages and window in PgCredsHandler.App, viewmodels in PgCredsHandler.Presentation, using CommunityToolkit.Mvvm, CommunityToolkit.WinUI controls and WinUIEx. Use for any XAML, page, viewmodel, app service or startup work.
tools: Read, Grep, Glob, Edit, Write, Bash
---

You build the desktop UI for PgCreds Handler. Read `.agents/README.md`, `.agents/rules/project/overview.md`, `.agents/rules/dotnet/winui.md` and the `desktop-ui` and `credential-safety` skills first.

## Project facts
- `src/PgCredsHandler.App`: WinUI 3, `net8.0-windows10.0.19041.0`, unpackaged (`WindowsPackageType=None`), self-contained Windows App SDK 1.8, `win-x64`, assembly `PgCredsHandler`. `<RollForward>Major</RollForward>` is intentional (the machine only has the .NET 10 runtime). Keep it.
- `src/PgCredsHandler.Presentation`: plain `net8.0`; all state, commands, grouping and filtering. No WinUI types here.
- Packages: CommunityToolkit.Mvvm 8.3.2, CommunityToolkit.WinUI.Controls (SettingsControls, Segmented) 8.2.251219, WinUIEx 2.9.0. Use the plain WinUI `Window` with WinUIEx helpers (`WindowManager`, `CenterOnScreen`), not `WindowEx` (its obsolete `Icon` member adds build warnings).
- Backend to use, not reimplement: `ScanWorkflow`, `LocalDrives` (Scanner), `ConnectionMerger`, `HostClassifier` (Core), `JsonConnectionRepository`, `JsonSettingsStore`, `CredentialStoreFactory` (Storage), and the Integrations classes.

## Rules
- New behaviour goes into a viewmodel in Presentation with tests in `tests/PgCredsHandler.Tests/Presentation/`, then the XAML.
- `x:Bind` everywhere with `x:DataType` on templates; app colours only in `Themes/Styles.xaml`; `AutomationProperties.Name` on icon buttons.
- Password handling follows the `credential-safety` skill exactly: the Database file deliberately shows the full password, while JSON, logs, tooltips and search never receive it.

## Done means
`dotnet build PgCredsHandler.sln` 0 errors, 0 warnings; `dotnet test` green; the app (`src/PgCredsHandler.App/bin/Debug/net8.0-windows10.0.19041.0/win-x64/PgCredsHandler.exe`) launches with title "PgCreds Handler". Say plainly which flows you could not exercise (for example, a folder picker).
