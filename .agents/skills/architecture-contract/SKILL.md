---
name: architecture-contract
description: "PgCreds Handler design contract: the five projects and which may reference which, who owns each behaviour, and the design patterns in use (ports and adapters, functional core, parser strategy+registry, repository, MVVM, result objects). Use before adding a class, a project reference or a new dependency, before moving behaviour between projects, or when unsure where code belongs. WHAT_TO_FIX.md lists known debt."
---

# Design contract

Status: accepted, recorded 2026-11-17. This is the authoritative architecture contract; `.agents/rules/project/overview.md` summarizes it. Code that does not follow it yet is listed in [WHAT_TO_FIX.md](WHAT_TO_FIX.md), which is debt to pay down, never a precedent to copy.

The settled requirements: a small local tool whose security rules are easy to see, pure logic separated from I/O, one owner per behaviour, plain readable C#.

## Layers and dependencies

```
                 ┌──────────────────────────┐
                 │   PgCredsHandler.App     │  WinUI 3 window and pages, shell service,
                 │   (composition root)     │  wiring in App.OnLaunched
                 └──┬─────────┬─────────┬───┘
                    │   ┌─────▼───────────────────┐
                    │   │ PgCredsHandler.Presentation │  viewmodels: state, commands,
                    │   │ (no UI framework)       │  grouping, filtering (→ Core, Scanner)
                    │   └─────────────────────────┘
                    │         │         │
        ┌───────────▼──┐ ┌────▼──────┐ ┌▼──────────────┐
        │   Scanner    │ │  Storage  │ │ Integrations  │   adapters: files, disk,
        │ (files→creds)│ │(JSON+Cred │ │(Npgsql, procs,│   Credential Manager,
        │              │ │  Manager) │ │ export files) │   processes, network
        └───────────┬──┘ └────┬──────┘ └┬──────────────┘
                    │         │         │
                 ┌──▼─────────▼─────────▼───┐
                 │    PgCredsHandler.Core   │  models, ports (interfaces), pure helpers,
                 │    (no I/O, no OS APIs)  │  HostClassifier, ConnectionMerger
                 └──────────────────────────┘
```

- Core references nothing in the solution. Scanner, Storage and Integrations reference Core only, never each other. Presentation references Core and Scanner and no UI framework. App references everything. Tests reference whatever they test, except the WinUI App.
- Core, Scanner and Integrations target `net8.0` and stay OS-neutral. Windows-only code (`[SupportedOSPlatform("windows")]`, P/Invoke, WinRT) lives in Storage or App.
- Core does no I/O: no `File`, `Directory`, `Process`, network, clock (`now` is a parameter) or credential store (passwords arrive through a delegate).

## Who owns what

| Responsibility | Home | Owns | Does not own |
| --- | --- | --- | --- |
| Domain model | Core `Models/` | `SavedConnection`, `DiscoveredConnection`, `AppSettings` and their invariants | Persistence, UI state |
| Ports | Core `Abstractions/Interfaces.cs` | `ICredentialStore`, `IConnectionRepository`, `ISettingsStore` | Implementations |
| Pure helpers | Core `Util/` | URL and key=value parsing, `ConnectionKey`, `HostClassifier` | Reading files |
| Merge policy | Core `Services/ConnectionMerger` | Every rule about what a scan adds, updates, marks stale or skips | Writing passwords (it returns `PasswordWrites`) |
| Discovery | Scanner | Walking scan roots, finding project roots, turning files into `DiscoveredConnection`s, defaults (localhost / 5432 / postgres) | Saving anything, deciding merges |
| Persistence | Storage | JSON files (atomic writes), Credential Manager | Business rules |
| Handoff to clients | Integrations | Connection strings, connectivity test, launching DBeaver, writing pgAdmin `servers.json` | Reading the credential store, UI |
| Screen state | Presentation | Viewmodels: what each screen shows, commands, grouping, filtering, counts | XAML, pickers, OS APIs |
| Presentation | App | XAML pages and window, pickers, Explorer, tray/startup services, wiring | Parsing, merge rules, file formats, screen logic |

## Locate the owner before changing code

| Behaviour | Owner | Callers |
| --- | --- | --- |
| Decide whether two connections are the same | `ConnectionKey.Create` | `SavedConnection.Fingerprint`, `DiscoveredConnection.Fingerprint` |
| Pick one entry among duplicates found in a scan | `ConnectionMerger.Deduplicate` | `ConnectionMerger.Merge` |
| Apply a scan to the saved list | `ConnectionMerger.Merge` | `ScanWorkflow.ScanAndSave` |
| Scan, merge and save in one step (the "Scan my PC" button) | `ScanWorkflow.ScanAndSave` in Scanner | `MainViewModel.ScanCommand` |
| Pick the default scan roots (all fixed drives) | `LocalDrives.ListFixedDriveRoots` | `ScanWorkflow` via the composition root |
| Find config files and project roots | `ProjectWalker.Walk` | `ProjectScanner.Scan` |
| Turn one file into credentials | the matching `IConfigParser` | `ProjectScanner.Scan` |
| Expand `${VAR}` references | `VariableExpander.Expand` | `EnvFileParser`, `DockerComposeParser` |
| Classify a host as Local / DockerService / Private / Remote | `HostClassifier.Classify` | App row badges (to build) |
| Store, read, delete a password | `ICredentialStore` (`WindowsCredentialStore`) | App only (Integrations receive the password as an argument) |
| Load and save connections and settings | `IConnectionRepository`, `ISettingsStore` (`JsonDataFile` does the file work: missing = empty, broken = backup + `DataFileUnreadableException`) | `ScanWorkflow`, App |
| Build a connection string | `ConnectionStringBuilder` (to build; today `PostgresUrl.ToUrl`/`ToJdbc`) | App copy commands |
| Open in DBeaver / export to pgAdmin / test | `DbeaverLauncher`, `PgAdminExporter`, `ConnectionTester` (to build) | App row commands |
| Show complete database credentials | `ConnectionRowViewModel` reads through `ICredentialStore` | Database file template |

## Design patterns in use

When to use or avoid any pattern, and how each looks in C#, is in `.agents/rules/common/design-patterns.md` and `.agents/rules/dotnet/design-patterns.md`. This table is the project's own choices.

| Pattern | Where | Rule |
| --- | --- | --- |
| **Ports and adapters** | Core interfaces; Storage and Integrations implement them | New external dependencies go behind an interface. Viewmodels depend on interfaces, not on `WindowsCredentialStore` or `Process`. |
| **Functional core, imperative shell** | `ConnectionMerger`, `HostClassifier`, the parsing helpers | Pure static functions take everything they need as parameters (including `now` and a `readPassword` delegate) and return a result. Keep them pure so tests need no files. |
| **Strategy + registry** | `IConfigParser` implementations, `DefaultParsers.All` | One class per format. Register it in one place. Parsers never call each other except through shared helpers (`EnvFileParser.ReadFile`, `EnvCredentialExtractor`). |
| **Repository** | `JsonConnectionRepository`, `JsonSettingsStore` | Whole-file load and atomic whole-file save. No partial updates, no caching inside the repository. |
| **Factory** | `CredentialStoreFactory.Create` | The one place that picks the credential store by OS. |
| **MVVM** | Presentation (viewmodels) + App (views) | Views `x:Bind` to viewmodels; viewmodels call services; services call ports. See [desktop-ui](../desktop-ui/SKILL.md). |
| **Result object** | Integrations (to build), `MergeResult` | Expected failures are values with a user-facing message, not exceptions. |
| **Iterator** | `ProjectWalker.Walk` yields files lazily | Keep the walk lazy and cancellable; don't collect every file before parsing. |

Patterns we deliberately do not use: a DI container for a handful of services (manual wiring is clearer until it isn't), a mediator or event bus, an ORM, generic repository base classes, inheritance hierarchies for parsers.

## Data flow of a scan

```
ScanRoots ─► ProjectWalker.Walk ─► FoundFile ─► IConfigParser.Parse ─► ParsedCredential
         ─► ProjectScanner (defaults, project name) ─► DiscoveredConnection (password in memory)
         ─► ConnectionMerger.Merge(existing, found, roots, hidden, readPassword, now)
         ─► MergeResult { Connections, PasswordWrites, counts }
         ─► App: ICredentialStore.Save for each PasswordWrite, then IConnectionRepository.SaveAll
```

Order matters at the end: write the passwords first, then the metadata. If a password write fails, do not save metadata that claims `HasPassword = true` for it.

## Enforcement

No automated gate yet. `dotnet build` with warnings treated seriously, `dotnet test`, and the `secrets-reviewer` agent for anything touching passwords. Reviewers check that Core gained no I/O and that no project gained a reference that breaks the arrows above.
