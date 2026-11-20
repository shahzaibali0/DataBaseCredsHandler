# DB Connect Hub: project overview

What this project is, what must never break, and the facts every change needs. Loaded every session. The index of everything in `.agents/` is [.agents/README.md](../../README.md).

## What is this

DB Connect Hub is a **Windows desktop tool** (WinUI 3, .NET 8) that each user runs on their own machine. It is open source (MIT). It scans folders the user chooses (e.g. `C:\Users\you\Projects`), finds Postgres credentials in each project's config files (`.env`, `docker-compose.yml`, `appsettings*.json`), stores them securely, shows them grouped by project, and opens a connection in DBeaver, pgAdmin or any other client with one click.

It is a personal password manager for databases that discovers its own entries. It is not a web app, it has no server, and it never touches anyone else's data.

## What matters most

Two failures defeat the point of the tool. Be careful wherever they can happen:

- **A password leaks outside its intended screen.** Passwords live only in Windows Credential Manager at rest. They never reach `connections.json`, `settings.json`, a pgAdmin export, a log, an exception message, a `ToString()`, a tooltip or search. The expanded Database file deliberately binds the full password because the owner wants it visible. Before adding any code that touches a password, say where the value goes and when it leaves memory. Skill: `credential-safety`.
- **A scan destroys the user's data.** Scans are additive. A manual entry is never overwritten, a connection that disappears is marked `IsStale`, never deleted, and a connection the user deleted stays deleted (`HiddenFingerprints`). Before changing `ConnectionMerger`, the repositories or the delete flow, say what happens to a manual entry, a stale entry and a hidden one. Skill: `scan-and-merge`.

## Commands

```bash
dotnet build DbConnectHub.sln                 # must be 0 errors, 0 warnings
dotnet test                                     # xunit, tests/DbConnectHub.Tests
dotnet run --project src/DbConnectHub.App     # Windows only; or double-click start.cmd
```

## Stack

- **WinUI 3** (Windows App SDK 1.8, unpackaged, self-contained) on **.NET 8**, MVVM with **CommunityToolkit.Mvvm** 8.3.2, controls from **CommunityToolkit.WinUI** 8.2 (SettingsCard/SettingsExpander, Segmented) and **WinUIEx** 2.9. Mica, NavigationView, theme follows Windows.
- **Npgsql** 8.0.5 (Integrations), **YamlDotNet** (Scanner), **xunit** 2.9 (Tests).
- The App targets `net8.0-windows10.0.19041.0` (needed for `UserConsentVerifier`) and sets `<RollForward>Major</RollForward>`: the dev machine has only the .NET 10 runtime. Keep it unless a .NET 8 runtime gets installed.
- Build on Windows. Core, Scanner, Integrations and Presentation are plain `net8.0` and must stay OS-neutral; Storage and App are Windows-specific. Tests target `net8.0-windows10.0.19041.0` so they can cover Storage; they do not reference the WinUI App.
- The App's assembly is named `DbConnectHub`, so the program is `DbConnectHub.exe`. Debug builds land in `src/DbConnectHub.App/bin/Debug/net8.0-windows10.0.19041.0/win-x64/`. Releases are one self-contained file built by `.github/workflows/release.yml` on a `v*` tag; `ci.yml` builds and tests every push.

## Solution layout

```
src/
  DbConnectHub.Core          models, interfaces, pure parsing helpers, HostClassifier, ConnectionMerger
  DbConnectHub.Scanner       ProjectWalker + IConfigParser implementations (.env, compose, appsettings)
  DbConnectHub.Storage       JSON metadata repositories + WindowsCredentialStore
  DbConnectHub.Integrations  connection strings, Npgsql test, DBeaver launch, pgAdmin export  [to build]
  DbConnectHub.Presentation  viewmodels (MainViewModel, rows, project groups), IDesktopShell; no UI framework
  DbConnectHub.App           WinUI 3: window, pages (Databases, Privacy & data, Settings), shell service, styles
tests/
  DbConnectHub.Tests         xunit: storage, merge, walker, scan workflow, viewmodels, privacy
```

Dependencies point inward: everything may reference Core; Scanner, Storage and Integrations never reference each other or App; Presentation references Core and Scanner; only App references everything. Layers, owners and the patterns in use: skill `architecture-contract`.

## Constraints

- The app reads only database connection details, dev-server ports, and the package names in `package.json` / `.csproj` (to show the stack and key packages). It never reads, stores or shows other values from `.env` or config files (API keys, tokens, secrets).
- **Writing to a project happens in one case only**: the user confirms a new dev-server port. `ServerPorts` then replaces that one port value where the scan read it (the dev script in `package.json`, the first `PORT=` line of the `.env` file, the address in `launchSettings.json`), or adds `--port N` to a plain dev script for a framework default. Every other byte of the file is kept, the write is atomic (temp file + `File.Replace`), and a file that isn't valid UTF-8 is left alone. Never add another write to a project without the owner's decision.
- **Starting a project's server** happens only when the user presses Run: `ServerLauncher` opens a visible console (`cmd.exe /k` from System32) in the app folder with a fixed command (`npm`/`pnpm`/`yarn`/`bun run dev|start`, or `dotnet run`). No value from a project file reaches the command line, and every word is checked to be plain. The user stops the server by closing that window.
- **Passwords never touch JSON.** `SavedConnection` has no password field and must never get one. The password is written with `ICredentialStore.Save(connectionId, username, password)` under the target `DbConnectHub:{connectionId}` and read back with `Read(connectionId)` at the moment it is needed.
- `InMemoryCredentialStore` exists only so non-Windows builds and tests work. Never use it as a fallback on Windows; if Credential Manager fails, the operation fails visibly.
- `DiscoveredConnection` is the only type that carries a password in a field, and only between the scanner and `ConnectionMerger`. `MergeResult.PasswordWrites` hands those passwords to the caller, who writes them to the credential store and drops the list.
- **Connection identity** is `ConnectionKey.Create(projectPath, host, port, database, username)` (`Fingerprint`). Do not add fields to it: a changed identity orphans stored passwords and resurrects hidden entries.
- **Scans are additive.** Only `ConnectionMerger.Merge` decides what a scan changes. Manual entries (`IsManual`) are never touched; missing ones become `IsStale` only if under a scanned root; a changed password sets `PasswordChangedAt`; `HiddenFingerprints` are skipped.
- **Deleting a scanned entry** removes its credential and adds its `Fingerprint` to `AppSettings.HiddenFingerprints` in the same user action. Deleting a manual entry just removes it and its credential.
- **Database files show complete credentials.** Opening a Database file shows host, port, database, username and the full password without masking or Windows Hello. The password is read from Credential Manager only while that file is open, then cleared when it collapses, the page changes, a rescan replaces rows, or the window hides. It must never be added to search, tooltips, JSON, logs or exception messages.
- **Scanner scope:** the user's `ScanRoots`, or, when none are chosen (the default), every fixed local drive from `LocalDrives.ListFixedDriveRoots` (USB, network and CD drives never). `ExcludedFolderNames`, dot-folders and `$`-folders are skipped; junctions and symlinks are not followed; depth is capped (12). Never read `%APPDATA%` or system folders, with one exception: when a scanned `.csproj` declares a `UserSecretsId`, that project's own `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` is read for database connection strings only (every other value in it is ignored). A scan only starts when the user presses the button.
- **Host safety:** every connection shows its `HostClassifier` kind. `Remote` is red and must be visible before the user connects.
- A malformed config file is skipped quietly. One bad file never stops a scan.
- Writes to `connections.json` and `settings.json` are atomic (temp file + `File.Replace`). Keep it that way for any new file the app owns.
- **Privacy promise:** nothing the app finds ever leaves the PC. No network calls except Integrations connecting to a database the user asked to test or open, and `LocalPortProbe` opening and closing a connection to a port on this PC (127.0.0.1 / ::1 only, nothing sent) to show whether a local dev server or database is running. A remote host is never contacted for status. No telemetry, no update checks, no crash upload. `PrivacyTests` fails the build if Core, Scanner, Storage or App reference a networking library; keep it passing and keep the promise visible in the app's footer and the README.

## Vocabulary

One word per concept across code, UI text and these instructions.

- **Scan root**: a folder the user chose to scan (`AppSettings.ScanRoots`). Not "workspace" or "search path".
- **Project**: the nearest ancestor of a config file holding a project marker (`.git`, `package.json`, `go.mod`…), found by `ProjectWalker`. The UI groups by `ProjectName`.
- **Discovered connection**: what a scan found, with its password in memory (`DiscoveredConnection`). **Saved connection**: what the app stores, without a password (`SavedConnection`).
- **Fingerprint**: the identity string from `ConnectionKey.Create`. Not "hash" or "id". `Id` is the random GUID that keys the credential.
- **Manual entry**: a connection the user added or edited by hand (`IsManual`). Scans never change it.
- **Stale**: the last scan of its root did not find it (`IsStale`). It is kept, shown dimmed, and can be deleted by the user.
- **Hidden**: a scanned connection the user deleted; its fingerprint is in `HiddenFingerprints`.
- **Database file**: the clickable project entry that expands to show every connection value, including the full password.
- **Host kind**: `Local`, `DockerService`, `Private`, `Remote` (`HostClassifier`). Not "environment" or "prod/dev".
- **Source**: the file a connection came from (`SourceFile`), its format (`SourceKind`: `.env`, `docker-compose`, `appsettings.json`) and the key inside it (`SourceKey`).

## Before calling it done

Run `dotnet build DbConnectHub.sln` (0 errors, 0 warnings) and `dotnet test`, and say what passed. For App changes, also launch the app and confirm it starts and the window title is "DB Connect Hub". Say plainly what you could not exercise (for example, a DBeaver that is not installed). After touching Storage, Integrations or any password flow, run the `secrets-reviewer` agent.

## Build order (what's left)

Done: the scan module (first-run screen, one-button whole-PC scan with progress and cancel, merge and save, list grouped by project with host badges and search, privacy footer), the data-loss fix, `start.cmd`, CI and release workflows, 37 tests.

1. **Enforce the rules in the build**: `.editorconfig`, analyzers, warnings as errors.
2. **Parser tests**: `PostgresUrl`, `KeyValueConnectionString`, `EnvFileParser`, `DockerComposeParser`, `AppSettingsJsonParser`, `HostClassifier`. Skill: `xunit-testing`.
3. **Integrations, then auto-connect**: `ConnectionStringBuilder`, `ConnectionTester`, `DbeaverLauncher`, `PgAdminExporter`, then one-click "Open in DBeaver / pgAdmin" from a row. Skill: `db-client-integrations`.
4. **Password display**: complete credentials in an expandable Database file. Skills: `credential-safety`, `desktop-ui`.
5. **Settings and editing**: settings page, add/edit dialog, delete with hidden fingerprints.
6. Later: more parsers (Django `settings.py`, Rails `database.yml`, Prisma, Spring), rescan on file change.

## Git

Public repository: `https://github.com/shahzaibali0/DataBaseCredsHandler`, default branch `main`. It is open source, so everything committed is public.

- Commit only when asked. Never `--no-verify`.
- Never commit user data: `connections.json`, `settings.json`, exports, a real `.env`, or any credential used for manual testing. The app keeps data in `%APPDATA%\DbConnectHub\`, outside the repo; `.gitignore` is a safety net, not the plan.
- No personal paths, names or machine details in code, docs or tests: never the owner's real project, database or folder names. Use made-up examples (`ShopRepo`, `shop_db`, `C:\Users\you\Projects`).
- `.githooks/pre-commit` (enabled with `git config core.hooksPath .githooks`) enforces this: it blocks app data files, real `.env` files, and added lines containing anything from the owner's `%APPDATA%\DbConnectHub` data. Never bypass it with `--no-verify`.
- `.claude/rules`, `.claude/skills/*` and `.claude/agents` are committed as relative symlinks into `.agents/`. Keep them symlinks (git mode `120000`), never copies or junctions. Creating one on Windows needs Developer Mode and `git config core.symlinks true`.

## Compact instructions

When summarizing, keep these verbatim: which build-order step is in progress, which projects changed, whether `dotnet build` and `dotnet test` last passed, and any change to `SavedConnection`, `ConnectionKey` or the credential target format (these affect existing users' data).
