---
name: scan-and-merge
description: "How DB Connect Hub scans: ProjectWalker exclusions and project roots, the rules of the .env, docker-compose and appsettings.json parsers, the recipe for adding a new IConfigParser (Django, Rails, Prisma, Spring…), and the ConnectionMerger rules (manual untouched, stale not deleted, hidden not resurrected, password change). Use when changing the scanner, any parser, ConnectionKey or ConnectionMerger, or adding a config format."
---

# Scanning and merge

## Pipeline

0. **Roots** (`ScanWorkflow`): `AppSettings.ScanRoots` if the user chose folders, else every fixed, ready drive (`LocalDrives`).
1. **Walk** (`ProjectWalker.Walk`): depth-first from each scan root, depth cap 12. Skips folders named in `ExcludedFolderNames` (case-insensitive), every dot-folder except `.config`, every `$`-folder, and reparse points (junctions, symlinks). Unreadable folders are skipped. Yields only files some parser `CanParse`. The project root is looked up only for folders that hold a config file, which keeps a whole-drive scan fast.
2. **Project root** (`FindProjectRoot`): the nearest ancestor, not above the scan root, containing a marker: `.git`, `package.json`, `pyproject.toml`, `requirements.txt`, `go.mod`, `composer.json`, `Cargo.toml`, `pom.xml`, `build.gradle`, `Gemfile`, `manage.py`, `artisan`. No marker: the file's own folder. The project name is that folder's name.
3. **Parse**: the first parser whose `CanParse(fileName)` matches. A parser that throws is skipped for that file.
4. **Defaults** (`ProjectScanner`): host `localhost`, port `5432`, user `postgres`, database = user (or `postgres`). A hit with no host, user, database or password is dropped.
   **Template placeholders are dropped** (`PlaceholderValues.LooksLikePlaceholder`): a host that is a template word (`host`, `your-host`…) or still holds a word-port (`HOST:PORT`), any bracketed token (`<host>`, `{user}`), or a database *and* user that are both template words (`dbname` + `user`). A real database called `database`, or a real user called `user`, is kept.
5. **Apps** (`AppDetector`): `package.json` and web `.csproj` files identify backend/frontend apps. Dev-server ports may come from a script, framework default, launch settings or `PORT=` in an adjacent `.env` file.
6. **Merge** (`ConnectionMerger.Merge`), below. `InventoryMerger` replaces apps under scanned roots and keeps apps elsewhere.

The scanner reads only database connection details and dev-server ports. It never reads, stores or shows API keys, tokens or other values from `.env` or config files.

## Parsers

| Parser | Files | What it reads |
| --- | --- | --- |
| `EnvFileParser` (`.env`) | `.env`, `.env.*`, `*.env` | dotenv syntax, then `EnvCredentialExtractor` |
| `DockerComposeParser` (`docker-compose`) | `docker-compose.yml/.yaml`, `compose.yml/.yaml`, `docker-compose.*.yml` | Postgres-looking services |
| `AppSettingsJsonParser` (`appsettings.json`) | `appsettings.json`, `appsettings.*.json` | Strings under `ConnectionStrings` or under a key that looks like a connection |

### `.env` rules
- `export ` prefix allowed. Keys: letter or `_` first, then letters, digits, `_ . -`.
- Double quotes: multi-line, `\n` `\"` `\\` escapes, expanded. Single quotes: literal, not expanded. Backticks: multi-line, not expanded.
- Unquoted: an inline comment starts at ` #` (space then hash). A `#` with no space before it is part of the value, because passwords contain `#`.
- `${VAR}`, `${VAR:-def}`, `${VAR-def}`, `${VAR:+alt}`, `${VAR:?err}`, `$VAR`, `$$` are expanded against the same file (up to 5 passes). Unknown variables are left as written.
- `EnvCredentialExtractor` finds: any value that is a Postgres URL with nothing unresolved, and groups of variables sharing a prefix (`PG*`, `POSTGRES_*`, `DB_*`, `*_DATABASE_*`…) with suffixes HOST/PORT/NAME/USER/PASSWORD. A group with a `*_CONNECTION`/`*_ENGINE`/`*_DIALECT` that is not Postgres is ignored. Bare `HOST`/`PORT` with no prefix are ignored.
- `.env.example`, `*sample*`, `*template*`, `*.dist` are example files: lower priority when de-duplicating.

### docker-compose rules
- A service is Postgres if its image mentions postgres / postgis / timescale / pgvector / supabase/postgres / citusdata, or its environment has `POSTGRES*` or `PGDATA`.
- Environment as map or list; values interpolated from the `.env` beside the compose file. `env_file:` is not followed.
- User `POSTGRES_USER` / `PGUSER` / `postgres`; password `POSTGRES_PASSWORD` / `PGPASSWORD`; database `POSTGRES_DB` / `PGDATABASE` / user.
- Host is always `localhost`; port is the published side of a mapping to 5432 (`5433:5432`, `127.0.0.1:5433:5432`, long form `published`/`target`), else 5432.

### appsettings rules
- JSON with comments and trailing commas. Paths are `Section:Key`.
- A value is a Postgres URL, or a key=value string that `KeyValueConnectionString.TryParsePostgres` accepts: rejected if it has SQL Server/SQLite/MySQL-only keys; accepted if it has `Host=`, `Username=`, a port 5432–5439, or the project references Npgsql in any `.csproj`.

## Adding a parser

1. Create `src/DbConnectHub.Scanner/Parsers/<Format>Parser.cs`, `public sealed class … : IConfigParser`.
2. `Kind` is the short label shown in the UI. `CanParse` matches by file name only, cheaply: it runs for every file in every folder.
3. `Parse` returns `ParsedCredential(sourceKey, PgParts)`, with `null` for unknown parts (the scanner applies defaults). Catch format errors and return what you have; never throw for a malformed file.
4. Reuse `PostgresUrl`, `KeyValueConnectionString`, `VariableExpander`, `EnvCredentialExtractor` instead of new parsing code.
5. Register it in `DefaultParsers.All`. Order matters: the first parser that can parse a file wins.
6. Give it a priority in `ConnectionMerger.SourcePriority` if it should win or lose ties against existing kinds.
7. Add tests with inline file content in a temp folder (see [testing.md](../xunit-testing/SKILL.md)).

## Merge rules (`ConnectionMerger`)

Inputs: existing saved list, discovered list, the roots that were scanned, hidden fingerprints, a `readPassword` delegate, `now`. Output: a new list (inputs are cloned, never mutated), the passwords to write, and counts.

1. **Real config beats examples**: if a project has any connection from a non-example file, its example-file connections (`.env.example`, `.env.production.example`…) are ignored. An example file only counts when it is the project's only config.
   **De-duplicate** the discovered list by fingerprint. Winner: has a password, then not an example file, then source priority `.env` < `appsettings.json` < `docker-compose` < others.
2. For each winner:
   - fingerprint hidden → skip.
   - matches a **manual** entry → skip; the user owns it.
   - matches a scanned entry → update project name and source fields, `LastSeenAt = now`, `IsStale = false`. If the discovered password differs from the stored one: queue a write; if one was stored before, set `PasswordChangedAt = now`. A discovered entry with no password never clears a stored one.
   - no match → new `SavedConnection`, queue its password if any.
3. Every non-manual, not-yet-stale entry under a scanned root that was not seen → `IsStale = true`. Entries outside the scanned roots are untouched, so scanning one root never stales another root's entries.

Invariants a change must keep: the merger never deletes an entry; never changes a manual entry; never resurrects a hidden fingerprint; is deterministic for the same inputs.
