# Architecture (common)

How code is organised, whatever the stack. The stack-specific mapping is in `dotnet/architecture.md` (or the matching file for another stack). This project's own layers and owners are in the `architecture-contract` skill.

## One home for each kind of code

- Code is split by what it does, not by feature: entry points, services (what the app does), data access, integrations with outside systems, configuration, interface, small shared helpers. A new feature adds a file to several homes, not a folder of its own.
- Each kind of code has exactly one home, so a person or an agent always knows where to look and where to add.
- Generated code has its own folder. It isn't edited by hand, linted or formatted.

## How work flows

1. Something enters the app: a page, an API route, a job, a command, a button.
2. It calls **one** service operation. That operation is the whole process.
3. The operation runs its steps in order. Each step is one of:
   - a small private helper in the same service, for a decision that belongs to this process;
   - a data-access function, for anything that reads or writes storage;
   - another service's public operation, for work reused elsewhere (sending a message, calling an outside API).
4. The result goes back to the caller.

## Calls go down or sideways, never up

- Entry points call services. They never reach data access or the database client directly. Importing a *type* from a lower layer is fine.
- Services hold every decision. They call data access, other services, configuration and shared helpers. They know nothing about the interface.
- Data access only talks to storage. It makes no decisions and never calls a service.
- Configuration and the storage client sit at the bottom and import nothing above them.
- Interface code (components, views, viewmodels' visual concerns) never imports data access or configuration.
- Tests may import anything.
- **Enforce the layers with a tool**: import rules in lint, project references in .NET. A layering rule that only lives in a document gets broken.

## Writing a service

- A public operation reads like a list of steps; each line's output feeds the next.
- **Save first, then do the steps that can fail.** A notification, an email or a call to an outside model runs after the main record is saved, in its own error handling, so its failure never loses or fails the record.
- A service is a deep module: a few public operations and as many private helpers as they need, in the same file. A long file with a short list of exports is the goal. Don't export a helper just so a test can reach it.
- When a caller needs a data-access read unchanged, expose it through the service without wrapping it in a function that only forwards the call. A service function earns its place when it adds something.
- Pass the few values a step needs and return an explicit result. Don't thread a growing context object through steps.

## Input, configuration, logging

- **Outside input is checked once, where it comes in**: request bodies, route and query values, file contents, environment variables, an outside model's structured output. Inside the app, trust the checked type.
- **One place reads configuration.** It validates every setting when the app starts and stops with a clear message if one is wrong. Every setting has a documented example value (an `.env.example`, an `appsettings.json` with safe defaults).
- **One logger.** Structured entries, one line per event, the error object under its own key so the stack is kept. No raw console printing in finished code. Tests run with logging silenced.
- Never log a secret, a token, a password or a connection string that holds one. See `security.md`.

## Outside systems

- Each outside system (a payment gateway, an LLM, a DB client, the OS credential store) is wrapped by one module. Everything else calls that module's operations.
- Every outside call has a timeout. No silent automatic retries: a retry is a second call nothing recorded.
- An expected outside failure (not found, refused, unreachable) is returned as a result the caller can show, not thrown as a crash.
- Decide in code anything code can decide (a threshold, a category). An outside model only puts a decision into words.

## Background and timed work

- A job is an entry point like any other: it calls one service operation and logs each run, what it did or why it failed.
- Each run catches its own errors, so one failed run never stops the next.
- Runs never overlap: every slow step has a timeout, and their total stays under the interval.
- A job is safe to run twice. A daily job checks whether the day's work exists and does it if not, instead of waiting for an exact time.
- A cap or a counter is computed from stored data, never kept only in memory, where a restart resets it.
