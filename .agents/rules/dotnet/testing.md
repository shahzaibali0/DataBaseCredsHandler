---
paths:
  - "tests/**"
  - "**/*Tests.cs"
  - "**/*Tests.csproj"
---

# Testing in .NET

How `common/testing.md` applies with xunit. Everything there still holds. What this project must cover, in order, is in the `xunit-testing` skill.

## Setup

- xunit with `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio`. Run everything with `dotnet test`; one test with `dotnet test --filter "FullyQualifiedName~ClassName.MethodName"`.
- One test project per solution until it gets slow; it references only the projects it tests.
- No mocking library by default. Fakes are small hand-written classes that implement the interface at the edge (`ICredentialStore`, `IProcessRunner`, a clock). Add a library only when hand-written fakes get repetitive, and record it.
- Coverage, when wanted: `coverlet.collector` and `dotnet test --collect:"XPlat Code Coverage"`.

## Writing a test

- One test class per class under test, in a folder that mirrors the source (`tests/X.Tests/Parsers/EnvFileParserTests.cs`).
- Names: `Method_Situation_Expected` (`TryParse_UnencodedAtInPassword_SplitsOnLastAt`).
- Arrange, act, assert, in that order, separated by a blank line. One behaviour per test.
- `[Theory]` with `[InlineData]` for tables of inputs; `[MemberData]` when the inputs are objects.
- Assert with xunit's `Assert.*`. Check exact values: `Assert.Equal(5433, connection.Port)`, not `Assert.True(connection.Port > 0)`.
- `Assert.Throws<T>` only for bugs the code should reject; expected failures are asserted on the returned result.
- Pass `now` and other inputs explicitly. Never assert against `DateTimeOffset.Now`.
- Async tests return `Task` and await; never `.Result`.

## Files and the OS

- A test that needs files creates a unique temp folder in its constructor (`Path.Combine(Path.GetTempPath(), "<app>-tests", Guid.NewGuid().ToString("N"))`) and deletes it in `Dispose`.
- Repositories and stores take their path as a constructor argument, so tests pass a temp path. Never read or write the real `%APPDATA%`.
- Never write to the real Windows Credential Manager, registry or clipboard from a test. Use the in-memory or fake implementation of the interface.
- Tests that need a real outside service (a live Postgres) carry `[Trait("Category", "Integration")]` and skip unless an environment variable names the service. `dotnet test --filter "Category!=Integration"` runs the rest.

## Proving a test

- Break the code on purpose, watch the test fail for the right reason, restore it. Check the restore with `Get-FileHash <file> -Algorithm SHA256` (PowerShell) or `sha256sum <file>` before and after.
- Report what was broken, which test failed, and that the file was restored.
