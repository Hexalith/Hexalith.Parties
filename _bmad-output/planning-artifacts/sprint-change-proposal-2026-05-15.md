---
project: Hexalith.Parties
date: 2026-05-15
workflow: bmad-correct-course
mode: batch
status: implemented
trigger: implementation-readiness-report-2026-05-15
scope_classification: moderate
approval: approved
implemented: 2026-05-15
---

# Sprint Change Proposal: Phase and Dependency Readiness Cleanup

## 1. Issue Summary

The 2026-05-15 implementation readiness assessment found that Hexalith.Parties is product-complete enough to plan implementation, but the planning artifacts still blur release-phase intent and contain stale dependency references.

The core trigger is not missing PRD coverage: all 74 functional requirements are represented in the epics document. The problem is that roadmap, preparatory, deferred, and implementation-complete coverage currently look equivalent to implementation agents. That creates a risk that MVP planning accidentally pulls v1.1 or v1.2 work forward, or blocks on the stale `Story 12.5` admin client contract wording without a clear current dependency record.

Evidence:

- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-15.md` reports overall status `NEEDS WORK`.
- `epics.md` maps all FR1-FR74, but examples such as Story 2.9 cover deferred FR16 and FR72 through preparation rather than implementation.
- `ux-admin-portal-2026-05-10.md`, UX-DR11, and Story 7.6 refer to `Blocked on Story 12.5 EventStore Parties client contract`, while the current epics document is organized as Epics 1-8.
- `architecture.md` still says `54 FRs` and `54/54 COVERED` even though the PRD and epics now contain 74 FRs.
- Story 6.10 uses non-standard traceability wording: `Requirements covered: Supports FR53, FR55 and NFR11`.

## 2. Impact Analysis

### Epic Impact

Epic 1 through Epic 5 remain valid as the MVP implementation spine. Epic 2 and Epic 5 need coverage metadata that separates active MVP implementation from reserved or future-facing compatibility work.

Epic 6 is a v1.1 GDPR capability set. It should not appear equivalent to MVP execution work without explicit phase metadata.

Epic 7 and Epic 8 are frontend-facing v1.2 capability sets. They remain valuable, but implementation planning needs to know they depend on the accepted EventStore-fronted Parties client/gateway contract and should not block the MVP foundation.

No epic should be removed. No rollback is recommended.

### Story Impact

The following stories need explicit metadata or wording changes before implementation planning:

- Story 2.9: mark as `Phase: v1.1 preparation` and `Coverage type: prepared/deferred`, not implementation-complete for FR16 and FR72.
- Story 5.6: mark as `Phase: MVP preparation / future-compatible contracts` and keep FR37 as implementation coverage while marking GDPR and merge references as preparation unless emitted in MVP.
- Story 5.7: mark as `Phase: MVP documentation preparation / v1.1 dependency` and clarify that FR38 guidance is MVP documentation coverage while FR46 remains future GDPR coverage.
- Stories 6.1-6.10: mark as `Phase: v1.1`.
- Stories 7.1-7.10: mark as `Phase: v1.2`, with Story 7.6 explicitly dependent on the accepted client/gateway contract.
- Stories 8.1-8.6: mark as `Phase: v1.2`.
- Story 6.10: normalize requirement coverage wording so automated parsers do not treat support references as primary coverage.

### Artifact Conflicts

PRD: No product-scope change is required. The PRD already carries phase annotations for MVP, v1.1, v1.2, and future concerns.

Epics: Needs the main repair. Add phase metadata, coverage-type metadata, and a summary coverage map that distinguishes `implemented`, `prepared`, and `deferred`.

Architecture: Needs freshness updates from 54 FRs to 74 FRs and a dedicated party picker architecture subsection. It also needs validation language that separates implemented coverage from deferred architecture support.

UX: Needs dependency wording refreshed so the admin portal blocker points to a named current dependency rather than an orphaned or historical Story 12.5 reference.

Sprint status: No epic/story additions or removals are required by this proposal, so `sprint-status.yaml` does not need structural changes unless the team chooses to schedule the metadata cleanup as explicit backlog work.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Effort estimate: Low to medium.

Risk level: Low if limited to planning artifacts; medium if implementation planning starts before the correction is applied.

Rationale:

- The readiness report found complete FR coverage, so there is no need for MVP scope reduction.
- The identified blockers are traceability and dependency clarity issues, not a failed technical direction.
- Existing PRD phase language is already strong enough to anchor the correction.
- Updating the epics, architecture, and UX documents will make implementation planning deterministic without changing product intent.

Alternatives considered:

- Rollback: Not recommended. No recently completed implementation work is invalidated by the readiness findings.
- MVP Review: Not recommended as a scope-reduction exercise. MVP remains achievable; the issue is that non-MVP work needs clear labels.
- New epic: Not required. The existing epic structure can absorb metadata and dependency clarification.

## 4. Detailed Change Proposals

### Proposal A: Add Release Phase and Coverage Type Legend to `epics.md`

Section: before `## Epic List`

OLD:

```markdown
## Epic List
```

NEW:

```markdown
## Release Phase and Coverage Legend

Every epic and story carries two planning fields:

- `Phase`: `MVP`, `MVP preparation`, `v1.1`, `v1.1 preparation`, `v1.2`, or `future`.
- `Coverage type`: `implemented`, `prepared`, or `deferred`.

`Implemented` means the story delivers the user-visible or system-visible requirement in that phase.
`Prepared` means the story lays contract, documentation, compatibility, or architectural groundwork but does not complete the requirement.
`Deferred` means the PRD requirement is intentionally post-MVP and must not be scheduled as MVP implementation work.

## Epic List
```

Rationale: Gives implementation agents one canonical way to tell current delivery work from roadmap preparation.

### Proposal B: Add Epic-Level Phase Metadata to `epics.md`

Section: each epic summary under `## Epic List`

Representative OLD:

```markdown
### Epic 6: GDPR Compliance Operations

Administrators and DPO workflows can erase, restrict, export, verify, and audit party data with crypto-shredding and subscriber notifications.

**FRs covered:** FR44, FR45, FR46, FR47, FR48, FR49, FR50, FR51, FR52, FR53, FR54, FR55
```

Representative NEW:

```markdown
### Epic 6: GDPR Compliance Operations

Administrators and DPO workflows can erase, restrict, export, verify, and audit party data with crypto-shredding and subscriber notifications.

**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled; deferred for MVP implementation planning
**FRs covered:** FR44, FR45, FR46, FR47, FR48, FR49, FR50, FR51, FR52, FR53, FR54, FR55
```

Apply equivalent metadata:

- Epic 1: `Phase: MVP`, `Coverage type: implemented`
- Epic 2: `Phase: MVP with v1.1 preparation in Story 2.9`, `Coverage type: mixed`
- Epic 3: `Phase: MVP`, `Coverage type: implemented`
- Epic 4: `Phase: MVP`, `Coverage type: implemented`
- Epic 5: `Phase: MVP with v1.1/future preparation in Stories 5.6 and 5.7`, `Coverage type: mixed`
- Epic 6: `Phase: v1.1`, `Coverage type: deferred for MVP`
- Epic 7: `Phase: v1.2`, `Coverage type: deferred for MVP`
- Epic 8: `Phase: v1.2`, `Coverage type: deferred for MVP`

Rationale: Preserves roadmap coverage while making MVP execution boundaries explicit.

### Proposal C: Correct Deferred Coverage for Story 2.9

Section: `Story 2.9: Prepare Deferred Search and Temporal Query Extensions`

OLD:

```markdown
**Requirements covered:** FR16, FR72; future portions of FR15, FR17
```

NEW:

```markdown
**Phase:** v1.1 preparation
**Coverage type:** prepared/deferred
**Requirements prepared:** FR16, FR72; future portions of FR15, FR17
**MVP implementation coverage:** none beyond preserving event/history and contract extension points required by earlier MVP stories
```

Rationale: Prevents FR16 and FR72 from looking implementation-complete in an MVP execution wave.

### Proposal D: Correct Future Coverage for Stories 5.6 and 5.7

Section: `Story 5.6: Prepare Forward-Compatible Party Lifecycle Events`

OLD:

```markdown
**Requirements covered:** FR37
```

NEW:

```markdown
**Phase:** MVP preparation with future lifecycle/GDPR compatibility
**Coverage type:** implemented for FR37 contract compatibility; prepared for future merge/GDPR event behavior
**Requirements covered:** FR37
**Requirements prepared:** future merge behavior and v1.1 GDPR event naming conventions
```

Section: `Story 5.7: Document Erasure Subscriber Responsibilities`

OLD:

```markdown
**Requirements covered:** FR38; supports FR46
```

NEW:

```markdown
**Phase:** MVP documentation preparation for v1.1 GDPR
**Coverage type:** implemented for FR38 guidance; prepared for FR46 subscriber cleanup behavior
**Requirements covered:** FR38
**Requirements prepared:** FR46
```

Rationale: Keeps consumer guidance in MVP while preventing GDPR erasure implementation from being implied before v1.1.

### Proposal E: Replace the Stale Story 12.5 Blocker

Sections: `UX-DR11`, `Story 7.6`, and `ux-admin-portal-2026-05-10.md`

OLD:

```markdown
Blocked on Story 12.5 EventStore Parties client contract
```

NEW:

```markdown
Blocked on accepted EventStore-fronted Parties client/gateway contract
```

Add dependency note near Story 7.6:

```markdown
**Dependency:** Accepted EventStore-fronted Parties client/gateway contract. If the dependency is tracked outside this epics document, reference the external dependency record in implementation planning before scheduling Story 7.6.
```

Rationale: Removes stale numbering while preserving the real dependency.

### Proposal F: Normalize Story 6.10 Traceability

Section: `Story 6.10: Rotate Tenant Encryption Keys`

OLD:

```markdown
**Requirements covered:** Supports FR53, FR55 and NFR11
```

NEW:

```markdown
**Phase:** v1.1
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** NFR11
**Requirements supported:** FR53, FR55
```

Rationale: Makes primary coverage parseable and keeps supporting relationships visible.

### Proposal G: Refresh `architecture.md` Requirements Counts

Sections: `Requirements Overview`, `Requirements Coverage Validation`, `Architecture Completeness Checklist`, and `Architecture Readiness Assessment`

OLD:

```markdown
**Functional Requirements: 54 FRs across 9 groups**
```

NEW:

```markdown
**Functional Requirements: 74 FRs across 11 groups**
```

OLD:

```markdown
**Functional Requirements Coverage: 54/54 COVERED**
```

NEW:

```markdown
**Functional Requirements Coverage: 74/74 COVERED**

Coverage is phase-aware: MVP stories implement the MVP subset, while v1.1/v1.2/future stories and architecture decisions preserve deferred roadmap coverage.
```

OLD:

```markdown
- [x] Project context thoroughly analyzed (54 FRs, 33 NFRs mapped)
...
- Comprehensive requirements coverage validation (54 FRs + 33 NFRs)
```

NEW:

```markdown
- [x] Project context thoroughly analyzed (74 FRs, 33 NFRs mapped)
...
- Comprehensive requirements coverage validation (74 FRs + 33 NFRs)
```

Rationale: Removes traceability noise caused by stale architecture counts.

### Proposal H: Add Party Picker Architecture Subsection

Section: `architecture.md`, near the existing frontend architecture decision or D20 affected areas.

NEW:

```markdown
### Party Picker Frontend Surface

The party picker is a v1.2 embeddable FrontComposer/Blazor component for tenant-safe party search and durable selection. Its durable selection contract is party id only. It must not store or emit display names, contact values, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, or raw query payloads as durable host keys, URLs, telemetry dimensions, filenames, DOM event names, JavaScript event payloads, or logs.

The picker queries through the accepted EventStore-fronted Parties client/gateway boundary. It must not call retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals. Host request/auth context is supplied through accepted client/gateway configuration; the picker does not persist, refresh, parse for authorization, or log tokens.

The picker suppresses stale responses when token, tenant, user, host configuration, selected id, or search options change, and it handles loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, and transient-failure states without leaking personal data.
```

Rationale: Aligns architecture with the UX addendum so Epic 8 implementation has a canonical technical boundary.

## 5. Implementation Handoff

Scope classification: Moderate planning correction.

Recommended recipients:

- Product Owner / Developer agent: apply `epics.md` metadata and traceability wording updates.
- Architect agent: refresh `architecture.md` counts and add the party picker architecture subsection.
- UX/Developer agent: update admin UX blocker wording to remove the stale Story 12.5 reference while preserving the accepted-contract dependency.

Suggested execution order:

1. Update `epics.md` phase and coverage metadata.
2. Normalize Story 12.5 blocker wording across `epics.md` and `ux-admin-portal-2026-05-10.md`.
3. Update Story 6.10 traceability wording.
4. Refresh `architecture.md` 54-to-74 FR count references and coverage summary.
5. Add the party picker architecture subsection.
6. Rerun implementation readiness assessment.

Success criteria:

- MVP stories are distinguishable from v1.1/v1.2/future stories.
- Deferred requirements such as FR16 and FR72 are no longer counted as MVP implementation-complete.
- The admin GDPR contract dependency is explicit and not tied to an orphaned Story 12.5 reference.
- `architecture.md` consistently reflects 74 FRs and 33 NFRs.
- Party picker architecture includes durable selection, stale-response handling, host auth context, and accepted boundary behavior.
- A rerun of readiness no longer reports the 7 issues from the 2026-05-15 assessment.

## 6. Checklist Summary

- [x] 1.1 Triggering issue identified: `implementation-readiness-report-2026-05-15.md`.
- [x] 1.2 Core problem defined: phase/dependency traceability ambiguity, not missing requirements.
- [x] 1.3 Evidence gathered from readiness report, `epics.md`, `architecture.md`, and UX specs.
- [x] 2.1 Current epic impact assessed: no rollback; Epic 2, 5, 6, 7, and 8 need phase clarity.
- [x] 2.2 Epic-level changes identified: metadata only, no new epic required.
- [x] 2.3 Remaining epics reviewed for phase/dependency impact.
- [x] 2.4 No future epics invalidated.
- [x] 2.5 No resequencing required beyond implementation-wave gating.
- [x] 3.1 PRD conflicts checked: no PRD product-scope change required.
- [x] 3.2 Architecture conflicts checked: stale FR counts and party picker architecture gap.
- [x] 3.3 UX conflicts checked: stale Story 12.5 blocker wording.
- [x] 3.4 Other artifacts checked: sprint status does not need structural change for metadata cleanup.
- [x] 4.1 Direct adjustment evaluated as viable.
- [x] 4.2 Rollback evaluated as not viable/necessary.
- [x] 4.3 MVP review evaluated as not necessary for scope reduction.
- [x] 4.4 Direct adjustment selected.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact and action plan defined.
- [x] 5.5 Handoff plan established.
- [x] 6.1 Checklist completion reviewed.
- [x] 6.2 Proposal accuracy reviewed.
- [x] 6.3 User approval received on 2026-05-15.
- [N/A] 6.4 `sprint-status.yaml` structural update not required unless cleanup work is explicitly scheduled.
- [x] 6.5 Final handoff approved for direct planning-artifact edits.

## 8. Implementation Log

Applied after user approval on 2026-05-15:

- Updated `epics.md` with the release phase and coverage legend, epic-level phase metadata, story-level phase metadata, deferred/prepared coverage wording for Stories 2.9, 5.6, and 5.7, normalized Story 6.10 traceability, and refreshed Story 7.6 dependency/blocker wording.
- Updated `ux-admin-portal-2026-05-10.md` to replace the stale Story 12.5 blocker with the accepted EventStore-fronted Parties client/gateway contract dependency.
- Updated `architecture.md` to reflect 74 FRs, phase-aware coverage, 11 FR groups, and a dedicated party picker frontend surface subsection.

## 7. Approval Request

Approve this proposal to proceed with direct planning-artifact edits:

- `yes`: apply the proposed artifact updates.
- `revise`: provide requested changes to the proposal before edits.
- `no`: keep the proposal as a record and do not apply the cleanup.
