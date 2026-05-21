# Story 3.4: Map Domain Rejections to ProblemDetails

Status: done

## Story

As a developer integrating with Parties,
I want domain rejections to map to standardized HTTP error responses,
so that I can understand failures and corrective actions without debugging service internals.

## Acceptance Criteria

1. Given a domain command is rejected with a typed rejection event, when the rejection reaches the REST API boundary, then it is mapped to an RFC 7807 ProblemDetails response with stable type URI, title, status, human-readable detail, and corrective action where available.
2. Given a validation failure occurs before command handling, when the API returns the error response, then it uses a standardized validation ProblemDetails shape and field-level errors are bounded and encoded.
3. Given a tenant, authorization, or cross-tenant rejection occurs, when the API returns the error response, then it fails closed with the documented status code and does not reveal whether data exists in another tenant.
4. Given infrastructure or projection degradation prevents a safe response, when the API returns the error response, then it uses a bounded ProblemDetails response with retry/degradation guidance and no raw exception, raw payload, or personal data.
5. Given rejection mapping tests run, when they cover duplicate creation, invalid type update, missing party, missing channel, missing identifier, invalid lifecycle transition, validation failure, auth failure, and degraded read/write failures, then each case maps to the documented ProblemDetails response and logs contain only safe metadata.

## Architecture Reconciliation

The REST boundary for this story is the EventStore gateway, not the Parties actor host. Domain rejection mapping belongs in EventStore's gateway exception handlers because EventStore owns public command/query ingress, authentication, tenant/RBAC validation, and generic HTTP response mapping. Parties remains responsible for producing typed domain rejection events and safe projection failure categories.

## Acceptance Evidence

| AC | Evidence to provide |
| --- | --- |
| 1 | Gateway tests prove typed Parties rejection events map to stable `https://hexalith.io/problems/domain-rejections/{reasonCode}` URIs, stable reason codes, bounded detail, concrete rejection type extension, and corrective action. |
| 2 | Gateway tests prove invalid command envelope shape returns `https://hexalith.io/problems/validation-error`, HTTP 400, and a bounded `errors` object before command/archive state is touched. |
| 3 | Existing and preserved gateway tests prove missing auth returns 401, tenant/domain/permission failures return 403 before command/query routing, and wrong-tenant not-found semantics do not enumerate data. |
| 4 | Gateway tests prove projection query failures return bounded ProblemDetails with `retryAfter`, degradation classification, stable reason code, and no stack trace or payload leak. |
| 5 | Focused gateway tests cover validation rejection, typed domain rejection status mapping, auth/tenant denial, query not-found/forbidden/failure, and actor-host/client/sample guardrails. |

## Tasks

- [x] Create the story artifact and reconcile direct REST wording to the accepted EventStore-fronted boundary. (AC: 1-5)
- [x] Harden EventStore domain rejection ProblemDetails mapping with stable type URI, reason code, title, corrective action, and concrete rejection type extension. (AC: 1, 5)
- [x] Classify current Parties rejection names such as `PartyNotFound`, duplicate rejection names, and mismatch/invalid/cannot rejection names into 404, 409, and 422 statuses. (AC: 1, 5)
- [x] Preserve standardized validation ProblemDetails for invalid gateway envelope shape. (AC: 2)
- [x] Add bounded retry/degradation guidance for projection query execution failures. (AC: 4)
- [x] Add story-scoped gateway tests for typed domain rejections, validation shape, and projection failure ProblemDetails. (AC: 1, 2, 4, 5)
- [x] Run focused gateway, architectural fitness, client boundary, sample guardrail, and EventStore gateway build validation. (AC: 1-5)
- [x] Run review with Claude and apply required review fixes. (AC: 1-5)
- [x] Mark sprint status and story status done after successful review and validation. (AC: 1-5)

## Dev Notes

Implementation constraints:

- Do not add public REST controllers or minimal APIs to `src/Hexalith.Parties`.
- Do not reintroduce retired direct Parties routes.
- Keep gateway ProblemDetails generic enough for EventStore, while preserving enough domain metadata for clients to act on Parties rejections safely.
- Do not expose raw command payloads, personal data, bearer tokens, stack traces, sidecar details, or actor internals in ProblemDetails or tests.
- Keep existing public reason-code vocabulary where it already exists, such as `query_internal_error`.

Current baseline:

- `DomainCommandRejectedExceptionHandler` previously used the raw rejection type as `ProblemDetails.Type` and only mapped `*NotFoundRejection` names to 404.
- `ValidationProblemDetailsFactory` already produces standardized validation ProblemDetails.
- `AuthorizationExceptionHandler` already sanitizes internal terms and maps authorization failures to 403 with stable reason codes.
- `QueryExecutionFailedExceptionHandler` already returns bounded query ProblemDetails; this story adds explicit retry/degradation guidance for 500 projection failures.

## Current Code Surfaces

- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainCommandRejectedExceptionHandler.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
- `_bmad-output/implementation-artifacts/3-4-map-domain-rejections-to-problemdetails.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Anti-Patterns

- Using raw CLR type names as the only public ProblemDetails `type`.
- Treating all typed domain rejections as HTTP 422 when the rejection name clearly represents not-found or duplicate/conflict semantics.
- Echoing payload JSON, personal data, tenant membership details, tokens, stack traces, or sidecar/actor internals into HTTP error responses.
- Adding domain-specific response mapping into the Parties actor host instead of the EventStore gateway boundary.

## Deferred Decisions

- The exhaustive public error catalog is Story 3.5; this story only adds the stable machine-readable fields and focused gateway behavior.
- Per-rejection bespoke corrective action text can be expanded later. This story provides bounded generic guidance by status/category.
- Log capture assertions for every gateway error path remain future hardening if a dedicated log collector fixture is introduced; current tests assert response safety and state-access boundaries.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 3.4 source acceptance criteria.
- `_bmad-output/implementation-artifacts/3-3-expose-versioned-rest-party-api.md` - Predecessor EventStore-fronted REST boundary.
- `_bmad-output/project-context.md` - Current retired-route and actor-host constraints.
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/ValidationProblemDetailsFactory.cs` - Existing validation ProblemDetails shape.
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs` - Existing authorization ProblemDetails shape.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet test .\tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~EventStoreGatewayRoutingTests"` - passed, 29 tests.
- `dotnet test .\tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~ArchitecturalFitnessTests"` - passed, 19 tests.
- `dotnet test .\tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release --filter "FullyQualifiedName~ClientArchitecturalFitnessTests"` - passed, 13 tests.
- `dotnet test .\tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --configuration Release --filter "FullyQualifiedName~SampleOnboardingGuardrailTests"` - passed, 6 tests.
- `dotnet build .\Hexalith.EventStore\src\Hexalith.EventStore\Hexalith.EventStore.csproj --configuration Release` - passed.
- `dotnet test .\tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~EventStoreGatewayRoutingTests|FullyQualifiedName~ArchitecturalFitnessTests"` - post-review validation passed, 48 tests.

### Completion Notes

- Added stable domain rejection ProblemDetails URIs under `https://hexalith.io/problems/domain-rejections/{reasonCode}`.
- Added reason-code, rejection-type, and corrective-action extensions for domain rejections.
- Broadened rejection status mapping to current Parties names: not-found to 404, duplicate/already to 409, and other typed rejections to 422.
- Added bounded retry/degradation metadata for projection query execution failures.
- Added gateway regression tests for validation, typed domain rejection mapping, and bounded projection failure ProblemDetails.

### File List

- `_bmad-output/implementation-artifacts/3-4-map-domain-rejections-to-problemdetails.md`
- `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainCommandRejectedExceptionHandler.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Review Follow-ups (AI) - Applied

- [x] [AI-Review][HIGH] Removed dead/misleading status-switch in `DomainCommandRejectedExceptionHandler` title assignment; title now derives directly from `ToTitle(rejectionName)` since all branches were identical (DomainCommandRejectedExceptionHandler.cs:34-38).
- [x] [AI-Review][MEDIUM] Promoted `rejectionType`, `correctiveAction`, and `degradation` extension keys to public constants on `GatewayProblemDetailsExtensions`, then routed both gateway handlers through them so the public RFC 7807 extension surface stays documented and stable (GatewayProblemDetailsExtensions.cs, DomainCommandRejectedExceptionHandler.cs:46-52, QueryExecutionFailedExceptionHandler.cs:58).
- [x] [AI-Review][MEDIUM] `QueryExecutionFailedExceptionHandler` now sets the standard HTTP `Retry-After: 30` response header alongside the body field for projection-degradation 500s, matching every other retryable handler in the gateway (BackpressureExceptionHandler, AuthorizationServiceUnavailableHandler, etc.) so off-the-shelf HttpClient resilience policies see the guidance (QueryExecutionFailedExceptionHandler.cs:61). Test covers the header assertion (EventStoreGatewayRoutingTests.cs:`PostQueries_RouterFailure_ReturnsBoundedRetryableProblemDetailsAsync`).

Deferred to Story 3.5 (acknowledged in this story's Deferred Decisions):

- [LOW] `GetStatusCode` uses naive substring `Contains` matching (`NotFound`, `Already`, `Duplicate`), which has no word-boundary guarantee. The exhaustive public error catalog will formalize per-rejection mapping.

### Change Log

- 2026-05-21: Created story and implemented EventStore gateway ProblemDetails hardening.
- 2026-05-21: Applied review fixes - collapsed dead title switch, promoted three new ProblemDetails extension keys to `GatewayProblemDetailsExtensions`, and added the standard `Retry-After` HTTP header on 500 projection failures (with test coverage). Focused EventStoreGatewayRoutingTests suite re-run: 29 passed.
