# Code style (common)

Rules that hold in any language. The numbers and syntax for C# are in `dotnet/code-style.md`. Where a tool can check a rule, the tool is the authority; fix the code, never silence the rule.

## Names

- Clear names, at least 3 characters and at most 40. Allowed short names: `id`, `ok`, `_`, and a one-letter index in a very short loop or lambda.
- No abbreviations: `button`, not `btn`; `message`, not `msg`; `connection`, not `conn`. Well-known words are fine: `args`, `params`, `props`, `env`, `url`, `json`.
- No vague names: `data`, `result`, `res`, `ret`, `item`, `obj`, `val`, `tmp`, `temp`, `info`, `stuff`, `thing`, `handle`, `handler`, `process`, `manager`, `helper`, `util`, `cb`, `fn`, `func`, `arr`, `str`, `num`, `flag`, `foo`, `bar`. Name the value by its role: `savedConnection`, `orderRows`, `existingOrder`.
- No type prefixes: not `strName`, `arrOrders`, `blnDone`. (The platform convention for interfaces in .NET, `IThing`, is the one exception; see `dotnet/code-style.md`.)
- Booleans start with `is`, `has`, `can` or `should`.
- `get…` returns one thing, `list…` many, `find…` one or nothing, `try…` reports success without throwing. None of them write.
- A command says what changes: `markStale`, `resolvePaidPayment`, `hideConnection`. Never `update`, `handle`, `process` or `doWork` on their own.
- Ids are `<noun>Id`. Collections are plural. Durations and sizes carry their unit: `timeoutMs`, `revealSeconds`, `maxDepth`.
- One word per concept across code, interface text and docs. The project vocabulary is in `.agents/rules/project/overview.md`.
- Tests may use looser names; the type-prefix ban still applies.

## Shape

- A function reads as a short list of steps. If it needs comments to mark its stages, those stages are named functions.
- Return early. No `else` after a `return`. No nested ternaries. No negated condition with two branches (`!isReady ? a : b`): flip it.
- At most two `&&` / `||` / `?:` in one expression. Give a longer condition a name: `const isReachableFromHost = …`.
- One level of nesting inside a function, two at most. Nest callbacks two deep at most.
- At most three parameters. Pass one object when you need more.
- Return a value directly; don't store it in a variable only to return it on the next line. Return a boolean expression, not `if (x) return true; return false`.
- A function that uses nothing from its enclosing function moves to the top level of the file.
- Never change a parameter or its properties. Build a new value. Prefer constants and read-only values by default.
- No magic numbers or strings in logic: a fixed value is a named constant at the top of the file, or a setting.

## File order

Imports, then constants, then the main public operation, then the helpers it uses, below it. A reader meets the story first and the details after.

## Size

- Short functions, small files, no copied block of about eight lines or more: extract the shared part.
- Interface components and views are mostly markup, so they get a looser limit than logic; split them into sections where that makes the screen easier to follow, not just to hit a number.
- Code already over a limit is listed as debt. It may only shrink; an edit never makes it longer or more complex. Never split code into pass-through functions just to meet a number.

## Comments

- Comments explain *why*: a business rule, an outside system's quirk, a past incident, a security reason. Never what the next line does. Good names and small functions explain the *what*.
- No commented-out code. No TODO without an owner or a decision in `.agents/decisions.md`.
- Never disable a lint or analyzer rule with a comment to get past it. Change the code; if the rule is wrong, change the rule's config on purpose and record why.

## Errors

- Separate an **expected failure** (not found, invalid input, outside system refused) from a **bug**. Expected failures are returned as results with a message a person can act on. Bugs throw.
- An error carries a clear message and its cause. Never throw away the original error.
- Never swallow an error silently. Catching to skip one bad input (one malformed file in a scan) is allowed when a comment says why.
- An error message never contains a secret.

## Formatting and checks

- A formatter decides layout, not a person: 120-character lines, consistent indentation, sorted imports. Run it on every file you edit.
- Every lint/analyzer rule is an error, not a warning. The checks pass only when they report nothing.
- Before calling a change done, run the project's checks in the order CI runs them (format, lint/analyze, type-check/build, test), and fix everything they report.
