# Design patterns (common)

When to reach for one of the 22 classic patterns, and when not to. The catalogue and names follow [refactoring.guru](https://refactoring.guru/design-patterns/catalog). How each one looks in C#, and where this repo already uses it, is in `dotnet/design-patterns.md`.

## The rules

1. **Start with plain code.** A function, an interface with one implementation, a `switch`. Introduce a pattern only when a real, present problem matches its intent, never because a change *might* need it later.
2. **Name the problem first.** Before adding a pattern, say in one sentence which problem it solves here ("three config formats need the same parse step, and more are coming"). If you can't, don't add it.
3. **Prefer the language feature.** Many patterns are workarounds for missing language features (refactoring.guru's criticism: "Strategy can be replaced by a lambda"). Use a function value, a lambda, an iterator, an event, a record copy or pattern matching first; build the class-based form only when the feature can't carry the state or the variety.
4. **Adapt, don't copy the diagram.** Use the parts of a pattern that solve the problem. No `ConcreteStrategyA`, `AbstractFactoryImpl` or empty base classes kept just because the textbook has them.
5. **Composition over inheritance.** Patterns that work by wrapping or delegating (Strategy, Decorator, Adapter, Proxy, Composite) are preferred to those that need a class hierarchy (Template Method, Factory Method by subclassing).
6. **Name by role, not by pattern.** `EnvFileParser`, `CredentialStoreFactory`, `UnlockService`, not `ParserStrategyImpl` or `CredentialSingleton`. Use a pattern's name only where it *is* the role (`…Factory`, `…Builder`, `…Adapter`) and the reader gains from knowing it.
7. **Record it.** A pattern that shapes a whole area (a registry of parsers, a pipeline of named steps) is written down in the project's architecture contract, so the next change follows it instead of inventing a second way.
8. **Remove a pattern that stopped paying.** If only one implementation is left, or the indirection hides more than it helps, inline it.

## Creational: how objects get made

| Pattern | Use it when | Don't, use instead |
| --- | --- | --- |
| **Factory Method** | Callers need an object but shouldn't know which concrete type (chosen by OS, config, file type). | One concrete type: just `new` it. Prefer a static factory function over subclassing a creator. |
| **Abstract Factory** | Several related objects must be created as a matching family (a whole set of platform-specific services). | One object type: a factory function. Rare in app code. |
| **Builder** | An object has many optional parts, or must be assembled step by step and validated at the end (connection strings, queries, requests). | A constructor or object initializer with named/required properties covers most cases. |
| **Prototype** | You need a copy of an existing object without knowing its concrete type, or copying is cheaper than building. | Immutable records copied with a change (`with`, spread). Never a deep-copy framework. |
| **Singleton** | Almost never. refactoring.guru: many consider it an antipattern; it has the pros and cons of a global variable and makes testing hard. | One instance created in the composition root and passed in (a DI "singleton lifetime"). Pure functions need no instance at all. |

## Structural: how objects fit together

| Pattern | Use it when | Don't, use instead |
| --- | --- | --- |
| **Adapter** | Your code expects one interface and an outside library, OS API or legacy code offers another. Every outside system gets one. | Calling the outside API directly from services or the interface. |
| **Bridge** | Two dimensions vary independently (what is shown × where it is sent) and subclassing every combination would explode. | Usually unnecessary; two interfaces passed in are already a bridge. |
| **Composite** | The data is a tree and callers treat a leaf and a group the same way (folders and files, menu groups and items). | A flat list when there is no real nesting. |
| **Decorator** | Add behaviour (caching, logging, retry limits, timing) around an object without changing it or its callers. | Flags inside the object; inheritance to add one behaviour. |
| **Facade** | A subsystem needs many calls in the right order, and callers only want "do the thing" (one service operation in front of parsing + merging + saving). | Exposing every lower-level piece to the interface layer. |
| **Flyweight** | Huge numbers of objects share identical immutable parts and memory is a measured problem. | Premature. Measure first. |
| **Proxy** | Control access to an object: lazy creation, an access check, a remote or expensive resource behind the same interface. | A plain wrapper is a Decorator; if it adds behaviour rather than controlling access, call it that. |

## Behavioural: how objects divide the work

| Pattern | Use it when | Don't, use instead |
| --- | --- | --- |
| **Chain of Responsibility** | A request passes through ordered handlers, each of which may handle, change or stop it (middleware, validation steps). | A fixed sequence of named steps in one function is clearer when the order never changes at runtime. |
| **Command** | An action must be passed around as a value: bound to a button, queued, undone, logged or retried. | A direct method call when nothing needs to hold the action. |
| **Iterator** | Callers walk a collection or a lazy stream without knowing how it's stored (walking a folder tree). | The language's built-in iteration (`foreach`/`for…of`, generators). Never a hand-written iterator class. |
| **Mediator** | Many objects (screens, panels) would otherwise all reference each other. | A single service the parties call. Avoid a global message bus that hides who calls whom. |
| **Memento** | State must be saved and restored without exposing internals (undo, "cancel edits"). | Immutable snapshots: keep the previous record. |
| **Observer** | Several parts must react when something changes, and the source shouldn't know them (UI binding, progress). | Built-in events, observable properties, progress callbacks. Unsubscribe when done. |
| **State** | An object's behaviour changes by state and the `if (status == …)` checks are spreading across many methods. | A `switch` over an enum in one place is fine for a few states. |
| **Strategy** | One step has several interchangeable algorithms chosen at runtime (parsers per file format, exporters per client). | A function value or lambda when the algorithm has no state of its own. |
| **Template Method** | Rarely. A fixed algorithm with steps subclasses override. | Strategy or plain functions passed in; inheritance couples every subclass to the base. |
| **Visitor** | Many operations over a stable set of node types (a syntax tree). | Pattern matching / `switch` over the types. Rare in app code. |

## Smells that suggest a pattern

- The same `switch` on a type or kind appears in several places → **Strategy** or **State**.
- Adding a format/provider means editing an existing class → **Strategy + a registry** (one new class, one registration line).
- Services call an outside library's API directly, with its types leaking upward → **Adapter** behind an interface.
- Callers must call five things in the right order → **Facade** (one service operation).
- A constructor with many optional parameters, or objects built half-valid → **Builder**.
- A global `Instance` property → replace the **Singleton** with one instance passed in.
- Cross-cutting behaviour (timing, caching) copied into many methods → **Decorator**.

## Smells that a pattern is overused

- An interface with exactly one implementation and no test fake, for a thing that will never vary.
- Class names that are just pattern names (`Manager`, `Handler`, `Processor`, `Impl`).
- A factory that only calls `new` on one type.
- A layer that only forwards calls without adding anything.
- Reading one feature means jumping through five files of indirection.
