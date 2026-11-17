# Working style

How to work on any change, in any stack. These bias toward caution over speed; for a trivial task, use judgment.

## 1. Think before coding

Don't assume. Don't hide confusion. Surface tradeoffs.

- State your assumptions. If uncertain, ask.
- If a request has more than one reading, name them; don't pick one silently.
- If a simpler approach exists, say so. Push back when it's warranted.
- If something is unclear, stop, name what's confusing, and ask.

## 2. Simplicity first

The least code that solves the problem. Nothing speculative.

- No features beyond what was asked.
- No abstraction for code used once.
- No "flexibility" or settings nobody asked for.
- No error handling for cases that cannot happen.
- If 200 lines could be 50, rewrite it. Would a senior engineer call it overcomplicated? Then simplify.

## 3. Surgical changes

Touch only what you must. Clean up only your own mess.

- Don't "improve" nearby code, comments or formatting. Don't refactor what isn't broken.
- Match the style of the file you're in, even if you'd do it differently.
- Unrelated dead code: mention it, don't delete it.
- Remove the imports, variables and functions that *your* change made unused, and nothing else.
- Every changed line traces back to the request.

## 4. Goal-driven execution

Define success, then loop until it's verified.

- "Add validation" → write tests for the invalid inputs, then make them pass.
- "Fix the bug" → write a test that reproduces it, then make it pass.
- "Refactor X" → the tests pass before and after, unchanged.

For a task with several steps, state the plan with a check per step:

```
1. <step> → verify: <check>
2. <step> → verify: <check>
```

## 5. Report honestly

- Say what you ran and what it printed. A check that failed is reported as failed, with its output.
- Say what you could not verify (a prompt that needs a human, a service that isn't running).
- Keep what was reported to you separate from what you confirmed yourself. Never invent a cause.

## 6. Record decisions

- A choice that someone would otherwise re-litigate goes in `.agents/decisions.md`, one line: the choice, then *because* and why.
- Something still undecided goes under "Open" there, not in code comments.
- A rule that a tool can check (lint, analyzer, project reference, hook) should be checked by the tool. A rule only written down will drift.

## 7. Subagents

Subagents may not receive these rule files. When you start one, tell it to read `.agents/README.md` and the rule files for the area it will touch, and put any decision the owner already made into its prompt, because it can't ask.
