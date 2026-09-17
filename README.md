# csharp-project-004-maui-enterprise-patterns

An authoritative, production-oriented engineering reference for enterprise application
architecture and patterns using .NET MAUI: credible, tested, documented implementations grounded
in current Microsoft guidance, verified rather than assumed.

Grounded in Microsoft's **Enterprise Application Patterns using .NET MAUI, Edition v2.0** (Microsoft
Developer Division), used for context and cross-checked against current guidance before being
adopted — never reproduced blindly. See `docs/maui-enterprise-pattern-catalogue-v1.0.0.md` for the
full catalogue and its source citation.

**All thirteen entries are implemented.** Each one builds, is tested against its `.Core` project at
100% line coverage, carries its own README and diagram, and states what it is **not** for.

## The patterns

`docs/maui-enterprise-pattern-catalogue-v1.0.0.md` is the contract: it defines the section order
every entry's README follows and records each entry's status. **The table below is a way in, not a
second source of truth** — where the two disagree, the catalogue is right.

### Presentation & MVVM

*Structuring the view layer so it is testable, declarative and decoupled from platform UI*

| Entry | What it settles |
|---|---|
| [Model-View-ViewModel](patterns/Mvvm/README.md) | The view layer's shape, and what belongs on which side of it |
| [Commanding and Behaviours](patterns/CommandBehavior/README.md) | Reaching UI events without code-behind |
| [Loosely-Coupled Messaging](patterns/Messaging/README.md) | Talking between view models that must not reference one another |
| [Navigation](patterns/Navigation/README.md) | Moving between pages, and passing parameters, from a project that cannot see MAUI |
| [Validation](patterns/Validation/README.md) | Rules as attributes, including the cross-property ones an attribute cannot express alone |

### Application Infrastructure

*Composing and configuring the application itself, independent of any one feature*

| Entry | What it settles |
|---|---|
| [Dependency Injection](patterns/DependencyInjection/README.md) | Composition, lifetimes, and what .NET MAUI does and does not scope |
| [Application Settings Management](patterns/AppSettings/README.md) | A stored setting is untrusted input from a version that no longer exists |

### Data & Integration

*Reaching external systems and services the application depends on*

| Entry | What it settles |
|---|---|
| [Accessing Remote Data (REST)](patterns/RemoteData/README.md) | Failure as a result, cancellation as an exception, and which failures earn stale data |
| [Containerized Service Integration](patterns/ContainerServices/README.md) | What changes for a mobile client because the services are containers on a developer's machine |

### Resilience & Connectivity

*Surviving the failures a networked mobile client actually experiences*

| Entry | What it settles |
|---|---|
| [Retry](patterns/Retry/README.md) | Which failures may be retried — the decision, not the loop |
| [Circuit Breaker](patterns/CircuitBreaker/README.md) | The state Retry refused to hold, and how not to wedge it shut |

### Security

*Establishing and enforcing who the application is acting for, and what they may do*

| Entry | What it settles |
|---|---|
| [Authentication](patterns/Authentication/README.md) | An access token through its whole life — acquire, cache, expire, refresh once, discard |
| [Authorization](patterns/Authorization/README.md) | What a screen should offer — and why that is never the decision that matters |

**Nothing in either Security entry reaches an identity provider, and no secret exists outside a test
fixture.** The Authorization entry states, first and on its own demonstration screen, that a mobile
client cannot enforce authorisation: it shapes the interface, and the server decides.

## Building

```bash
dotnet build
```

Requires the .NET SDK with the `android`, `ios`, `maccatalyst` and `maui-windows` workloads
installed (`dotnet workload list`).

## Testing

```bash
dotnet test
```

Each pattern separates a platform-agnostic `.Core` project (unit tested, coverage-measured) from a
thin `.Demo` (excluded from the coverage threshold — the exclusion and its reason are declared in
`coverlet.runsettings`).

**What the tests do not cover.** The `.Demo` projects compile for all four platform heads and have
**not been run on a device**, so anything that only happens on a device — `SecureStorage` in the
Authentication entry, for one — is demonstrated rather than proven. Each entry's README says which
of its claims were verified by execution and which were not.

## Structure

```
patterns/<Pattern>/
├── README.md     what the pattern is, how this implementation works, when not to use it
├── docs/         supporting material
├── src/
│   ├── <Pattern>.Core/   platform-agnostic: view models, services, business logic
│   └── <Pattern>.Demo/   the runnable demonstration
└── tests/                xUnit, against .Core only
```

A `.Core` project targets plain `net10.0` and never references .NET MAUI. Where an entry needs a
platform type, `.Core` declares its own abstraction and `.Demo` adapts the platform's — which is why
every entry is testable without a device.

## Governance

This repository follows the same governance, documentation and engineering conventions as its
sibling repositories in this account (`csharp-project-001-gof-design-patterns`,
`csharp-project-002-cloud-design-patterns`, `csharp-project-003-solid-principles`), adapted where
.NET MAUI requires a documented difference. See `config/markdown-governance.policy.json`.

## Licence

MIT — see `LICENSE`.
