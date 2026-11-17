# PgCreds Handler: project overview

What this project is, what must never break, and the facts every change needs. Loaded every session. The index of everything in `.agents/` is [.agents/README.md](../../README.md).

## What is this

PgCreds Handler is a **Windows desktop tool** (WPF, .NET 8) that each user runs on their own machine. It is open source (MIT). It scans folders the user chooses (e.g. `C:\Users\you\Projects`), finds Postgres credentials in each project's config files (`.env`, `docker-compose.yml`, `appsettings*.json`), stores them securely, shows them grouped by project, and opens a connection in DBeaver, pgAdmin or any other client with one click.

It is a personal password manager for databases that discovers its own entries. It is not a web app, it has no server, and it never touches anyone else's data.

## What matters most

Two failures defeat the point of the tool. Be careful wherever they can happen:

- **A password leaks.** Passwords live only in Windows Credential Manager. They never reach `connections.json`, `settings.json`, a pgAdmin export, a log, an exception message, a `ToString()`, a tooltip or a list binding. Before adding any code that touches a password, say where the value goes and when it leaves memory. Skill: `credential-safety`.
- **A scan destroys the user's data.** Scans are additive. A manual entry is never overwritten, a connection that disappears is marked `IsStale`, never deleted, and a connection the user deleted stays deleted (`HiddenFingerprints`). Before changing `ConnectionMerger`, the repositories or the delete flow, say what happens to a manual entry, a stale entry and a hidden one. Skill: `scan-and-merge`.

## Commands

```bash
dotnet build PgCredsHandler.sln                 # must be 0 errors, 0 warnings
dotnet test                                     # xunit, tests/PgCredsHandler.Tests
dotnet run --project src/PgCredsHandler.App     # Windows only
```

## Stack

- **WPF on .NET 8**, MVVM with **CommunityToolkit.Mvvm** 8.3.2, UI controls from **WPF-UI** 3.0.5 (Fluent, dark theme).
- **Npgsql** 8.0.5 (Integrations), **YamlDotNet** (Scanner), **xunit** 2.9 (Tests).
- The App targets `net8.0-windows10.0.19041.0` (needed for `UserConsentVerifier`) and sets `<RollForward>Major</RollForward>`: the dev machine has only the .NET 10 Desktop Runtime. Keep it unless the .NET 8 Desktop Runtime gets installed.
- Build on Windows. Core, Scanner, Integrations and Tests are plain `net8.0` and must stay OS-neutral; only Storage and App are Windows-specific.

## Solution layout

```
src/
  PgCredsHandler.Core          models, interfaces, pure parsing helpers, HostClassifier, ConnectionMerger
  PgCredsHandler.Scanner       ProjectWalker + IConfigParser implementations (.env, compose, appsettings)
  PgCredsHandler.Storage       JSON metadata repositories + WindowsCredentialStore
  PgCredsHandler.Integrations  connection strings, Npgsql test, DBeaver launch, pgAdmin export  [to build]
  PgCredsHandler.App           WPF views, viewmodels, app services                             [to build]
tests/
  PgCredsHandler.Tests         xunit                                                           [to build]
```

Dependencies point inward: everything may reference Core; Scanner, Storage and Integrations never reference each other or App; only App references everything. Layers, owners and the patterns in use: skill `architecture-contract`.

## Constraints

- **Passwords never touch JSON.** `SavedConnection` has no password field and must never get one. The password is written with `ICredentialStore.Save(connectionId, username, password)` under the target `PgCredsHandler:{connectionId}` and read back with `Read(connectionId)` at the moment it is needed.
- `InMemoryCredentialStore` exists only so non-Windows builds and tests work. Never use it as a fallback on Windows; if Credential Manager fails, the operation fails visibly.
- `DiscoveredConnection` is the only type that carries a password in a field, and only between the scanner and `ConnectionMerger`. `MergeResult.PasswordWrites` hands those passwords to the caller, who writes them to the credential store and drops the list.
- **Connection identity** is `ConnectionKey.Create(projectPath, host, port, database, username)` (`Fingerprint`). Do not add fields to it: a changed identity orphans stored passwords and resurrects hidden entries.
- **Scans are additive.** Only `ConnectionMerger.Merge` decides what a scan changes. Manual entries (`IsManual`) are never touched; missing ones become `IsStale` only if under a scanned root; a changed password sets `PasswordChangedAt`; `HiddenFingerprints` are skipped.
- **Deleting a scanned entry** removes its credential and adds its `Fingerprint` to `AppSettings.HiddenFingerprints` in the same user action. Deleting a manual entry just removes it and its credential.
- **Reveal and copy are gated.** Masked by default. With `RequireWindowsHello`, a Windows Hello check (`UserConsentVerifier`) unlocks for `UnlockMinutes` (5). A revealed password hides after `RevealSeconds` (30). A copied password is cleared after `ClipboardClearSeconds` (30), only if the clipboard still holds that value. These numbers are `AppSettings`, not constants.
- **Scanner scope:** only the user's `ScanRoots`; `ExcludedFolderNames` are skipped; junctions and symlinks are not followed; depth is capped (12). Never scan outside a chosen root, never read `%APPDATA%` or system folders.
- **Host safety:** every connection shows its `HostClassifier` kind. `Remote` is red and must be visible before the user connects.
- A malformed config file is skipped quietly. One bad file never stops a scan.
- Writes to `connections.json` and `settings.json` are atomic (temp file + `File.Replace`). Keep it that way for any new file the app owns.
- No network calls except `ConnectionTester` connecting to the database the user asked to test. No telemetry, no update checks.

## Vocabulary

One word per concept across code, UI text and these instructions.

- **Scan root**: a folder the user chose to scan (`AppSettings.ScanRoots`). Not "workspace" or "search path".
- **Project**: the nearest ancestor of a config file holding a project marker (`.git`, `package.json`, `go.mod`…), found by `ProjectWalker`. The UI groups by `ProjectName`.
- **Discovered connection**: what a scan found, with its password in memory (`DiscoveredConnection`). **Saved connection**: what the app stores, without a password (`SavedConnection`).
- **Fingerprint**: the identity string from `ConnectionKey.Create`. Not "hash" or "id". `Id` is the random GUID that keys the credential.
- **Manual entry**: a connection the user added or edited by hand (`IsManual`). Scans never change it.
- **Stale**: the last scan of its root did not find it (`IsStale`). It is kept, shown dimmed, and can be deleted by the user.
- **Hidden**: a scanned connection the user deleted; its fingerprint is in `HiddenFingerprints`.
- **Reveal** shows a password on screen; **copy** puts it on the clipboard; **unlock** is the Windows Hello check that permits both for `UnlockMinutes`.
- **Host kind**: `Local`, `DockerService`, `Private`, `Remote` (`HostClassifier`). Not "environment" or "prod/dev".
- **Source**: the file a connection came from (`SourceFile`), its format (`SourceKind`: `.env`, `docker-compose`, `appsettings.json`) and the key inside it (`SourceKey`).

## Before calling it done

Run `dotnet build PgCredsHandler.sln` (0 errors, 0 warnings) and `dotnet test`, and say what passed. For App changes, also launch the app and confirm it starts and the window title is "PgCreds Handler". Say plainly what you could not exercise (a Windows Hello prompt, a DBeaver that is not installed). After touching Storage, Integrations or any password flow, run the `secrets-reviewer` agent.

## Build order (what's left)

1. **Integrations**: `ConnectionStringBuilder`, `ConnectionTester`, `DbeaverLauncher`, `PgAdminExporter`. Skill: `db-client-integrations`.
2. **Tests**: `PostgresUrl`, `KeyValueConnectionString`, `EnvFileParser`, `DockerComposeParser`, `ConnectionMerger`, then Integrations. Skill: `xunit-testing`.
3. **App**: connection list grouped by project, search, reveal/copy with Windows Hello, settings page, add/edit dialog. Skill: `wpf-mvvm-ui`.
4. Later: more parsers (Django `settings.py`, Rails `database.yml`, Prisma, Spring), rescan on file change.

## Git

Public repository: `https://github.com/shahzaibali0/DataBaseCredsHandler`, default branch `main`. It is open source, so everything committed is public.

- Commit only when asked. Never `--no-verify`.
- Never commit user data: `connections.json`, `settings.json`, exports, a real `.env`, or any credential used for manual testing. The app keeps data in `%APPDATA%\PgCredsHandler\`, outside the repo; `.gitignore` is a safety net, not the plan.
- No personal paths, names or machine details in code, docs or tests. Use `C:\Users\you\Projects`-style examples.
- `.claude/rules`, `.claude/skills/*` and `.claude/agents` are committed as relative symlinks into `.agents/`. Keep them symlinks (git mode `120000`), never copies or junctions. Creating one on Windows needs Developer Mode and `git config core.symlinks true`.

## Compact instructions

When summarizing, keep these verbatim: which build-order step is in progress, which projects changed, whether `dotnet build` and `dotnet test` last passed, and any change to `SavedConnection`, `ConnectionKey` or the credential target format (these affect existing users' data).
