# csharp-project-004-maui-enterprise-patterns

An authoritative, production-oriented engineering reference for enterprise application
architecture and patterns using .NET MAUI: credible, tested, documented implementations grounded
in current Microsoft guidance, verified rather than assumed.

Grounded in Microsoft's **Enterprise Application Patterns using .NET MAUI, Edition v2.0** (Microsoft
Developer Division), used for context and cross-checked against current guidance before being
adopted — never reproduced blindly. See `docs/maui-enterprise-pattern-catalogue-v1.0.0.md` for the
full catalogue and its source citation.

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
`coverlet.runsettings`). See the catalogue for the current scope: this repository builds one
pattern at a time, each proven end to end before the next begins.

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

## Governance

This repository follows the same governance, documentation and engineering conventions as its
sibling repositories in this account (`csharp-project-001-gof-design-patterns`,
`csharp-project-002-cloud-design-patterns`, `csharp-project-003-solid-principles`), adapted where
.NET MAUI requires a documented difference. See `config/markdown-governance.policy.json`.

## Licence

MIT — see `LICENSE`.
