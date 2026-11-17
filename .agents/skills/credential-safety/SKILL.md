---
name: credential-safety
description: "The password-handling rules for PgCreds Handler: Credential Manager storage, the owner's explicit full-password database view, forbidden JSON/log/search/tooltip paths, the ICredentialStore contract, and delete. Use whenever code reads, writes, shows, copies, logs, serialises or passes a password or connection string, and when reviewing such code."
---

# Credential handling

The one rule: **a password is stored only in Windows Credential Manager, and is in memory only while it is being moved or shown.**

## Where a password may exist

| Place | Allowed? | Notes |
| --- | --- | --- |
| Windows Credential Manager, target `PgCredsHandler:{connectionId}` | Yes, the only store | Generic credential, DPAPI-protected per Windows user. `UserName` is the DB user, `Comment` is "Saved by PgCredsHandler". |
| `DiscoveredConnection.Password` | Yes, transient | From parse until merge. Dropped when the scan finishes. |
| `MergeResult.PasswordWrites` | Yes, transient | The caller writes each one, then drops the list. |
| A local variable during display, copy, test, DBeaver launch | Yes, transient | Read from the store right before use. |
| `ConnectionRowViewModel.Password` | Yes, while saved rows are loaded | The owner explicitly requires the full password in the expanded Database file. Never include it in search, tooltips, logs or serialization. |
| DBeaver's command line | Yes, unavoidable | Visible to other processes of the same user while DBeaver starts. Accepted trade-off; documented in [integrations.md](../db-client-integrations/SKILL.md). |
| `connections.json`, `settings.json` | **Never** | |
| pgAdmin `servers.json` | **Never** | pgAdmin prompts for it. |
| Logs, `Debug`, `Console`, exception messages, `ToString()` | **Never** | `DiscoveredConnection.ToString()` deliberately omits it. |
| The Database file's password binding | Yes | This is the one deliberate plaintext UI binding. |
| Other list bindings, tooltips, the search index | **Never** | Search covers project, host, database, user only. |

## Store contract (`ICredentialStore`)

- `Save(connectionId, username, password)` overwrites. Throws if Credential Manager refuses; the caller shows an error and does not mark `HasPassword = true`.
- `Read(connectionId)` returns `null` when nothing is stored, `""` for an empty password.
- `Delete(connectionId)` is idempotent.
- `connectionId` is `SavedConnection.Id` (a GUID), never the fingerprint, so editing host or port of a manual entry keeps its password.
- `WindowsCredentialStore` clears its byte buffer after writing. Keep that for any new buffer.

## Complete credential display

- `ConnectionRowViewModel.IsOpen` reads that database's password through `ICredentialStore.Read` only when its Database file expands.
- The password is shown unmasked only inside the expanded Database file, as explicitly requested by the owner.
- This is a project-specific exception to the common rule against plaintext secret bindings. It does not permit password values in JSON, logs, errors, tooltips, search, app summaries or unrelated screens.
- A missing Credential Manager value is shown as `Not stored`; the app never substitutes a value from a file or JSON.
- Collapsing the Database file, leaving the Databases page, rescanning, hiding the window to the tray, or closing the app clears the viewmodel property. The password remains stored at rest only in Windows Credential Manager.

## Delete

- Scanned entry: `ICredentialStore.Delete(id)`, remove from the list, add `Fingerprint` to `HiddenFingerprints`, save both files.
- Manual entry: `Delete(id)`, remove from the list, save.
- Stale entries are deleted the same way as scanned ones.

## What a reviewer checks

Run the `secrets-reviewer` agent. By hand: grep for `Password` in serialized types and file writers, `Debug.`/`Console.` near connection objects, interpolated strings containing a connection string, and any `catch` that rethrows with a connection string in the message.
