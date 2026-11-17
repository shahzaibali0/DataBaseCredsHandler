# What to fix

Known bugs, risks and departures from the [design contract](SKILL.md), found while documenting the code on 2026-11-17. This is debt to pay down, not a pattern to copy. Remove an item when it is fixed.

## Bugs and risks

### 1. A corrupted `connections.json` can silently wipe the saved list (high)
`JsonConnectionRepository.GetAll` (`src/PgCredsHandler.Storage/JsonConnectionRepository.cs`) catches every exception and returns an empty list. If the file is ever unreadable (bad JSON, a hand edit, a locked file), the app sees zero connections. The next scan then calls `SaveAll` with only what it found, so every **manual entry is permanently lost** and their passwords are orphaned in Credential Manager. `JsonSettingsStore.Load` has the same shape: a bad `settings.json` resets scan roots and `HiddenFingerprints`, so deleted connections come back.
**Fix:** distinguish "file missing" (empty is correct) from "file unreadable" (back the file up as `connections.json.corrupt-<timestamp>`, tell the user, and refuse to save over it until they choose).

### 2. Credential persistence comment is wrong; `ENTERPRISE` roams (low)
`WindowsCredentialStore` says `CRED_PERSIST_LOCAL_MACHINE` "would be shared" and uses `CRED_PERSIST_ENTERPRISE`. Per the Win32 docs both are visible only to the same Windows user; `LOCAL_MACHINE` stays on this computer, `ENTERPRISE` also roams to other computers with a roaming domain profile. For a single-machine tool `LOCAL_MACHINE` matches the intent. Changing it requires re-saving existing credentials, so decide deliberately and fix the comment either way.

### 3. Docker services without a published port get port 5432 (low)
`DockerComposeParser.ReadHostPort` returns `null` when no port maps to 5432, and the parser then uses `localhost:5432`. That container is not reachable from the host at all; 5432 may be a different local Postgres. Mark such connections (e.g. `SourceKey` suffix or a flag) so the UI can say "not published to the host".

### 4. Two connection-string builders (planned duplication)
`PostgresUrl.ToUrl` / `ToJdbc` in Core build strings; `ConnectionStringBuilder` in Integrations is specified to do the same. When it is built, have one owner and delete or delegate the other. Note `ToJdbc` does not encode the database name while `ToUrl` does.

## Over the size limits

These existed before the limits were written. They may only shrink; an edit must not make them longer. Split along named steps when you next change them.

| Method | File | Lines |
| --- | --- | --- |
| `ConnectionMerger.Merge` | `src/PgCredsHandler.Core/Services/ConnectionMerger.cs` | ~89 (split: update existing / add new / mark stale) |
| `PostgresUrl.TryParse` | `src/PgCredsHandler.Core/Util/PostgresUrl.cs` | ~70 (split: user-info, host/path, query params) |
| `EnvFileParser.ParseText` | `src/PgCredsHandler.Scanner/Parsers/EnvFileParser.cs` | ~69 (split: quoted value reader) |
| `ProjectScanner.Scan` | `src/PgCredsHandler.Scanner/ProjectScanner.cs` | ~61 (split: build `DiscoveredConnection`) |

## Against the naming and shape rules

Written before `.agents/rules/` existed. Fix when you next edit the method; don't rename across the codebase in one sweep.

- Short and vague names: `kv`, `sb`, `q`, `v`, `p`, `pp`, `hp`, `h`, `g`, `d`, `c`, `fp`, `m`, `s`, `segs`, `li` in `KeyValueConnectionString`, `PostgresUrl`, `EnvFileParser`, `EnvCredentialExtractor`, `DockerComposeParser`, `ConnectionMerger`; `result`/`results` as variable names in several parsers.
- More than three parameters: `ConnectionMerger.Merge` (six). Group them into a `MergeInput` record when the method is split.
- Nesting deeper than two levels in `PostgresUrl.TryParse`, `EnvFileParser.ParseText`, `KeyValueConnectionString.Parse`.
- No `.editorconfig`, analyzers or `TreatWarningsAsErrors` yet, so the rules are checked only in review. See "Open" in `.agents/decisions.md`.

## Small departures

- `Paths.DataFolder` calls `Directory.CreateDirectory` on every property read. Harmless, but it is I/O in a getter.
- `MergeResult.Updated` counts every re-seen entry, changed or not. Rename to `Seen` or count only real changes before showing it in the UI.
