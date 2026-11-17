# Secrets and safety (common)

Rules for secrets, input and outside access, in any stack. This project's password rules are in the `credential-safety` skill.

## Secrets

- A secret is a password, token, API key, connection string with a password, session id, one-time code, card number or decrypted credential.
- Secrets live in one designated store: the OS credential store, a secret manager, or an encrypted column read through one module. Never in source code, committed config, plain-text files, URLs or query strings.
- Never log, print, show in an error message, return from an API, put in a `ToString()`, or send to telemetry a secret. That includes a whole connection string or a request body that carries one.
- Read a secret at the moment you need it and drop the reference after. Don't cache it in a long-lived object or bind it into a list.
- Showing a secret on screen or copying it is a deliberate user action, gated where the app has a gate, and undone after a short time.
- `.env` and other local secret files are never committed. The committed example file lists every setting with a dummy value.
- Rotating a secret that two places share means changing both in the same step.

## Input

- Every value from outside (request, file, environment, command line, another process, a model's output) is checked once where it enters, against a schema or a parser that returns a result.
- Never build a shell command, a SQL query or a process command line by joining strings. Use parameters, argument lists and query builders.
- An id or token that can't be parsed is treated as "doesn't exist", with the same answer as an unknown one, so it reveals nothing.

## Scope and access

- Code touches only what the user chose: their folders, their accounts, their data. Default exclusions (dependency folders, system folders) stay on.
- Don't follow links or junctions out of a chosen folder.
- No network call the feature doesn't need. No telemetry or update checks unless the owner asked for them.
- When the app can't tell whether something is safe because *its own* infrastructure failed (a missing header, an unreachable service), decide on purpose whether to fail open or closed, and write down why. Don't let it happen by accident.

## Review

- Any change that touches a secret, an auth check or outside input says, in its summary, where the secret goes and when it leaves memory, or what happens to an honest user when the input is missing.
- Run the project's security reviewer (an agent or a checklist) after such a change.
