---
date: 2026-05-23
project: Hexalith.Parties
project_lead: Jerome
trigger: create-story 8.1 request blocked by Epic 8 EventStore-fronted Parties client/gateway scheduling gate
scope_classification: Moderate (planning-gate resolution + dependency record + story status + sprint-status)
recommended_path: Direct Adjustment
status: approved-and-applied
related_artifacts:
  - _bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md
  - _bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - docs/frontend/party-picker.md
  - src/Hexalith.Parties.Picker/Components/PartyPicker.razor
  - src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs
  - src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs
  - src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
  - src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
  - src/Hexalith.Parties.Client/PartiesClientOptions.cs
  - tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs
---

# Sprint Change Proposal - Unblock Story 8.1 (Embeddable Party Picker Shell)

## 1. Issue Summary

A `create-story 8.1` request surfaced that Story 8.1 (Compose Embeddable Party Picker Shell) was intentionally `blocked` by the shared EventStore-fronted Parties client/gateway scheduling gate:

- At trigger time, the dependency record was `Risk Accepted` only for Epic 7 Story 7.7.
- At trigger time, Epic 8 picker stories, including Story 8.1, remained explicitly `Required`.
- Story 8.1 therefore could not move to development scheduling unless the dependency was updated to `Satisfied` or a separate scoped `Risk Accepted` decision was recorded for Story 8.1.

The blocker is a planning-gate decision, not a missing story draft. The refreshed Story 8.1 file already contains implementation context, acceptance criteria, source references, and post-unblock tasks.

Evidence:

- Before application, `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` stated Epic 8 picker stories remained `Required`.
- Before application, `_bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md` had `Status: blocked`.
- `docs/frontend/party-picker.md` documents the existing Parties-owned picker RCL and its typed client boundary.
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs` consumes `IPartiesQueryClient` instead of retired REST, DAPR actor, projection actor, controller, or actor-host internals.
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` enforces transport/source guardrails for the picker package.

## 2. Impact Analysis

| Dimension | Impact |
|---|---|
| Epic Impact | Epic 8 scope is unchanged. Only Story 8.1 scheduling changed from blocked to ready-for-dev after approval. |
| Story Impact | Story 8.1 becomes schedulable against the existing picker RCL and typed query client boundary. Stories 8.2, 8.3, 8.4, 8.5, and 8.6 remain separately governed by their own acceptance criteria and the dependency gate unless explicitly accepted later. |
| Artifact Conflicts | No PRD, UX, or architecture rewrite is required. The proposed acceptance aligns with existing FR67, UX-DR22/25/32, and the architecture's Party Picker Frontend Surface. |
| Technical Impact | No source code change is required by this correction. Implementation would proceed against the current `Hexalith.Parties.Picker` and `Hexalith.Parties.Client` surfaces, with guardrails preserved. |
| Risk | Medium. The picker has an implementation and guardrail tests, but the full EventStore-fronted Parties client/gateway contract is not globally marked `Satisfied`. The risk is contained by scoping acceptance to Story 8.1 only. |

### Keystone Scoping Decision

Do not globally satisfy the dependency for all Epic 8 picker work. Accept the existing picker/client surface only for the Story 8.1 shell composition path.

The scoped contract of record for Story 8.1 would be:

- Picker composition surface: `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- Picker registration/configuration: `src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs`
- Picker query adapter/failure mapping: `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- Typed Parties query contract: `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- HTTP EventStore gateway adapter/options: `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`, `src/Hexalith.Parties.Client/PartiesClientOptions.cs`
- Usage/contract documentation: `docs/frontend/party-picker.md`
- Guardrail tests: `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`

## 3. Options Considered

### Option A - Direct Adjustment (recommended)

Update the dependency record to `Risk Accepted` scoped to Story 8.1, then update Story 8.1 and sprint-status to ready for development.

- Effort: Low.
- Risk: Medium, bounded by explicit scope and existing transport/privacy guardrails.
- Benefit: Unblocks the shell story without pretending the full Epic 8 contract is globally complete.

### Option B - Mark Dependency Satisfied

Rejected. The full shared dependency includes admin browse/search/detail, GDPR command methods, picker typeahead and selected-display resolution, capability detection, route support, and complete failure semantics. The current evidence is enough for Story 8.1 shell scheduling, not enough to honestly mark the whole dependency satisfied.

### Option C - Keep Story 8.1 Blocked Until Formal Contract Freeze

Viable but slow. This preserves maximum planning purity but leaves the already-existing picker RCL and guardrail tests idle. Use this only if product/architecture owners do not want to accept any Epic 8 implementation risk before the formal contract is complete.

## 4. Applied Changes

### Change 1 - Dependency Record

Artifact: `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`

- Extended the current scoped risk acceptance to include Epic 8 Story 8.1 only.
- Kept the dependency `Required` for the remaining Epic 8 picker stories unless separately accepted.
- Added a `Risk Acceptance (2026-05-23 - Story 8.1)` section that links the scoped contract of record listed above.
- Stated that Story 8.1 may compose/use the existing picker shell and typed query boundary but must not introduce retired REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, actor-host internals, or token/storage/URL/filename/telemetry/DOM/callback PII leaks.

### Change 2 - Story 8.1

Artifact: `_bmad-output/implementation-artifacts/8-1-compose-embeddable-party-picker-shell.md`

- Changed `Status: blocked` to `Status: ready-for-dev`.
- Replaced the blocking section with a gate-resolution section that cites this proposal and the updated dependency record.
- Preserved the current implementation snapshot, tasks, known risks, and guardrails.
- Kept the .NET callback shape risk visible for Story 8.3 rather than hiding it under the Story 8.1 acceptance.

### Change 3 - Sprint Status

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

- Changed `8-1-compose-embeddable-party-picker-shell` from `blocked` to `ready-for-dev`.
- Added the relevant note that the unblock is scoped to Story 8.1 only and does not satisfy the full Epic 8 dependency.

## 5. Acceptance Conditions

The correction is valid only if all of the following remain true:

- Host request/auth context is supplied through accepted picker/client configuration.
- The picker does not persist, refresh, parse, or log tokens.
- Durable DOM callback payloads remain narrow and must not include tenant ids, JWTs, search text, display names, contact values, identifiers, consent text, backend problem details, or raw query payloads.
- Results and selected preview state clear on tenant, user, host config, auth context, selected id, and search option changes.
- Typeahead queries remain bounded and routed through `IPartiesQueryClient.SearchPartiesAsync`.
- Failure states remain fail-closed and PII-safe for unauthorized, forbidden, unavailable, malformed, timeout, degraded, not found, gone/erased, and stale responses.
- Existing picker guardrail tests remain in force before development completion.

## 6. Checklist Review

| Correct-Course Area | Result |
|---|---|
| Trigger/context | Confirmed. The trigger is Story 8.1 scheduling blocked by the Epic 8 dependency gate. |
| Epic impact | No epic scope change required. Scheduling impact is limited to Story 8.1. |
| Artifact conflicts | No PRD, architecture, or UX contradiction found. Dependency/story/sprint-status were updated after approval. |
| Path forward | Direct Adjustment recommended. Rollback and MVP replan are not applicable. |
| Proposal completeness | Proposed artifact edits, scope boundaries, risks, and acceptance conditions are documented here. |
| Handoff | Approval received from Jérôme on 2026-05-23; changes applied. |

## 7. Approval And Application

Approved by Jérôme on 2026-05-23 and applied as Option A.

Applied changes:

1. Dependency record scoped risk acceptance for Story 8.1.
2. Story 8.1 status and gate-resolution update.
3. Sprint-status update.

No code changes are part of this correct-course transaction.
