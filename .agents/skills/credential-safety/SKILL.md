---
name: credential-safety
description: "The password-handling rules for PgCreds Handler: where a password may and may never exist (Credential Manager only; never JSON, logs, exceptions, bindings, pgAdmin exports), the ICredentialStore contract, the Windows Hello unlock and reveal/copy/clipboard-clear flow, and delete. Use whenever code reads, writes, shows, copies, logs, serialises or passes a password or connection string, and when reviewing such code."
---

# Credential handling

The one rule: **a password is stored only in Windows Credential Manager, and is in memory only while it is being moved or shown.**

## Where a password may exist

| Place | Allowed? | Notes |
| --- | --- | --- |
| Windows Credential Manager, target `PgCredsHandler:{connectionId}` | Yes, the only store | Generic credential, DPAPI-protected per Windows user. `UserName` is the DB user, `Comment` is "Saved by PgCredsHandler". |
| `DiscoveredConnection.Password` | Yes, transient | From parse until merge. Dropped when the scan finishes. |
| `MergeResult.PasswordWrites` | Yes, transient | The caller writes each one, then drops the list. |
| A local variable during reveal, copy, test, DBeaver launch | Yes, transient | Read from the store right before use. |
| A viewmodel property | Only the revealed-password property, only during the reveal window | Cleared when the reveal timer fires, the row is collapsed, or the window locks. |
| The clipboard | Yes, for `ClipboardClearSeconds` | Cleared afterwards if still unchanged. |
| DBeaver's command line | Yes, unavoidable | Visible to other processes of the same user while DBeaver starts. Accepted trade-off; documented in [integrations.md](../db-client-integrations/SKILL.md). |
| `connections.json`, `settings.json` | **Never** | |
| pgAdmin `servers.json` | **Never** | pgAdmin prompts for it. |
| Logs, `Debug`, `Console`, exception messages, `ToString()` | **Never** | `DiscoveredConnection.ToString()` deliberately omits it. |
| List bindings, tooltips, the search index | **Never** | Search covers project, host, database, user only. |
| A copied connection string | Only when the user picks a "with password" copy | Default copy is without the password. |

## Store contract (`ICredentialStore`)

- `Save(connectionId, username, password)` overwrites. Throws if Credential Manager refuses; the caller shows an error and does not mark `HasPassword = true`.
- `Read(connectionId)` returns `null` when nothing is stored, `""` for an empty password.
- `Delete(connectionId)` is idempotent.
- `connectionId` is `SavedConnection.Id` (a GUID), never the fingerprint, so editing host or port of a manual entry keeps its password.
- `WindowsCredentialStore` clears its byte buffer after writing. Keep that for any new buffer.

## Reveal, copy, unlock

```
 user clicks Show/Copy
        │
        ▼
 RequireWindowsHello? ──no──► proceed
        │yes
        ▼
 unlocked in the last UnlockMinutes? ──yes──► proceed
        │no
        ▼
 UserConsentVerifier.CheckAvailabilityAsync
   ├─ available ─► RequestVerificationAsync ─ verified ─► unlockedUntil = now + UnlockMinutes ─► proceed
   │                                        └ anything else ─► stop, say "Not verified"
   └─ unavailable ─► stop, explain Windows Hello is not set up and how to turn the setting off
        │
 proceed: Read(connection.Id)
   ├─ Show: display, start RevealSeconds timer, then clear the property
   └─ Copy: set clipboard, start ClipboardClearSeconds timer,
            then clear only if Clipboard text still equals the copied value
```

- Unavailable Windows Hello is never a silent bypass. The user turns `RequireWindowsHello` off in Settings on purpose.
- Locking the workstation or minimizing for longer than `UnlockMinutes` ends the unlock.
- Changing `RequireWindowsHello` from on to off requires a successful unlock first.
- Copying a "with password" connection string goes through the same gate.

## Delete

- Scanned entry: `ICredentialStore.Delete(id)`, remove from the list, add `Fingerprint` to `HiddenFingerprints`, save both files.
- Manual entry: `Delete(id)`, remove from the list, save.
- Stale entries are deleted the same way as scanned ones.

## What a reviewer checks

Run the `secrets-reviewer` agent. By hand: grep for `Password` in serialized types and file writers, `Debug.`/`Console.` near connection objects, interpolated strings containing a connection string, and any `catch` that rethrows with a connection string in the message.
