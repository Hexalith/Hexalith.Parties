# Story 12.6: Parties MCP Thin Host

Status: blocked

## Story

As an AI agent,
I want a Parties MCP host that proxies tool calls to EventStore commands and queries,
so that Parties remains MCP-accessible after the in-process MCP wiring is removed.

## Acceptance Criteria

1. Given a new project `src/Hexalith.Parties.Mcp`, then it is a separate ASP.NET Core MCP host and depends only on `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, `Hexalith.EventStore.Client` or its accepted public gateway contract surface, and `Hexalith.Parties.ServiceDefaults`.
2. Given the Parties MCP host, then the canonical MCP tool names `get_party`, `find_parties`, `create_party`, `update_party`, and `delete_party` are preserved with AI-friendly descriptions, parameter names, forgiving input semantics, and response shapes compatible with the pre-pivot MCP behavior where practical.
3. Given a tool call that reads or writes Parties data, then the tool implementation calls `Hexalith.Parties.Client` typed command/query methods and never calls DAPR actors, `ICommandRouter`, `IActorProxyFactory`, projections actors, `IPartySearchService`, validators, controllers, or `Hexalith.Parties` internals directly.
4. Given the MCP host receives tenant/user context, then the context is forwarded to EventStore through the same auth/token/header mechanism accepted by Story 12.5; the MCP host must not reimplement Parties tenant authorization with `ITenantAccessService` or `TenantAccessDenialTranslator`.
5. Given EventStore or Parties client validation, authorization, not-found, conflict, degradation, timeout, or malformed-response failures, then MCP responses map them to sanitized agent-actionable errors without leaking access tokens, signing keys, raw command/query payload JSON, protected personal data, or DAPR sidecar configuration.
6. Given the AppHost, then `parties-mcp` is wired as a separate Aspire project resource that depends on `eventstore` and the Parties actor host, uses the EventStore gateway as its command/query endpoint, and does not run inside the `parties` actor-host process.
7. Given `src/Hexalith.Parties`, then the old in-process `MapMcp()` registration and `src/Hexalith.Parties/Mcp/**` implementation are removed or quarantined by the predecessor story; this story must not reintroduce MCP registration into the actor host.
8. Given a new test project `tests/Hexalith.Parties.Mcp.Tests`, then it covers the same canonical scenarios as `tests/Hexalith.Parties.Tests/Mcp/*` for create, find/list, get, update patch semantics, delete/deactivate idempotency, validation failures, not-found/erased-party behavior, tenant/user context, and safe error output.
9. Given source and dependency fitness tests, then `Hexalith.Parties.Mcp` has no project reference to `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, `Hexalith.Parties.Security`, DAPR, MediatR, FluentValidation, MVC controllers, or EventStore server assemblies.
10. Given the pre-pivot code currently also contains `get_party_name_at`, then the dev pass must make an explicit compatibility decision: either preserve it in `Hexalith.Parties.Mcp` using the EventStore query/client path, or record a deferred product decision with replacement owner. Do not drop it silently while claiming complete MCP parity.

## Tasks / Subtasks

- [x] Confirm predecessor gates before implementation. (AC: 1-10)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [x] If Story 12.5 has not landed, limit implementation to red/failing MCP guardrail tests and project scaffolding that does not assume a final client API.
  - [x] If Story 12.4 remains blocked and the EventStore query contract is not frozen, do not invent a direct projection or actor workaround for reads.
- [x] Create the new MCP host projects and solution entries. (AC: 1, 6, 8, 9)
  - [x] Add `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj` as an ASP.NET Core host targeting `net10.0`.
  - [x] Add `tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj`.
  - [x] Add both projects to `Hexalith.Parties.slnx`.
  - [x] Reference `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, `Hexalith.Parties.ServiceDefaults`, and only the accepted EventStore client/gateway contract dependency required by Story 12.5.
  - [x] Reference `ModelContextProtocol.AspNetCore` from centrally managed package version `1.0.0`.
- [ ] Implement MCP host startup and context capture. (AC: 1, 4, 5, 6)
  - [x] Use the current C# MCP SDK pattern: `AddMcpServer().WithHttpTransport(options => options.Stateless = true).WithToolsFromAssembly()` or explicit `WithTools<T>()` registration, followed by `app.MapMcp()`.
  - [ ] Add service defaults, health/default endpoints, authentication context forwarding, and options validation needed to locate the EventStore gateway.
  - [x] Keep the host stateless unless a required compatibility scenario proves per-session state is necessary.
  - [ ] Do not use the old `RunSessionHandler`/AsyncLocal tenant model unless it is retained only as a thin adapter over the host's authenticated request context.
- [ ] Port canonical tool contracts to the new host. (AC: 2, 3, 5, 10)
  - [ ] Port `create_party` with the same forgiving person/organization/contact/identifier parameters, but dispatch through `IPartiesCommandClient.CreatePartyCompositeAsync` or the accepted Story 12.5 command method.
  - [ ] Port `update_party` with patch semantics, route `partyId` as authoritative, preserve add/update/remove contact-channel and identifier behavior, and dispatch through `IPartiesCommandClient`.
  - [ ] Port `delete_party` as deactivate/soft-delete, preserving idempotent already-inactive behavior where the client/query contract can observe current state.
  - [ ] Port `get_party` and `find_parties` through `IPartiesQueryClient` without actor/projection/search-service shortcuts.
  - [x] Decide and record the fate of `get_party_name_at`; if preserved, route it through an EventStore query/client path rather than the old projection actor.
  - [ ] Keep output JSON camelCase, omit nulls, and serialize enums as strings to match existing MCP/client behavior.
- [ ] Map safe MCP errors and lifecycle responses. (AC: 4, 5)
  - [ ] Convert `PartiesClientException` and EventStore gateway classifications into stable MCP error categories such as validation failed, unauthorized/forbidden, not found, conflict/rejected, degraded/downstream failed, timeout, and canceled.
  - [ ] Include safe correlation/message ids when available.
  - [ ] Avoid localized or brittle message assertions; expose machine-readable codes/categories for agent branching.
  - [ ] Add tests proving exception messages and structured payloads do not contain raw payload JSON, protected PII samples, bearer tokens, DAPR sidecar names, or sidecar configuration.
- [x] Wire AppHost and deployment topology. (AC: 6, 7)
  - [x] Update `src/Hexalith.Parties.AppHost/Program.cs` to add a `parties-mcp` project resource after the MCP host project exists.
  - [x] Ensure `parties-mcp` references/waits for `eventstore`; reference/wait for `parties` only for startup/liveness ordering, not for direct command/query calls.
  - [x] Configure the EventStore gateway base URL/app id for the MCP host using the same convention as Story 12.5.
  - [x] Add focused AppHost topology tests proving `parties-mcp` is separate from `parties` and does not receive DAPR actor sidecar privileges intended for actor-host invocation.
- [ ] Migrate and rewrite tests from the old MCP suite. (AC: 2, 3, 5, 8, 9, 10)
  - [ ] Move canonical tests from `tests/Hexalith.Parties.Tests/Mcp/*` into `tests/Hexalith.Parties.Mcp.Tests`.
  - [ ] Replace actor/search/router fakes with `IPartiesCommandClient` and `IPartiesQueryClient` fakes from Story 12.5 or narrow local test doubles.
  - [ ] Preserve tests for create person/organization, missing required fields, invalid UUIDs, find/list pagination and filters, erased-party handling, update patch merge, duplicate-safe/idempotent behavior, delete idempotency, cancellation propagation, and malformed downstream responses.
  - [x] Add source/dependency fitness tests proving no old in-process `Hexalith.Parties.Mcp` namespace remains under `src/Hexalith.Parties` after Story 12.2 and no direct DAPR/EventStore-server APIs are referenced by the new MCP project.
  - [x] Add a compatibility-matrix note in the Dev Agent Record mapping each old MCP test file/scenario to its new test path, future owner, or explicit retirement reason.
- [ ] Verify the MCP host. (AC: 1-10)
  - [x] Run `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`.
  - [x] Run focused AppHost topology/fitness tests covering `parties-mcp`.
  - [x] Run `dotnet build Hexalith.Parties.slnx`.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.6.
- The pivot decision is that all consumer-facing commands and queries go to EventStore. `Hexalith.Parties` becomes an actor host/projection runtime and must not keep public REST or in-process MCP as the primary surface.
- Story 12.6 is in Wave 2. It should start after Wave 1 lands and after Story 12.5 provides the typed client wrapper. Current sprint status has Story 12.4 blocked, so implementation should honor predecessor gates.
- The MCP host is a consumer of the Parties/EventStore client boundary, not a second gateway, actor host, projection reader, validation layer, or tenant authorization owner.

### Current Implementation to Inspect

- `src/Hexalith.Parties/Mcp/CreatePartyMcpTool.cs` currently builds `CreatePartyComposite`, validates with FluentValidation, dispatches through EventStore server `ICommandRouter`, and reads the projection actor. The new host must keep the tool contract but replace those internals with `Hexalith.Parties.Client`.
- `src/Hexalith.Parties/Mcp/UpdatePartyMcpTool.cs` currently queries projection state to merge patch input and dispatches `UpdatePartyComposite`. Reuse the user-facing patch behavior, but do not retain direct actor/search/router dependencies.
- `src/Hexalith.Parties/Mcp/DeletePartyMcpTool.cs`, `GetPartyMcpTool.cs`, and `FindPartiesMcpTool.cs` currently depend on actor proxies, projection actors, search services, and local tenant access. Those are exactly the dependencies the new host must remove.
- `src/Hexalith.Parties/Mcp/GetPartyNameAtMcpTool.cs` exists even though the Epic 12 proposal names only five canonical tools. Treat this as a compatibility decision, not an accidental deletion.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` currently registers MCP with `AddMcpServer().WithHttpTransport(...).WithToolsFromAssembly()` inside the actor host. Story 12.2 should remove/quarantine that path; Story 12.6 must not add it back.
- `src/Hexalith.Parties/Program.cs` currently maps `app.MapMcp().RequireAuthorization()`. After the pivot, MCP routing belongs in `src/Hexalith.Parties.Mcp/Program.cs`.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/**` is a useful local reference for MCP host patterns: explicit options validation, `MapMcp`, structured failure categories, bounded arguments, tenant context accessors, and safe result envelopes. Reuse patterns, not FrontComposer-specific command descriptors unless they fit the Parties tool contract.
- `tests/Hexalith.Parties.Tests/Mcp/**` is the old sidecar-free MCP behavior suite. It should become the scenario inventory for the new test project.

### Technical Constraints

- Keep package versions aligned with this repository: .NET SDK `10.0.103`, `net10.0`, `ModelContextProtocol.AspNetCore` `1.0.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, xUnit `2.9.3`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present in the workspace.
- Do not edit the `Hexalith.EventStore` submodule in this story. If the client/gateway contract cannot support required MCP behavior, record the blocker and stop rather than calling EventStore server internals.
- Do not add DAPR, MediatR, FluentValidation, MVC controller, EventStore server, Parties actor-host, projection, search, or security project references to `Hexalith.Parties.Mcp`.
- Do not resurrect old Parties REST/admin URLs or actor-projection reads as compatibility shims.
- The MCP SDK documentation current for the C# SDK recommends HTTP transport registration with `WithHttpTransport(options => options.Stateless = true)` and tools registered by `WithToolsFromAssembly()` or explicit `WithTools<T>()`; use that unless local code proves a stronger repository pattern.

### Architecture and Security Guidance

- `Hexalith.Parties.Mcp` should be boring: parse MCP arguments, normalize forgiving AI input, call typed Parties client methods, and convert typed results/errors into safe MCP responses.
- EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties owns domain execution behind EventStore. The MCP host should not duplicate either responsibility.
- Tenant/user context must come from authenticated MCP HTTP context or a deliberate host option/API-key mapping. Fail closed on missing tenant or user context.
- Safe error payloads may include tool name, category, reason code, retry guidance, and correlation/message ids. They must not include raw submitted party data, protected PII, tokens, signing keys, stack traces, DAPR ports, sidecar names, or internal config.
- For create/update/delete tools, if the client returns only command acceptance and projection read-after-write is eventually consistent, prefer an explicit accepted/lifecycle response over locally fabricating a complete `PartyDetail` from raw input. Do not fake read-your-write success.
- For find/list/search, if Story 12.5 or Story 12.4 records a query-adapter blocker, stop and record the exact missing query contract instead of reading projection actors directly.

### Testing Guidance

- Minimum focused tests:
  - `create_party` maps forgiving input to the accepted Parties client command method and preserves complete/accepted response semantics without using EventStore server or DAPR APIs.
  - `update_party` preserves patch semantics, UUID validation, route `partyId` authority, contact/identifier add-update-remove behavior, and no-change rejection.
  - `delete_party` maps to deactivate and handles already-inactive parties idempotently when the client/query contract can prove that state.
  - `get_party` and `find_parties` call query client methods and preserve pagination, active/type filters, erased-party/not-found behavior, and safe search metadata where available.
  - Missing tenant/user context fails closed before any client method is called.
  - Client validation/authorization/not-found/conflict/degraded/timeouts map to sanitized MCP error categories.
  - Fitness tests prove `Hexalith.Parties.Mcp` has no forbidden project/package references and `Hexalith.Parties` no longer maps MCP after the predecessor cleanup.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj`
  - `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj`
  - `dotnet build Hexalith.Parties.slnx`

### Out of Scope

- Rebuilding `Hexalith.Parties.Client`; Story 12.5 owns that.
- Rewriting server gateway tests; Story 12.4 owns that and is currently blocked until predecessor contracts land/freeze.
- Rebuilding Admin Portal, Picker, sample app, or getting-started docs; Stories 12.7-12.9 own those.
- Editing the EventStore, Tenants, FrontComposer, or Memories submodules.
- Keeping MCP inside `src/Hexalith.Parties`.
- Adding new non-canonical MCP business capabilities beyond the explicit `get_party_name_at` compatibility decision.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.6 ACs, Epic 12 pivot rationale, Wave 2 sequencing, AppHost update, and MCP host scope.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - EventStore/Parties routing constraints and no-submodule-edit stop rule.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - current EventStore, Parties, Tenants, and DAPR topology context.
- `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md` - predecessor cleanup expected to remove public REST/MCP from the Parties actor host.
- `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md` - EventStore-owned tenant/RBAC boundary and Parties-owned payload validation boundary.
- `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md` - blocked server test rewrite and query-gateway contract risks.
- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md` - typed client contract the MCP host must consume.
- `src/Hexalith.Parties/Mcp/**` - old tool contracts and compatibility inventory, not a dependency model.
- `tests/Hexalith.Parties.Tests/Mcp/**` - old MCP test scenario inventory to migrate.
- `src/Hexalith.Parties.AppHost/Program.cs` - AppHost topology to add `parties-mcp`.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/**` - local MCP host reference for safe registration, options validation, and structured MCP error patterns.
- C# MCP SDK docs via Context7 `/modelcontextprotocol/csharp-sdk` - current ASP.NET Core pattern for `AddMcpServer`, `WithHttpTransport`, stateless mode, `WithToolsFromAssembly`, and `MapMcp`.

## Project Structure Notes

- Keep production code in `src/Hexalith.Parties.Mcp`.
- Keep new MCP tests in `tests/Hexalith.Parties.Mcp.Tests`.
- Keep typed contracts in `src/Hexalith.Parties.Contracts` and transport/client behavior in `src/Hexalith.Parties.Client`.
- Keep `src/Hexalith.Parties` focused on actor hosting and projections after the predecessor cleanup.
- Generated `bin/` and `obj/` outputs must stay out of commits.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-10: Confirmed predecessor gates. Stories 12.4 and 12.5 are both `blocked`; Story 12.5 has not landed and Story 12.4 has not frozen the EventStore query contract. Normal implementation is blocked by the story's own gate.
- 2026-05-10: Limited implementation to scaffold and executable guardrails: separate ASP.NET Core MCP host, separate test project, canonical tool metadata, safe blocked result envelope, solution entries, and AppHost `parties-mcp` resource.
- 2026-05-10: `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj` passed (8/8).
- 2026-05-10: `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj` passed (59 passed, 6 skipped).
- 2026-05-10: `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` passed.
- 2026-05-10: `dotnet build Hexalith.Parties.slnx` passed. Restore still prints a pre-existing skipped-project warning for `Hexalith.EventStore\Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj`.

### Completion Notes List

- Added `src/Hexalith.Parties.Mcp` as a separate ASP.NET Core host with service defaults, validated `Parties:Mcp` options, stateless HTTP MCP transport, assembly tool discovery, and `MapMcp()` in the new host only.
- Added canonical MCP tool scaffolding for `get_party`, `find_parties`, `create_party`, `update_party`, and `delete_party`; each currently returns a sanitized `contract_unavailable` blocked result rather than calling the old REST-backed client or any actor/projection/server internals.
- Compatibility decision: preserve `get_party_name_at` as a blocked scaffolded tool for now. It must be routed through an EventStore query/client path once Story 12.4/12.5 provides or freezes that contract; it was not dropped silently.
- Added `tests/Hexalith.Parties.Mcp.Tests` guardrails for canonical tool names/descriptions, read/write/idempotency annotations, sanitized blocked output, allowed project references, forbidden dependency/source markers, stateless startup registration, and AppHost `parties-mcp` separation.
- Wired AppHost with a separate `parties-mcp` resource that references/waits for `eventstore` and references/waits for `parties` only for startup/liveness ordering; it does not receive a DAPR actor sidecar.
- Story remains blocked because full tool dispatch, tenant/user context forwarding, safe client exception mapping, old MCP scenario migration, and complete parity cannot be implemented honestly until Story 12.5 lands or a formal Wave 1/typed-client contract freeze exists.
- Compatibility matrix: old `tests/Hexalith.Parties.Tests/Mcp/CreatePartyMcpToolTests.cs` -> future `tests/Hexalith.Parties.Mcp.Tests` command-dispatch coverage after 12.5; `FindPartiesMcpToolTests.cs` and `GetPartyMcpToolTests.cs` -> future query-client coverage after 12.4/12.5; `UpdatePartyMcpToolTests.cs` and `UpdateAndDeletePartyMcpToolTests.cs` -> future patch/deactivate coverage after typed command/query semantics freeze; `DeletePartyMcpToolTests.cs` -> future idempotent deactivate coverage after observable state/query contract freezes; `GetPartyNameAtMcpToolTests.cs` -> preserved compatibility owner in this story but blocked pending query path; `McpToolTenantAuthorizationTests.cs` -> future host-auth/context-forwarding coverage after EventStore auth/header contract freezes; `McpSessionScope.cs` and `McpToolTestServices.cs` -> old in-process test helpers retired, not copied into the new scaffold.

### File List

- `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `Hexalith.Parties.slnx`
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`
- `src/Hexalith.Parties.Mcp/Program.cs`
- `src/Hexalith.Parties.Mcp/PartiesMcpOptions.cs`
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpToolNames.cs`
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpToolResult.cs`
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`
- `tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj`
- `tests/Hexalith.Parties.Mcp.Tests/RepositoryRoot.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpProjectFitnessTests.cs`
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolContractTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-10 | 0.2 | Added blocked-scope MCP host scaffold, canonical tool metadata, safe blocked responses, AppHost resource wiring, and guardrail tests; story blocked pending Story 12.4/12.5 contract landing/freeze. | Codex |
