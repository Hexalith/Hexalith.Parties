---
baseline_commit: ef3b508
---

# Story 3.5: EventStore erasure-verification contract (backend, cross-submodule - approval-gated)

Status: done

<!-- Note: Validation completed during create-story. Run dev-story only after explicit cross-submodule approval is recorded. -->

## Story

As an EventStore maintainer,
I want a defined contract for erasure certification and verification retry,
so that the Parties tier can prove a party was shredded across projections instead of stubbing it.

## Acceptance Criteria

1. Given Story 3.5 is approval-gated, when implementation begins, then explicit approval for the EventStore/Parties contract shape is recorded in the story notes or an ADR before any `Hexalith.EventStore` submodule, DAPR access-control, or deployed gateway contract change is made; without that approval the implementation must stop after tests/design evidence and keep the existing 501/`ContractUnavailable` behavior.
2. Given approval is recorded, when `IAdminPortalGdprClient.GetErasureCertificateAsync(partyId, token)` is called, then it goes through the EventStore gateway contract for `Domain="party"` and `GetErasureCertificate`, returns the persisted `ErasureCertificate?` for the authorized tenant/party, and no longer throws the client-side 501 `ContractUnavailable` stub.
3. Given no erasure certificate exists, the party is not erased/key-destroyed/verified, the party is missing, the tenant is unavailable, authorization fails, or the downstream contract is unavailable, when the certificate query is called, then the result maps to the existing bounded `AdminPortalQueryFailureKind`/`AdminPortalGdprOutcome` paths without raw ProblemDetails, stack traces, actor ids, state keys, destroyed-key text, or personal data.
4. Given an erased party with a saved certificate, when the certificate is returned, then the payload exposes only stable bounded erasure/verification state already allowed by `ErasureCertificate` (`partyId`, `tenantId`, `timestamp`, `verificationStatus`, and approved non-secret key-version metadata if retained) and never exposes key material, cryptographic exception text, stale display names, contact values, identifiers, raw event payloads, or verification cleanup error details.
5. Given approval is recorded, when `IAdminPortalGdprClient.RetryErasureVerificationAsync(partyId, token)` is called for a retryable erased/key-destroyed/verification-failed or partial state, then it uses the approved EventStore-fronted command/handler path, reuses `PartyErasureOrchestrator`/`IErasureVerificationService`/`IPartyErasureRecordStore` instead of duplicating verification logic, persists the refreshed certificate/report/status as appropriate, and returns `Accepted` or `Completed` with a bounded correlation id.
6. Given retry is requested for an active, restricted-only, missing, unauthorized, already-complete, unavailable, or non-retryable party, when the request completes, then it is idempotent or rejected through the existing bounded outcome model and does not resurrect personal data, mutate normal party fields, bypass erasure guards, or leak cleanup internals.
7. Given the capability probe runs after the backend contract is available, when `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` evaluates the GDPR client, then `CanReadErasureCertificate` and `CanRetryVerification` become true only for the real contract path; `AdminPortalGdprCapability.ProvisionalBridge()` remains the honest fallback while the stubs are still present.
8. Given the approved design uses EventStore's domain `/query` handler route, when implementation changes the route, then `parties` maps the SDK-compatible `/query` endpoint and both run-mode and deploy-mode DAPR access-control allow only `eventstore -> POST /query` in addition to the existing `eventstore -> POST /process`; if the approved design uses the existing projection-query actor path instead, no new public endpoint or broader DAPR permission is added.
9. Given EventStore and Parties contract shapes change, when tests run, then client contract tests prove the query/command envelopes, query type names, projection/domain routing, capability transitions, bounded error mapping, and no-PII/no-raw-error serialization; EventStore submodule tests prove the approved route delegates correctly and preserves deny-by-default behavior.
10. Given Story 3.6 has not started, when Story 3.5 is implemented, then no new Admin erasure-verification report UI behavior is introduced beyond enabling the existing panel/capability seams to consume a real certificate and retry result.

## Tasks / Subtasks

- [x] Record and pin the approved D7 contract shape before code changes (AC: 1, 8, 9)
  - [x] Capture the approval source in this story's Dev Agent Record or an ADR/checkpoint note before modifying `Hexalith.EventStore`, DAPR access-control, `Program.cs`, or deployed gateway behavior.
  - [x] Decide and document one routing approach: existing projection-query actor route (`PartyDetailProjectionQueryActor`) or EventStore domain-handler `/query` route. Do not implement both.
  - [x] Keep query and command names stable as `GetErasureCertificate` and `RetryErasureVerification` unless approval explicitly chooses different names; update `AdminPortalGdprRoutes` and tests together if names change.

- [x] Replace the certificate query stub with the approved gateway-backed implementation (AC: 2-4, 7, 9)
  - [x] In `HttpAdminPortalGdprClient.GetErasureCertificateAsync`, remove the `Task.FromException<ErasureCertificate?>(ContractUnavailable())` stub and post the approved EventStore query using the same typed-client JSON/error handling pattern as export and processing records.
  - [x] If using the projection-query actor path, extend `PartyDetailProjectionQueryActor` with a `GetErasureCertificate` query type that resolves the same tenant/party route, reads `IPartyErasureRecordStore.GetCertificateAsync`, returns `ErasureCertificate?`, and fails closed on malformed tenant/route mismatches.
  - [x] If using the domain-handler path, add an `IDomainQueryHandler` for `Domain="party"` / `QueryType="GetErasureCertificate"` and ensure the `parties` app maps the SDK-compatible `/query` route with only EventStore allowed by DAPR ACL.
  - [x] Preserve bounded null/not-found semantics: no certificate should not become a leaked exception or a raw problem-detail display.
  - [x] Do not add a browser-visible endpoint, actor-host public controller, direct UI host call to `parties`, or direct Razor injection of lower-level EventStore clients.

- [x] Implement retry verification through existing erasure services (AC: 5, 6, 9)
  - [x] Add the approved command/handler contract for `RetryErasureVerification` using additive contract evolution only; do not remove or rename existing erasure command/event fields.
  - [x] Reuse `PartyErasureOrchestrator.ExecuteVerificationAsync`, `IErasureVerificationService`, and `IPartyErasureRecordStore` for cleanup/report/certificate persistence. Do not duplicate projection cleanup delegates or invent a second verification state machine.
  - [x] Keep retry idempotent for already-complete/verified states and bounded for non-retryable states; failed/partial cleanup must return bounded status and sanitized store result summaries only.
  - [x] If aggregate lifecycle events are emitted (`MarkErasureVerified`, `CompletePartyErasure`, or a newly approved additive command/event), preserve the existing guard cascade and erasure status rules in `PartyAggregate`.
  - [x] Ensure retry cancellation tokens propagate through DAPR/service calls; do not swallow `OperationCanceledException`.

- [x] Update capability, adapters, and fallback behavior (AC: 3, 7, 10)
  - [x] Update `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` so real certificate/retry support turns on only when the underlying client is not the provisional stub path.
  - [x] Keep `AdminPortalGdprCapability.ProvisionalBridge()` disabled for certificate/retry with `ContractUnavailableReason`.
  - [x] Preserve `PartyGdprOperationsPanel` stale-result guards (`TenantContextSignature`, party id, cancellation token, `_capabilityVersion`, `IsCurrentOperation`) for certificate refresh and retry.
  - [x] Keep `ErasureVerificationReportPanel` bounded; Story 3.5 may make it receive a real certificate but must not build Story 3.6's full report UI.

- [x] Apply platform and DAPR boundary changes only if required by the approved route (AC: 1, 8, 9)
  - [x] If `/query` is used, map the route in `src/Hexalith.Parties/Program.cs` or adopt the EventStore DomainService extension without duplicating `/process`, and preserve middleware ordering.
  - [x] If `/query` is used, update both `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml` and `deploy/dapr/accesscontrol-parties.yaml` to allow only `eventstore` `POST /query`; keep default deny and do not allow actor, health, metrics, or wildcard operations.
  - [x] If projection-query actor routing is used, explicitly leave DAPR access-control unchanged and add tests proving no new service-invocation route is required.
  - [x] Keep AppHost/deploy EventStore domain service registration compatible with the chosen route.

- [x] Extend focused tests at existing seams (AC: 2-10)
  - [x] Extend `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` for real `GetErasureCertificateAsync` request shape, no client-side 501, retry request shape, and bounded error mapping.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` for capability true/false transitions, contract-unavailable fallback, retry outcomes, and sanitized failures.
  - [x] Add or extend `tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs` or new domain-query-handler tests for certificate query tenant/route safety, null certificate, erased certificate, malformed tenant, and no payload/PII leakage.
  - [x] Add retry tests around `PartyErasureOrchestrator`, `ErasureVerificationService`, `PartyErasureRecordStore`, aggregate lifecycle commands, and any new command/handler contract.
  - [x] If DAPR ACL or EventStore submodule routing changes, add deploy/AppHost static validation and EventStore QueryRouting/DomainService tests proving `/query` is allowed only from EventStore and handler-aware routing still delegates correctly.

- [x] Validate with focused lanes and record limitations (AC: 1-10)
  - [x] Run `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -v:minimal` if retry touches security services.
  - [x] Run relevant direct xUnit v3 test assemblies when `dotnet test`/MTP is blocked by local IPC/socket limits.
  - [x] Run EventStore submodule tests for any changed EventStore routing/contracts.
  - [x] Run `bash scripts/check-no-warning-override.sh` and `git diff --check`.

## Dev Notes

### Current Implementation State

- `IAdminPortalGdprClient` already exposes `GetErasureCertificateAsync` and `RetryErasureVerificationAsync`, and `AdminPortalGdprRoutes` already names `eventstore:query:party:GetErasureCertificate` and `eventstore:command:party:RetryErasureVerification`. The methods are intentionally stubbed today. [Source: src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs] [Source: src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs] [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- `HttpAdminPortalGdprClient.GetErasureCertificateAsync` currently throws a 501 `PartiesClientException` with `ContractUnavailable`; `RetryErasureVerificationAsync` returns `AdminPortalGdprOutcome.ContractUnavailable` and deliberately sends no request. Story 3.5 replaces these exact stubs only after approval. [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- `AdminPortalGdprCapability.ProvisionalBridge()` enables the seven working GDPR operations and keeps certificate/retry disabled with `ContractUnavailableReason`. Preserve that honest fallback until the real contract is available. [Source: src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs]
- `PartyGdprOperationsPanel` already fetches certificates only when `CanReadErasureCertificate` is true and renders the safe certificate-unavailable fallback otherwise. It also routes retry through `QueryService.ApiClient.RetryErasureVerificationAsync` and uses stale operation guards. Build on these seams; do not create a parallel report flow. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- `ErasureVerificationReportPanel` currently renders only bounded certificate status/timestamp or certificate-unavailable copy. Story 3.6 owns the full UI report. [Source: src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor]

### Backend Contract and Security Context

- Existing erasure models include `ErasureCertificate`, `ErasureVerificationReport`, `ErasureVerificationStoreResult`, `ErasureVerificationStatus`, and `ErasureVerificationOverallStatus`. Use these before adding new DTOs; contract evolution must be additive only. [Source: src/Hexalith.Parties.Contracts/Security/ErasureCertificate.cs] [Source: src/Hexalith.Parties.Contracts/Security/ErasureVerificationReport.cs] [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- `IPartyErasureRecordStore` already persists and reads certificate, status, and verification report records through DAPR state. `PartyErasureRecordStore` keys are implementation details; do not surface state keys in payloads, logs, ProblemDetails, or UI copy. [Source: src/Hexalith.Parties.Contracts/Security/IPartyErasureRecordStore.cs] [Source: src/Hexalith.Parties.Security/PartyErasureRecordStore.cs]
- `ErasureVerificationService` already sanitizes store cleanup errors and treats corrupted stores after key destruction as clean under the documented D15 pattern. Reuse it for retry; do not expose raw cleanup exceptions or destroyed-key failure text. [Source: src/Hexalith.Parties.Security/ErasureVerificationService.cs]
- `PartyErasureOrchestrator` already performs key-destruction retry and verification execution. Use it rather than duplicating retry loops in the client, UI, or query actor. [Source: src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs]
- `PartiesServiceCollectionExtensions` wires cleanup delegates for detail projection, index projection, projection cache, aggregate readable state, snapshots, and optional Memories search. Retry verification must use these configured delegates so optional Memories cleanup remains respected. [Source: src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs]
- Aggregate erasure lifecycle commands already exist: `EraseParty`, `MarkPartyEncryptionKeyDeleted`, `MarkErasureVerified`, and `CompletePartyErasure`; `PartyAggregate` enforces erasure-status guards and idempotency. Reuse or add to this lifecycle carefully. [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] [Source: src/Hexalith.Parties.Contracts/Commands/MarkErasureVerified.cs] [Source: src/Hexalith.Parties.Contracts/Commands/CompletePartyErasure.cs]

### EventStore and DAPR Routing Guardrails

- The browser talks only to the UI host/BFF; the UI host talks to the EventStore gateway through typed clients. Do not add public actor-host APIs or browser-visible EventStore gateway calls. [Source: _bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries]
- Public command/query traffic enters EventStore through `POST /api/v1/commands` and `POST /api/v1/queries` with `Domain="party"`; the `parties` actor host remains internal. [Source: docs/api-contracts.md#The-boundary-in-one-picture]
- EventStore generic query routing can invoke projection actors with `projectionActorType`, and Parties already uses `PartyDetailProjectionQueryActor` for `ExportPartyData` and `GetProcessingRecords`. This is the closest existing seam for certificate retrieval if approval chooses projection-query actor routing. [Source: src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs] [Source: tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs]
- EventStore also has handler-aware domain query routing through `IDomainQueryHandler`, `DomainQueryDispatcher`, `DaprDomainQueryInvoker`, and `HandlerAwareQueryRouter`; this requires the domain service to expose `/query` and DAPR ACL to allow EventStore to call it. Parties currently maps only `/process` manually and its access-control permits only `eventstore -> POST /process`. [Source: references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs] [Source: references/Hexalith.EventStore/src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs] [Source: src/Hexalith.Parties/Program.cs] [Source: src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml] [Source: deploy/dapr/accesscontrol-parties.yaml]
- If the approved design changes the EventStore submodule, keep it scoped to the contract/routing needed for D7. Do not convert reference submodules to NuGet packages and do not initialize nested submodules. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]

### Previous Story Intelligence

- Story 3.4 intentionally left `GetErasureCertificateAsync` and `RetryErasureVerificationAsync` contract-unavailable and required export/processing records to stay separate from D7. Do not regress Story 3.4 by changing export filenames, processing-record fields, erased/gone route behavior, or Art.20/Art.30 bounded rendering. [Source: _bmad-output/implementation-artifacts/3-4-data-export-art-20-and-processing-records-art-30.md#Acceptance-Criteria]
- Story 3.4's focused seams were `HttpAdminPortalGdprClient`, `PartiesAdminPortalApiClient`, `PartyGdprOperationsPanel`, `PartyDetailProjectionQueryActor`, AdminPortal service tests, gateway query actor tests, and optional E2E fixtures. Continue through those seams unless the approved D7 route requires the EventStore domain-query path. [Source: _bmad-output/implementation-artifacts/3-4-data-export-art-20-and-processing-records-art-30.md#Files-Likely-to-Change]
- Recent commits for Stories 3.1-3.4 touched AdminPortal GDPR panels/services, client contract tests, projection query actor tests, E2E fixtures, and sprint status. Keep this story backend-focused and avoid broad AdminPortal restructuring. [Source: git log -5]

### Privacy and Error-Handling Requirements

- Never log or display event payloads, raw command/query JSON, raw ProblemDetails, parser exceptions, destroyed-key text, contact values, identifiers, names, tenant-scoped secrets, tokens, claims, state keys, free-text restriction reasons, or cleanup exception details. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- `PartiesClientException` detail is already sanitized when sensitive text is detected. Preserve bounded error mapping and add tests for 401, 403, 404/410, 501, timeout/429, 5xx, malformed payload, null payload, and cancellation where touched. [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- Tenant access fails closed and is eventually consistent. A missing tenant, unknown tenant, forbidden user, malformed tenant id, or route mismatch must fail without a certificate payload and without mutating erasure state. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas] [Source: src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs]

### Files Likely to Change

- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs` - replace certificate/retry stubs with approved gateway-backed query/command implementation and bounded mapping.
- `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs` - only if approval changes route names; keep tests synchronized.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs` and `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` - capability transitions and fallback mapping.
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` - only for capability-aware refresh/retry adjustments; preserve stale-result guards.
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs` - likely certificate query implementation if approval uses projection-query actor routing.
- `src/Hexalith.Parties.Contracts/Commands/` and `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` - only if retry requires an additive command/event lifecycle change.
- `src/Hexalith.Parties.Security/` and `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` - only if retry orchestration/persistence wiring is missing.
- `src/Hexalith.Parties/Program.cs`, `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`, and `deploy/dapr/accesscontrol-parties.yaml` - only if approval uses the EventStore `/query` domain-handler route.
- `references/Hexalith.EventStore/src/**` and `references/Hexalith.EventStore/tests/**` - only for the approved cross-submodule contract/routing changes.
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`, `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`, `tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs`, `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs`, and deploy validation tests as needed.

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Build with `Hexalith.Parties.slnx` or focused project builds in Release. Do not add `Version=` attributes to `.csproj` files; Central Package Management owns versions. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- Treat warnings as errors and do not disable `TreatWarningsAsErrors`. If local MTP or Playwright runtime is blocked by socket/IPC permissions, run direct compiled xUnit assemblies and record the limitation as prior stories did. [Source: _bmad-output/project-context.md#Testing-Rules] [Source: _bmad-output/implementation-artifacts/3-4-data-export-art-20-and-processing-records-art-30.md#Senior-Developer-Review-AI]

### Project Structure Notes

- Keep adopter-facing contracts in `Hexalith.Parties.Contracts` infrastructure-free; no DAPR, ASP.NET, EventStore server, or persistence references belong there.
- Keep public traffic gateway-fronted. Do not add a controller/minimal API endpoint for browsers or external clients on `parties`.
- Keep DAPR ACL deny-by-default. Any new service-invocation allowance must be exact caller, exact path, exact verb, and present in both run-mode and deploy-mode YAML.
- No new NuGet package is expected.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.5-EventStore-erasure-verification-contract-backend-cross-submodule-approval-gated]
- [Source: _bmad-output/planning-artifacts/architecture.md#D7-GDPR-stub-completion]
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-submodule-D7-requires-explicit-approval]
- [Source: docs/api-contracts.md#IAdminPortalGdprClient]
- [Source: references/Hexalith.EventStore/docs/reference/query-api.md#POST-api-v1-queries]
- [Source: references/Hexalith.EventStore/docs/reference/command-api.md#POST-api-v1-commands]
- [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- [Source: src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs]
- [Source: src/Hexalith.Parties.Security/ErasureVerificationService.cs]
- [Source: src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs]
- [Source: _bmad-output/implementation-artifacts/3-4-data-export-art-20-and-processing-records-art-30.md]

## Validation Summary

- Source discovery loaded project context, sprint status, `epics.md`, `architecture.md`, UX design/review docs, previous Story 3.4, API contracts, EventStore command/query docs, current GDPR client stubs, AdminPortal capability/panel seams, erasure security services/contracts, projection query actor code/tests, DAPR access-control files, AppHost registration, and recent git history.
- Checklist fixes applied before finalizing: made the cross-submodule approval gate explicit, identified the exact stubs to replace, documented both approved routing choices and their consequences, required reuse of existing erasure services and stale guards, added DAPR ACL guardrails, excluded Story 3.6 UI scope, and tied tests to existing client/backend/EventStore seams.
- Latest-technology review did not identify a dependency change needed for this story. The relevant stack is pinned locally (.NET 10, Dapr packages, EventStore reference submodule, xUnit v3/Shouldly/NSubstitute), and no new packages or external APIs are required.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10 - Chose existing EventStore projection-query actor route (`PartyDetailProjectionQueryActor`) for `GetErasureCertificate`; no EventStore submodule, `/query` endpoint, `Program.cs`, or DAPR ACL change required.
- 2026-06-10 - `dotnet test ... --no-build` attempted for focused Client, Parties, and AdminPortal tests; blocked by local .NET test IPC named-pipe/socket permission (`System.Net.Sockets.SocketException (13): Permission denied`). Used direct xUnit v3 assemblies instead.
- 2026-06-10 - Focused Release builds passed: Client.Tests, AdminPortal.Tests, Parties.Tests, DeployValidation.Tests, and Parties.UI.
- 2026-06-10 - Direct xUnit v3 focused runs passed: Client AdminPortal GDPR contract tests (16), Parties gateway/domain tests (32), AdminPortal API client tests (34), DeployValidation DAPR ACL tests (5).
- 2026-06-10 - E2E validation passed: `npm run typecheck`; `npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list` discovered 1 test.
- 2026-06-10 - Playwright runtime attempted for Story 3.5 and blocked by local Kestrel socket binding permission (`System.Net.Sockets.SocketException (13): Permission denied`).
- 2026-06-10 - Quality gates passed: `bash scripts/check-no-warning-override.sh`; `git diff --check`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-06-10T14:52:10Z - Approval recorded from the story-automator session: user approved EventStore/Parties cross-submodule D7 erasure-verification contract implementation. Dev-story must decide and document one route before code changes, per AC1/AC8.
- 2026-06-10 - Implemented the approved projection-query actor route with stable `GetErasureCertificate` and `RetryErasureVerification` names; `AdminPortalGdprRoutes` stayed unchanged.
- 2026-06-10 - Replaced client-side certificate/retry stubs with EventStore gateway query/command calls, including nullable certificate semantics and bounded query error mapping.
- 2026-06-10 - Added additive `RetryErasureVerification` command contract and validator; Parties domain service invokes `PartyErasureOrchestrator.ExecuteVerificationAsync`, persists refreshed report/certificate/status through `IPartyErasureRecordStore`, and emits existing bounded erasure lifecycle events only for complete verification.
- 2026-06-10 - Updated GDPR capability to report certificate/retry support for the real GDPR client while preserving `AdminPortalGdprCapability.ProvisionalBridge()` fallback behavior.
- 2026-06-10 - Left EventStore submodule, `Program.cs`, AppHost/deploy DAPR ACLs, and UI components unchanged; added static ACL test proving projection-query actor routing does not require `POST /query`.
- 2026-06-10 - Senior review auto-fixes applied: preserved provisional capability fallback for non-real GDPR clients, mapped HTTP 501 to bounded `ContractUnavailable`, classified 403 tenant failures before sanitizing returned details, and documented the E2E/test-summary files changed by automation.

### File List

- _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md
- _bmad-output/story-automator/orchestration-1-20260609-205725.md
- src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs
- src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs
- src/Hexalith.Parties.Contracts/Commands/RetryErasureVerification.cs
- src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs
- src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs
- src/Hexalith.Parties/Validation/RetryErasureVerificationValidator.cs
- src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs
- tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs
- tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs
- tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs
- tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs
- tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts

### Change Log

- 2026-06-10 - Created Story 3.5 backend contract context and marked ready-for-dev pending explicit D7 cross-submodule approval.
- 2026-06-10 - Implemented approved EventStore-fronted erasure certificate query and verification retry backend contract via existing projection-query actor and Parties domain service seams.
- 2026-06-10 - Senior Developer Review (AI) auto-fixed capability fallback, bounded error mapping, HTTP 501 contract-unavailable mapping, and story file-list discrepancies; status set to done.

## Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-06-10

Outcome: Approved after auto-fixes. No critical issues remain.

Findings fixed:

- HIGH: `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` enabled certificate/retry for any registered `IAdminPortalGdprClient`, including provisional/stub implementations. Fixed by returning `Available()` only for the real `HttpAdminPortalGdprClient` path and `ProvisionalBridge()` for other registered GDPR clients.
- HIGH: HTTP 501 from the gateway mapped to `TransientFailure` instead of bounded `ContractUnavailable`. Fixed `HttpAdminPortalGdprClient` error mapping and added regression coverage.
- MEDIUM: 403 tenant failures were sanitized before tenant classification, so `MissingTenant` could degrade to `Forbidden`. Fixed by classifying against raw detail internally while returning only sanitized detail.
- MEDIUM: Story File List omitted changed E2E fixture/spec and automation summary files. Fixed by updating the File List.

Validation:

- `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`
- Direct xUnit v3: Client AdminPortal GDPR contract tests 16/16, AdminPortal API client tests 34/34, Parties gateway/domain tests 32/32, DeployValidation DAPR ACL tests 5/5.
- `cd tests/e2e && npm run typecheck`
- `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list`
- `bash scripts/check-no-warning-override.sh`
- `git diff --check`

Limitations:

- `dotnet test --no-build` remains blocked by local .NET test IPC named-pipe/socket permission.
- Playwright runtime remains blocked by local Kestrel socket binding permission; discovery and TypeScript validation pass.
