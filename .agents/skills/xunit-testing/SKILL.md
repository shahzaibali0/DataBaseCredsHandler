---
name: xunit-testing
description: "Testing rules for PgCreds Handler: the xunit project layout, what to cover in priority order (PostgresUrl, KeyValueConnectionString, EnvFileParser, DockerComposeParser, AppSettingsJsonParser, ConnectionMerger, HostClassifier, ProjectWalker, Integrations), temp-folder and fake-credential rules, naming. Use when writing or running tests, or reproducing a bug as a failing test."
---

# Testing

`tests/PgCredsHandler.Tests`, xunit 2.9, `net8.0`. Run with `dotnet test`. It references Core and Scanner; add Storage or Integrations references only when a test needs them.

## Layout

Mirror the source: `Util/PostgresUrlTests.cs`, `Parsers/EnvFileParserTests.cs`, `Services/ConnectionMergerTests.cs`, `Integrations/…`. One test class per class under test.

## What to cover, in order

1. **`PostgresUrl`**: `postgres://` / `postgresql://` / `jdbc:postgresql://` / `postgresql+asyncpg://`; encoded and unencoded special characters in passwords (`@ : / ? #`); missing port, database, user; IPv6 `[::1]`; multiple hosts; libpq query params; malformed input returns `false`, never throws. `ToUrl` round-trips through `TryParse`.
2. **`KeyValueConnectionString`**: quoted values with `;` inside, doubled quotes, SQL Server strings rejected, `Server=…;User Id=…` accepted only when the project uses Npgsql, `Host=localhost:5433`.
3. **`EnvFileParser` + `VariableExpander`**: every rule in [scanning-and-merge.md](../scan-and-merge/SKILL.md): `export`, quote kinds, multi-line, ` #` vs `#`, expansion operators, unknown vars, CRLF, BOM. `EnvCredentialExtractor`: URL values, prefix groups, non-Postgres `DB_CONNECTION=mysql` ignored, `REDIS_HOST` ignored.
4. **`DockerComposeParser`**: map and list environment, `.env` interpolation, every port form, no ports, non-Postgres services ignored, malformed YAML returns nothing.
5. **`AppSettingsJsonParser`**: `ConnectionStrings`, nested keys, comments and trailing commas, SQL Server strings skipped.
6. **`ConnectionMerger`**, the most important: manual untouched; missing → stale only under scanned roots; stale entry found again → not stale; password changed → write queued and `PasswordChangedAt` set; first password → write queued, no `PasswordChangedAt`; discovered without password keeps stored one; hidden not resurrected; duplicates resolved by the priority order; inputs not mutated.
7. **`HostClassifier`**: one `[InlineData]` per rule.
8. **`ProjectWalker`**: excluded folders, dot-folders, depth cap, project-root detection (temp folders).
9. **Integrations**: see [integrations.md](../db-client-integrations/SKILL.md).

## Rules

The general rules are in `.agents/rules/common/testing.md` (writing, proving, data) and `.agents/rules/dotnet/testing.md` (xunit, temp folders, fakes, naming). Read both first. For this project, in addition:

- Read the implementation first. If the code looks wrong, write the test for the correct behaviour, let it fail, and report it; don't change production code in a test-only task.
- Temp folders go under `Path.Combine(Path.GetTempPath(), "pgcreds-tests", …)`.
- Credential tests use `InMemoryCredentialStore`; repositories and settings stores get a temp path.
- Pass `now` explicitly to `ConnectionMerger`.
- Report pass/fail counts and any failure output verbatim.
