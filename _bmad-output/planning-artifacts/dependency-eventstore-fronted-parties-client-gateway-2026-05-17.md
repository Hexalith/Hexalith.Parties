---
project: Hexalith.Parties
date: 2026-05-17
status: Required
dependency_type: External contract / scheduling gate
required_before:
  - Epic 7 implementation scheduling
  - Epic 8 implementation scheduling
source: sprint-change-proposal-2026-05-17.md
---

# Dependency: Accepted EventStore-Fronted Parties Client/Gateway Contract

## Status

Required before scheduling Epic 7 or Epic 8 v1.2 UI implementation work.

## Affected Stories

- Story 7.6: Gate GDPR Operations on Accepted Client Contract
- Story 7.7: Implement GDPR Operation Panels
- Story 8.1: Compose Embeddable Party Picker Shell
- Story 8.2: Implement Typeahead Search and Bounded Results
- Story 8.3: Emit Durable Selection by Party Id
- Story 8.6: Enforce Picker Privacy and Integration Boundary

## Dependency Definition

The dependency is satisfied when an accepted EventStore-fronted Parties client/gateway contract exists and documents the capabilities required by the admin portal and party picker.

The accepted contract must include:

- Typed query methods required by admin browse, search, detail, and picker typeahead or selected-display resolution.
- Typed command methods required by GDPR operation panels.
- Capability detection for unavailable, partially available, malformed, stale, and tenant-switch states.
- FrontComposer route support for `/admin/parties`, `/admin/parties/{partyId}`, and `/admin/parties/{partyId}/gdpr`.
- Failure semantics for unauthorized, forbidden, not found, gone or erased, degraded, timeout, malformed response, and contract-unavailable states.
- Boundary rules prohibiting retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.
- Privacy rules for tokens, tenant context, party data, logs, telemetry, storage keys, URLs, filenames, and callbacks.

## Scheduling Gate

No Epic 7 or Epic 8 implementation story should be scheduled until this dependency is either satisfied or explicitly accepted as a blocking risk by product and architecture owners.

## Verification

Before scheduling affected stories, planning review must confirm:

- The dependency status is updated from `Required` to `Satisfied` or `Risk Accepted`.
- The accepted contract reference is linked from sprint planning or story metadata.
- Story acceptance criteria still fail closed when the contract is unavailable or malformed.
- QA traceability includes UX-DR identifiers as well as FR/NFR identifiers.
