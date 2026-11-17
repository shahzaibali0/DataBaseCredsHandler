# PgCreds Handler

A desktop tool that scans your project folders for Postgres connection credentials, stores them
securely, and opens them in DBeaver / pgAdmin — so you stop digging through `.env` files by hand.

> Windows desktop app (WPF, .NET 8). Scaffold stage: the scanner and storage layers are implemented;
> the UI and DB-client integrations are stubbed out and ready to build.

## Getting started (VS Code)

1. Install the **[.NET 8 SDK](https://dot.net)**. Check: `dotnet --version`.
2. Install the **C# Dev Kit** extension.
3. Open this folder in VS Code.
4. Build: `dotnet build` (or the **build** task). Run: `dotnet run --project src/PgCredsHandler.App`.

You must build on **Windows** — WPF and the Credential Manager APIs are Windows-only.

## What works now
- Recursive project scanner that skips `node_modules`, `.git`, `venv`, etc.
- Parsers for `.env` (with `${VAR}` expansion), `docker-compose.yml`, and `appsettings.json`.
- Secure storage: metadata as JSON, passwords in Windows Credential Manager.
- Merge logic: de-dupe, stale detection, password-change detection, manual entries.
- Host classifier (flags remote/production hosts).

## What's left
See `.agents/rules/project/overview.md` → "Build order". Short version: DB-client integrations, the WPF UI, and tests.

## Where data lives
`%APPDATA%\PgCredsHandler\` — `connections.json` (no passwords) and `settings.json`.
Passwords: Windows Credential Manager, entries prefixed `PgCredsHandler:`.

## Project layout
Agent instructions, rules and design decisions are in [.agents/](.agents/README.md).
