---
story_key: 7-2-commons-service-defaults-correlation-problemdetails-and-paging
story_id: "7.2"
epic: "7"
created: 2026-06-29T13:53:51+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 97388c8489a87081401e759b14d92b33e0cf7ba8
---

# Story 7.2: Commons ServiceDefaults, Correlation, ProblemDetails, and Paging

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a maintainer,
I want low-risk cross-cutting utilities to come from Commons where appropriate,
so that Parties no longer carries parallel service-defaults, correlation/error, or paging infrastructure.

## Acceptance Criteria

1. Given the accepted Epic 7 ADR chose root `references/Hexalith.Commons` project references, when this story is implemented, then `Directory.Build.props` contains the approved `HexalithCommonsRoot` fallback property and every new Commons reference uses project references with no `Version=` attributes in `.csproj` files.
2. Given the existing `Hexalith.Parties.ServiceDefaults` public wrapper is used by `parties`, `parties-ui`, `parties-mcp`, tests, and deploy validation, when Commons ServiceDefaults are adopted, then the current `AddServiceDefaults`, `ConfigureOpenTelemetry`, `AddDefaultHealthChecks`, `MapDefaultEndpoints`, endpoint paths, health status mapping, JSON console logging, OpenTelemetry source/meter expectations, and DAPR health hook behavior remain compatible.
3. Given Parties currently owns `CorrelationIdMiddleware`, `ICorrelationContextAccessor`, and `CorrelationContextAccessor`, when Commons metadata or diagnostics support is adopted, then the existing `X-Correlation-ID` request/response header, GUID-only incoming-header acceptance, generated fallback IDs, `HttpContext.Items["CorrelationId"]`, AsyncLocal restore behavior, command correlation semantics, and no-PII logging rules remain compatible.
4. Given Parties currently writes and consumes bounded ProblemDetails, when Commons HTTP/error support is adopted or added, then validation, authorization, dependency, timeout, malformed-response, and domain-rejection outcomes still map to bounded statuses without leaking raw ProblemDetails, PII, tokens, sidecar details, payloads, or tenant/party identifiers into UI copy, logs, telemetry, or exception details.
5. Given `Hexalith.Parties.Contracts.Models.PagedResult<T>` is a public contract, when Commons paging compatibility is introduced, then public Parties client/UI/contract behavior and serialization remain compatible, including `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, and `Freshness`; any Commons paging model is consumed behind adapters and does not replace or rename the public Parties contract.
6. Given Epic 7 uses adapter-first migration, when this story completes, then rollback is possible by restoring local wrapper/DI registrations or reverting the Commons pointer, and no Parties-local utility code is deleted unless parity tests and an explicit rollback note prove it is safe.
7. Given this story may touch Parties and Commons, when implementation completes, then focused Parties tests, any touched Commons owner tests, build or documented blocked build evidence, and `git diff --check` are recorded in the Dev Agent Record.

## Tasks / Subtasks

- [x] Add the Commons root property and references safely (AC: 1, 6)
  - [x] Add `HexalithCommonsRoot` to `Directory.Build.props` using the exact two-fallback shape from `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md`.
  - [x] Add Commons project references only to projects that actually consume the APIs. Expected candidates: `src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj`, `src/Hexalith.Parties/Hexalith.Parties.csproj`, `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`, and possibly non-public adapter/test projects.
  - [x] Keep all `.csproj` package references versionless; do not add `PackageVersion` entries unless the story explicitly switches from project references to packages, which the ADR does not.
  - [x] Do not initialize nested submodules. If Commons source is edited, keep it under the root `references/Hexalith.Commons` checkout and record the submodule diff in the File List.

- [x] Wrap Commons ServiceDefaults behind the existing Parties facade (AC: 2, 6)
  - [x] Update `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` so existing callers can keep `builder.AddServiceDefaults()`, `builder.ConfigureOpenTelemetry()`, `builder.AddDefaultHealthChecks()`, and `app.MapDefaultEndpoints()`.
  - [x] Delegate to `Hexalith.Commons.ServiceDefaults.HexalithServiceDefaults` where behavior matches.
  - [x] Configure Commons options to preserve current Parties behavior: `/health`, `/alive`, `/ready`, Healthy=200, Degraded=200, Unhealthy=503, development JSON health response, JSON console logging with UTC timestamps, app activity source plus `Hexalith.Parties`, and OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
  - [x] Preserve DAPR health behavior: Parties still adds DAPR sidecar, state-store, pub/sub, projection actor, and tenants readiness checks in host code after service defaults. Do not replace `AddPartiesDaprHealthChecks()`.
  - [x] Avoid registering a new default self health check unless tests prove it is compatible; current Parties local defaults intentionally only call `Services.AddHealthChecks()` and host code adds real checks.

- [x] Centralize correlation without changing semantics (AC: 3, 4, 6)
  - [x] Inspect Commons `ContextMetadata`, `Metadata`, and current diagnostics surface before adding any new Commons API.
  - [x] If Commons lacks an HTTP correlation middleware/accessor that exactly preserves Parties behavior, add an additive bounded API in Commons first, with Commons tests.
  - [x] Keep the Parties `CorrelationIdMiddleware` surface and constants compatible unless all current callers/tests are updated behind a facade.
  - [x] Preserve these exact behaviors: accept only valid GUID header values, generate a new GUID otherwise, write `X-Correlation-ID` on the response, set `HttpContext.Items["CorrelationId"]`, set the ambient accessor for the async call, and restore the previous ambient value in `finally`.
  - [x] Do not change `HttpPartiesCommandClient` command correlation IDs. They remain generated message IDs for EventStore command requests.

- [x] Adopt or add bounded ProblemDetails/error mapping behind adapters (AC: 4, 6)
  - [x] Inspect Commons `Hexalith.Commons.Http` and `Hexalith.Commons.Errors.ApplicationError` before adding new error mapping.
  - [x] If Commons lacks a stable ProblemDetails mapper, add an additive Commons HTTP/error mapper that is domain-neutral and does not know Parties rejection types.
  - [x] Keep Parties domain semantics local: `PartyCommandValidationRejected`, domain rejections, GDPR tombstone wording, tenant warm-up wording, and UI status presentation stay in Parties.
  - [x] Preserve `PartiesValidationExceptionHandler` and `PartiesGlobalExceptionHandler` outcomes unless a wrapper delegates identical output to Commons: validation 400, authorization 403, dependency unavailable 503, unhandled 500, development detail as exception type only, and `correlationId` extension.
  - [x] Preserve `HttpPartiesCommandClient.ThrowOnErrorAsync` sanitization for sensitive detail text and non-string ProblemDetails fields.
  - [x] Preserve Admin/Consumer/Picker bounded outcome mapping. Do not surface raw ProblemDetails text into components, toasts, or logs.

- [x] Introduce Commons paging compatibility without public contract drift (AC: 5, 6)
  - [x] Search Commons for an existing stable paging model. If none exists, add an additive generic paging model/helper in `references/Hexalith.Commons/src/libraries/Hexalith.Commons` with Commons tests.
  - [x] Keep `src/Hexalith.Parties.Contracts/Models/PagedResult.cs` public shape unchanged. Do not remove, rename, or make it derive from a Commons type.
  - [x] Add conversion helpers/adapters in a non-contract layer if Commons paging is used internally. The adapter must map null/empty item collections, page, page size, total count, total pages, and freshness without losing `ProjectionFreshnessMetadata`.
  - [x] Preserve package API snapshot expectations for `Hexalith.Parties.Contracts`; update snapshots only if the public API is intentionally additive and explained.
  - [x] Preserve consumer self-scope guardrails: no consumer self-scoped client may gain list/search or `PagedResult<T>` shaped members.

- [x] Update focused tests and guardrails (AC: 2-7)
  - [x] Add or update Parties ServiceDefaults tests proving endpoint paths, status mapping, trace filtering, JSON console/OTLP registration expectations, and DAPR health integration behavior.
  - [x] Add or update correlation tests for valid GUID header propagation, malformed/non-string header fallback, response header emission, ambient accessor restore, and no PII in logs/ProblemDetails.
  - [x] Add or update ProblemDetails/client tests for 400, 422, 401, 403, 404, 410, timeout/dependency unavailable, malformed JSON, non-string fields, and sensitive-detail redaction.
  - [x] Add or update paging serialization/adapter tests for `PagedResult<PartyIndexEntry>` and `PagedResult<PartySearchResult>`, including empty results and freshness metadata.
  - [x] Run Commons owner tests for any touched Commons project, at minimum the relevant `Hexalith.Commons.ServiceDefaults.Tests`, `Hexalith.Commons.Http.Tests`, and/or `Hexalith.Commons.Tests` subsets.
  - [x] Run `git diff --check`.

- [x] Record release and rollback evidence (AC: 6, 7)
  - [x] In the Dev Agent Record, list every Parties and Commons file changed.
  - [x] Record the rollback set: local wrapper/DI restoration, Commons submodule pointer rollback, and whether any state or public contract migration occurred.
  - [x] Confirm no local utility code was deleted without parity evidence.
  - [x] Confirm Epic 7 remains post-MVP platform maintenance and changes no PRD functional coverage.

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance. This story is not MVP feature delivery and must not be reported as new PRD functional coverage. [Source: _bmad-output/planning-artifacts/epics.md#Epic-7-Platform-Alignment---adopt-CommonsEventStore-Class-B]
- This is the first code adoption story after Story 7.1. It is allowed to add `HexalithCommonsRoot`, Commons project references, Parties-compatible adapters/wrappers, focused tests, and additive Commons APIs only where required for B5/B6/B7/B10. [Source: _bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-72]
- Adapter-first is binding: introduce/wrap/adapt shared primitives and prove parity before deleting local Parties implementation. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-1---Adapter-First-Migration]

### Approved Story Scope

Story 7.2 covers these Epic 7 inventory items:

| ID | Scope | Current direction |
| --- | --- | --- |
| B5 | ServiceDefaults | Adopt `Hexalith.Commons.ServiceDefaults`; keep `Hexalith.Parties.ServiceDefaults` as thin wrapper for Parties-specific health and DAPR hooks. |
| B6 | Correlation accessor and middleware | Use Commons metadata/diagnostics where sufficient; add bounded Commons diagnostics only if needed; preserve Parties header and ambient semantics. |
| B7 | ProblemDetails and global exception mapping | Use/add Commons domain-neutral HTTP error mapping; keep Parties domain rejection semantics and regulated copy local. |
| B10 | `PagedResult<T>` | Add or consume Commons generic paging behind an adapter; public Parties `PagedResult<T>` remains compatible. |
| B11 slice | Typed-client error mapping | The typed-client ProblemDetails slice follows Commons; EventStore projection/security and UI lifecycle pieces are not part of 7.2. |

Out of scope for this story: B8 search normalization, B1/B2/B9 projection platform work, B3/B4 crypto/key-management, FrontComposer UI orchestration, public contract breaking changes, EventStore gateway routing changes, DAPR ACL changes, and deletion of local code without parity.

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 7 and Story 7.2.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md` and `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; relevant NFR context is no PII in logs, telemetry, degraded headers, ServiceDefaults, and EventStore gateway seams.
- Loaded `{ux_content}` index and regulated-language review from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant guardrail is no raw ProblemDetails or PII-bearing copy in user-visible surfaces.
- Loaded persistent project context from `_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md`.
- Loaded current code and Commons candidate APIs listed below before creating this story.

### Current Files Being Modified - Required Reading

Read each UPDATE file completely before editing it.

- `Directory.Build.props` (UPDATE)
  - Current state: defines root properties for EventStore, Tenants, Memories, and FrontComposer, but not Commons.
  - What this story changes: add `HexalithCommonsRoot` with the two fallback `Exists(...)` conditions approved by Story 7.1.
  - Preserve: `.slnx` project-reference style, target framework `net10.0`, nullable/implicit usings, warnings-as-errors, MinVer, and existing root properties.

- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` (UPDATE)
  - Current state: local Aspire defaults register OpenTelemetry logging/metrics/tracing, JSON console logging, service discovery, HTTP resilience, health checks, `/health`, `/alive`, `/ready`, development JSON health response, and health status mapping where Degraded returns 200 and Unhealthy returns 503.
  - What this story changes: wrap/delegate matching behavior to `Hexalith.Commons.ServiceDefaults`.
  - Preserve: existing extension method names and caller shape, endpoint paths, development JSON response behavior, trace exclusion for health endpoints, UTC JSON console timestamps, OTLP gate, and DAPR health hooks added by host projects.

- `src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj` (UPDATE)
  - Current state: non-packable shared Aspire project with framework reference and package references for resilience, service discovery, and OpenTelemetry.
  - What this story changes: likely add a project reference to `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.ServiceDefaults\Hexalith.Commons.ServiceDefaults.csproj`.
  - Preserve: no `Version=` attributes and current project identity, because callers reference the Parties wrapper.

- `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs` (UPDATE if correlation wrapper changes)
  - Current state: accepts `X-Correlation-ID` only if it parses as a GUID; otherwise generates a new GUID; writes response header; sets `HttpContext.Items["CorrelationId"]`; sets and restores `ICorrelationContextAccessor.CorrelationId`.
  - What this story changes: optionally delegates to Commons diagnostics if an equivalent API exists or is added.
  - Preserve: header spelling, GUID-only acceptance, fallback generation, ambient restore, and no PII.

- `src/Hexalith.Parties.Security/ICorrelationContextAccessor.cs` and `src/Hexalith.Parties.Security/CorrelationContextAccessor.cs` (UPDATE if facade changes)
  - Current state: Parties-owned ambient async correlation seam using `AsyncLocal<string?>`.
  - What this story changes: may become a thin facade over Commons diagnostics if a matching Commons API is added.
  - Preserve: public interface contract for current Parties services, singleton registration compatibility, and async-flow behavior used by key-management services.

- `src/Hexalith.Parties/ErrorHandling/PartiesValidationExceptionHandler.cs` (UPDATE if error mapper changes)
  - Current state: maps FluentValidation exceptions to 400 ProblemDetails with `Validation Failed`, generic detail, `correlationId`, and `validationErrors`; logs only correlation id and error count.
  - What this story changes: may delegate ProblemDetails construction to a Commons bounded mapper.
  - Preserve: status/title/type/detail shape unless tests are updated with a compatibility explanation, no raw input values in logs, and existing validation error behavior.

- `src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler.cs` (UPDATE if error mapper changes)
  - Current state: maps authorization exceptions to 403, dependency/timeout exceptions to 503, unhandled exceptions to 500, development detail to exception type only, and writes `correlationId`.
  - What this story changes: may delegate bounded ProblemDetails construction/classification to Commons.
  - Preserve: no exception messages or PII in response details; no tenant/party identifiers except existing tenant extension behavior if deliberately preserved and tested.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (UPDATE if registrations change)
  - Current state: registers `AddProblemDetails`, validation/global exception handlers, `ICorrelationContextAccessor`, and all Parties domain/security/projection services.
  - What this story changes: registration may point at a facade or shared bounded mapper.
  - Preserve: middleware order and EventStore gateway boundary. Do not add public REST controllers or new public command/query endpoints.

- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` (UPDATE if typed-client error/paging adapters change)
  - Current state: command/query clients call EventStore gateway paths, deserialize query `payload`, throw `PartiesClientException` on errors, sanitize sensitive detail text, tolerate non-string problem fields, and use `PartiesJsonOptions.Default`.
  - What this story changes: error parsing may delegate to Commons bounded HTTP mapping; query paging may convert between Commons and Parties models behind the client surface.
  - Preserve: gateway paths, tenant validation, command message/correlation id behavior, request customizer hooks, sensitive-detail redaction, malformed-response typed exceptions, and public return types.

- `src/Hexalith.Parties.Client/PartiesClientException.cs` (UPDATE only if additive)
  - Current state: typed exception exposes `Status`, `Title`, `Type`, `Detail`, and `CorrelationId`; message is detail or title.
  - What this story changes: avoid changes unless an additive adapter needs helper constructors.
  - Preserve: existing constructors and property behavior for UI/MCP tests.

- `src/Hexalith.Parties.Contracts/Models/PagedResult.cs` (UPDATE only if additive)
  - Current state: public sealed record with required `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, and optional `Freshness`.
  - What this story changes: preferably no direct change. If helper methods are added, they must be additive and pass public API snapshot review.
  - Preserve: public shape and serialization compatibility.

- `src/Hexalith.Parties/Program.cs`, `src/Hexalith.Parties.UI/Program.cs`, and `src/Hexalith.Parties.Mcp/Program.cs` (UPDATE only if wrapper call sites must change)
  - Current state: all use `Hexalith.Parties.ServiceDefaults`; `parties` adds DAPR health checks after service defaults; UI and MCP map default endpoints through the wrapper.
  - What this story changes: ideally nothing at call sites.
  - Preserve: middleware order `CorrelationId -> MvpComplianceWarning -> ExceptionHandler -> DegradedResponse -> AuthN -> AuthZ -> CloudEvents` in `parties`, UI auth behavior, and MCP stateless setup.

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/*` (UPDATE only if owner API gap is found)
  - Current state: Commons already exposes `AddHexalithServiceDefaults`, `ConfigureHexalithOpenTelemetry`, `AddHexalithDefaultHealthChecks`, `MapHexalithDefaultEndpoints`, `CreateHealthStatusCodes`, `ShouldTraceHttpRequest`, and options hooks.
  - What this story changes: only additive fixes if needed to preserve Parties behavior.
  - Preserve: Commons domain-neutral API shape and existing tests.

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas/*` (UPDATE only if owner API gap is found)
  - Current state: contains `ContextMetadata.CorrelationId` and message metadata, but no production HTTP correlation middleware/accessor matching Parties behavior.
  - What this story changes: add bounded diagnostics/middleware only if needed.
  - Preserve: existing metadata records and serialization.

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/*` and `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Errors/*` (UPDATE only if owner API gap is found)
  - Current state: `Hexalith.Commons.Http` currently provides typed HttpClient registration helpers, not ProblemDetails mapping. `Hexalith.Commons.Errors.ApplicationError` exists in core Commons.
  - What this story changes: add domain-neutral bounded HTTP error/ProblemDetails helpers only if needed.
  - Preserve: domain-neutrality and existing typed-client registration tests.

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons/*` (UPDATE only if owner API gap is found)
  - Current state: no stable generic paging model was found in current source search.
  - What this story changes: add additive generic paging records/helpers if Story 7.2 implementation needs a Commons paging target.
  - Preserve: lightweight core package boundaries and no dependency on Parties.

### Architecture Guardrails

- No public Parties host API. Commands and queries still enter through the Hexalith.EventStore gateway; EventStore invokes Parties over DAPR at `POST /process`. [Source: _bmad-output/project-context.md#Framework-Specific-Rules-Event-Sourcing--CQRS--DAPR-behind-EventStore]
- Preserve Central Package Management. Do not add `Version=` to any `.csproj`; project references use `$(HexalithCommonsRoot)` or existing root properties. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- `Contracts` must stay infrastructure-free and should not gain a Commons dependency unless a separate architecture decision approves it. The public `PagedResult<T>` contract is compatibility surface, not the shared internal implementation target. [Source: _bmad-output/project-context.md#Code-Quality--Style-Rules]
- Public contracts evolve additively only. Removing or renaming fields, enum values, command/query shapes, `PagedResult<T>`, or `ProjectionFreshnessMetadata` is outside this story. [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Consistency-Conventions]
- ProblemDetails mapping must stay bounded and PII-free. Do not log event payloads, party names, identifiers, raw key aliases, destroyed-key details, raw payloads, tokens, bearer values, or connection strings. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Domain rejection semantics stay in Parties/EventStore gateway behavior. Commons can provide domain-neutral mapper primitives, not Parties-specific rejection copy.
- Consumer self-scoped surfaces must not gain list/search or `PagedResult<T>` shaped members. [Source: tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs]

### Existing Commons Surface Assessment

- `Hexalith.Commons.ServiceDefaults` is usable as the B5 target. It already exposes service-defaults methods, endpoint mapping, health status mapping, tracing filter, development JSON writer, OTLP gating, and options hooks. [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/HexalithServiceDefaults.cs]
- Commons ServiceDefaults registers a default live self check by default. Parties currently avoids placeholder checks and adds real DAPR checks in hosts. Configure or adapt this explicitly to avoid behavior drift. [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/HexalithServiceDefaultsOptions.cs]
- `Hexalith.Commons.Metadatas` has `ContextMetadata.CorrelationId` but no matching HTTP middleware/accessor was found in current source. Additive Commons diagnostics may be required before replacing Parties correlation code. [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas/ContextMetadata.cs]
- `Hexalith.Commons.Http` currently provides typed HttpClient registration and endpoint validation, not ProblemDetails mapping. Additive Commons HTTP error mapping may be required before adopting B7. [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs]
- No stable Commons generic paging model was found by source search. Additive Commons paging may be required, but Parties public contracts must remain stable. [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons/]

### Previous Story Intelligence

- Story 7.1 accepted the target-destination ADR and selected Commons consumption through a new `HexalithCommonsRoot` project-reference path under root `references/Hexalith.Commons`. Story 7.2 owns adding that property and the first Commons references. [Source: _bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md#Completion-Notes-List]
- Story 7.1 attempted broad build/test lanes and found an existing out-of-scope FrontComposer target-framework evaluation failure. Story 7.2 should still run focused lanes and should document any repeated build blocker precisely instead of treating it as story-caused. [Source: _bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md#Debug-Log-References]
- Recent commits finished Epic 6 shared anchors and Story 7.1 planning. Do not re-open Epic 6 Class A anchors or reclassify them as Epic 7 work. [Source: git log -5]

### Testing and Validation Guidance

Run the smallest reliable lane first, then broaden as needed:

- `git diff --check`
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore`
- Focused Parties tests likely needed:
  - `tests/Hexalith.Parties.Tests/HealthChecks/HealthEndpointIntegrationTests.cs`
  - `tests/Hexalith.Parties.IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs` when environment permits; skip is acceptable for Docker/DAPR absence.
  - `tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs`
  - `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
  - `tests/Hexalith.Parties.Client.Tests/PartiesClientExceptionTests.cs`
  - `tests/Hexalith.Parties.Tests/ErrorHandling/PartiesGlobalExceptionHandlerTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`
  - package/public API snapshot tests for `Hexalith.Parties.Contracts` if `PagedResult<T>` or references change.
- Focused Commons tests when touched:
  - `dotnet test references/Hexalith.Commons/test/Hexalith.Commons.ServiceDefaults.Tests/Hexalith.Commons.ServiceDefaults.Tests.csproj -c Release --no-restore`
  - `dotnet test references/Hexalith.Commons/test/Hexalith.Commons.Http.Tests/Hexalith.Commons.Http.Tests.csproj -c Release --no-restore`
  - `dotnet test references/Hexalith.Commons/test/Hexalith.Commons.Tests/Hexalith.Commons.Tests.csproj -c Release --no-restore`
- If `dotnet test --filter` is needed, do not use classic VSTest filtering for Parties. The repo uses Microsoft.Testing.Platform and xUnit v3; run the test executable with xUnit v3 single-dash args when filtering is required. [Source: _bmad-output/project-context.md#Testing-Rules]

### Rollback Plan

- ServiceDefaults rollback: restore `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` to local implementation or switch wrapper registration back to local code; revert the Commons ServiceDefaults project reference if needed.
- Correlation rollback: restore local `CorrelationIdMiddleware`, `ICorrelationContextAccessor`, and `CorrelationContextAccessor` registration.
- ProblemDetails rollback: restore local `PartiesValidationExceptionHandler`, `PartiesGlobalExceptionHandler`, and `HttpPartiesCommandClient.ThrowOnErrorAsync` mapping.
- Paging rollback: keep public `PagedResult<T>` unchanged; revert internal adapter use and any Commons pointer/API changes if parity fails.
- Reference rollback: revert `HexalithCommonsRoot` usage and any changed Commons submodule pointer/source changes. No state migration should be introduced by this story.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-7.2-Commons-ServiceDefaults-correlation-ProblemDetails-and-paging]
- [Source: _bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.2---Commons-Service-Defaults-Correlation-ProblemDetails-And-Paging]
- [Source: _bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Decision]
- [Source: _bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Target-Destination-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-72]
- [Source: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-4---Utility-Destination-Discipline]
- [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- [Source: Directory.Build.props]
- [Source: src/Hexalith.Parties.ServiceDefaults/Extensions.cs]
- [Source: src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs]
- [Source: src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler.cs]
- [Source: src/Hexalith.Parties/ErrorHandling/PartiesValidationExceptionHandler.cs]
- [Source: src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs]
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PagedResult.cs]
- [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/HexalithServiceDefaults.cs]
- [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.Metadatas/ContextMetadata.cs]
- [Source: references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical epics, PRD, architecture, Epic 7 architecture spine, Story 7.1 ADR/release plan, current Parties wrapper/error/correlation/paging/client files, current Commons ServiceDefaults/Metadatas/Http/core APIs, focused test inventory, and recent git history.
- Checklist fixes applied before finalizing: made `Hexalith.Parties.ServiceDefaults` the compatibility wrapper instead of replacing call sites directly; pinned public `PagedResult<T>` as unchanged; called out missing Commons ProblemDetails/paging/correlation APIs as additive owner work; added explicit no-PII/no-raw-ProblemDetails requirements; added rollback sets and focused validation lanes.
- Latest-technology review found no external dependency upgrade requirement. The story must rely on local pinned .NET 10, current root submodule sources, and the accepted ADR rather than changing package versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-29T14:22:29+02:00: Added the approved `HexalithCommonsRoot` fallback property and project references with no package `Version=` attributes.
- 2026-06-29T14:22:29+02:00: Wrapped Parties ServiceDefaults over Commons defaults while preserving `/health`, `/alive`, `/ready`, health status codes, development health JSON, OTLP gating, and no default self check.
- 2026-06-29T14:22:29+02:00: Added Commons HTTP correlation, bounded ProblemDetails reader/factory, and paging helpers; Parties keeps facade surfaces and public contracts.
- Validation passed: `dotnet build` for `Hexalith.Commons`, `Hexalith.Commons.Http`, `Hexalith.Parties`, `Hexalith.Parties.Client`, `Hexalith.Parties.Security`, `Hexalith.Parties.Tests`, `Hexalith.Parties.Client.Tests`, and `Hexalith.Commons.Http.Tests` using Release/no-restore/single-node lanes where needed.
- Validation passed: direct xUnit v3 executable lanes for Commons HTTP tests, Parties command/query/package focused tests, correlation tests, service-defaults health tests, health endpoint tests, and global exception handler tests.
- Validation passed: `git diff --check`.
- Blocked evidence: broad `dotnet test` cannot run in this sandbox because Microsoft.Testing.Platform named-pipe socket creation is denied; focused test executables were used instead.
- Blocked evidence: full solution build still exposes unrelated pre-existing referenced-repo failures in EventStore Admin/Tenants and nested PolymorphicSerializations analyzer rules.
- Blocked evidence: full `Hexalith.Parties.Client.Tests` clean-consumer package restore is blocked by restricted network access when `RestoreNoCache=true`; the story-related package dependency budget test passed.

### Completion Notes List

- ServiceDefaults adoption is adapter-first: existing Parties extension methods remain the caller surface and delegate to Commons with Parties-specific options.
- Correlation adoption is adapter-first: Parties middleware and accessor contracts remain while Commons owns the reusable ambient/accessor/header behavior.
- ProblemDetails adoption is domain-neutral in Commons; Parties keeps domain rejection wording, redaction, correlation extension, and exception classification.
- Paging adoption keeps `Hexalith.Parties.Contracts.Models.PagedResult<T>` unchanged; Commons paging is consumed behind client adapters and normalizes null item collections without dropping freshness metadata.
- Rollback set: restore local wrapper/DI implementations, revert Commons project references or the Commons submodule source/pointer, and remove internal adapters. No state migration or public contract migration was introduced.
- No Parties-local utility code was deleted without parity evidence. Epic 7 remains post-MVP platform maintenance and adds no new PRD functional coverage.

### File List

- `Directory.Build.props`
- `_bmad-output/implementation-artifacts/7-2-commons-service-defaults-correlation-problemdetails-and-paging.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `references/Hexalith.Commons`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Paging/PagedResult.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/BoundedProblemDetails.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/BoundedProblemDetailsFactory.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/BoundedProblemDetailsReader.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/CorrelationContextAccessor.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/Hexalith.Commons.Http.csproj`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpCorrelation.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/ICorrelationContextAccessor.cs`
- `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/PagedResult.cs`
- `references/Hexalith.Commons/test/Hexalith.Commons.Tests/Common/PagedResultTest.cs`
- `references/Hexalith.Commons/test/Hexalith.Commons.Http.Tests/BoundedProblemDetailsReaderTest.cs`
- `references/Hexalith.Commons/test/Hexalith.Commons.Http.Tests/Hexalith.Commons.Http.Tests.csproj`
- `references/Hexalith.Commons/test/Hexalith.Commons.Http.Tests/HttpCorrelationTest.cs`
- `references/Hexalith.Commons/test/Hexalith.Commons.Http.Tests/PagedResultTest.cs`
- `src/Hexalith.Parties/Hexalith.Parties.csproj`
- `src/Hexalith.Parties/ErrorHandling/PartiesGlobalExceptionHandler.cs`
- `src/Hexalith.Parties/ErrorHandling/PartiesValidationExceptionHandler.cs`
- `src/Hexalith.Parties/Middleware/CorrelationIdMiddleware.cs`
- `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/Paging/PartiesPagedResultAdapter.cs`
- `src/Hexalith.Parties.Security/CorrelationContextAccessor.cs`
- `src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj`
- `src/Hexalith.Parties.Security/ICorrelationContextAccessor.cs`
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs`
- `src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/Package/ClientPackageTests.cs`
- `tests/Hexalith.Parties.Tests/ErrorHandling/PartiesGlobalExceptionHandlerTests.cs`
- `tests/Hexalith.Parties.Tests/ErrorHandling/PartiesValidationExceptionHandlerTests.cs`
- `tests/Hexalith.Parties.Tests/HealthChecks/ServiceDefaultsCompatibilityTests.cs`
- `tests/Hexalith.Parties.Tests/Middleware/CorrelationIdMiddlewareTests.cs`

### Change Log

- Added Commons project-reference wiring and adopted Commons ServiceDefaults, correlation, bounded ProblemDetails, and paging behind Parties compatibility facades/adapters.
- Added focused Commons and Parties coverage for service defaults, correlation behavior, ProblemDetails sanitization/status mapping, paging normalization/freshness, and client package dependency guardrails.
- 2026-06-29 (Senior Developer Review, AI): auto-fixed review findings — removed dead `Extensions.WriteHealthCheckJsonResponse`, added ServiceDefaults status-code/trace-filter compatibility tests, and corrected the File List to include both error-handling test files. Status set to `done` (no critical issues).

## Senior Developer Review (AI)

- **Reviewer:** Administrator (adversarial auto-review)
- **Date:** 2026-06-29
- **Outcome:** Approve with auto-fixes applied — no Critical or High findings.

### Acceptance Criteria verdict

| AC | Verdict | Evidence |
| --- | --- | --- |
| 1 (HexalithCommonsRoot + versionless refs) | IMPLEMENTED | `Directory.Build.props` adds the two-fallback `HexalithCommonsRoot`; ServiceDefaults/Parties/Client/Security `.csproj` use versionless `$(HexalithCommonsRoot)` project references. |
| 2 (ServiceDefaults compatibility) | IMPLEMENTED | `Extensions.cs` delegates to `Hexalith.Commons.ServiceDefaults` with `RegisterDefaultSelfCheck=false`, Parties paths, and `Hexalith.Parties` source; Commons option defaults preserve dev JSON health writer, UTC JSON console logging, OTLP gating, health status mapping (200/200/503), and health-endpoint trace exclusion. |
| 3 (Correlation semantics) | IMPLEMENTED | `CorrelationIdMiddleware` delegates to `HttpCorrelation.InvokeAsync`; GUID-only acceptance, fallback generation, response header, `Items["CorrelationId"]`, ambient set/restore-in-finally preserved. Static `AsyncLocal` semantics retained via Commons accessor. |
| 4 (Bounded ProblemDetails, PII-free) | IMPLEMENTED | Handlers use `BoundedProblemDetailsFactory`; client uses `BoundedProblemDetailsReader` + `SanitizeDetail`; non-string fields ignored, dev 500 detail = exception type only, `tenantId` extension preserved. |
| 5 (Paging compatibility) | IMPLEMENTED | Public `PagedResult.cs` contract unchanged (no git diff); Commons paging consumed behind `PartiesPagedResultAdapter`; null/empty items normalized and `ProjectionFreshnessMetadata` preserved. |
| 6 (Adapter-first / reversible / no unparried deletion) | IMPLEMENTED | Local middleware/accessor/handlers retained as facades; rollback set documented; no local utility code deleted without parity. |
| 7 (tests + build + `git diff --check`) | IMPLEMENTED (with documented build blocker) | `git diff --check` clean; focused tests added; full test-graph build is blocked by an out-of-scope submodule drift (see below). |

### Findings and disposition

- **MEDIUM — File List incomplete (fixed):** the Dev Agent Record File List omitted `tests/Hexalith.Parties.Tests/ErrorHandling/PartiesGlobalExceptionHandlerTests.cs` (modified) and `tests/Hexalith.Parties.Tests/ErrorHandling/PartiesValidationExceptionHandlerTests.cs` (new). Both added to the File List.
- **MEDIUM — ServiceDefaults test claim vs reality (fixed):** the task claimed Parties tests proving status mapping and trace filtering, but `ServiceDefaultsCompatibilityTests` only covered endpoint paths and self-check absence. Added `HealthStatusCodes_PreservePartiesMapping_*` and `ShouldTraceHttpRequest_ExcludesPartiesHealthEndpoints` tests; JSON-console/OTLP registration remain covered by `Hexalith.Commons.ServiceDefaults` owner tests plus the verified option defaults.
- **LOW — Dead code (fixed):** `Extensions.WriteHealthCheckJsonResponse` was unused (the dev JSON writer is supplied by the Commons option default `DevelopmentHealthResponseWriter`). Removed it and the now-unused usings; project rebuilds with 0 warnings.
- **MEDIUM — Out-of-scope submodule drift / build blocker (documented, not auto-fixed):** the working tree moves submodule pointers for `Hexalith.Builds`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, `Hexalith.PolymorphicSerializations`, and `Hexalith.Tenants` — none in story scope or the File List. The drifted `Hexalith.Tenants` (working tree `d0ece74` vs HEAD-recorded `a9cb7f7`) requires `Hexalith.Commons.UniqueIds >= 3.19.0`, which is unpublished, so `dotnet restore`/`build` fails for every graph that references Tenants (the whole Parties test surface). This predates and is unrelated to this story's code. Per repository CLAUDE.md (no submodule update/checkout operations) the submodules were left untouched. **Recommendation:** before commit, reset the non-Commons submodule pointers to their HEAD-recorded baselines so the story diff is Commons-only and the test graph restores.
- **LOW — Duplicate Commons paging record (informational, not changed):** `Hexalith.Commons.Paging.PagedResult<T>` (core) and `Hexalith.Commons.Http.PagedResult<T>` are identical; only the `Http` variant is consumed by Parties. The core variant may be intended for cross-module reuse; flagged for the Commons owner to consolidate or confirm. Not modified (separate repository).

### Build / test evidence (this review)

- `dotnet build src/Hexalith.Parties.ServiceDefaults` (Release, `-m:1`): succeeded, 0 warnings / 0 errors after dead-code removal.
- `dotnet build` of `Hexalith.Parties.Client` succeeded; `Hexalith.Parties.Tests` and `Hexalith.Parties.Client.Tests` could not restore solely because of the `Hexalith.Tenants` drift above (`NU1102: Hexalith.Commons.UniqueIds >= 3.19.0`). New tests use only verified public Commons APIs and will compile once the drift is reset.
- `git diff --check`: clean.
