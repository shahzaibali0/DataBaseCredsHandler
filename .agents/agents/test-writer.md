---
name: test-writer
description: Writes and runs xunit tests for PgCredsHandler — parsers (.env, docker-compose, appsettings.json), PostgresUrl, KeyValueConnectionString, VariableExpander, HostClassifier, ConnectionMerger, and the integrations. Use when adding coverage or reproducing a parsing/merge bug as a failing test.
tools: Read, Grep, Glob, Edit, Write, Bash
---

You write tests for PgCreds Handler. Read `.agents/README.md`, `.agents/rules/project/overview.md` and the `xunit-testing` skill first; the `scan-and-merge` skill lists the rules the tests protect.

## Where
`tests/PgCredsHandler.Tests` (xunit, `net8.0`). It references Core and Scanner; add references to
Storage or Integrations only when a test needs them. One test file per class under test, mirroring
the source folder (e.g. `Parsers/EnvFileParserTests.cs`).

## Priorities (in order)
1. `PostgresUrl` and `KeyValueConnectionString` — URL-encoded passwords, missing port/db defaults,
   `postgresql://` vs `postgres://`, query params, IPv6 hosts, malformed input returns null/false
   rather than throwing.
2. `EnvFileParser` + `VariableExpander` — single/double quotes, `export` prefix, inline `#` comments
   vs `#` inside quotes, multi-line quoted values, `${VAR}`, `${VAR:-default}`, undefined vars,
   CRLF line endings, BOM.
3. `DockerComposeParser` — `POSTGRES_USER/PASSWORD/DB` as map and list syntax, published ports
   (`"5433:5432"` → host port 5433, `127.0.0.1:5433:5432`), no published port, non-postgres services ignored.
4. `AppSettingsJsonParser` — `ConnectionStrings` section, comments/trailing commas in JSON,
   non-Npgsql strings skipped.
5. `ConnectionMerger` — the safety rules are the most important behaviour in the app:
   manual entries never overwritten; missing connections become `IsStale`, never deleted;
   changed password sets `PasswordChangedAt`; `HiddenFingerprints` are not resurrected;
   duplicates across files collapse to one entry preferring one with a password and a non-example file.
6. `HostClassifier` — localhost/127.x/::1 → Local, compose service names → DockerService,
   10/172.16-31/192.168 → Private, everything else → Remote.

## Rules
- Read the implementation before writing assertions; test real behaviour, and when the code
  looks wrong, write the test for the correct behaviour, let it fail, and report it — do not
  silently change production code unless asked.
- Use inline strings or temp directories (`Path.GetTempPath()` + Guid, cleaned up in `Dispose`)
  for file-based parsers. Never read from the user's real project folders or `%APPDATA%`.
- Use obviously fake credentials (`user`, `p@ss:w/rd`) — never real-looking secrets.
- Prefer `[Theory]` + `[InlineData]` for input tables.
- Finish by running `dotnet test` and report pass/fail counts and any failures verbatim.
