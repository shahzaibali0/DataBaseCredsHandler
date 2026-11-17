---
paths:
  - "**/*.xaml"
  - "**/*.xaml.cs"
  - "**/ViewModels/**"
  - "**/Views/**"
  - "src/*.App/**"
---

# WPF screens

How the common rules apply to a WPF desktop app with MVVM. This project's screens, services and password UX are in the `wpf-mvvm-ui` skill.

## MVVM

- A view is XAML. Its code-behind holds only view concerns: focus, animation, a `PasswordBox` hand-off. No logic, no service calls.
- A viewmodel is the entry point for a screen. It turns a user action into **one** service call and the result into properties. Business decisions stay in services.
- CommunityToolkit.Mvvm: `partial class … : ObservableObject`, `[ObservableProperty]` on private fields, `[RelayCommand]` on methods (async methods become async commands with built-in "is running" state). No hand-written `INotifyPropertyChanged` boilerplate.
- Viewmodels take interfaces through the constructor and never touch `MessageBox`, `Clipboard`, `Process`, `File`, `Dispatcher` or an OS API directly. A `DialogService`, `ClipboardService` and similar wrap those, so viewmodels can be tested without a window.
- The composition root (`App.OnStartup`) builds services and viewmodels and shows the first window. Remove `StartupUri` when the main window needs constructor arguments.

## Binding and threading

- Views bind to viewmodel properties and commands only. Converters format values; they make no decisions.
- `x:Name` only when code-behind needs the element.
- Long work never runs on the UI thread: the command awaits a service call that runs off-thread, with `IProgress<T>` for progress and a cancel command bound to a `CancellationTokenSource`.
- Collections the view shows are `ObservableCollection<T>` updated on the UI thread. Grouping, sorting and filtering go through a `CollectionViewSource` / `ICollectionView`, not by rebuilding the list.
- An `async` command catches its own exceptions and shows them through the dialog service; an unhandled one would crash the app.

## Look and feel

- Use the chosen control library's controls (WPF-UI here) and its theme resources. No hard-coded colours or font sizes in a view; shared values live in one `ResourceDictionary`.
- Every action has a visible label or tooltip and works from the keyboard (`AccessKey`, `IsDefault`, `IsCancel`, tab order).
- Destructive actions (delete) ask for confirmation through the dialog service.
- Empty, loading and error states are designed, not left blank.

## Secrets on screen

- A secret is never bound in plain text into a list, a tooltip or a search index. `PasswordBox` for input; its value is read in code-behind or through a behaviour at the moment of saving, never kept in a bound string property.
- Showing or copying a secret follows `common/security.md` and this project's `credential-safety` skill.

## Checks

The app builds with 0 warnings and launches: start it, confirm the main window appears with the right title, close it. Say which flows needed a human (an OS prompt, a file dialog).
