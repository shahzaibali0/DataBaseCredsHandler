# .agents: every instruction for coding agents

This folder is the single home for agent instructions in this repo: rules, skills, subagents, decisions and known debt. Nothing outside it gives instructions. `AGENTS.md` and `CLAUDE.md` at the root only point here.

## Read in this order

1. [rules/project/overview.md](rules/project/overview.md): what the app is, what must never break, constraints, vocabulary, commands, build order.
2. Every file in [rules/common/](rules/common/): how to work and write code in any stack.
3. The files in [rules/dotnet/](rules/dotnet/) for the kind of file you're touching.
4. The skill in [skills/](skills/) for the area you're changing.

Claude Code loads `rules/` itself through `.claude/rules` (files without `paths:` every session, the others when it reads a matching file). It does **not** load a scoped rule when it *creates* a new file, so read the matching one yourself first. Other tools: open the files above yourself.

## Layout

```
.agents/
  README.md            this index
  decisions.md         every choice and why, plus what's still open
  rules/
    project/           this project's facts and constraints (always loaded)
    common/            rules for any stack, shared with the owner's Next.js projects
    dotnet/            how the common rules apply in C#, WPF and xunit
  skills/<name>/       detailed, area-specific rules and specs (SKILL.md + references)
  agents/<name>.md     subagent definitions
```

`.claude/rules`, `.claude/skills` and `.claude/agents` are relative symlinks to `rules/`, `skills/` and `agents/` here (on Windows, clone with Developer Mode on and `core.symlinks=true`), so Claude Code finds them in its usual places. Edit the files here, never through a copy.

## Rules

| File | Loads | Covers |
| --- | --- | --- |
| [project/overview.md](rules/project/overview.md) | always | Purpose, what matters most, commands, stack, layout, constraints, vocabulary, done, build order, git |
| [common/working-style.md](rules/common/working-style.md) | always | Think first, simplicity, surgical changes, verifiable goals, honest reports, decisions, subagents |
| [common/architecture.md](rules/common/architecture.md) | always | One home per kind of code, one entry → one service, calls go down, save first, input/config/logging, outside systems, jobs |
| [common/code-style.md](rules/common/code-style.md) | always | Names, shape, file order, size, comments, errors, formatting and checks |
| [common/security.md](rules/common/security.md) | always | Secrets, input, scope, review |
| [common/design-patterns.md](rules/common/design-patterns.md) | always | The 22 refactoring.guru patterns: when to use each, when not, the simpler alternative, smells |
| [common/testing.md](rules/common/testing.md) | tests | Writing a test, proving it fails, test data, folders |
| [dotnet/architecture.md](rules/dotnet/architecture.md) | `.cs`, `.csproj`, `.xaml` | Homes as projects, references as layers, composition root, config, logging, async, atomic files |
| [dotnet/code-style.md](rules/dotnet/code-style.md) | `.cs` | Limits (60-line methods, 400-line files, 3 params), C# naming, types, exceptions, comments, `dotnet format` |
| [dotnet/design-patterns.md](rules/dotnet/design-patterns.md) | `.cs`, `.xaml` | Each pattern as C#/.NET idioms, and where this repo already uses it |
| [dotnet/testing.md](rules/dotnet/testing.md) | tests | xunit setup, naming, fakes over mocks, temp folders, integration traits |
| [dotnet/wpf.md](rules/dotnet/wpf.md) | `.xaml`, views, viewmodels | MVVM, binding and threading, look and feel, secrets on screen |

## Skills

If your tool does not load skills on its own, open the `SKILL.md` and follow it when the task matches.

| Skill | Use it to |
| --- | --- |
| [architecture-contract](skills/architecture-contract/SKILL.md) | Decide where code belongs: layers, project references, owners, patterns in use. [WHAT_TO_FIX.md](skills/architecture-contract/WHAT_TO_FIX.md) lists the debt |
| [credential-safety](skills/credential-safety/SKILL.md) | Write or review anything that reads, shows, copies, logs or passes a password |
| [scan-and-merge](skills/scan-and-merge/SKILL.md) | Change the scanner, a parser or the merge rules, or add a config format |
| [db-client-integrations](skills/db-client-integrations/SKILL.md) | Build connection strings, the connection test, DBeaver launch, pgAdmin export |
| [wpf-mvvm-ui](skills/wpf-mvvm-ui/SKILL.md) | Build XAML, viewmodels, app services and startup |
| [xunit-testing](skills/xunit-testing/SKILL.md) | Write or run tests, or reproduce a bug as a failing test |

## Subagents

| Agent | Use it to |
| --- | --- |
| [integrations-builder](agents/integrations-builder.md) | Build or change `PgCredsHandler.Integrations` |
| [test-writer](agents/test-writer.md) | Add xunit coverage, or reproduce a parsing/merge bug as a failing test |
| [wpf-ui-builder](agents/wpf-ui-builder.md) | Build XAML, viewmodels and app services |
| [secrets-reviewer](agents/secrets-reviewer.md) | Read-only review of password handling after Storage, Integrations or App changes |

Subagents may not receive the rule files. Whoever starts one tells it to read this file, `rules/project/overview.md` and the rules and skill for its area.

## Decisions and debt

- [decisions.md](decisions.md): why each rule exists. New decisions go under "Project decisions", undecided ones under "Open".
- [skills/architecture-contract/WHAT_TO_FIX.md](skills/architecture-contract/WHAT_TO_FIX.md): code that breaks a rule today. It may only get better.
