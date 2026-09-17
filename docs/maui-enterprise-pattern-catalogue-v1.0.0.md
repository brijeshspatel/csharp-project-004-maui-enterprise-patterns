---
doc_id: maui-enterprise-pattern-catalogue
title: Enterprise .NET MAUI pattern catalogue
type: index
version: 1.0.0
status: active
created: 2026-08-22
updated: 2026-09-17
owner: Brijesh Patel
change_summary: Thirteen entries across five categories. Twelve are implemented and one remains a candidate. Presentation & MVVM, Application Infrastructure, Data & Integration and Resilience & Connectivity are complete; only Authorization remains. The version stays 1.0.0 deliberately: the filename carries it, and both README.md and tools/pattern_relationship_check.py cite that path.
---

# Enterprise .NET MAUI pattern catalogue

The contract for this repository. It records which patterns exist, how they are grouped, the gloss
each category carries, and the section order every pattern README follows.

**An entry that links to a folder rather than to a README is not yet done.** A `candidate` entry has
not been confirmed for implementation by any specification; a `confirmed` entry has been specified,
whether or not it is built yet.

## Source

Microsoft. *Enterprise Application Patterns using .NET MAUI*, Edition v2.0. Microsoft Developer
Division, .NET, and Visual Studio product teams. Authored by Michael Stonis (Eight-Bot); reviewed
by James Montemagno and David Pine (Microsoft). Retrieved 2026-08-22.

Used for context and grounding. Every entry is verified against current, officially supported
Microsoft guidance before being confirmed — the reference material is a starting point, not
reproduced blindly.

## Categories

| Category | Gloss |
|---|---|
| **Presentation & MVVM** | Structuring the view layer so it is testable, declarative and decoupled from platform UI |
| **Application Infrastructure** | Composing and configuring the application itself, independent of any one feature |
| **Data & Integration** | Reaching external systems and services the application depends on |
| **Resilience & Connectivity** | Surviving the failures a networked mobile client actually experiences |
| **Security** | Establishing and enforcing who the application is acting for, and what they may do |

Testing is not a category. It is cross-cutting practice, mandatory for every entry regardless of
category.

## Entries

| Category | Entry | Status |
|---|---|---|
| Presentation & MVVM | [Model-View-ViewModel](../patterns/Mvvm/README.md) | confirmed, implemented |
| Presentation & MVVM | [Commanding and Behaviours](../patterns/CommandBehavior/README.md) | confirmed, implemented |
| Presentation & MVVM | [Loosely-Coupled Messaging](../patterns/Messaging/README.md) | confirmed, implemented |
| Presentation & MVVM | [Navigation](../patterns/Navigation/README.md) | confirmed, implemented |
| Presentation & MVVM | [Validation](../patterns/Validation/README.md) | confirmed, implemented |
| Application Infrastructure | [Dependency Injection](../patterns/DependencyInjection/README.md) | confirmed, implemented |
| Application Infrastructure | [Application Settings Management](../patterns/AppSettings/README.md) | confirmed, implemented |
| Data & Integration | [Accessing Remote Data (REST)](../patterns/RemoteData/README.md) | confirmed, implemented |
| Data & Integration | [Containerized Service Integration](../patterns/ContainerServices/README.md) | confirmed, implemented |
| Resilience & Connectivity | [Retry](../patterns/Retry/README.md) | confirmed, implemented |
| Resilience & Connectivity | [Circuit Breaker](../patterns/CircuitBreaker/README.md) | confirmed, implemented |
| Security | [Authentication](../patterns/Authentication/README.md) | confirmed, implemented |
| Security | Authorization | candidate |

## README section order

Every pattern README, starting with Model-View-ViewModel, follows this order:

| Section | Contains |
|---|---|
| Title and subtitle | The pattern, and beneath it its category and that category's gloss above, worded identically |
| Intent | One sentence: the problem this pattern addresses |
| The problem and context | The business or technical context |
| The idiomatic approach | The current-guidance implementation |
| The manual alternative, where the reference material draws one | Only where applicable |
| Why the idiomatic approach is preferable | What improved, checkable against the code |
| Architecture and components | Participants, dependencies, a Mermaid diagram |
| When to apply it | Conditions that make it the right choice |
| When not to — over-application | Misuse and over-engineering, not optional |
| Production-readiness considerations | The applicable subset of the workspace's quality list |
| Trade-offs | What it buys and what it costs |
| Relationships | Other catalogue entries and related design patterns — prose and diagrams only, no code reference |
| What the tests assert | What the tests are about, never how many |
