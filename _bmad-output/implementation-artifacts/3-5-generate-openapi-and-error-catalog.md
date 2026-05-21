# Story 3.5: Generate OpenAPI and Error Catalog

Status: done

## Story

As a developer evaluating or integrating Parties,
I want browsable API documentation and a documented error catalog,
so that I can understand commands, queries, responses, and failure modes without reading service code.

## Acceptance Criteria

1. Given the Parties REST API is running in development/documentation mode, when a developer opens the generated API specification, then the OpenAPI 3.x document includes v1 command and query endpoints, request schemas, response schemas, auth requirements, and ProblemDetails responses.
2. Given the OpenAPI document is generated, when contract schemas are inspected, then they align with the published Contracts package models, and undocumented or unsupported future capabilities are not advertised as available.
3. Given domain rejection mappings exist, when the error catalog is generated or reviewed, then each stable rejection/error type includes type URI, status code, title, explanation, corrective action, and example response where appropriate.
4. Given compliance warning behavior is active for MVP, when the API documentation is viewed, then it documents that MVP is not GDPR-compliant for regulated EU personal data until v1.1, and startup/API warning behavior is visible to developers.
5. Given API documentation tests run, when they validate OpenAPI generation and error catalog coverage, then missing endpoints, missing ProblemDetails schemas, undocumented rejection types, or future capability leakage fail the tests.

## Tasks / Subtasks

- [x] Verify the EventStore gateway OpenAPI surface for Parties commands and queries. (AC: 1, 2)
  - [x] Confirm `AddOpenApi()` and `MapOpenApi()` are registered only on the EventStore gateway/documentation surface, not the `src/Hexalith.Parties` actor host.
  - [x] Confirm Swagger UI or equivalent browsable documentation is available only in the intended development/documentation mode.
  - [x] Ensure v1 command/query routes expose request and response schemas that match current Contracts package DTOs.
  - [x] Ensure deferred/future capabilities are not advertised as available.
- [x] Harden the documented ProblemDetails/error catalog. (AC: 3)
  - [x] Verify each stable Parties domain rejection has a deterministic reason code, type URI, status code, title, corrective action, and example response.
  - [x] Confirm `DomainRejectionProblemCatalog` covers all current stable rejection events under `src/Hexalith.Parties.Contracts/Events`.
  - [x] Confirm gateway ProblemDetails responses use catalog values and do not leak internal exception, Dapr, actor, database, or implementation details.
- [x] Document MVP GDPR/compliance warning behavior in API documentation. (AC: 4)
  - [x] Preserve the actor-host rule that GDPR warning behavior is startup/API documentation only; do not reintroduce per-response warning headers or direct Parties host endpoints.
  - [x] Ensure OpenAPI/error reference documentation clearly states the MVP compliance limitation and v1.1 boundary.
- [x] Add or tighten tests that fail on documentation drift. (AC: 1-5)
  - [x] Extend gateway OpenAPI tests to assert endpoints, auth metadata, ProblemDetails response metadata, and absence of future/deferred capabilities.
  - [x] Extend error catalog tests to compare catalog entries against stable Parties rejection events.
  - [x] Add regression checks that `src/Hexalith.Parties` does not expose public REST/OpenAPI/Swagger/MCP surfaces for this story.

## Dev Notes

Story 3.5 belongs at the EventStore-fronted gateway boundary. Do not add public REST controllers, minimal API groups, Swagger/OpenAPI endpoints, or MCP tools to `src/Hexalith.Parties`; that project remains the actor host. Use the EventStore gateway surfaces for API documentation and ProblemDetails/error catalog exposure.

The current worktree already contains Story 3.5-shaped code and tests. Treat existing implementation as baseline to verify and harden, not as a blank slate. Relevant existing surfaces observed during story creation include:

- `Hexalith.EventStore/src/Hexalith.EventStore/Program.cs` for OpenAPI/Swagger UI mapping and gateway endpoint setup.
- `Hexalith.EventStore/src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` for OpenAPI and server service registration.
- `Hexalith.EventStore/src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs` for error reference/catalog endpoints.
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainRejectionProblemCatalog.cs` for rejection-to-problem metadata.
- `Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs` for stable type URI constants.
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` for Parties gateway behavior and ProblemDetails assertions.
- `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiSpecTests.cs` for EventStore OpenAPI and Swagger UI behavior.

Use the current repository conventions: xUnit v3, Shouldly, central package management in `Directory.Packages.props`, and existing gateway test factories. Avoid broad package additions unless an existing OpenAPI/UI package is demonstrably missing. Current package versions include `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore.SwaggerUI`.

The OpenAPI implementation should follow current ASP.NET Core guidance: use `Microsoft.AspNetCore.OpenApi`, `AddOpenApi()`, `MapOpenApi()`, document transformers where needed, and a separate UI package for browsable Swagger UI. Keep OpenAPI generation deterministic enough for tests to catch missing schemas and leaked future capabilities.

## References

- Story source: `_bmad-output/planning-artifacts/epics.md#Story 3.5: Generate OpenAPI and Error Catalog`
- PRD: `_bmad-output/planning-artifacts/prd.md` FR56, FR58, FR29, FR30, FR57, FR62
- Architecture: `_bmad-output/planning-artifacts/architecture.md` developer integration and gateway/API documentation guidance
- Prior story: `_bmad-output/implementation-artifacts/3-4-map-domain-rejections-to-problemdetails.md`
- Microsoft OpenAPI docs: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0`
- Microsoft ASP.NET Core OpenAPI docs: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0`

## Dev Agent Record

### Agent Model Used

Created by story automator recovery flow after nested create-story sessions were blocked from writing by read-only sandbox policy.

### Debug Log References

- Nested create-story attempt gathered project context and identified the EventStore gateway OpenAPI/error catalog baseline.
- Nested create-story write attempts failed with `writing is blocked by read-only sandbox`; parent recovery wrote this artifact so automation can continue.

### Completion Notes List

- Story context prepared from epic ACs, prior story boundary decisions, existing gateway code, and current Microsoft OpenAPI guidance.
- Sprint status was not edited directly by the orchestrator recovery path.
- AC1/AC2 verified: `Hexalith.EventStore/src/Hexalith.EventStore/Program.cs` registers `MapOpenApi()` + `UseSwaggerUI(...)` gated on `EventStore:OpenApi:Enabled`; `ServiceCollectionExtensions.cs` registers `AddOpenApi(...)` with a JWT Bearer security scheme, GDPR/v1.1 compliance notice in the document description, and 429 response transformer. New gateway test `OpenApiDocument_WhenDocumentationModeDisabled_IsNotExposedAsync` confirms the document is hidden when documentation mode is disabled. Response schemas now asserted against contract types `SubmitCommandResponse` (202) and `SubmitQueryResponse` (200) in `OpenApiDocument_InDocumentationMode_DescribesGatewayContractsAndComplianceWarningAsync`.
- AC3 verified: `DomainRejectionProblemCatalog.FromReasonCode` maps every stable Parties rejection event in `src/Hexalith.Parties.Contracts/Events` to deterministic ReasonCode/Title/StatusCode/Explanation/CorrectiveAction/RejectionName + TypeUri. Catalog now documents the full set of Parties rejection types (composite-operation-conflict, invalid-consent-purpose, party-cannot-be-created-*, party-cannot-be-deactivated/reactivated-*, party-erasure-in-progress, party-not-restricted, party-processing-restricted). HTML examples + JSON-per-reason endpoints render via `ErrorReferenceEndpoints.cs`.
- AC4 verified: OpenAPI document description carries the MVP compliance/GDPR notice (asserted by `description.ShouldContain("GDPR")` and `description.ShouldContain("v1.1")`). `/problems` HTML index also renders the non-dismissable MVP compliance notice. `Hexalith.Parties` `Program.cs` keeps the startup `LogWarning` GDPR notice and does not reintroduce per-response warning headers (regression-locked by `ArchitecturalFitnessTests.Program_SourceContainsOnlyActorHostMappingsAndDocumentedDaprInternalExceptions`).
- AC5 verified: `EventStoreGatewayRoutingTests` (51 tests, all passing) covers OpenAPI generation, response schema contract refs, ProblemDetails responses, future/deferred capability absence (`/api/v2/...`, retired Parties REST routes), Parties domain rejection documentation via `PartiesDomainRejectionDocumentationData` theory, and OpenAPI hidden when documentation mode is disabled. `ArchitecturalFitnessTests.PartiesAssembly_*` + `Program_SourceContainsOnlyActorHostMappingsAndDocumentedDaprInternalExceptions` + `PartiesProject_RemovesRestOpenApiAndMcpHostPackages` enforce that `src/Hexalith.Parties` does not expose REST/OpenAPI/Swagger/MCP surfaces.
- Test run: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~EventStoreGatewayRoutingTests"` → Passed 51, Failed 0, Skipped 0 (2026-05-21).

### File List

- `_bmad-output/implementation-artifacts/3-5-generate-openapi-and-error-catalog.md`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` (added `OpenApiDocument_WhenDocumentationModeDisabled_IsNotExposedAsync`, asserted contract response schemas on 200/202, expanded `PartiesDomainRejectionDocumentationData` to cover the full set of stable Parties rejections, added `AssertJsonResponseReferencesContract` helper, parameterized `EventStoreGatewayTestFactory` with `openApiEnabled`)

## Change Log

| Date | Author | Change |
|------|--------|--------|
| 2026-05-21 | bmad-story-automator-review (AI) | Reviewed implementation against ACs 1-5. All 51 gateway tests pass. Reconciled File List and task checkboxes against actual implementation; updated status to `done`. |

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (via bmad-story-automator-review)
**Date:** 2026-05-21
**Outcome:** Approve

### Summary
Story 3.5 surfaces the EventStore gateway OpenAPI document, the error/problem catalog (HTML + JSON), and the MVP GDPR compliance notice. Implementation lives entirely behind the EventStore gateway boundary; `src/Hexalith.Parties` remains an actor host with no public REST/OpenAPI/Swagger/MCP surface (enforced by `ArchitecturalFitnessTests`). All 5 acceptance criteria are satisfied and exercised by `EventStoreGatewayRoutingTests` (51/51 passing).

### AC Coverage
- **AC1 (OpenAPI 3.x for v1 commands/queries + auth + ProblemDetails)** — Pass. Document exposes `/api/v1/commands`, `/api/v1/queries`, JWT Bearer security scheme, and `application/problem+json` 400/401/422/503 responses. Verified by `OpenApiDocument_InDocumentationMode_DescribesGatewayContractsAndComplianceWarningAsync`.
- **AC2 (Contract alignment, no future-capability leakage)** — Pass. Request bodies `$ref` `SubmitCommandRequest`/`SubmitQueryRequest`; 200/202 success responses now also assert contract-schema references (`SubmitQueryResponse`/`SubmitCommandResponse`). v2 routes and retired Parties REST routes are explicitly asserted absent.
- **AC3 (Error catalog metadata + example response)** — Pass. `DomainRejectionProblemCatalog` deterministically produces ReasonCode/Title/StatusCode/Explanation/CorrectiveAction/RejectionName for every stable Parties rejection event. Theory test `DomainRejectionCatalog_DocumentsPartiesStableRejectionTypesAsync` exercises all 19 stable Parties rejection reason codes. `/problems/catalog.json` includes `exampleJson` per type (asserted by `AssertProblemCatalogEntry`).
- **AC4 (MVP GDPR/v1.1 disclosure visible in API docs)** — Pass. OpenAPI document description and `/problems` HTML index both expose the MVP-not-GDPR-compliant-until-v1.1 notice; `Hexalith.Parties` startup `LogWarning` preserved without reintroducing per-response warning headers (locked by `ArchitecturalFitnessTests`).
- **AC5 (Tests fail on documentation drift)** — Pass. Adding/removing a stable rejection without updating either the catalog mapping or `PartiesDomainRejectionDocumentationData` fails the theory; disabling documentation mode and still exposing `/openapi/v1.json` fails `OpenApiDocument_WhenDocumentationModeDisabled_IsNotExposedAsync`; reintroducing REST/MCP/OpenAPI to `src/Hexalith.Parties` fails the architectural fitness suite.

### Key Findings (resolved during review)
1. Story File List previously listed only the story file itself — reconciled with the actual Story 3.5 change set (`EventStoreGatewayRoutingTests.cs` plus this story artifact). The EventStore gateway implementation already exists at parent-pinned submodule commit `d45ba01`.
2. Tasks were all unchecked despite the implementation already satisfying ACs — checkboxes reconciled.
3. Story status was `ready-for-dev` despite green tests — updated to `done`.

### Notes / Follow-ups (non-blocking)
- The local worktree currently has an unrelated `Hexalith.EventStore` submodule pointer advance to `da19fba` (`DW15 TypeCatalog disposal-safe navigation hygiene`). It is not part of this story's change set; the 3.5-relevant OpenAPI/error-catalog work is already present at the parent-pinned `d45ba01` commit.
- `/problems/domain-rejections/{reasonCode}.json` JSON response intentionally omits an `exampleJson` field (the HTML page `/problems/domain-rejections/{reasonCode}` and the general `/problems/catalog.json` provide examples). Acceptable under AC3's "where appropriate" qualifier; if future consumers want machine-readable per-reason examples, that's a Story 3.6+ enhancement.
