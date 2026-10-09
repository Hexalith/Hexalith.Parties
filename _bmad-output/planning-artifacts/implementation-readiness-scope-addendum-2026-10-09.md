---
date: 2026-10-09
project: parties
type: implementation-readiness-scope-addendum
decision: spec-only-external-consumer-extension
---

# EXT-PARTIES-1 readiness scope addendum

The 2026-10-09 sprint planning CONCERNS finding identified active
`EXT-PARTIES-1` Branch B work with no Parties UI PRD requirement, epic/story,
or sprint-status entry. The [course correction](sprint-change-proposal-2026-10-09.md)
classifies it as a spec-only Agents-consumer extension outside Parties sprint
tracking. The [owner spec](../implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md)
is its acceptance source; the [consumer binding ADR](adr-consumer-party-id-binding.md)
and [actor-history policy](actor-history-retention-policy-v1.md) govern its
identity and retention decisions.

Epics 7 and 8 remain maintenance only, with zero new PRD functional coverage.
Readiness reports must keep `EXT-PARTIES-1` separate from Parties UI MVP
requirements, Epic 7/8 completion, and sprint progress. Do not infer an epic or
story key from the spec's `feature` type or `in-progress` status. Agents Story
5.4 dependency status is a consumer-side record, not Parties sprint status.

The extension changes public Contracts, Party identity history, and host policy.
Local source/test evidence is recorded in the spec and companion notes; it does
not establish installed availability. Production custody/restore qualification,
actor-free continuation, complete owner compatibility acceptance, and live
P-01–P-10 evidence remain open. A readiness check must report those gates
separately and must not count the extension as delivered until they pass.
