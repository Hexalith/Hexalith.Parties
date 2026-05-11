# Story 12.5: Parties Client Thin Wrapper

Status: done

## Story

As a consuming application developer,
I want `Hexalith.Parties.Client` to be a typed thin wrapper over `Hexalith.EventStore.Client`,
so that I can submit Parties commands and queries with strongly typed payloads without re-implementing EventStore client plumbing.

## Acceptance Criteria

1. Given `src/Hexalith.Parties.Client`, then the package references the EventStore client/gateway contract surface used by this repository and removes the old direct Parties REST endpoint implementation.
2. Given `IPartiesCommandClient`, then every command method keeps the existing typed Parties command signature and submits an EventStore command for `Domain="party"` through the EventStore gateway contract, with `AggregateId` derived from the command or route party id and `CommandType` matching the concrete Parties contract type.
3. Given `IPartiesQueryClient`, then every query method keeps its existing typed return shape and submits an EventStore query for `Domain="party"` through the EventStore gateway contract, including projection actor/type metadata only when required by the query adapter delivered by Wave 1.
4. Given EventStore command/query responses, then the Parties client preserves the existing consumer-facing behavior where practical: command methods return the EventStore correlation id and query methods deserialize typed Parties projection models.
5. Given EventStore validation, authorization, not-found, conflict, and degraded responses, then `PartiesClientException` maps the EventStore problem/details payload without leaking raw command/query payloads, tokens, protected personal data, or sidecar configuration details.
6. Given existing client tests, then tests are rewritten away from old `/api/v1/parties` URL assertions and toward EventStore client/testing fakes or gateway request DTO assertions for `POST /api/v1/commands` and `POST /api/v1/queries`.
7. Given package boundary fitness tests, then `Hexalith.Parties.Client` has no direct reference to `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, DAPR, MediatR, FluentValidation, MVC, or the old Parties service endpoint shape.
8. Given Wave 2 sequencing, then this story does not implement the new MCP host, Admin Portal, Picker, sample app, or getting-started documentation; those remain with Stories 12.6-12.9.

## Party-Mode Clarifications

- Normal production implementation is blocked until Wave 1 behavior from Stories 12.1-12.4 is merged or formally frozen. Until then, development may only add red guardrail tests and contract probes that encode the intended EventStore client boundary; do not claim the typed wrapper is production-ready against unstable gateway contracts.
- The client is a typed adapter over EventStore command/query submission only. It must not fallback to direct Parties REST/admin endpoints, call the Parties actor host, use DAPR service invocation, duplicate EventStore tenant/RBAC authorization, or duplicate server-side payload validation beyond existing local argument/serialization guardrails.
- Existing `IPartiesCommandClient` and `IPartiesQueryClient` method names, parameters, return shapes, cancellation token behavior, nullability expectations, and typed DTO expectations must remain source-compatible unless a separately approved breaking-change decision says otherwise.
- Every Parties command/query envelope must use `Domain="party"` and the EventStore command/query gateway contract accepted by Wave 1. Command traffic must target `POST /api/v1/commands`; query traffic must target `POST /api/v1/queries`; old `/api/v1/parties` and `/api/v1/admin` route literals are regression markers, not compatibility shims.
- Error handling must map EventStore validation, authorization/forbidden, not-found, conflict/concurrency, transient transport, malformed response, timeout, and cancellation paths into the existing client-facing exception/result model where practical without leaking raw command/query payloads, protected personal data, tokens, sidecar configuration, server stack traces, or EventStore internal transport details.
- Dependency guardrails must be executable: `Hexalith.Parties.Client` must not reference Parties service/server/projection assemblies or DAPR, MediatR, FluentValidation, ASP.NET MVC, Swagger/OpenAPI, or actor-host packages. EventStore client dependencies are allowed only to the extent needed for the thin wrapper boundary.

## Tasks / Subtasks

- [x] Confirm predecessor gates before implementation. (AC: 1-8)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Confirm Story 12.4 is no longer blocked, or confirm a formal Wave 1 contract freeze covers the command/query envelope, response/error taxonomy, validation/auth ownership, and query adapter contract. If not, stop normal implementation and add only red guardrail tests or contract probes.
  - [x] If Wave 1 has not landed, limit work to failing/red client guardrail tests and do not claim production readiness.
- [x] Inventory the current client surface and preserve consumer API shape unless a breaking change is explicitly justified. (AC: 1, 2, 3, 4)
  - [x] Inspect `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`.
  - [x] Inspect `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`.
  - [x] Inspect `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`.
  - [x] Inspect `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`.
  - [x] Inspect `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`.
  - [x] Keep method names and typed command/query return models stable for downstream projects unless an accepted EventStore client contract forces an intentional migration note.
- [x] Replace direct Parties REST command transport with EventStore command submission. (AC: 1, 2, 4, 5)
  - [x] Remove hard-coded old command URLs such as `api/v1/parties`, `api/v1/parties/{id}/update-person-details`, and `api/v1/parties/create-composite` from the client implementation.
  - [x] Build EventStore command requests using `Domain="party"`, the concrete Parties command type name, the aggregate id from the command/party id, a client-owned message id, a correlation id, and the serialized command payload.
  - [x] Keep route-party-id override behavior for update/contact/identifier/composite methods: the method `partyId` parameter remains authoritative over any stale `PartyId` already inside the command object.
  - [x] Return the EventStore correlation id accepted by the gateway; do not return an old Parties-controller response field.
  - [x] Do not bypass EventStore authorization or validation by calling `Hexalith.Parties` actor host, DAPR sidecar, `PartyDomainServiceInvoker`, MediatR, or old controllers directly.
- [x] Replace direct Parties REST query transport with EventStore query submission. (AC: 1, 3, 4, 5)
  - [x] Remove hard-coded old query URLs such as `api/v1/parties/{id}`, `api/v1/parties`, and `api/v1/parties/search`.
  - [x] Map `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync` to EventStore query requests using `Domain="party"` and the query type/projection metadata proven by Story 12.4.
  - [x] Keep current typed result contracts: `PartyDetail`, `PagedResult<PartyIndexEntry>`, and `PagedResult<PartySearchResult>`.
  - [x] Preserve ISO 8601 date filter serialization, string enum behavior, page/page-size semantics, and null optional filter omission when encoding query payloads.
  - [x] If Story 12.4 records a query adapter blocker, stop and record the exact missing EventStore/Parties projection contract instead of reviving direct Parties REST reads.
- [x] Update DI and options for the EventStore gateway boundary. (AC: 1, 4, 7)
  - [x] Reassess `PartiesClientOptions.BaseUrl`; if retained, document that it is the EventStore gateway base URL, not a Parties service URL.
  - [x] Prefer the existing `AddPartiesClient(...)` one-liner while wiring EventStore client dependencies internally.
  - [x] Do not require consumers to configure DAPR, actor proxies, MediatR, FluentValidation, or the Parties actor host.
  - [x] Keep package dependencies under the NFR31 target and update fitness tests if the EventStore client dependency changes the accepted transitive package set.
- [x] Rewrite client tests to the EventStore contract. (AC: 2, 3, 4, 5, 6)
  - [x] Update `tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs` or its replacement to assert EventStore command request construction rather than old Parties REST paths.
  - [x] Update `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs` or its replacement to assert EventStore query request construction and typed response deserialization.
  - [x] Add red guardrail tests first when Wave 1 is not merged or formally frozen: old `/api/v1/parties` route literals are absent, `Domain="party"` is used, public typed interfaces remain source-compatible, and forbidden dependencies stay out of the client project.
  - [x] Use `Hexalith.EventStore.Client.Testing` fakes if they expose the needed command/query seam; otherwise use a narrow mocked HTTP handler against EventStore DTOs and record the absence of a higher-level fake as a test seam gap.
  - [x] Keep negative tests for validation/problem details, not found, conflict, unauthorized/forbidden, malformed accepted responses, malformed query payloads, and cancellation propagation.
  - [x] Assert the client never sends or stores raw protected payloads in exception messages.
- [x] Harden package-boundary and endpoint fitness tests. (AC: 1, 6, 7)
  - [x] Update `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs` so the allowed package/reference list reflects the EventStore client dependency and still blocks Parties service/server/projection/DAPR/MediatR/FluentValidation leakage.
  - [x] Add source-text checks proving `Hexalith.Parties.Client` no longer contains old Parties REST path literals.
  - [x] Add tests proving the DI option name or documentation cannot be interpreted as a Parties actor-host endpoint when it now targets EventStore.
  - [x] Keep the client package size/transitive-dependency guardrail aligned with NFR31; update the expected dependency list only with explicit rationale.
- [x] Verify the rewritten client. (AC: 1-8)
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj`.
  - [x] Run any focused EventStore client/testing tests required by the new dependency seam.
  - [x] Run `dotnet build Hexalith.Parties.slnx`.
  - [x] If Wave 1 is still active or dirty in the working tree, record that this story was verified only to the extent possible against the current gateway contract.

### Review Findings

- [x] [Review][Patch] Query envelopes target invalid or unproven EventStore query metadata [src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs:40] — `ProjectionType` is sent as `PartyDetail`/`PartyIndex`/`PartySearch`, but EventStore validates projection types as lowercase tenant/domain-style identifiers, and Story 12.4 only proved `queryType = "PartyDetail"` with default `ProjectionType = "party"`. The client also sends `ProjectionActorType = "PartyDetailProjectionActor"`, while EventStore's `QueryRouter` invokes `IProjectionActor.QueryAsync` and Parties projection actors intentionally implement `IPartyDetailProjectionActor`/`IPartyIndexProjectionActor`, not the EventStore generic projection contract. List/search payload filtering is not proven by the Wave 1 adapter either. Applied: client query envelopes no longer send invalid projection type or projection actor metadata; query type constants are aligned to the EventStore gateway-tested shape and tests now assert the corrected envelope.
- [x] [Review][Patch] Missing tenant configuration silently falls back to `default` [src/Hexalith.Parties.Client/PartiesClientOptions.cs:13] — `PartiesClientOptions.Tenant` defaults to `"default"` and `AddPartiesClient` validates only `BaseUrl`, so a missing tenant config can send real command/query traffic to the wrong tenant instead of failing closed. Applied: tenant defaults to empty, DI validates `Parties:Tenant`, and command/query sends fail closed when manually constructed without a tenant.
- [x] [Review][Patch] Required EventStore error-path coverage is incomplete [tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs:129] — the rewritten tests cover one 404, one sensitive 422 detail, malformed accepted command response, and missing query payload, but the story explicitly required validation/problem details, not-found, conflict, unauthorized/forbidden, malformed query payloads, and cancellation propagation. Applied: command and query client tests now cover validation, unauthorized, forbidden, conflict, degraded responses, malformed query JSON, and cancellation propagation.
- [x] [Review][Patch] Search query payload assertions no longer prove pagination or full typed result shape [tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs:172] — the old search test asserted `page` and `pageSize`, and typed result assertions for nested party type, match count, and match type were removed. A regression that drops search pagination or corrupts nested match data would now pass. Applied: search tests assert payload pagination and nested typed result shape.
- [x] [Review][Patch] Problem detail sanitization misses common secret spellings [src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs:233] — `SanitizeDetail` redacts `connection string` but not `connectionString`, `apiKey`, `client_secret`, or similar gateway secret spellings, despite AC5's no-token/no-sidecar/no-protected-detail leakage constraint. Applied: sanitization now catches common API-key/client-secret/connection-string spellings.
- [x] [Review][Defer] Command gateway request DTO remains duplicated locally [_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md:200] — deferred, EventStore currently keeps the API-facing command request DTO in the service assembly rather than the contracts package, so the local client record matches today's shape but remains a cross-package contract drift risk.
- [x] [Review][Defer] Query paging and blank-search input validation remains unspecified [src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs:53] — deferred, page/page-size bounds and blank search handling were not introduced by this transport rewrite and should be resolved as an API behavior policy rather than guessed in the thin wrapper.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.5.
- This is the first Wave 2 consumer-migration story. Wave 2 starts after Wave 1 lands: AppHost recomposition, actor-host cleanup, validation/authorization ownership, and server test rewrite must provide the EventStore gateway/query contract this client wraps.
- The pivot decision is that consuming apps submit commands and queries to EventStore. `Hexalith.Parties.Client` remains a typed convenience package, not a transport owner.
- Story 12.0 selected `Domain="party"` with explicit routing to `AppId=parties`, `MethodName=process` unless a later accepted architecture update changes it.
- Story 12.4 owns server-side gateway coverage. This story should consume the gateway contract proven there, not redefine server routing.

### Current Implementation to Inspect

- `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj` currently references only `Hexalith.Parties.Contracts` plus Microsoft HTTP/options packages. It must add only the EventStore client/contract/testing dependency surface needed for the thin wrapper.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` currently posts directly to old Parties REST routes and extracts `correlationId` from a Parties-controller response. Those route literals are obsolete after Epic 12.
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` currently performs direct `GET` requests against old Parties REST query/search routes. Those calls must become EventStore query submissions.
- `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs` currently interprets `Parties:BaseUrl` as the Parties service endpoint. After the rewrite, any retained base URL must point to the EventStore gateway.
- `tests/Hexalith.Parties.Client.Tests/**` currently asserts old URL paths. Rewrite those tests so they fail if the old REST surface reappears.
- `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs` currently forbids EventStore dependencies indirectly by allowing only Microsoft packages. Update it carefully so EventStore client usage is allowed while server/projection/DAPR/MediatR/FluentValidation leakage remains forbidden.

### EventStore Contract Context

- EventStore API command ingress is `POST /api/v1/commands` with a request shape containing `MessageId`, `Tenant`, `Domain`, `AggregateId`, `CommandType`, `Payload`, optional `CorrelationId`, and optional `Extensions`.
- EventStore API query ingress is `POST /api/v1/queries`; `QueryRouter` routes through `QueryEnvelope` and uses `ProjectionActorType` only when the default `ProjectionActor` actor type is not enough.
- EventStore command responses return a correlation id and command status is EventStore-owned.
- EventStore validation and authorization happen before Parties domain invocation. Client-side code must not duplicate tenant/RBAC policy or call Parties internals to pre-authorize requests.
- If the current `Hexalith.EventStore.Client` package does not yet expose high-level `SubmitCommandAsync` or `SubmitQueryAsync` methods, build the Parties wrapper against the current EventStore public DTO/HTTP seam and record the gap; do not edit the root-level EventStore submodule in this story.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, `Dapr.Client`/`Dapr.AspNetCore` `1.17.7`, `Dapr.Actors` `1.16.1`, xUnit `2.9.3`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present in the workspace.
- Do not edit the `Hexalith.EventStore` submodule for this story. If a platform client helper is missing, use a local Parties client adapter or record the gap for an EventStore story.
- Keep `Hexalith.Parties.Contracts` as the source of typed Parties command/query/result models.
- Do not add DAPR, MediatR, FluentValidation, ASP.NET MVC, or actor proxy dependencies to `Hexalith.Parties.Client`.
- Do not resurrect old Parties REST, admin REST, OpenAPI, Swagger, or in-process MCP URLs as compatibility shims.
- Keep JSON conventions aligned with existing client tests: camelCase, ISO 8601 dates, string enums, and omit nulls.

### Architecture and Security Guidance

- The client should be boring: typed payload construction, EventStore command/query submission, typed response deserialization, and safe exception mapping.
- Treat EventStore client and gateway behavior as the contract source. If the current EventStore client surface cannot express the accepted command/query envelope, record the client-contract blocker instead of inventing a parallel Parties transport.
- The client must not own domain validation, tenant authorization, RBAC, stream persistence, EventStore command-status polling policy beyond a typed convenience if already available, or projection materialization.
- Safe exception mapping may include status, title, type, detail, and correlation id. It must not include access tokens, signing keys, raw command/query payload JSON, protected personal data, or sidecar names/configuration.
- Query methods must not silently fall back to stale direct Parties endpoints when EventStore query routing fails. A missing query adapter is a blocker, not a reason to bypass the platform boundary.
- Keep command id/correlation id generation deterministic and injectable in tests if a new generator is introduced.

### Testing Guidance

- Minimum focused tests:
  - `CreatePartyAsync` submits an EventStore command with `Domain="party"`, the command type, aggregate id equal to `PartyId`, serialized command payload, and returns the EventStore correlation id.
  - Update/contact/identifier/composite methods overwrite stale body `PartyId` with the method `partyId` before serializing.
  - `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync` submit EventStore query requests and deserialize typed Parties models.
  - Validation/problem-details, forbidden, not-found, conflict, and malformed-success responses become `PartiesClientException` without leaking payload contents.
  - Source/fitness tests prove old `api/v1/parties` path literals are absent from `src/Hexalith.Parties.Client`.
  - Fitness tests prove no DAPR, MediatR, FluentValidation, server, projection, or Parties service assembly reference leaks into the client.
  - Guardrail tests prove the public command/query client interfaces remain source-compatible unless an approved breaking-change decision is recorded.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`
  - `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj`
  - `dotnet build Hexalith.Parties.slnx`

### Out of Scope

- Implementing `src/Hexalith.Parties.Mcp` or preserving MCP tool behavior. Story 12.6 owns that.
- Rebuilding Admin Portal or Picker consumers. Stories 12.7 and 12.8 own those migrations.
- Updating sample app or getting-started documentation. Story 12.9 owns that.
- Changing EventStore server, EventStore client SDK, EventStore testing package, or EventStore query routing inside the root-level submodule.
- Rewriting server gateway tests or AppHost topology. Stories 12.1-12.4 own those.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.5 source, Epic 12 pivot rationale, Wave 2 sequencing, and superseded client scope.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - `Domain="party"` guidance and EventStore/Parties routing constraints.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - EventStore gateway topology and base URL ownership context.
- `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md` - removal of direct Parties public REST/MCP surfaces.
- `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md` - EventStore-owned authorization and Parties-owned payload validation boundary.
- `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md` - EventStore command/query test contract this client should wrap.
- `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs` - existing command client API to preserve.
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` - existing query client API to preserve.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` - old direct REST command implementation to replace.
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs` - old direct REST query implementation to replace.
- `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs` - DI entry point and base URL option to reinterpret or rename.
- `tests/Hexalith.Parties.Client.Tests/**` - client tests to rewrite.
- `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs` - EventStore command gateway.
- `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/QueriesController.cs` - EventStore query gateway.
- `Hexalith.EventStore/src/Hexalith.EventStore/Models/SubmitCommandRequest.cs` - EventStore command request DTO.
- `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` - EventStore query request contract.
- `Hexalith.EventStore/src/Hexalith.EventStore.Testing/**` - preferred EventStore test fakes/builders if usable from Parties client tests.

## Project Structure Notes

- Keep implementation changes primarily in `src/Hexalith.Parties.Client` and `tests/Hexalith.Parties.Client.Tests`.
- Keep typed command/result contracts in `src/Hexalith.Parties.Contracts`; do not move contract types into the client package.
- Keep all EventStore server or testing dependency usage behind client tests or thin transport helpers; production client code must stay usable by ordinary .NET consumers.
- Generated `bin/` and `obj/` outputs must stay out of commits.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-10: Loaded predecessor story records 12.0 through 12.4. Stories 12.0 through 12.3 are `done`; Story 12.4 is `review` and records the earlier blocked state as stale after Wave 1 predecessors landed. Proceeding with normal client implementation.
- 2026-05-10: Red client tests initially failed because the production clients still used retired Parties REST paths and lacked tenant-aware EventStore envelope options.
- 2026-05-10: Focused client tests passed: `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --no-restore` (49 passed, 6 pre-existing skipped).
- 2026-05-10: Contract tests passed: `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --no-restore` (42 passed, 15 pre-existing skipped).
- 2026-05-10: Full solution build passed: `dotnet build Hexalith.Parties.slnx --no-restore`.
- 2026-05-10: Full no-build regression passed: `dotnet test Hexalith.Parties.slnx --no-build` (all projects green; integration health skips remain pre-existing).
- 2026-05-11: Code-review remediation passed: `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --no-restore --configuration Release -p:UseSharedCompilation=false` (74/74), `dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --no-restore --configuration Release -p:UseSharedCompilation=false` (57/57), and `dotnet build Hexalith.Parties.slnx --no-restore --configuration Release -p:UseSharedCompilation=false`.

### Completion Notes List

- Replaced the command client transport with EventStore `POST /api/v1/commands` submission using `Domain="party"`, configured tenant, aggregate id from the typed command or method `partyId`, concrete Parties command type name, client-owned message id/correlation id, and serialized typed payload.
- Replaced the query client transport with EventStore `POST /api/v1/queries` submission for `GetParty`, `ListParties`, and `SearchParties`, preserving typed return shapes and encoding page/date/filter payloads with camelCase, string enums, ISO 8601 dates, and null omission.
- Kept `IPartiesCommandClient` and `IPartiesQueryClient` method surfaces unchanged; restored concrete `HttpClient` constructors while marking the options constructor for DI activation.
- Added `PartiesClientOptions.Tenant` and documented `PartiesClientOptions.BaseUrl` as the EventStore gateway base URL. `AddPartiesClient(...)` remains the one-line DI entry point and does not require DAPR, actor, MediatR, FluentValidation, or Parties host configuration.
- Added a narrow `Hexalith.EventStore.Contracts` project reference for the query gateway DTO. The command gateway request DTO is still local because the API-facing command request lives in the EventStore service assembly rather than the contracts package; no EventStore submodule source was edited.
- Rewrote client tests from old `/api/v1/parties` URL assertions to EventStore command/query envelope assertions, including route-party-id override behavior, typed query response deserialization, malformed success handling, and safe problem-detail redaction for sensitive payload/token markers.
- Hardened client fitness tests to require the EventStore contracts boundary, reject EventStore server/service and Parties service/projection/server references, prove retired Parties REST/admin route literals are absent from client source, and pin BaseUrl documentation to the EventStore gateway.

### File List

- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/PartiesClientOptions.cs`
- `tests/Hexalith.Parties.Client.Tests/DependencyInjectionTests.cs`
- `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-10 | 0.2 | Party-mode review blocked normal implementation until Wave 1 contracts land/freeze and added guardrails for EventStore boundary, source compatibility, old-route removal, dependency exclusions, and safe error mapping. | Codex |
| 2026-05-10 | 0.3 | Started implementation after predecessor gate recheck confirmed Story 12.4 is no longer blocked and the Wave 1 command/query envelope is available for the client rewrite. | Codex |
| 2026-05-10 | 1.0 | Rewrote Parties client as an EventStore command/query gateway wrapper, preserved public typed interfaces, added tenant/gateway options, hardened client boundary tests, and completed required validation. | Codex |
| 2026-05-11 | 1.1 | Applied code-review remediation: gateway-valid query envelopes, fail-closed tenant configuration, expanded error/cancellation coverage, stronger search assertions, and broader safe-detail redaction. | Codex |
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T08:49:28Z
- Selected story key: `12-5-parties-client-thin-wrapper`
- Command/skill invocation used: `/bmad-party-mode 12-5-parties-client-thin-wrapper; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: reviewers agreed the story direction is correct but unrestricted implementation is premature while Story 12.4 is blocked on unfrozen Wave 1 contracts. The main risks are unstable EventStore command/query envelope details, unclear error taxonomy, source-compatibility drift for existing typed interfaces, hidden fallback to old Parties REST paths, and quiet dependency leakage into the client package.
- Changes applied: marked the story blocked; added Party-Mode Clarifications; tightened predecessor gating around Story 12.4 and formal Wave 1 contract freeze; added red guardrail test guidance; clarified EventStore-only transport, `Domain="party"`, old route-literal removal, source compatibility, safe error mapping, and executable dependency exclusions.
- Findings deferred: exact EventStore error-to-client exception/result taxonomy; final command/query envelope type names and metadata once Wave 1 freezes; source versus binary compatibility policy if a breaking change is proposed; retry, resilience, and telemetry behavior owned by the EventStore client; adopter-facing examples and documentation updates owned by later Wave 2 documentation work.
- Final recommendation: `blocked`
