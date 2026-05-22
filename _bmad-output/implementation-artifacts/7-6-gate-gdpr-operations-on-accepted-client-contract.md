# Story 7.6: Gate GDPR Operations on Accepted Client Contract

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want GDPR actions disabled until the accepted Parties client command/query contract is available,
so that the admin console does not present operations it cannot safely execute.

## Acceptance Criteria

1. Given the accepted EventStore-fronted Parties client/gateway contract is unavailable, when the admin console renders GDPR actions, then erasure, restriction, consent, portability, verification, and processing-record actions are disabled, and each disabled action shows the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`.
2. Given the accepted EventStore-fronted Parties client/gateway contract becomes available, when capability detection refreshes, then supported GDPR actions become enabled according to the available contract methods, and unsupported actions remain disabled with bounded reasons.
3. Given a contract-unavailable state occurs after an action panel was open, when the state is detected, then sensitive form data and operation state are cleared, and no stale command can be submitted.
4. Given capability detection fails or returns malformed capability data, when the console renders GDPR controls, then it fails closed by disabling affected actions, and it does not render raw backend details.
5. Given GDPR gating tests run, when they cover contract unavailable, available, partially available, malformed, stale, and tenant switch states, then operation controls are enabled only when safe and supported.

## Tasks / Subtasks

- [x] Introduce explicit GDPR operation capability state. (AC: 1, 2, 4)
  - [x] Add a small AdminPortal capability model for GDPR operation support; keep it in `src/Hexalith.Parties.AdminPortal/Services/` and avoid transport-specific or backend-specific naming.
  - [x] Represent unavailable, available, partially available, degraded/malformed, and stale states with bounded reasons only.
  - [x] Use the exact unavailable blocker text `Blocked on accepted EventStore-fronted Parties client/gateway contract` for the contract gate.

- [x] Gate every GDPR action in the operations panel. (AC: 1, 2)
  - [x] Gate request erasure, retry verification, restrict processing, lift restriction, add/revoke consent, export party data, and processing-record refresh.
  - [x] Preserve existing erased, erasure-pending, restriction, busy, and input-validation guards; the new contract gate must compose with them, not replace them.
  - [x] Render bounded disabled-state reasons near the affected controls without adding a new card shell, modal, generic browser, or explanatory feature text.

- [x] Detect and refresh GDPR capability safely. (AC: 2, 4)
  - [x] Prefer existing typed Parties admin/GDPR client capability when present; do not query retired REST paths, DAPR actors, EventStore streams, or projection actors directly from the component.
  - [x] Treat missing clients, malformed capability data, transport failures, timeout, or unknown responses as fail-closed.
  - [x] Keep raw response bodies, ProblemDetails text, parser details, tokens, claims, tenant ids, party data, and backend exception names out of rendered text, logs, telemetry dimensions, URLs, storage keys, and filenames.

- [x] Clear sensitive operation state when capability becomes unavailable or stale. (AC: 3, 5)
  - [x] Clear erasure confirmation state, restriction reason, consent channel/purpose/lawful-basis edits, export filename, processing records, correlation id, last outcome, and focused outcome when the capability gate closes.
  - [x] Cancel or ignore in-flight GDPR operations after party change, tenant switch, auth change, or capability version change.
  - [x] Ensure no stale command can be submitted from controls that were enabled under a previous party, tenant, auth context, or capability state.

- [x] Add focused tests for the contract gate. (AC: 1-5)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` or a focused test fake to provide GDPR capability states and malformed/failing capability responses.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to cover unavailable, available, partially available, malformed/degraded, stale response, tenant switch, and open-panel state clearing.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` only if API-client capability mapping or failure handling changes.
  - [x] Preserve existing Story 7.1-7.5 tests for route hardening, empty/error states, GDPR operation rendering, EventStore Admin UI links, and privacy-safe rendering.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.6 to `7-6-gate-gdpr-operations-on-accepted-client-contract`.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface.
- Story 7.6 covers FR66 and UX-DR7/UX-DR11. FR66 requires administrators to process GDPR requests through the administration interface.
- The dependency record `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` is still `Required`. This story must therefore fail closed unless an accepted client/gateway capability is present or explicitly surfaced by the typed admin client.

### Architecture Guardrails

- The Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It reads through EventStore query/client abstractions and routes supported commands through the typed Parties client/EventStore command boundary.
- Do not implement GDPR command/query transport in `src/Hexalith.Parties` actor host. Do not add public REST controllers, Swagger/OpenAPI, in-process MCP tools, DAPR actor calls, or direct EventStore stream browsing for this story.
- GDPR and privacy behavior is structural: fail closed, clear sensitive state, preserve tenant isolation, and avoid raw downstream text. When personal data classification is ambiguous, treat it as sensitive.
- Capability detection must be additive and forward-compatible. Prefer a small local model that can represent unsupported operations independently instead of a single global Boolean.

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`

- Current state: renders erasure, restriction, consent, portability, and processing-record controls for the selected party.
- Current state: `CanRequestErasure`, `CanRestrict`, `CanLiftRestriction`, `CanAddConsent`, `CanExport`, and `CanRetryVerification` combine busy, erased, restriction, erasure-pending, and input-validation guards.
- Current state: party/tenant changes cancel in-flight operations and clear confirmation, correlation, erasure status, certificate, processing records, export filename, and operation outcomes.
- Story change: introduce an operation capability gate and compose it with these existing guards. Do not weaken erasure/restriction/busy/input guards.

`src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprStateCoordinator.cs`

- Current state: tracks active tenant/party, request version, and reset states for tenant switch, sign-out, party change, authorization failure, and erased terminal state.
- Story change should use or extend this coordinator for stale/capability reset semantics instead of adding an unrelated component-local state machine.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`

- Current state: accepts optional `IAdminPortalGdprClient`; missing GDPR client throws `ContractUnavailable` for GDPR queries and maps GDPR command failures to bounded `AdminPortalGdprOutcome` values.
- Current state: typed `IPartiesQueryClient` is preferred for query paths; `IQueryService` fallback remains only for FrontComposer query compatibility.
- Story change may add a capability probe to `IPartiesAdminPortalApiClient` only if needed; keep missing/malformed/transport failure behavior bounded.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: includes `PartiesAdminPortal_GdprOperations_SurfaceUsesEventStoreClientContract`, GDPR command tests, state reset tests, route hardening tests, stale response tests, and EventStore link safety tests.
- Story change should add tests near the GDPR operation tests and preserve existing helpers such as `RenderAuthorized`, `ClickFluentButton`, and `RecordingAdminPortalApiClient`.

### Previous Story Learnings

- Story 7.1 completed the operational first viewport and route support for `/admin/parties/{partyId}` and `/admin/parties/{partyId}/gdpr`; selected rows navigate only for bounded non-PII route tokens. Preserve route hardening.
- Story 7.3 and Story 7.4 strengthened detail safety, stale response suppression, authorization reset, empty/error/degraded states, and GDPR coordinator reset behavior. Do not weaken those tests.
- Story 7.5 preserved safe EventStore Admin UI delegation and generic labels. GDPR correlation links must remain safe and should not include raw payloads or party PII.
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links. Preserve typed client preference, query-service fallback, and safe link generation.

### Anti-Patterns to Avoid

- Do not enable GDPR controls just because a selected party is active; operation support must come from accepted capability/contract state.
- Do not use a global all-or-nothing Boolean if individual operations can be partially supported.
- Do not leave confirmation forms, typed reasons, consent text, export filenames, correlation ids, processing records, or outcome messages visible after the capability gate closes.
- Do not render raw backend details, parser exception names, ProblemDetails details, tokens, claims, tenant membership data, personal data, or operator-entered form values in unavailable/degraded capability reasons.
- Do not query retired REST paths, `/api/v1/admin`, DAPR actor internals, projection actors, EventStore streams, or local search services from UI components.
- Do not weaken existing bUnit, route-hardening, stale-response, tenant-switch, localization, accessibility, privacy, EventStore link, or GDPR operation tests.

### Testing

- Primary component test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- Test API fake: `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- API/client boundary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for Epic 7 Story 7.6 with current GDPR panel, capability gate, dependency, and test guardrails. | Codex |
| 2026-05-22 | 0.2 | Added bounded GDPR capability gate, disabled-state reasons, stale capability resets, and focused AdminPortal tests; moved story to review. | Codex |
| 2026-05-22 | 0.3 | Review closed null/malformed capability and tenant-switch capability coverage gaps; moved story to done. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Resolved current sprint status and confirmed story key `7-6-gate-gdpr-operations-on-accepted-client-contract`.
- Loaded Epic 7 Story 7.6 source, the accepted EventStore-fronted Parties client/gateway dependency record, project context, current GDPR operations panel, GDPR state coordinator, admin portal API client, prior Story 7.4/7.5 notes, and current AdminPortal component test patterns.
- Skipped web research; local .NET/Fluent UI versions and existing component patterns are authoritative for this story.
- Added `AdminPortalGdprCapability` as a bounded operation-support model with available, unavailable, degraded, and partial states.
- Added `GetGdprCapabilityAsync` to the admin portal API abstraction and default implementation; missing GDPR client now reports the exact accepted-contract blocker.
- Wired `PartyGdprOperationsPanel` to load capability once per selected party/tenant context, compose capability support with existing erased/restricted/busy/input guards, and ignore stale operations after capability changes.
- Extended child GDPR panels to render bounded disabled reasons and to disable processing-record refresh when the capability gate is closed.
- Added AdminPortal component tests for contract unavailable, partial support, malformed capability fail-closed behavior, and sensitive state clearing when capability closes.
- Review closed two gaps: null capability results now fail closed as degraded, and tenant switches explicitly re-probe GDPR capability before enabling controls.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 96/96 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-6-gate-gdpr-operations-on-accepted-client-contract`.
- Checklist review applied: story includes BDD acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, validation commands, dependency context, and source references.
- Implemented a fail-closed GDPR capability gate that disables unsupported operations with bounded reasons while preserving existing operation guards.
- Cleared sensitive operation state when the capability gate closes and added version checks so stale operations cannot mutate current panel state.
- Added review hardening for null/malformed capability data and tenant-switch capability invalidation.
- Story moved to `done` after AdminPortal tests and Release solution build passed.

## Senior Developer Review

Reviewer: Codex

Date: 2026-05-22

### Findings

- No critical, high, or medium issues remain.
- Review fixed a fail-closed edge case for null capability results and added explicit tenant-switch capability regression coverage.

### Verification

- `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 96/96 tests.
- `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### File List

- `_bmad-output/implementation-artifacts/7-6-gate-gdpr-operations-on-accepted-client-contract.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PortabilityExportPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/RestrictionActionsPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
