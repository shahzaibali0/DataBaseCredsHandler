---
name: db-client-integrations
description: "Spec for PgCredsHandler.Integrations: ConnectionStringBuilder (URL/JDBC/ADO.NET, with or without password), ConnectionTester (Npgsql, friendly errors), DbeaverLauncher (find exe, -con arguments via ArgumentList, IProcessRunner) and PgAdminExporter (servers.json with no password). Use when building or changing anything that hands a connection to an external client or tests connectivity."
---

# Integrations

Status: spec, not built. Project `src/PgCredsHandler.Integrations` (`net8.0`, references Core, has Npgsql 8.0.5). Delete `Placeholder.cs` when the first class lands.

Every entry point takes a `SavedConnection` and the password as a separate `string?` argument. The App reads the password from `ICredentialStore` right before the call. Integrations never read the credential store and never keep the password.

## `ConnectionStringBuilder` (static, pure)

| Method | Output |
| --- | --- |
| `ToUrl(c, password?)` | `postgresql://user[:pass]@host:port/db`, user and password with `Uri.EscapeDataString`, IPv6 host in `[]` |
| `ToJdbc(c, password?)` | `jdbc:postgresql://host:port/db?user=…[&password=…]`, query-encoded |
| `ToAdoNet(c, password?)` | built with `NpgsqlConnectionStringBuilder` so quoting is correct |

`password = null` means "without password", which is the default copy. Reuse or move `PostgresUrl.ToUrl` / `ToJdbc` from Core rather than keeping two builders (see [WHAT_TO_FIX.md](../architecture-contract/WHAT_TO_FIX.md)).

## `ConnectionTester`

```csharp
Task<ConnectionTestResult> TestAsync(SavedConnection c, string? password, CancellationToken ct);
record ConnectionTestResult(bool Success, string Message, string? ServerVersion, TimeSpan Elapsed);
```

- Timeout ≈ 5 s (`Timeout`, `CommandTimeout`), runs `SELECT version()`, `Pooling = false` so no connection lingers holding the password.
- Maps failures to friendly messages: `28P01` wrong password, `28000` auth rejected, `3D000` database does not exist, `SocketException` / timeout host unreachable. `DockerService` hosts get a hint that the service name only resolves inside Docker.
- Never throws for expected failures. Never puts the connection string in `Message`.
- `Remote` hosts: the App asks for confirmation before testing.

## `DbeaverLauncher`

- Find `dbeaver.exe`: `AppSettings.DbeaverPath` if set and exists; else `%ProgramFiles%\DBeaver`, `%LocalAppData%\DBeaver`, `%LocalAppData%\Programs\DBeaver`.
- Launch: `-con "driver=postgresql|host=…|port=…|database=…|user=…|password=…|name=<project> – <db>|connect=true|folder=PgCreds"`. Values containing `|` or `=` are escaped or rejected.
- Use `ProcessStartInfo.ArgumentList`, never a concatenated command line. Process start goes through an `IProcessRunner` interface so tests can assert arguments.
- Returns `LaunchResult(bool Success, string Message)`: "DBeaver not found, set its path in Settings" is a result, not an exception.
- Known trade-off: the password is in DBeaver's command line, readable by other processes of the same Windows user while it starts. Offer "open without password" (DBeaver prompts) as an option.

## `PgAdminExporter`

- Writes a pgAdmin 4 import file: `{"Servers": {"1": {"Name", "Group", "Host", "Port", "MaintenanceDB", "Username", "SSLMode": "prefer"}}}`. `Group` is the project name.
- **No password field.** pgAdmin asks on first connect.
- Path chosen by the user (save dialog in App). Atomic write. Export one connection or a whole project.

## Tests

Each class gets xunit tests in `tests/PgCredsHandler.Tests/Integrations/`: encoding of special characters (`p@ss:w/rd#`), IPv6, without-password variants, DBeaver argument list through a fake `IProcessRunner`, pgAdmin JSON shape and absence of any password. `ConnectionTester` needs a live Postgres; mark those tests with a trait and skip when `PGCREDS_TEST_PG` is not set.
