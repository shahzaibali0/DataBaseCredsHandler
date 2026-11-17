# PgCreds Handler

A Windows desktop app that finds the Postgres connection details scattered across your project folders, keeps them safe, and opens any of them in DBeaver, pgAdmin or another database client with one click.

If you have many projects, each with its own database in a `.env`, `docker-compose.yml` or `appsettings.json`, you no longer have to dig through those files to find a host, port, database name, user and password.

> **Status: early development.** The scanner, the config-file parsers and the secure storage work. The window you see when you run the app is still a placeholder; the connection list, the DBeaver/pgAdmin buttons and tests are being built now. See [Roadmap](#roadmap).

## What it does

- **Scans folders you choose** (for example `C:\Users\you\Projects`) and every project inside them, skipping `node_modules`, `.git`, `venv`, `bin`, `obj` and similar.
- **Finds Postgres credentials** in:
  - `.env` files, including quoted and multi-line values and `${VAR}` / `${VAR:-default}` references
  - `docker-compose.yml` Postgres services (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, published ports such as `5433:5432`)
  - .NET `appsettings*.json` connection strings
- **Stores them securely**: connection details in a local file, passwords in Windows Credential Manager. Never a password in plain text.
- **Shows them grouped by project**, with a red badge on hosts that look like remote or production servers.
- **Keeps your edits safe**: rescanning never overwrites an entry you edited, never deletes anything (missing ones are marked stale), and never brings back one you deleted.

## Requirements

- Windows 10 version 2004 or later, or Windows 11
- [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or newer. The app targets .NET 8 and also runs on newer runtimes.
- Optional: [DBeaver](https://dbeaver.io/) or [pgAdmin](https://www.pgadmin.org/) to open connections in
- Optional: Windows Hello (PIN, fingerprint or face) to protect "show password"

## Get started

```powershell
git clone https://github.com/shahzaibali0/DataBaseCredsHandler.git
cd DataBaseCredsHandler
dotnet build PgCredsHandler.sln
dotnet run --project src/PgCredsHandler.App
```

You can also open the folder in Visual Studio 2022, or in VS Code with the C# Dev Kit extension, and run the `PgCredsHandler.App` project. It must be built on Windows.

## Your data stays on your PC

Nothing you scan is ever sent anywhere or stored in this repository. The first time the app runs, it creates its own data folder for your Windows account:

| What | Where |
| --- | --- |
| Connection details (no passwords) | `%APPDATA%\PgCredsHandler\connections.json` |
| Settings (folders to scan, timers) | `%APPDATA%\PgCredsHandler\settings.json` |
| Passwords | Windows Credential Manager, entries named `PgCredsHandler:<id>` |

`%APPDATA%` is usually `C:\Users\<you>\AppData\Roaming`. Paste it into File Explorer's address bar to open it.

The app makes no network connections except to a database you explicitly ask it to test.

### Start over or uninstall

1. Close the app.
2. Delete the folder `%APPDATA%\PgCredsHandler`.
3. Open **Credential Manager** (Start → "Credential Manager") → **Windows Credentials**, and remove the entries starting with `PgCredsHandler:`.

## Run the tests

```powershell
dotnet test
```

## Roadmap

1. DB-client integrations: copy as URL / JDBC / ADO.NET, test connection, open in DBeaver, export to pgAdmin
2. Tests for the parsers and the merge logic
3. The main window: connections grouped by project, search, show/copy password with Windows Hello, settings, add/edit
4. More config formats: Django `settings.py`, Rails `database.yml`, Prisma, Spring

## Contributing

Issues and pull requests are welcome.

- Never commit real credentials, `.env` files or your `connections.json` / `settings.json`. `.gitignore` blocks the usual names, but check your diff.
- The design rules, coding style and decisions are in [.agents/](.agents/README.md). They're written for AI coding agents and work just as well for people. Start with [.agents/rules/project/overview.md](.agents/rules/project/overview.md).
- Contributing with an AI coding agent on Windows: turn on **Developer Mode** and clone with `git clone -c core.symlinks=true …`, so the `.claude/` links into `.agents/` work. Building and running the app doesn't need this.

## License

[MIT](LICENSE)
