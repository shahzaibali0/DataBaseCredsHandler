---
paths:
  - "**/*.cs"
  - "**/*.xaml"
---

# Design patterns in C#

How `common/design-patterns.md` applies in C# and .NET, based on the [refactoring.guru C# catalogue](https://refactoring.guru/design-patterns/csharp). The common rules still hold: plain code first, name the problem, prefer the language feature, composition over inheritance.

## Where this repo already uses them

Follow these. Don't add a second way of doing the same thing.

| Pattern | Where | How |
| --- | --- | --- |
| Strategy + registry | `IConfigParser`, `DefaultParsers.All` | One class per config format; the scanner picks the first whose `CanParse` matches. A new format is a new class and one registry line. |
| Adapter | `WindowsCredentialStore : ICredentialStore`, `JsonConnectionRepository : IConnectionRepository` | Wrap Win32 `CredWrite/CredRead` and the file system behind Core interfaces. Integrations (Npgsql, DBeaver, pgAdmin) follow the same shape. |
| Factory Method | `CredentialStoreFactory.Create()` | Static factory picks the implementation by OS. The only place that decides. |
| Iterator | `ProjectWalker.Walk` | `IEnumerable<FoundFile>` with `yield return`: lazy and cancellable. |
| Prototype | `SavedConnection.Clone()` | `MemberwiseClone` so `ConnectionMerger` never mutates its input. Fine because every field is a value or string. |
| Builder | `NpgsqlConnectionStringBuilder` (planned in `ConnectionStringBuilder`) | Use the library's builder for ADO.NET strings so quoting is right. |
| Observer | `IProgress<ScanProgress>`; `INotifyPropertyChanged` via `[ObservableProperty]` | Built-in .NET forms; no custom event bus. |
| Command | `[RelayCommand]` → `ICommand` (planned in the viewmodels) | Every button binds to a command; async work uses async commands. |
| Facade | `ScanService` (planned) | One operation in front of scan → merge → write passwords → save. |
| Pure function (not a GoF pattern) | `ConnectionMerger`, `HostClassifier`, parsers' helpers | The default. Most logic here needs no pattern at all. |

## Creational

- **Factory Method**: a `static` method that returns the interface (`CredentialStoreFactory.Create()`), or a `Func<T>` passed in. Don't build a creator class hierarchy to vary one `new`.
- **Abstract Factory**: only if a whole family of implementations must switch together. Not needed in this app.
- **Builder**: use the framework's builders (`NpgsqlConnectionStringBuilder`, `StringBuilder`, `UriBuilder`, `HostApplicationBuilder`). For your own types, prefer `required`/`init` properties and an object initializer; write a builder only when parts are added step by step and validated at the end.
- **Prototype**: `record` + `with` (`connection with { Port = 5433 }`) for immutable data; `MemberwiseClone` only for flat classes whose fields are values or strings. A deep clone of a graph is a smell.
- **Singleton**: don't write `static Instance` / double-checked locking (refactoring.guru notes its use "is on the decline in C# code" because it acts like a global). Create one instance in `App.OnStartup` or register it with a singleton lifetime, and pass it through constructors. A `static class` is for stateless pure functions and constants only. `Lazy<T>` if something must be created on first use.

## Structural

- **Adapter**: a `sealed class` that implements a Core interface and takes or wraps the outside API (identified, per refactoring.guru, by "a constructor which takes an instance of a different abstract/interface type", or here by wrapping a static API). Translate types at the boundary: outside exceptions become results or our own messages, outside types never leak upward. One adapter per outside system.
- **Bridge**: two interfaces injected side by side are enough. No abstract class hierarchy.
- **Composite**: model a tree as a record with `IReadOnlyList<Child>`; walk it recursively or with an explicit stack (as `ProjectWalker` does) to avoid deep recursion.
- **Decorator**: a class that implements the same interface and wraps an inner instance (`LoggingCredentialStore : ICredentialStore` wrapping the real one). .NET's own `Stream` wrappers (`BufferedStream`, `GZipStream`) are the model. Never let a decorator see or log a password it passes through.
- **Facade**: an app service with a few `async` operations that hides the order of lower calls (`ScanService.ScanAndMergeAsync`). Viewmodels call the facade, not the scanner, merger and stores separately.
- **Flyweight**: `static readonly` shared lookup tables and string literals already give you this. Don't build one without a profiler result.
- **Proxy**: `Lazy<T>` for lazy creation; a wrapper that checks an unlock before reading a secret is a protection proxy (`UnlockService` gating `ICredentialStore.Read` calls).

## Behavioural

- **Chain of Responsibility**: for a fixed order, write named steps in one method and return early on refusal. A runtime chain (`IEnumerable<IStep>` run in order) only when steps are added by configuration. ASP.NET middleware is the framework's version.
- **Command**: `ICommand` via CommunityToolkit's `[RelayCommand]` / `AsyncRelayCommand` in WinUI or WPF. For undo or queues, a `record` describing the action plus one executor.
- **Iterator**: `IEnumerable<T>` / `IAsyncEnumerable<T>` with `yield return`. Never a hand-written iterator class. Don't enumerate a lazy sequence twice; materialize with `ToList()` when you need to.
- **Mediator**: avoid a mediator library (MediatR-style) in this app: it hides who calls whom. A viewmodel calls a service directly. If screens must talk, give them one shared service.
- **Memento**: keep the previous immutable `record`, or `Clone()` before editing, so "Cancel" restores it.
- **Observer**: C# `event` / `EventHandler<T>`, `INotifyPropertyChanged` (via `[ObservableProperty]`), `ObservableCollection<T>`, `IProgress<T>`. Unsubscribe (`-=`) when the subscriber's lifetime is shorter than the source's, or it leaks.
- **State**: an `enum` plus a `switch` expression in one place for a few states. Separate state classes only when each state has substantial behaviour of its own.
- **Strategy**: an interface with one class per variant when each variant has its own logic or several members (`IConfigParser`). A `Func<TIn, TOut>` when the variant is a single function (`ConnectionMerger.Merge`'s `readPassword`). Refactoring.guru's identification: a method that lets a nested object do the actual work. Register variants in one list; select with a predicate (`CanParse`), not a `switch` on type names.
- **Template Method**: avoid. Replace the abstract base class with a Strategy or with functions passed in. All concrete classes here are `sealed`.
- **Visitor**: use `switch` expressions with type patterns (`node switch { Leaf l => …, Group g => … }`) instead of `Accept`/`Visit` double dispatch.

## Checks

- A new interface needs either a second implementation, a test fake, or a boundary to an outside system. Otherwise, use the concrete class.
- A new class named after a pattern (`…Strategy`, `…Singleton`, `…Visitor`) or a vague role (`…Manager`, `…Handler`, `…Processor`) gets a role name instead.
- A pattern that shapes an area gets a row in the table above and in the `architecture-contract` skill.
