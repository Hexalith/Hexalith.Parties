---
project: Hexalith.Parties
date: 2026-05-17
status: Risk Accepted (scoped — Epic 7 Story 7.7 and Epic 8 Story 8.1; remaining Epic 8 picker stories remain Required)
dependency_type: External contract / scheduling gate
required_before:
  - Epic 8 Stories 8.2-8.6 implementation scheduling
risk_accepted_for:
  - Epic 7 Story 7.7 (Implement GDPR Operation Panels)
  - Epic 8 Story 8.1 (Compose Embeddable Party Picker Shell)
risk_accepted_date: 2026-05-23
risk_accepted_by: Jérôme (project lead)
source: sprint-change-proposal-2026-05-17.md
risk_acceptance_source: sprint-change-proposal-2026-05-23.md; sprint-change-proposal-2026-05-23-epic8-picker-gate.md
---

# Dependency: Accepted EventStore-Fronted Parties Client/Gateway Contract

## Status

**Risk Accepted (2026-05-23) for Epic 7 Story 7.7 and Epic 8 Story 8.1 only.** Remaining Epic 8 picker implementation stories (8.2-8.6) remain **Required** unless separately risk-accepted or the full dependency is marked `Satisfied`. (Original gate text — Required before scheduling Epic 7 or Epic 8 v1.2 UI implementation work — is superseded for Story 7.7 and Story 8.1 by the scoped risk acceptances below.)

## Risk Acceptance (2026-05-23 - Story 7.7)

Decided by: Jérôme (project lead) via `sprint-change-proposal-2026-05-23.md`.
Scope: Story 7.7 (Implement GDPR Operation Panels) only. Epic 8 remains `Required` except for the separate scoped Story 8.1 acceptance below.

Decision: No fully-built, formally accepted EventStore-fronted Parties client/gateway contract exists yet. The existing `IAdminPortalGdprClient`, `HttpAdminPortalGdprClient`, and `AdminPortalGdprRoutes` are accepted as a **temporary bridge** — usable for Story 7.7, but explicitly provisional and subject to reshaping when the formal contract lands.

Residual risks accepted:

- Implementation churn when the provisional client/gateway shape is finalized or replaced.
- `HttpAdminPortalGdprClient` mixed maturity: some commands post through the EventStore command gateway, some queries derive from `PartyDetail`, and erasure-certificate + retry-verification still report contract-unavailable.

Conditions of acceptance (BINDING on Story 7.7 implementation):

- The Story 7.6 fail-closed capability gate (UX-DR11) MUST be preserved. Operations whose provisional client methods are unavailable (currently erasure certificate and retry verification) MUST stay capability-gated/disabled with the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract` — never faked or stubbed as working.
- Story 7.7 builds ONLY on provisional client methods that genuinely exist; invent no contract methods.
- All privacy, tenant-safety, fail-closed, accessibility (Story 7.9), and privacy-encoding (Story 7.10) guardrails remain in force.
- When the formal contract is accepted, this record flips to `Satisfied` and the provisional bridge is reconciled or replaced.

Linked contract-of-record (provisional bridge):

- `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
- `AdminPortalGdprRoutes`

## Risk Acceptance (2026-05-23 - Story 8.1)

Decided by: Jérôme (project lead) via `sprint-change-proposal-2026-05-23-epic8-picker-gate.md`.
Scope: Story 8.1 (Compose Embeddable Party Picker Shell) only. Stories 8.2, 8.3, 8.4, 8.5, and 8.6 remain `Required` unless separately risk-accepted or the full dependency is marked `Satisfied`.

Decision: No fully-built, formally accepted EventStore-fronted Parties client/gateway contract exists yet. The existing `Hexalith.Parties.Picker` Razor class library and the `IPartiesQueryClient` query boundary are accepted as a **temporary picker bridge** for Story 8.1 shell composition, host configuration, bounded initialization, and focused shell testing.

Residual risks accepted:

- Implementation churn when the formal picker query/configuration contract is frozen.
- Possible callback/model reconciliation in later stories, especially because the .NET `PartyPickerSelection` model currently carries more display metadata than the narrow DOM `party-selected` event detail.
- Typeahead, selected-display resolution, stale-response hardening, accessibility/localization hardening, and privacy-boundary hardening still require their own Epic 8 story validation.

Conditions of acceptance (BINDING on Story 8.1 implementation):

- Story 8.1 may compose and reconcile the existing picker shell only through the accepted picker/client surfaces listed below.
- Host request/auth context must remain host supplied through picker/client configuration; the picker must not persist, refresh, parse for authorization, or log tokens.
- Typeahead initialization and shell query behavior must remain routed through `IPartiesQueryClient.SearchPartiesAsync`; no retired REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals may be introduced.
- Durable DOM callback payloads must remain narrow and must not include tenant ids, JWTs, search text, display names, contact values, identifiers, consent text, backend problem details, or raw query payloads.
- Failure states must remain bounded and PII-safe for unauthorized, forbidden, unavailable, malformed, timeout, degraded, not found, gone/erased, and stale responses.
- Existing picker transport/privacy guardrail tests remain binding before Story 8.1 can be closed.
- This acceptance does not satisfy the full EventStore-fronted Parties client/gateway dependency for remaining Epic 8 stories.

Linked contract-of-record (temporary picker bridge):

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/PartiesClientOptions.cs`
- `docs/frontend/party-picker.md`
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs`

## Affected Stories

- Story 7.6: Gate GDPR Operations on Accepted Client Contract
- Story 7.7: Implement GDPR Operation Panels
- Story 8.1: Compose Embeddable Party Picker Shell
- Story 8.2: Implement Typeahead Search and Bounded Results
- Story 8.3: Emit Durable Selection by Party Id
- Story 8.4: Handle Picker States and Stale Responses
- Story 8.5: Enforce Picker Accessibility and Localization
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

No affected Epic 7 or Epic 8 implementation story should be scheduled until this dependency is either satisfied or explicitly accepted as a blocking risk by product and architecture owners. Current exceptions: Story 7.7 and Story 8.1 are risk-accepted under the scoped decisions above.

## Verification

Before scheduling affected stories, planning review must confirm:

- The dependency status is updated from `Required` to `Satisfied` or `Risk Accepted`.
- The accepted contract reference is linked from sprint planning or story metadata.
- Story acceptance criteria still fail closed when the contract is unavailable or malformed.
- QA traceability includes UX-DR identifiers as well as FR/NFR identifiers.
