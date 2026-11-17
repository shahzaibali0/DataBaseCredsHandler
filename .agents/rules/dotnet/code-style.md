---
paths:
  - "**/*.cs"
  - "**/*.xaml.cs"
---

# C# code style

How `common/code-style.md` applies to C#. Everything there still holds; this file adds the numbers and the C# specifics.

## Limits

- Methods: **60 lines** or fewer, cognitive complexity around **15** or lower, **3 parameters** or fewer for new methods (pass a record beyond that), nesting **2** deep at most.
- Files: **400 lines** or fewer. One public type per file, named after the file. Small records that belong to one interface may share its file.
- Names: **40 characters** or fewer.
- No copied block of **8 or more lines**.
- XAML and `.xaml.cs` view files are markup; split a view into `UserControl`s where that helps a reader, not to hit a number.
- Code already over a limit is listed in the project's debt list (`architecture-contract/WHAT_TO_FIX.md`) and may only shrink.

## Naming in C#

- PascalCase for types, methods, properties, events, constants and enum members. `_camelCase` for private fields. camelCase for locals and parameters.
- Interfaces start with `I` (`ICredentialStore`). This is the .NET convention and the only allowed type prefix.
- `Get…` one, `List…`/`GetAll` many, `Find…` one or `null`, `Try…(…, out T value)` returns `bool` and never throws.
- Async methods end in `Async`.
- Constants are `const` or `static readonly` in PascalCase (`DefaultPort`, `CredentialPrefix`), not UPPER_SNAKE.
- The common vague-name and abbreviation bans apply. `e` for an event-args parameter and `ex` are not allowed: write `eventArgs`, `exception`.

## Types

- `sealed record` for immutable data passed between layers. `required` + `init` for objects built once.
- `sealed class` with `{ get; set; }` for persisted, mutable models that `System.Text.Json` round-trips.
- `static class` for pure helpers. `sealed` on every concrete class unless it is designed for inheritance.
- Enums for closed sets the code branches on; plain strings only where outside data decides the values.
- `Nullable` is enabled in every project. No `!` to silence a warning unless the line above proves the value is set. Return `null` only from `Find…`/`Try…`-style members.
- `StringComparer.Ordinal` / `StringComparison.Ordinal` for keys and identifiers, `OrdinalIgnoreCase` for file names, paths and config keys. Never culture-sensitive comparison on data; `ToLowerInvariant`, never `ToLower`.
- `DateTimeOffset` for points in time, passed in as `now` to pure logic instead of reading `DateTimeOffset.Now` inside it.

## Writing a method

- Early returns and guard clauses; no `else` after `return`; `switch` expressions and pattern matching over `if` chains.
- Expression-bodied members (`=>`) for one-line members.
- LINQ for transforming and filtering collections. A `foreach` is fine when it reads more clearly than the query, when it yields lazily, or for a hot path; never index loops where `foreach` works.
- Don't mutate parameters or the collections passed in; clone or build a new collection, the way a pure merge clones its input.
- `var` when the type is obvious from the right-hand side; the explicit type otherwise.
- File-scoped namespaces. `using` directives at the top, `System` first, sorted.
- Collection expressions and target-typed `new()` are fine; match the file you're in.

## Exceptions

- Expected failures are results: `sealed record LaunchResult(bool Success, string Message)`, or `Try…` methods. Throw for bugs and broken invariants.
- Throw the most specific built-in exception (`ArgumentException`, `InvalidOperationException`, `FileNotFoundException`) with a message that says what was expected. Custom exception types only when a caller catches that exact type.
- Catch the specific exceptions you can handle. A bare `catch { }` is allowed only at a deliberate skip boundary (one malformed file, one unreadable folder) with a short comment saying why.
- Rethrow with `throw;`, never `throw exception;`, so the stack survives. Wrap with `new XException(message, inner)` to add context.
- `using` / `using var` for every `IDisposable`.

## Comments

- `///` XML doc comments on public types and members: one or two sentences on what it is for and anything surprising.
- Inline comments only for *why*: a file-format quirk, an OS behaviour, a security reason.
- No `#pragma warning disable` or `[SuppressMessage]` to get past an analyzer; fix the code or change the rule in `.editorconfig` on purpose and record it.

## Formatting and checks

- `dotnet format` is the formatter, driven by `.editorconfig`. Four-space indents, Allman braces, 120-character lines.
- Before calling a change done: `dotnet format --verify-no-changes` (once `.editorconfig` exists), `dotnet build` with **0 errors and 0 warnings**, `dotnet test`. Say what passed.
