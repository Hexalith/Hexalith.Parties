---
project: Hexalith.Parties
date: 2026-05-17
source_report: implementation-readiness-report-2026-05-17.md
status: Approved
approved_date: 2026-05-17
change_scope: Moderate
recommended_path: Direct Adjustment
---

# Sprint Change Proposal: Implementation Readiness Cleanup

## 1. Issue Summary

The Implementation Readiness Assessment completed on 2026-05-17 found Hexalith.Parties ready for MVP implementation, with later-phase dependency and traceability items tracked for scheduling.

The trigger is the assessment status: **READY FOR MVP IMPLEMENTATION**.

Evidence from the readiness report:

- All required planning documents exist.
- All 74 PRD functional requirements are covered by epics.
- UX is aligned with PRD and architecture.
- All 69 stories have acceptance criteria.
- No critical issues were found.
- The assessment identified 6 attention items: 1 major later-phase dependency gate, 2 minor planning/traceability cleanups, and 3 UX scheduling warnings.
- The major item is tracked in `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` and does not block MVP implementation.

The issue is not a scope failure. It is an execution-boundary and scheduling risk: v1.2 UI work depends on an accepted EventStore-fronted Parties client/gateway contract that must be satisfied or risk-accepted before scheduling Epic 7 or Epic 8 UI implementation. Minor traceability and NFR labeling items are already represented in the planning handoff.

## 2. Impact Analysis

### Epic Impact

Epic 2 currently titled **Searchable Tenant-Safe Read Models** remains valid, but its title and early stories can lead implementation toward projection internals instead of consumer-observable outcomes. Stories 2.1, 2.2, and 2.8 should be tightened with executable verification paths that prove detail query, list/search, health, rebuild, and degraded read behavior.

Story 2.9 remains valid as v1.1 preparation, but should be explicitly labeled as non-feature preparation so MVP implementation does not advertise semantic search or temporal query runtime behavior.

Epic 5 remains valid, but Stories 5.6 and 5.7 should be labeled as non-feature preparation where they anticipate future merge/GDPR behavior. Their MVP deliverables should stay constrained to contract compatibility and documentation.

Epic 6 remains valid. Story 6.10 covers NFR11 rather than a functional requirement and should be labeled as operational/security NFR coverage in planning.

Epics 7 and 8 remain valid, but Stories 7.6, 7.7, 8.1, 8.2, 8.3, and 8.6 should not be scheduled until the accepted EventStore-fronted Parties client/gateway dependency is recorded with enough detail to verify capability detection, query/command methods, route support, and failure semantics.

### Story Impact

Affected stories:

- Story 2.1: Build Party Detail Projection
- Story 2.2: Build Tenant Party Index Projection
- Story 2.8: Projection Rebuild and Health Monitoring
- Story 2.9: Prepare Deferred Search and Temporal Query Extensions
- Story 5.6: Prepare Forward-Compatible Party Lifecycle Events
- Story 5.7: Document Erasure Subscriber Responsibilities
- Story 6.10: Rotate Tenant Encryption Keys
- Story 7.6: Gate GDPR Operations on Accepted Client Contract
- Story 7.7: Implement GDPR Operation Panels
- Story 8.1: Compose Embeddable Party Picker Shell
- Story 8.2: Implement Typeahead Search and Bounded Results
- Story 8.3: Emit Durable Selection by Party Id
- Story 8.6: Enforce Picker Privacy and Integration Boundary

### Artifact Conflicts

No PRD, architecture, or UX contradiction was found.

The PRD already identifies MVP, v1.1, v1.2, and future scope boundaries. Architecture already supports the EventStore-fronted admin/picker direction. UX already states the admin portal is blocked until the accepted client/gateway contract exposes required typed query and command capabilities.

The needed corrections are therefore limited to:

- `epics.md` story title/metadata/acceptance-criteria clarifications.
- A dependency record for the accepted EventStore-fronted Parties client/gateway contract.
- Sprint-status or planning metadata after the proposal is approved.

### Technical Impact

No code rollback or architecture rework is recommended.

The technical impact is planning and verification precision:

- Projection stories should prove observable query/health/rebuild outcomes, not only internal projection state.
- Deferred stories should not imply unavailable runtime behavior in MVP.
- v1.2 UI stories should have an explicit dependency gate before scheduling.
- QA traceability should continue tracking UX-DR identifiers, not only PRD FR/NFR identifiers.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Rationale:

- The PRD, architecture, UX, and epics are fundamentally aligned.
- Functional coverage is complete.
- The issues are local to story quality, scheduling labels, and dependency tracking.
- No completed implementation needs to be rolled back.
- MVP scope remains achievable if the readiness cleanup is done before execution.

Effort estimate: **Low to Medium**.

Risk level after correction: **Low**.

Timeline impact: small planning cleanup before implementation, with larger risk avoided for v1.2 scheduling.

Alternative paths considered:

- Rollback: not applicable because the issue is in planning artifacts, not a failed implementation path.
- PRD MVP review: not needed because MVP scope and requirement coverage remain valid.

## 4. Detailed Change Proposals

### Proposal A: Rename Epic 2 for User-Observable Value

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Epic 2 title and FR coverage map entries

OLD:

```markdown
Epic 2 - Searchable Tenant-Safe Read Models
```

NEW:

```markdown
Epic 2 - Tenant-Safe Party Search and Retrieval
```

Rationale:

The current title is architecture-shaped. The revised title keeps the same scope while orienting implementation around consumer search, retrieval, filtering, freshness, and tenant safety.

### Proposal B: Add Observable Query Verification to Story 2.1

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 2.1 Build Party Detail Projection

Section: Acceptance Criteria

OLD:

```markdown
**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative party event sequences
**Then** they verify detail state after create, update, contact channel, identifier, deactivate, and reactivate flows
**And** they verify event ordering assumptions documented by the architecture.
```

NEW:

```markdown
**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative party event sequences
**Then** they verify detail state after create, update, contact channel, identifier, deactivate, and reactivate flows
**And** they verify event ordering assumptions documented by the architecture.

**Given** a party detail projection has been built for the current tenant
**When** the same party is retrieved through the documented detail-query path
**Then** the query returns the projected party details with active status, type-specific details, display/sort names, contacts, identifiers, and timestamps
**And** the result proves the projection is observable through consumer-facing read behavior rather than only internal handler state.
```

Rationale:

This preserves the technical projection work while proving the user-observable outcome inside the same story.

### Proposal C: Add Observable List/Search Verification to Story 2.2

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 2.2 Build Tenant Party Index Projection

Section: Acceptance Criteria

OLD:

```markdown
**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative tenant event streams
**Then** they verify create, update, deactivate, reactivate, date metadata, and duplicate delivery behavior
**And** the handler remains free of DAPR dependencies.
```

NEW:

```markdown
**Given** projection handler tests run without DAPR infrastructure
**When** they replay representative tenant event streams
**Then** they verify create, update, deactivate, reactivate, date metadata, and duplicate delivery behavior
**And** the handler remains free of DAPR dependencies.

**Given** a tenant party index projection has been built for the current tenant
**When** consumers list, filter, or display-name search parties through the documented read path
**Then** results are served from the tenant-safe index with pagination, type/status/date filtering, display-name matching, and bounded match metadata where applicable
**And** the story demonstrates that the index enables observable consumer browsing and search behavior.
```

Rationale:

The story remains projection-focused but can no longer complete as pure infrastructure without proving consumer list/search value.

### Proposal D: Tighten Story 2.8 Health and Rebuild Verification

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 2.8 Projection Rebuild and Health Monitoring

Section: Acceptance Criteria

OLD:

```markdown
**Given** projection health/rebuild tests run
**When** they simulate healthy, corrupt, rebuilding, successful rebuild, failed rebuild, and cross-tenant conditions
**Then** status transitions and read responses match the documented behavior.
```

NEW:

```markdown
**Given** projection health/rebuild tests run
**When** they simulate healthy, corrupt, rebuilding, successful rebuild, failed rebuild, and cross-tenant conditions
**Then** status transitions and read responses match the documented behavior.

**Given** projection health is exposed through the service readiness or operational health surface
**When** a projection is healthy, rebuilding, degraded, or unsafe
**Then** the health/readiness response reports the bounded projection state without personal data
**And** detail, list, and search reads show the matching freshness/degradation behavior.
```

Rationale:

The readiness report specifically called out rebuild/health behavior as needing a user- or operator-observable verification path.

### Proposal E: Label Deferred Preparation Stories as Non-Feature Preparation

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories: 2.9, 5.6, 5.7

Section: metadata immediately below each story title

OLD example:

```markdown
**Coverage type:** prepared/deferred
```

NEW example:

```markdown
**Coverage type:** non-feature preparation / prepared-deferred
**Scheduling label:** preparation-only; does not deliver active MVP runtime behavior for deferred capabilities
```

Apply equivalent wording to:

- Story 2.9: semantic search and temporal query preparation only.
- Story 5.6: forward-compatible lifecycle/GDPR contract preparation only.
- Story 5.7: future erasure subscriber responsibility documentation only.

Rationale:

These stories are useful, but sprint planning must not treat them as feature delivery for deferred v1.1 or future behavior.

### Proposal F: Mark Story 6.10 as NFR/Security Operational Coverage

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 6.10 Rotate Tenant Encryption Keys

Section: metadata immediately below story title

OLD:

```markdown
**Coverage type:** implemented when v1.1 is scheduled
**Requirements covered:** NFR11
**Requirements supported:** FR53, FR55
```

NEW:

```markdown
**Coverage type:** operational/security NFR coverage when v1.1 is scheduled
**Requirements covered:** NFR11
**Requirements supported:** FR53, FR55
**Scheduling label:** NFR/security story; not counted as functional requirement coverage
```

Rationale:

This keeps important security work in Epic 6 while preventing confusion in FR traceability.

### Proposal G: Create Accepted Client/Gateway Dependency Record Before Epic 7 or 8 Scheduling

New artifact:

`_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`

Proposed content:

```markdown
# Dependency: Accepted EventStore-Fronted Parties Client/Gateway Contract

Status: Required before scheduling Epic 7 or Epic 8 v1.2 UI work

Affected stories:
- Story 7.6
- Story 7.7
- Story 8.1
- Story 8.2
- Story 8.3
- Story 8.6

The dependency is satisfied when an accepted EventStore-fronted Parties client/gateway contract exists and documents:
- Typed query methods required by admin browse/search/detail and picker typeahead/selection display.
- Typed command methods required by GDPR operation panels.
- Capability detection for unavailable, partially available, malformed, stale, and tenant-switch states.
- FrontComposer route support for `/admin/parties`, `/admin/parties/{partyId}`, and `/admin/parties/{partyId}/gdpr`.
- Failure semantics for unauthorized, forbidden, not found, gone/erased, degraded, timeout, malformed response, and contract unavailable states.
- Boundary rules prohibiting retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Privacy rules for tokens, tenant context, party data, logs, telemetry, storage keys, URLs, filenames, and callbacks.

No Epic 7 or Epic 8 implementation story should be scheduled until this dependency is either satisfied or explicitly accepted as a blocking risk by product and architecture owners.
```

Rationale:

The readiness report found this dependency is real and externally blocking. A dedicated record makes the scheduling gate visible without changing the PRD or architecture.

### Proposal H: Preserve UX-DR Traceability in QA and Sprint Tooling

Artifact: sprint planning and QA traceability metadata

OLD:

```markdown
Traceability checks may rely primarily on FR/NFR identifiers.
```

NEW:

```markdown
Traceability checks for Epic 7 and Epic 8 must include UX-DR identifiers in addition to PRD FR/NFR identifiers.
```

Rationale:

UX-DR1 through UX-DR32 are the detailed source of admin portal and party picker behavior. Losing them in traceability would weaken release readiness for v1.2.

## 5. Checklist Status

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | N/A | Trigger is the readiness assessment, not a single implementation story. |
| 1.2 Core problem | Done | Planning-quality and dependency-tracking cleanup after readiness assessment. |
| 1.3 Supporting evidence | Done | `implementation-readiness-report-2026-05-17.md` records READY FOR MVP IMPLEMENTATION with 6 non-critical attention items. |
| 2.1 Current epic impact | Done | Epic 2 needs title and observable verification cleanup. |
| 2.2 Epic-level changes | Done | Modify Epic 2 title and story metadata/criteria; no new epic needed. |
| 2.3 Future epic impact | Done | Epics 5, 6, 7, and 8 need labeling/dependency clarifications. |
| 2.4 Invalidated epics/new epics | Done | No epic invalidated; no new epic required. |
| 2.5 Priority/order | Done | Do cleanup before MVP execution; gate v1.2 UI scheduling on dependency record. |
| 3.1 PRD conflicts | Done | No PRD conflict; MVP remains achievable. |
| 3.2 Architecture conflicts | Done | No architecture conflict; architecture already supports EventStore-fronted UI boundary. |
| 3.3 UX conflicts | Done | No UX conflict; UX already states contract-unavailable behavior. |
| 3.4 Secondary artifacts | Done | Sprint-status/planning metadata records the approved cleanup and v1.2 scheduling gate. |
| 4.1 Direct adjustment | Viable | Low/medium effort, low risk. |
| 4.2 Rollback | Not viable | No failed implementation to revert. |
| 4.3 MVP review | Not needed | MVP scope and FR coverage remain valid. |
| 4.4 Recommended path | Done | Direct Adjustment. |
| 5.1 Issue summary | Done | Included above. |
| 5.2 Epic/artifact impact | Done | Included above. |
| 5.3 Path forward | Done | Direct Adjustment recommended. |
| 5.4 MVP impact/action plan | Done | No MVP scope reduction; cleanup required before execution. |
| 5.5 Handoff plan | Done | Product Owner/Developer for backlog edits; Architect/Product Owner for dependency acceptance. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Based on readiness report and impacted story text. |
| 6.3 User approval | Done | Approved by Jérôme on 2026-05-17. |
| 6.4 Sprint-status update | Done | `_bmad-output/implementation-artifacts/sprint-status.yaml` records the approved cleanup and dependency gate. |
| 6.5 Handoff confirmation | Done | MVP implementation may proceed; Epic 7 and Epic 8 remain gated by the accepted client/gateway dependency. |

## 6. Implementation Handoff

Scope classification: **Moderate**.

Rationale:

The change does not require a fundamental PRD or architecture replan, but it does require backlog/planning artifact edits and dependency tracking across MVP, v1.1, and v1.2 stories.

Handoff recipients:

- Product Owner / Developer agent: apply accepted `epics.md` edits and update sprint-status/planning metadata.
- Architect / Product Owner: create or approve the EventStore-fronted Parties client/gateway dependency record.
- QA/Test Architect: ensure traceability checks include UX-DR identifiers and observable verification for projection/rebuild stories.

Success criteria:

- Epic 2 title is user-outcome-focused.
- Stories 2.1, 2.2, and 2.8 include observable verification paths.
- Stories 2.9, 5.6, and 5.7 are clearly labeled as non-feature preparation.
- Story 6.10 is marked as operational/security NFR coverage.
- The accepted client/gateway dependency record exists before Epic 7 or Epic 8 scheduling.
- Sprint-status or planning metadata reflects approved labels and dependency gates.
- Readiness status is READY FOR MVP IMPLEMENTATION; later-phase UI scheduling remains gated until the accepted client/gateway dependency is satisfied or risk-accepted.

## 7. Approval

Approved by Jérôme on 2026-05-17.

The approved artifact updates are:

- Update `epics.md` with the approved Epic 2 rename, observable verification criteria, and scheduling labels.
- Create the accepted EventStore-fronted Parties client/gateway dependency record.
- Update sprint tracking metadata to reflect the readiness cleanup and v1.2 scheduling gate.
