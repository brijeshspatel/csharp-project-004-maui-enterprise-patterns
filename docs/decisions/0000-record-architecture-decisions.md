---
doc_id: adr-0000
title: "Record architecture decisions"
type: adr
version: 1.0.0
status: active
created: 2026-08-22
updated: 2026-08-22
supersedes: null
superseded_by: null
change_summary: "Establishes ADRs as the record format for architectural decisions."
---

# 0000. Record architecture decisions

- Status: accepted
- Date: 2026-08-22

## Context and problem statement

Architectural decisions get made and then lose their reasoning. The decision survives in
the code; the options it beat, and the costs knowingly accepted, do not. Revisiting one
then means re-deriving context nobody wrote down.

## Considered options

1. Record decisions informally, in prose or commit messages.
2. Adopt Architecture Decision Records (Nygard / MADR).

## Decision outcome

Chosen: **ADRs, MADR format**, in `docs/decisions/` as `NNNN-title.md`.

Sequential numbering is a deliberate exception to the MDGS date-first filename grammar.
It is the recognised ADR convention, ADR tooling depends on it, and the sequence conveys
decision *order*, which dates cannot when two land the same day.

### Consequences

- Good: decisions carry reasoning, alternatives and accepted costs.
- Good: superseding is explicit rather than implied by a later edit.
- Bad: one naming exception to explain.
- Neutral: existing decisions are not back-filled; ADRs start from now.

## Verification

A decision is properly recorded when a reader who was not present can state what was
chosen, what it beat, and what was accepted as a cost -- without asking anyone.
