# PgCreds Handler

A free, open-source Windows app that finds the Postgres connection details scattered across your projects and keeps them in one safe place, on your own PC.

If you have many projects, each with its own database in a `.env`, `docker-compose.yml` or `appsettings.json`, you no longer have to dig through those files to find a host, port, database name, user and password. Opening a database in DBeaver or pgAdmin with one click is coming next.

> **Status: early development.** Scanning works end to end: one button finds the databases on your PC, saves them securely and lists them by project. Opening a connection in DBeaver or pgAdmin and showing/copying passwords are being built next. See the [Roadmap](#roadmap).

## How it works

1. **Start the app.** The first time, it shows one button: **Scan my PC for databases**.
2. **Click it.** The app looks through your local drives, or only the folders you choose, for project config files. It skips Windows, Program Files, AppData, `node_modules`, `.git` and other non-project folders, and never touches USB or network drives. You see live progress and can cancel at any time; a cancelled scan changes nothing.
3. **See every database**, saved on your PC and grouped by project. Search by project, host, database or user. Click **Rescan** whenever you like.

## What it finds

- **`.env` files** (`.env`, `.env.local`, `.env.development`, …): `DATABASE_URL`-style URLs and groups like `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD`, including quoted and multi-line values and `${VAR}` / `${VAR:-default}` references.
- **`docker-compose.yml`** Postgres services: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, and the published port (`"5433:5432"` → `localhost:5433`).
- **.NET `appsettings*.json`** connection strings.

The same database found in several files of one project is shown once. Hosts that look like remote or production servers get a **red badge**, so you notice before you connect.

## Your data stays on your PC

**PgCreds Handler never sends your data anywhere.** It has no server, no account, no telemetry and no update check. What it finds is saved only on your own PC, in your own Windows account, and never in this repository.

You don't have to take our word for it:

- The code that scans, stores and shows your data doesn't use any networking library, and an automated test ([`PrivacyTests.cs`](tests/PgCredsHandler.Tests/PrivacyTests.cs)) fails the build if it ever does. The only network connections will come from the upcoming database-client features, and only to a database you click to test or open.
- It's open source: search the code for `HttpClient` or `Socket` and see for yourself.
- The app always shows where your data is, with an **Open data folder** button.

Where things are kept:

| What | Where |
| --- | --- |
| Connection details (no passwords) | `%APPDATA%\PgCredsHandler\connections.json` |
| Settings (folders to scan, timers) | `%APPDATA%\PgCredsHandler\settings.json` |
| Passwords | Windows Credential Manager, entries named `PgCredsHandler:<id>`. Never in a file. |

`%APPDATA%` is usually `C:\Users\<you>\AppData\Roaming`; paste it into File Explorer's address bar to open it. The folder is created the first time the app runs.

Your data is also protected from the app itself:

- **Rescans never delete.** A database that's no longer found is marked **No longer found**, not removed.
- **A damaged data file is never overwritten.** If `connections.json` or `settings.json` can't be read, the app saves a copy next to it (`….corrupt-<date>`), tells you, and saves nothing until you fix or delete the file.

### Start over or uninstall

1. Close the app.
2. Delete the folder `%APPDATA%\PgCredsHandler`.
3. Open **Credential Manager** (Start → "Credential Manager") → **Windows Credentials**, and remove the entries starting with `PgCredsHandler:`.
4. If you downloaded the `.exe`, delete it. The app installs nothing else.

## Get started

### Download (easiest)

Once the first release is published, get `PgCredsHandler.exe` from the [Releases](https://github.com/shahzaibali0/DataBaseCredsHandler/releases) page and double-click it. Nothing to install, no .NET needed.

Windows may show a SmartScreen warning because the file isn't code-signed yet: choose **More info → Run anyway**.

### Run from the source code

You need Windows 10 (version 2004 or later) or Windows 11, and the [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or newer.

```powershell
git clone https://github.com/shahzaibali0/DataBaseCredsHandler.git
cd DataBaseCredsHandler
.\start.cmd
```

`start.cmd` checks that the .NET SDK is installed, builds the app and starts it. You can also double-click it in File Explorer. Prefer the command line? `dotnet run --project src/PgCredsHandler.App` does the same. Visual Studio 2022 and VS Code (with C# Dev Kit) work too: run the `PgCredsHandler.App` project.

### Optional, for upcoming features

- [DBeaver](https://dbeaver.io/) or [pgAdmin](https://www.pgadmin.org/), to open a database with one click.
- Windows Hello (PIN, fingerprint or face), to protect showing and copying a password.

## Roadmap

- [x] One-button scan of the whole PC (or chosen folders), with progress and cancel
- [x] Connections saved securely and listed by project, with search and remote-host warnings
- [x] Damaged data files backed up, never overwritten
- [ ] One-click connect: open a database in DBeaver or pgAdmin with everything filled in, test a connection, copy as URL / JDBC / ADO.NET
- [ ] Show/copy a password, protected by Windows Hello
- [ ] Settings, add/edit and delete (edited entries are never overwritten by a rescan; deleted ones never come back)
- [ ] More config formats: Django `settings.py`, Rails `database.yml`, Prisma, Spring

## Contributing

Issues and pull requests are welcome.

**Project layout**

```
src/
  PgCredsHandler.Core          models, parsing helpers, merge rules (no file or network access)
  PgCredsHandler.Scanner       finds config files and reads credentials out of them; the scan workflow
  PgCredsHandler.Storage       the JSON data files and Windows Credential Manager
  PgCredsHandler.Integrations  DBeaver, pgAdmin and connection test (in progress)
  PgCredsHandler.Presentation  what each screen shows and does (viewmodels), tested without a window
  PgCredsHandler.App           the WinUI 3 window and pages
tests/
  PgCredsHandler.Tests         xunit tests
```

**Built with** WinUI 3 (Windows App SDK) on .NET 8, CommunityToolkit.Mvvm, CommunityToolkit.WinUI controls and WinUIEx. The app is unpackaged and ships as one self-contained `.exe`.

**Build and test**

```powershell
dotnet build PgCredsHandler.sln
dotnet test
```

Every push and pull request is built and tested on GitHub Actions ([`ci.yml`](.github/workflows/ci.yml)).

**Ground rules**

- Never commit real credentials, `.env` files or your `connections.json` / `settings.json`. `.gitignore` blocks the usual names, but check your diff.
- Keep the privacy promise: no networking outside `PgCredsHandler.Integrations`. `PrivacyTests` will fail if you add any.
- The design rules, coding style and decisions are in [.agents/](.agents/README.md). They're written for AI coding agents and work just as well for people. Start with [.agents/rules/project/overview.md](.agents/rules/project/overview.md).
- Contributing with an AI coding agent on Windows: turn on **Developer Mode** and clone with `git clone -c core.symlinks=true …`, so the `.claude/` links into `.agents/` work. Building and running the app doesn't need this.

**Publishing a release** (maintainers)

Push a version tag, for example `git tag v0.1.0` then `git push origin v0.1.0`. GitHub Actions ([`release.yml`](.github/workflows/release.yml)) runs the tests, builds the single-file `PgCredsHandler.exe` and attaches it to a new release.

## License

[MIT](LICENSE)
