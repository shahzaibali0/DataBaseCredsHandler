# Decisions

One line each: the choice, then why. Rules that follow from these are in `.agents/rules/`; this file keeps the reasons.

## Setup decisions

### Stack

- WPF on .NET 8 with CommunityToolkit.Mvvm and WPF-UI, because it is a Windows-only desktop tool that needs Windows Hello and Credential Manager, and the toolkit's source generators remove MVVM boilerplate.
- The App sets `<RollForward>Major</RollForward>`, because the dev machine has only the .NET 10 Desktop Runtime and the app must still run there.
- Core, Scanner and Integrations target plain `net8.0`, because keeping them OS-neutral lets them be built and tested anywhere and keeps Windows APIs in the two projects that need them.

### Layers

- Five projects (Core, Scanner, Storage, Integrations, App) with references pointing only inward, because project references enforce the layering at build time instead of in review.
- Core is pure (no I/O, `now` and password reads passed in), because the merge and parsing rules are the riskiest logic and pure functions can be tested without files or OS stores.
- One `IConfigParser` per config format in a registry, because new formats (Django, Rails, Prisma, Spring) are expected and each should be one new file.
- Manual wiring in `App.xaml.cs` instead of a DI container, because the app has a handful of services; revisit when the root gets hard to read.

### Secrets

- Passwords only in Windows Credential Manager, metadata in `%APPDATA%\PgCredsHandler\connections.json`, because Credential Manager is DPAPI-protected per Windows user and the JSON can then be read or backed up without exposing anything.
- The credential key is the random `SavedConnection.Id`, not the fingerprint, because editing a manual entry's host or port must keep its password.
- Reveal and copy are a wanted feature, gated by Windows Hello with a 5-minute unlock, 30-second reveal and 30-second clipboard clear, because it is the user's own machine and credentials, but a shoulder-surfer or a clipboard manager shouldn't get them for free.
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
- Each user's data stays on their own PC (`%APPDATA%\PgCredsHandler\` + Credential Manager) and is created on first run, because the repo is public and nobody's connections may ever be pushed; `.gitignore` also lists the data file names as a safety net.
- `.claude/` entries are committed as relative symlinks into `.agents/`, not junctions or copies, because git stores symlinks and every clone then gets one copy of the instructions.

_New decisions go here, one line each: the choice, then why._

## Open

- An `.editorconfig` with the C# naming and formatting rules plus `TreatWarningsAsErrors` and .NET analyzers in a `Directory.Build.props`, so the code-style rules are checked by the build instead of by review.
- A Claude Code `PostToolUse` hook that runs `dotnet format` on each edited `.cs` file, like the Next.js template's format-on-edit hook.
- Logging: whether the desktop app needs `ILogger` at all, and if so where the log goes (a rolling file under `%LOCALAPPDATA%`), given nothing secret may reach it.
- CRED_PERSIST_ENTERPRISE (roams with a domain profile) versus CRED_PERSIST_LOCAL_MACHINE (stays on this PC). See WHAT_TO_FIX item 2.
