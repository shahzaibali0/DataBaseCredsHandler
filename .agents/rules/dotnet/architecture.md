---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
  - "**/*.xaml"
  - "**/Directory.Build.*"
---

# Architecture in .NET

How `common/architecture.md` maps onto a .NET solution. This project's concrete projects and owners are in the `architecture-contract` skill.

## Homes

| Common home | In .NET |
| --- | --- |
| Domain types and the interfaces services depend on | A `*.Core` class library: models, interfaces (ports), pure helpers, pure decision logic. No I/O, no OS APIs. |
| Services | Static pure classes in Core when they need no I/O; otherwise a class in the project that owns the I/O, behind an interface from Core. |
| Data access | A `*.Storage` / `*.Data` project: repositories, file stores, `DbContext`. One per kind of storage. |
| Outside systems | One project or folder per outside system (`*.Integrations`), each behind an interface. |
| Entry points | WPF viewmodel commands, ASP.NET controllers or minimal-API endpoints, hosted services, a console `Main`. |
| Composition root | `App.xaml.cs` `OnStartup`, or `Program.cs`. The only place that creates concrete adapters. |
| Configuration | One settings type loaded and validated at startup (`IOptions<T>` with `ValidateOnStart`, or a settings store). |
| Tests | A `tests/*.Tests` project per area, mirroring the source folders. |

## Layers are project references

- The direction of calls is enforced by `ProjectReference`s: Core references nothing in the solution; adapter projects reference Core only, never each other; the app references everything. Adding a reference that points the wrong way is a design change, recorded in `.agents/decisions.md`.
- Core and adapter libraries target plain `net8.0` and stay OS-neutral. Windows-only code (`[SupportedOSPlatform("windows")]`, P/Invoke, WinRT) lives in the project that needs it.
- Keep `InternalsVisibleTo` out of production projects; tests use the public surface.
- If a rule can't be expressed as a reference (for example "viewmodels never use `System.IO`"), check it in a test with an architecture-test library, or in review.

## Composition

- Constructor injection of interfaces. A class never creates its own adapter with `new` (`new WindowsCredentialStore()` only in the composition root or a factory).
- Manual wiring is fine for a small app. Move to `Microsoft.Extensions.DependencyInjection` when the root gets hard to read, and record it.
- No service locator, no static mutable singletons. A static class is for pure functions and constants only.

## Configuration

- Exactly one place loads settings. It validates them at startup and fails with a clear message, or falls back to documented defaults, on purpose.
- Settings are typed properties with units in their names (`UnlockMinutes`, `TimeoutSeconds`), never magic strings looked up at call sites.
- Code defaults apply only when nothing is stored. Changing a default doesn't change a user's stored value; say so when you change one.

## Logging

- `Microsoft.Extensions.Logging` `ILogger<T>` when the app logs. Message templates with named holes, not interpolation: `logger.LogError(exception, "Scan of {ScanRoot} failed", scanRoot)`.
- The exception is the first argument, so the stack is kept.
- No `Console.WriteLine` or `Debug.WriteLine` in finished code.

## Async and background work

- I/O is `async` end to end and takes a `CancellationToken` as its last parameter. No `.Result`, `.Wait()` or `async void` (except UI event handlers, which catch their own errors).
- Library code uses `ConfigureAwait(false)`; UI code does not.
- CPU-heavy or blocking work leaves the UI thread with `Task.Run` at the entry point, not deep inside a service.
- Long work reports through `IProgress<T>` and checks the token inside loops.
- Timed work is a `PeriodicTimer` loop or a hosted service that catches each run's errors and never overlaps itself.

## Persistence

- A file the app owns is written atomically: write a temp file, then `File.Replace` / `File.Move`.
- "The file doesn't exist" (start empty) and "the file exists but can't be read" (stop, keep the file, tell the user) are different cases. Never treat a broken file as empty and save over it.
- Serialized models are `sealed class` with `{ get; set; }` and `System.Text.Json`; nothing secret is ever a serialized property.
