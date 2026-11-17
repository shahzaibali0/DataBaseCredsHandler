---
paths:
  - "tests/**"
  - "**/*.test.*"
  - "**/*.spec.*"
  - "**/*Tests.cs"
  - "**/*Tests.csproj"
---

# Testing (common)

How to write and prove tests in any stack. Framework details are in `dotnet/testing.md`. What this project must cover is in the `xunit-testing` skill.

## Writing a test

- Test behaviour through public operations, never the private helpers behind them. A refactor that keeps the behaviour must not break a test.
- Write the failing test first, then only enough code to pass it. One test at a time.
- Mock only at the edges: the network, the clock, outside services, OS stores, browser or UI APIs the test runner lacks. Don't mock your own code just to make a test easier to write.
- An expected value comes from a known-good literal or a worked example, never from computing it the way the code does. A test that recomputes the answer can never disagree with the code.
- Counts and limits are checked exactly. To prove a limit of 50, seed 51 and expect 50; `<= 50` passes on an empty store.
- Before adding a test, answer four questions: What behaviour does it protect? What bug would make it fail? Why doesn't an existing test already catch that bug? Does it need an export or hook no production code uses? If so, test at the real boundary instead.
- Agree which public operations to test with the owner before writing tests. A subagent can't ask, so whoever starts it writes the agreed list into its prompt.

## Proving a test

- Every new or changed test is seen failing for the right reason. New behaviour: the test fails until the code exists. Existing code: break the code on purpose (drop a filter, change a limit), watch the test fail, put the code back.
- Before deleting or merging a test, prove a remaining test catches the same bug the same way.
- Put broken code back straight away and confirm it matches the original byte for byte: compare a SHA-256 before and after (`sha256sum`, or `Get-FileHash` on Windows). Another session or a hot-reloading dev server may be running the same files.
- Don't break code that a running job or app executes for real; tamper with the test's input instead.
- Report each proof: what you broke, which test failed, and that the file was restored.

## Data

- Tests never touch the user's real data: real projects, real config folders, the real OS credential store, a production database.
- File-based tests use a fresh temp folder per test and delete it afterwards.
- A test against a real database deletes exactly the rows it wrote, tagged with a run id, and nothing else. Never wipe a whole table. Delete children before parents.
- Tests that depend on "now" pass the time in, or fake the clock. Tests that seed by time use a far-past or far-future window of their own, so suites running together never collide.
- Use obviously fake secrets (`secret`, `p@ss:w/rd#`). Never a real-looking token.
- No test calls a paid or outside service (an LLM, a payment gateway). Use the library's mock or a fake at the module that wraps it.

## Folders

- The test tree mirrors the source tree: the test for `src/X/Thing` lives at `tests/X/ThingTests` (or `thing.test.ts`).
- Tests that cross layers on purpose live in an `integration/` folder.
- Helpers shared by several test files live in a `support/` folder; a helper used by one file stays in that file.
- Test files get looser size and naming limits than production code; every other rule still applies.
