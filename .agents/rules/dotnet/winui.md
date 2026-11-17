---
paths:
  - "**/*.xaml"
  - "**/*.xaml.cs"
  - "src/*.App/**"
  - "src/*.Presentation/**"
---

# WinUI 3 screens

How the common rules apply to a WinUI 3 (Windows App SDK) desktop app with MVVM. This project's screens, services and password UX are in the `desktop-ui` skill.

## Two projects for the UI

- **Presentation** (`net8.0`, CommunityToolkit.Mvvm only): viewmodels and the interfaces they need (`IDesktopShell`). No `Microsoft.UI.*`, no `Windows.*`, no XAML types. Grouping, filtering, counts and text all happen here, as plain C#, so tests drive them without a window.
- **App** (WinUI 3): XAML pages, the window, `App.OnLaunched` as the composition root, and the shell service that wraps pickers and Explorer. No decisions, no data handling.
- A new screen gets its state and commands in Presentation first, with tests, then its XAML in App.

## MVVM

- CommunityToolkit.Mvvm: `partial class … : ObservableObject`, `[ObservableProperty]` on private fields, `[RelayCommand]` on methods (async methods become async commands; `IncludeCancelCommand = true` adds a cancel command).
- Viewmodels take interfaces through the constructor and never touch a picker, `Process`, the clipboard or an OS API directly.
- Pages get the viewmodel as the navigation parameter in `OnNavigatedTo` and expose it as a typed `ViewModel` property for `x:Bind`.
- Code-behind holds only view concerns: focus, keyboard shortcuts, routing a template button to a command.

## Binding

- Use `x:Bind` (compiled, typed), not `{Binding}`. Set `Mode=OneWay` for anything that changes; `Mode=TwoWay` for inputs. Every `DataTemplate` has `x:DataType`.
- `bool` binds straight to `Visibility` with `x:Bind`; expose a positive property (`ShowWelcome`) rather than inverting in XAML.
- No `IValueConverter` for simple mapping: use an `x:Bind` function binding to a static helper in `App/Ui/` (`ui:Badges.HostBrush(HostKind)`).
- Lists: `ObservableCollection<T>` from the viewmodel, shown with `ItemsRepeater` (or `ListView` when selection is needed). Grouping is done in the viewmodel as a collection of groups, not with `CollectionViewSource`.

## Threading

- Long work never runs on the UI thread: the command awaits `Task.Run(...)` around the service call, with `IProgress<T>` created on the UI thread so reports come back on it.
- After an `await` in a command, the viewmodel is back on the UI thread; update collections there.
- An async command catches its own exceptions and shows them through a bound error message; an unhandled one closes the app.

## Look and feel

- WinUI 3 controls plus CommunityToolkit.WinUI controls (`SettingsCard`, `SettingsExpander`, `Segmented`) and WinUIEx window helpers. No other UI library without a decision in `.agents/decisions.md`.
- Mica backdrop, custom title bar (`ExtendsContentIntoTitleBar` + `SetTitleBar`), `NavigationView` for pages.
- The theme follows Windows. Use theme resources (`{ThemeResource TextFillColorPrimaryBrush}`, `CardBackgroundFillColorDefaultBrush`, the built-in text styles). The app's own colours and shared styles live only in `App/Themes/Styles.xaml`.
- Every icon-only or icon-plus-text button has `AutomationProperties.Name`; important actions have a tooltip and, where natural, a keyboard accelerator (F5 rescan, Ctrl+F search).
- Empty, scanning, no-results and error states are designed, not left blank.

## Secrets on screen

- A secret is never bound in plain text into a list, a tooltip or the search. `PasswordBox` for input.
- Showing or copying a secret follows `common/security.md` and this project's `credential-safety` skill.

## Packaging and checks

- Unpackaged (`WindowsPackageType=None`), self-contained Windows App SDK, `win-x64`. Release builds are one file: `dotnet publish -p:PublishSingleFile=true` (needs `EnableMsixTooling`, build tooling only).
- Build output is `bin\<Config>\net8.0-windows10.0.19041.0\win-x64\DbConnectHub.exe`.
- The app builds with 0 warnings and launches: start it, confirm the window title "DB Connect Hub", close it. Say which flows needed a human (for example, a folder picker).
