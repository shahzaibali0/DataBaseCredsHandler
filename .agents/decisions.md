# Decisions

One line each: the choice, then why. Rules that follow from these are in `.agents/rules/`; this file keeps the reasons.

## Setup decisions

### Stack

- WinUI 3 (Windows App SDK 1.8) on .NET 8 with CommunityToolkit.Mvvm, because it is a Windows-only desktop tool that needs Credential Manager, WinUI 3 gives Microsoft's own native Windows 11 look (Mica, NavigationView), and the toolkit's source generators remove MVVM boilerplate. Proton VPN's Windows app made the same move from WPF to WinUI 3. (The first UI was WPF with WPF-UI; replaced on 2026-11-17 while it was still one screen, the cheapest time to switch.)
- Third-party UI: CommunityToolkit.WinUI controls (SettingsCard/SettingsExpander, Segmented) and WinUIEx window helpers, because they are free, MIT-licensed and the same set Proton VPN uses; commercial suites (Telerik, Syncfusion, DevExpress) were rejected because their licences don't fit an MIT project anyone can clone and build.
- The plain WinUI `Window` with WinUIEx helpers, not WinUIEx's `WindowEx` base class, because `WindowEx`'s obsolete `Icon` member puts warnings into generated code and the build must stay at 0 warnings without suppressions.
- Viewmodels live in a separate `DbConnectHub.Presentation` project (plain `net8.0`, no UI framework), because WinUI projects are hard to unit-test and the privacy test must cover the code that shows data; the App holds only XAML and a small shell service.
- Unpackaged, self-contained Windows App SDK, published as one file with `EnableMsixTooling` (build tooling only), because users should download one `.exe` and double-click it: no MSIX, no Store, no runtime install.
- The App sets `<RollForward>Major</RollForward>`, because the dev machine has only the .NET 10 Desktop Runtime and the app must still run there.
- Core, Scanner and Integrations target plain `net8.0`, because keeping them OS-neutral lets them be built and tested anywhere and keeps Windows APIs in the two projects that need them.

### Layers

- Five projects (Core, Scanner, Storage, Integrations, App) with references pointing only inward, because project references enforce the layering at build time instead of in review.
- Core is pure (no I/O, `now` and password reads passed in), because the merge and parsing rules are the riskiest logic and pure functions can be tested without files or OS stores.
- One `IConfigParser` per config format in a registry, because new formats (Django, Rails, Prisma, Spring) are expected and each should be one new file.
- Manual wiring in `App.xaml.cs` instead of a DI container, because the app has a handful of services; revisit when the root gets hard to read.
- The scan-and-save operation (`ScanWorkflow`) lives in the Scanner project, using only Core interfaces, because it can then be tested end to end on a temp folder without a window.
- Tests target `net8.0-windows10.0.19041.0`, because Storage and the App are Windows-only and the data-loss and privacy tests must cover them.

### Secrets

- Passwords only in Windows Credential Manager, metadata in `%APPDATA%\DbConnectHub\connections.json`, because Credential Manager is DPAPI-protected per Windows user and the JSON can then be read or backed up without exposing anything.
- The credential key is the random `SavedConnection.Id`, not the fingerprint, because editing a manual entry's host or port must keep its password.
- Database files show complete connection details, including the full password without masking or Windows Hello, because the owner explicitly wants every value immediately visible when the file is opened; passwords remain stored only in Windows Credential Manager at rest.
- The pgAdmin export never contains a password, because pgAdmin prompts for it and the export file may be shared or synced.
- DBeaver gets the password on its command line, because its `-con` option is the only one-click way in; other processes of the same user can see it while DBeaver starts, an accepted trade-off with an "open without password" option.

### Scans

- Connection identity is project path + host + port + database + user, because the same database appears in several files of one project and must collapse to one entry, while two projects using the same local server are different entries.
- Scans are additive: manual entries untouched, missing entries marked stale, deleted entries remembered as hidden fingerprints, because a rescan must never destroy something the user added or chose to remove.

### Rules

- Every agent instruction lives in `.agents/` (rules, skills, subagents, this file) and the root `AGENTS.md`/`CLAUDE.md` only point there, because two places giving instructions drift apart.
- Agent rules live in `.agents/rules/` (`project/` for this app, `common/` for any stack, `dotnet/` for this one), with `.claude/rules` linking to it, because the common rules are shared with the owner's Next.js projects and other agent tools read `.agents/`.
- Project-specific rules (layers, password handling, parser and merge rules, specs) are skills in `.agents/skills/`, because they are long and only needed for the area being changed.

## Project decisions

- Open source under the MIT license, published at github.com/shahzaibali0/DataBaseCredsHandler, because it is a general developer tool and MIT lets anyone use, change and ship it with only the notice kept.
- Each user's data stays on their own PC (`%APPDATA%\DbConnectHub\` + Credential Manager) and is created on first run, because the repo is public and nobody's connections may ever be pushed; `.gitignore` also lists the data file names as a safety net.
- `.claude/` entries are committed as relative symlinks into `.agents/`, not junctions or copies, because git stores symlinks and every clone then gets one copy of the instructions.

- The main button scans every fixed local drive by default, with "Choose folders instead" as the alternative, because users want one click to find everything; USB, network and CD drives are left out because they are slow and may not be the user's.
- A scan only runs when the user presses the button, never automatically on first launch, because walking a whole PC should be the user's choice.
- Two ways to start: a self-contained single-file `DbConnectHub.exe` on GitHub Releases (built by `release.yml` on a `v*` tag) for users, and `start.cmd` for people who clone, because neither should need to know `dotnet` commands.
- The privacy promise (nothing leaves the PC) is shown in the app's footer and the README and enforced by `PrivacyTests`, which fails if Core, Scanner, Storage or App reference a networking library, because an open-source tool that reads credentials must be easy to trust and easy to audit.
- A data file that exists but can't be read is backed up and never saved over, and the app disables scanning until the user fixes it, because silently starting empty would erase manual entries and resurrect deleted ones.

- A project with a real config file ignores its example files, and template placeholders (`USER@HOST:PORT/DATABASE`) are never saved, because one backend folder should show the one database it really uses; found on the owner's first real scan, where every project showed a second, fake `DATABASE` entry from `.env.production.example`.
- The app reads only database connection details and local dev-server ports, never other `.env` or config values, because the owner only wants databases and local URLs and reading API keys, tokens or other secrets was out of scope.

- Projects are square cards that open a project window (ports, database connections, stack, key packages), because the owner wants to see a project at a glance and everything about it in one place, like a reference design the owner provided.
- Stack and key packages come from package names in `package.json` / `.csproj`, filtered through a curated `PackageCatalog`, because the owner wants the important third-party choices, not every dependency; reading package names exposes no secrets.
- The database password is masked with an eye button in the project window, because the owner asked for an eye to see credentials; it is read from Credential Manager only when the eye or Copy is clicked and masked again when the window closes.
- "Open in" buttons: DBeaver connects directly (`-con`, password on its command line, an accepted trade-off), pgAdmin gets the server imported with its own `setup.py load-servers` (no password in the file, so pgAdmin asks once), DbVisualizer is started and the connection URL copied, because only DBeaver offers a direct connect option and passwords must not be written to files. pgAdmin is also found where the PostgreSQL installer bundles it (`Program Files\PostgreSQL\<version>\pgAdmin 4`).
- pgAdmin gets a "DB Connect Hub" group with **one server per opened database**, named `Project · database` and limited to that database (`DBRestriction`, type `databases`). The owner wants "Open in pgAdmin" to show only that project's database: a server is one Postgres login and would otherwise list every database in that Postgres. This replaced one server per login. The password key covers host, port, user and database, so it picks the right project's password. `PgAdminServers` runs `dump-servers` first and imports only when that database isn't already in the group, because `load-servers` never skips duplicates. A server the user added in another group doesn't count, so ours always appears in the group. The script runs with pgAdmin's `runtime` folder first on the PATH, because its Python finds `libpq.dll` only there. Success is read from the "Added … Server(s)" line, because `setup.py` prints its errors and still exits 0. Only pgAdmin 7+ (`dump-servers`/`load-servers` subcommands) is supported.
- pgAdmin signs in by itself (owner's choice over "pgAdmin asks once"). Each server it gets has a `PasswordExecCommand` that runs `DbConnectHub.exe --pgadmin-password <key>`, which prints the login's password from Credential Manager. This way no password is ever written to pgAdmin's file. The key is hex of host/port/user, because pgAdmin runs the command through cmd.exe and config values must not reach a shell. An app path containing `"`, `%`, `!` or `^` gets no command, and pgAdmin asks instead (`!` expands inside quotes when delayed expansion is on). Never use pgAdmin's `%HOSTNAME%`/`%USERNAME%` placeholders, because they would put scanned values into cmd. Only printable ASCII passwords without outer spaces are sent, because pgAdmin strips the output and its Python may decode it as cp1252 or UTF-8; anything else pgAdmin asks for. When one login has different saved passwords in different projects, nothing is sent rather than a guess. A same-login entry with a missing or different command (older version, or the app moved) can't be edited by setup.py, so the user is asked to delete it once. The exe has a hand-written `Main` (`DISABLE_XAML_GENERATED_MAIN`), so this answer runs before any window or single-instance check.
- `PgAdminServers` reuses its last `dump-servers` answer while `pgadmin4.db` (and its `-wal` file) keep the same size and write time, and forgets it before every import; opens run one at a time (`SemaphoreSlim`), so two clicks on the same database never both add it, because one dump takes 10 to 45 seconds on the owner's PC and any change in pgAdmin (a deleted server) rewrites that file. The stamp is taken before the dump, so a change made during it is never missed. The answer is kept only in memory, never on disk.
- Closing the window to the tray **hides** it and then runs one aggressive, compacting GC (`TrayMemory.Release`), which hands the freed .NET memory back to Windows; the window is built only the first time it is opened, and a `--background` start does the same GC after its startup scan. Closing and rebuilding the window was measured and rejected: WinUI kept about 7 MB per closed window, so the app grew on every open. The working set is **not** trimmed (`SetProcessWorkingSetSize`), because that pages dirty memory out to `pagefile.sys`, and freed memory can still hold a password that was shown or scanned (.NET can't wipe a string, and WinUI's text elements keep native copies). The trim gave 5 to 40 MB of working set; without it, measured in the tray: about 75 MB in Task Manager (private working set) and about 104 MB private, flat over repeated open/close cycles. The rest of the ~190 MB working set is WinUI code loaded from disk, which Windows drops for free when it needs the RAM.
- Each dev server has **Run** and **Change port** (owner's request). Run opens a visible console (`cmd /k`) instead of running the server hidden inside the app, because the user needs to see its output and errors, and closing the window is a stop button that always works; cmd is needed because npm, pnpm and yarn are `.cmd` scripts. The package manager comes from the lock file, searched from the app folder up to its project, because monorepos keep one lock file at the root. Change port edits the file the port was read from, not a setting inside this app, because the owner wants the project itself to run on the new port. It shares `AppDetector`'s rules so it edits the same place the scan reads, refuses rather than guesses (a script key found twice, a script running several commands, a backend whose port is in its code, a file that isn't UTF-8), and refuses a port another program is listening on right now. Ports below 1024 are refused, because they need admin rights on many setups. After the secrets review: the temp file a port change writes is a full copy of the file (of an .env, every secret), so its name keeps the last extension (`.env.dbconnecthub-tmp.local`) to stay under the same ignore rules, a stale one is cleared first or the change is refused, and a copy that can't be deleted is named in the message. Only `scheme://host:port` of a launchSettings URL is replaced, so a path (`/swagger`) stays. A symbolic link is refused, because replacing it would cut it off from the file it shares; a hard link isn't detected (.NET has no link count). The preview builds no edited text. The server window gets `NoDefaultCurrentDirectoryInExePath=1`, so cmd finds npm on PATH only, not in the project folder. cmd's AutoRun is kept (no `/d`), because it is the user's own setting and no project value can reach it.

- Live status dots (running / not running) come from a 300 ms connect to the port on localhost only, every 15 seconds and after scans, because the owner wants to see at a glance which projects are up; only loopback is ever probed, so a remote or production server is never contacted, and the check lives in Integrations so `PrivacyTests` still guards everything else.

- A .NET project's User Secrets file is read for its database connection strings, because that is where .NET keeps a dev connection string so it never reaches git; only the file named by the project's own `UserSecretsId` is opened, and only connection strings are taken (found when the owner's .NET project showed 0 databases).
- `.env` files are read for libpq's own variable names (`PGHOST`, `PGUSER`…, no underscore) and for .NET's `ConnectionStrings__X=Host=…` form, and template passwords (`YOUR_…`, `<…>`, `changeme`) are not stored as passwords.

_New decisions go here, one line each: the choice, then why._

## Open

- Auto-connect: after the integrations exist, one click on a row opens it in DBeaver or pgAdmin with everything filled in; decide whether to also detect installed clients and offer "Open in…" only for those.
- Code signing for the release exe, so Windows SmartScreen doesn't warn first-time users.

- An `.editorconfig` with the C# naming and formatting rules plus `TreatWarningsAsErrors` and .NET analyzers in a `Directory.Build.props`, so the code-style rules are checked by the build instead of by review.
- A Claude Code `PostToolUse` hook that runs `dotnet format` on each edited `.cs` file, like the Next.js template's format-on-edit hook.
- Logging: whether the desktop app needs `ILogger` at all, and if so where the log goes (a rolling file under `%LOCALAPPDATA%`), given nothing secret may reach it.
- CRED_PERSIST_ENTERPRISE (roams with a domain profile) versus CRED_PERSIST_LOCAL_MACHINE (stays on this PC). See WHAT_TO_FIX item 1.
