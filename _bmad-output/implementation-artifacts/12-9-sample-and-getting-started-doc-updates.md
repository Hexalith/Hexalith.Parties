# Story 12.9: Sample and Getting-Started Doc Updates

Status: ready-for-dev

## Story

As a new adopter,
I want the Parties Sample and getting-started guide to demonstrate the EventStore-fronted topology,
so that I onboard against the canonical platform pattern.

## Acceptance Criteria

1. Given `samples/Hexalith.Parties.Sample`, then the sample references only `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.ServiceDefaults` where needed, and sample-owned DAPR subscription packages; it must not reference `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, EventStore server assemblies, DAPR actors, MediatR, FluentValidation, MVC controllers, or retired Parties REST endpoints.
2. Given the sample sends commands and queries, then all consumer traffic goes through `Hexalith.Parties.Client` over the accepted EventStore gateway/client contract from Story 12.5; no sample code, comments, options, tests, or launch settings may point adopters at old `api/v1/parties`, `api/v1/parties/search`, `api/v1/admin`, or in-process `parties` service MCP URLs as product integration paths.
3. Given the sample receives events, then DAPR pub/sub subscription guidance remains subscriber-owned, idempotent, tolerant of unknown additive events, and aligned with the EventStore-published envelope and topic names accepted by Wave 1; if the final envelope/topic contract is not frozen, document the exact blocker instead of claiming live event parity.
4. Given `README.md`, then the first-run path names EventStore as the public command/query entry point, describes `parties` as the actor host behind EventStore, removes REST API and in-process MCP positioning from key features, removes OpenAPI/Swagger claims tied to the retired Parties service, and keeps the startup-log-only GDPR notice accurate after Story 12.2.
5. Given `docs/getting-started.md`, then all command/query curl examples target the EventStore gateway routes (`POST /api/v1/commands` and `POST /api/v1/queries`) with `Domain="party"` unless a later accepted architecture update changes the domain; examples must not use old direct Parties REST route literals.
6. Given the separate MCP host from Story 12.6, then README/getting-started MCP instructions point to the new `parties-mcp` host or record a dated blocker; they must not point adopters at `/mcp` on the Parties actor host.
7. Given the recomposed AppHost from Story 12.1, then README/getting-started local topology instructions mention the expected `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants` resources and explain that the EventStore Admin UI is used for generic stream/event browsing.
8. Given tenant/auth guidance, then docs state that EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping, while Parties owns domain execution behind the actor host; docs must not instruct adopters to manage tenant lifecycle, RBAC, or authorization by calling Parties internals.
9. Given adopter-facing examples include party data, error output, logs, or event payloads, then they avoid regulated personal data, access tokens, signing keys, tenant membership dictionaries, raw protected payloads, stack traces, DAPR sidecar configuration details, and PII in filenames, log messages, screenshots, or copied snippets.
10. Given tests are complete, then sample tests, docs/link/source fitness tests, and focused build checks prove that old Parties REST/admin/MCP route literals are absent from adopter docs/sample production code, the sample package references stay within the approved boundary, and the EventStore-fronted onboarding path is internally consistent.

## Party-Mode Review Clarifications

- Contract source of truth: final command/query, auth/tenant header, RBAC, routing, and response examples must be copied from accepted predecessor implementation evidence, especially Stories 12.4-12.6. Do not reconstruct public contracts from Parties internals or blocked consumer stories.
- Public gateway boundary: adopter-facing docs and sample production code must not present Parties as the public API surface. All public command/query examples go through EventStore `POST /api/v1/commands` or `POST /api/v1/queries` with `Domain="party"` unless a later accepted architecture decision changes the domain.
- Ownership split: EventStore owns public authentication, tenant resolution, RBAC, command/query routing, and generic response mapping. Parties owns domain execution, actors, projections, and internal domain behavior. `parties-mcp` is a separate MCP integration host, not the public command/query gateway and not a replacement for EventStore.
- Package/reference rule: sample package references are allow-listed only. Any dependency beyond `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.ServiceDefaults` where needed, and subscriber-owned DAPR packages requires an explicit story update.
- Text guardrail scope: scan `README.md`, `docs/getting-started.md`, `samples/Hexalith.Parties.Sample/**`, sample project files, and relevant sample tests. Tests may contain denylist literals as assertions. Historical story artifacts, generated output, `bin/`, `obj/`, package lock files, and archived notes are out of scope unless explicitly listed in a named allowlist with a reason.
- Forbidden positioning: adopter-facing docs and sample production code must not describe Swagger/OpenAPI, actor-host `/mcp`, in-process MCP, direct Parties REST/admin routes, projection endpoints, `X-GDPR-Warning`, or Parties-hosted command/query routes as supported setup or integration paths.
- MCP blocker wording: if `parties-mcp` is unavailable or not accepted when this story is implemented, use dated blocker language instead of routing users through the Parties actor host.
- Documentation UX: screenshots, diagrams, and code samples need descriptive alt text or adjacent prose, must not rely on color alone, and should keep EventStore/Parties boundary language plain and culture-neutral.

## Tasks / Subtasks

- [ ] Confirm predecessor gates and current contract state. (AC: 1-10)
  - [ ] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md` and `_bmad-output/implementation-artifacts/12-8-picker-rewrite.md` for consumer migration language.
  - [ ] If Story 12.5 is still blocked and no formal EventStore client/gateway contract is frozen, limit implementation to red guardrail tests, documentation placeholders with explicit blockers, and source-text cleanup that does not fabricate working commands.
  - [ ] If Story 12.6 is not implemented or frozen, do not publish final MCP setup instructions; record a dated blocker naming the missing `parties-mcp` endpoint.

- [ ] Update the sample project boundary. (AC: 1, 2, 3, 9)
  - [ ] Inspect `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj`.
  - [ ] Add `Hexalith.Parties.ServiceDefaults` only if the sample needs standard host health/telemetry wiring; keep the sample free of Parties service/server/projection references.
  - [ ] Keep `Dapr.AspNetCore` or its accepted replacement only for the subscriber endpoint; do not add DAPR actor, DAPR client, or sidecar invocation dependencies for command/query traffic.
  - [ ] Keep command/query calls behind `IPartiesCommandClient` and `IPartiesQueryClient`; do not call `HttpClient` directly for old Parties routes or EventStore DTOs unless Story 12.5 explicitly requires a lower-level seam.
  - [ ] Update configuration names and comments so `Parties:BaseUrl` or its replacement clearly points to the EventStore gateway/client boundary, not the Parties actor host.

- [ ] Rewrite sample walkthrough code and comments. (AC: 2, 3, 6, 9)
  - [ ] Update `samples/Hexalith.Parties.Sample/Program.cs` comments to remove old "REST API and MCP" positioning.
  - [ ] Replace the bottom MCP comment block that points to `https://localhost:5001/mcp` with the Story 12.6 `parties-mcp` host guidance or a dated blocker.
  - [ ] Keep demo output bounded and non-sensitive: correlation ids, party ids, counts, and generic statuses are acceptable; avoid printing tokens, raw payload JSON, tenant membership state, or backend ProblemDetails details.
  - [ ] Keep the event handler sample idempotent and tolerant of unknown events. Do not turn subscriber code into command/query integration code.
  - [ ] If sample event payload examples are updated, preserve tolerant deserialization and document that subscriber apps own their own local envelope models.

- [ ] Rewrite README for the pivoted topology. (AC: 4, 6, 7, 8, 9)
  - [ ] Reframe the service as EventStore-fronted: EventStore is the public command/query gateway; `parties` is the domain actor/projection host.
  - [ ] Replace key feature bullets for REST API, in-process MCP, and OpenAPI with EventStore command/query gateway, separate `parties-mcp` host, typed Parties client, DAPR event subscription, and EventStore Admin UI stream browsing.
  - [ ] Update Quick Start to verify the five AppHost resources from Story 12.1 and direct users to EventStore endpoints for commands/queries.
  - [ ] Keep the GDPR notice aligned with Story 12.2: startup warning only, no `X-GDPR-Warning` response header claim.
  - [ ] Keep links to `docs/getting-started.md`, Tenants access projection, picker docs, architecture, and EventStore/Admin UI guidance accurate.

- [ ] Rewrite the getting-started command/query path. (AC: 5, 7, 8, 9)
  - [ ] Replace `POST /api/v1/parties`, `GET /api/v1/parties/{id}`, `GET /api/v1/parties/search`, and `GET /api/v1/parties` examples with EventStore command/query gateway examples.
  - [ ] Use `Domain="party"` and the command/query type names accepted by Story 12.5 or the frozen Wave 1 contract. Do not use the sprint proposal's older `Domain="Parties"` wording unless a later accepted decision changes the domain.
  - [ ] Explain command acceptance versus projection/query availability without promising read-your-write behavior unless the accepted EventStore/client contract proves it.
  - [ ] Keep the non-.NET path focused on EventStore HTTP gateway calls, not old direct Parties REST.
  - [ ] Replace API overview tables so they list command/query gateway shapes and typed client methods instead of old Parties routes.
  - [ ] Update troubleshooting for `401`, `403`, projection lag, Tenants projection lag, and EventStore gateway readiness without telling adopters to call Parties internals.

- [ ] Update MCP and event subscription onboarding. (AC: 3, 6, 9)
  - [ ] Point MCP clients to the separate `parties-mcp` resource when Story 12.6 lands.
  - [ ] Preserve canonical tool names from Story 12.6 where applicable: `create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`, plus any explicit `get_party_name_at` decision.
  - [ ] State that event subscribers consume EventStore-published DAPR events and must implement idempotent handlers.
  - [ ] Keep `PartyErased` and future additive event guidance tolerant; unknown events should be acknowledged without redelivery loops unless the subscriber explicitly owns them.

- [ ] Add docs/sample regression guardrails. (AC: 1, 2, 4, 5, 6, 10)
  - [ ] Add or update source-text tests that scan `README.md`, `docs/getting-started.md`, `samples/Hexalith.Parties.Sample/**`, and sample tests for retired route literals: `api/v1/parties`, `api/v1/parties/search`, `api/v1/admin`, `openapi/v1.json`, `Swagger`, and actor-host `/mcp` setup guidance.
  - [ ] Allow those literals only in explicitly labeled historical story artifacts, not in current adopter docs or sample production code.
  - [ ] Add package/reference tests proving the sample does not reference Parties service/server/projection, DAPR actor, MediatR, FluentValidation, MVC controller, Swagger/OpenAPI, or EventStore server assemblies.
  - [ ] Update existing sample tests under `tests/Hexalith.Parties.Sample.Tests/**` so they validate EventStore-fronted comments/config and subscriber behavior rather than old direct Parties endpoint assumptions.
  - [ ] Add a doc command-shape test or checked snippet inventory so README/getting-started examples do not drift back to old routes.

- [ ] Verify the docs and sample update. (AC: 1-10)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release`.
  - [ ] Run the focused docs/source fitness tests added or updated by this story.
  - [ ] Run `dotnet build samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj --configuration Release`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [ ] If `dotnet aspire run --project src/Hexalith.Parties.AppHost` or `dotnet aspire` is unavailable, record the limitation and rely on static docs/source tests plus build verification.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.9.
- The pivot decision is that all public command/query traffic goes through EventStore. `Hexalith.Parties` is the actor host/projection runtime behind EventStore, not the adopter-facing REST or MCP surface.
- Story 12.9 is Wave 2 and follows the client, MCP, admin portal, and picker migration stories. Its docs must describe the accepted surfaces those stories create, not old compatibility paths.
- Story 12.5 was blocked when this story was created, but current implementation status must be treated as authoritative. If the typed client/gateway contract is accepted when development starts, use that evidence; if it is still blocked or materially disputed, limit implementation to red guardrail tests, documentation placeholders with explicit blockers, and source-text cleanup that does not fabricate working commands.
- No `project-context.md` persistent fact file was found during story creation.

### Current Implementation to Inspect

- `README.md` currently positions Hexalith.Parties as exposing a REST API, in-process MCP tools, OpenAPI/Swagger, and direct `parties` local endpoints. Those claims are obsolete after Story 12.2 and Epic 12.
- `docs/getting-started.md` currently uses direct `POST /api/v1/parties`, `GET /api/v1/parties/{id}`, `GET /api/v1/parties/search`, and `GET /api/v1/parties` curl examples. These are the main retired route literals to replace.
- `docs/getting-started.md` still claims every API response includes `X-GDPR-Warning`. Story 12.2 demoted FR62 to startup logging only.
- `samples/Hexalith.Parties.Sample/Program.cs` already consumes `IPartiesCommandClient` and `IPartiesQueryClient`, but its comments still say the sample demonstrates REST and MCP, and the bottom MCP configuration block points to `/mcp` on the old Parties service endpoint.
- `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj` currently references `Hexalith.Parties.Client` and `Dapr.AspNetCore`. Reassess whether `Hexalith.Parties.ServiceDefaults` should be added for host defaults, but do not add actor-host/server/projection references.
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` demonstrates subscriber-owned CloudEvents/EventStore envelope handling, idempotency, tolerant deserialization, and unknown-event acknowledgement. Preserve those patterns unless Wave 1 changes the wire envelope.
- `tests/Hexalith.Parties.Sample.Tests/**` currently covers tolerant deserialization, subscriber delivery, selective event handling, publisher/subscriber contracts, and customer summary state. Extend those tests rather than replacing them with broad integration tests.

### EventStore and Client Guidance

- EventStore command ingress is `POST /api/v1/commands`; query ingress is `POST /api/v1/queries`.
- Parties command/query examples should use `Domain="party"` unless a later accepted architecture update changes it.
- The typed `Hexalith.Parties.Client` package should hide raw EventStore transport details from .NET consumers once Story 12.5 lands.
- Adopter docs may show raw EventStore HTTP examples for non-.NET users, but .NET examples should prefer `IPartiesCommandClient` and `IPartiesQueryClient`.
- Do not document old Parties REST/admin route literals as supported compatibility routes. If historical migration notes are unavoidable, label them retired and keep them out of copy-pastable onboarding snippets.
- EventStore Admin UI is the generic stream/event browsing surface. Parties docs should link or describe it rather than rebuilding stream browsing in Parties docs.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, `Dapr.AspNetCore` `1.17.7`, xUnit `2.9.3`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present.
- Do not edit `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, or `Hexalith.Memories` in this story.
- Keep current adopter documentation under `README.md` and `docs/getting-started.md` unless a narrower docs file is required for a focused explanation.
- Keep sample production code under `samples/Hexalith.Parties.Sample` and sample tests under `tests/Hexalith.Parties.Sample.Tests`.
- Generated `bin/`, `obj/`, browser artifacts, screenshots, and test result outputs must stay out of commits.

### Security and Privacy Guidance

- EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping after the pivot.
- Parties owns domain execution behind EventStore. Sample and docs must not teach direct actor-host invocation, projection actor reads, local tenant authorization bypasses, or service-internal validation calls.
- Safe examples may include non-regulated demo names, generated party ids, command/query type names, correlation ids, bounded status categories, and counts.
- Unsafe examples include real personal data, access tokens, signing keys, raw protected payloads, full backend ProblemDetails details, stack traces, DAPR sidecar names/ports beyond operational setup, tenant membership dictionaries, and raw command/query JSON in logs.
- Use placeholders for tokens and ports. Avoid screenshots or snippets that expose live dashboard URLs with secrets or machine-specific credentials.

### Testing Guidance

- Minimum focused tests:
  - README and getting-started docs contain no copy-pastable old `api/v1/parties`, `api/v1/parties/search`, `api/v1/admin`, actor-host `/mcp`, Swagger, or `X-GDPR-Warning` claims.
  - Docs include EventStore command/query gateway language and the five AppHost resource names from Story 12.1.
  - Sample production code contains no old direct Parties REST/admin route literals and no old in-process MCP endpoint instructions.
  - Sample project references stay limited to client/contracts/service defaults where needed plus subscriber-owned DAPR packages.
  - Sample event handler tests still prove idempotent delivery, tolerant deserialization, unknown-event acknowledgement, and no redelivery loops for unhandled additive events.
  - Docs/source fitness tests check internal consistency of the onboarding path, not only absence of retired route literals.
  - Package/reference tests parse project files where practical rather than relying only on raw text scans.
  - Relative docs links and referenced sample files are checked without requiring external network access in normal CI.
  - If Story 12.5 or 12.6 remains blocked, docs tests assert a dated blocker is present instead of runnable final snippets.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release`
  - the focused docs/source fitness tests added by this story
  - `dotnet build samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

### Out of Scope

- Implementing `Hexalith.Parties.Client` EventStore transport; Story 12.5 owns that.
- Implementing the separate Parties MCP host; Story 12.6 owns that.
- Rebuilding Admin Portal or Picker consumers; Stories 12.7 and 12.8 own those.
- Rewriting deployment validation/topology fitness; Story 12.10 owns that.
- Editing EventStore, Tenants, FrontComposer, or Memories submodules.
- Publishing final runnable command/query/MCP snippets against unfrozen or blocked contracts.
- Reintroducing old Parties REST/admin/MCP routes as compatibility shims.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.9 source, Epic 12 pivot rationale, and Wave 2 sequencing.
- `_bmad-output/planning-artifacts/prd.md` - FR32 getting-started, FR59 sample integration, FR62 GDPR notice, NFR30 first-run timing, and documentation requirements.
- `_bmad-output/planning-artifacts/architecture.md` - EventStore-fronted topology, solution structure, sample/docs placement, and dependency boundaries.
- `_bmad-output/implementation-artifacts/6-2-getting-started-guide-and-readme.md` - original README/getting-started story context.
- `_bmad-output/implementation-artifacts/6-3-sample-integration-project.md` - original sample integration story context.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - five-resource AppHost topology, EventStore Admin UI, and runtime proof limitation.
- `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md` - removal of Parties public REST/MCP, startup-log-only GDPR notice, and actor-host boundary.
- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md` - typed client boundary and current blocker.
- `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` - separate MCP host scope and canonical tool names.
- `_bmad-output/implementation-artifacts/12-8-picker-rewrite.md` - consumer docs ownership boundary and EventStore/client wording.
- `README.md` - project overview and quick start to rewrite.
- `docs/getting-started.md` - main adopter walkthrough to rewrite.
- `samples/Hexalith.Parties.Sample/Program.cs` - current sample command/query walkthrough and obsolete MCP comments.
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` - subscriber event handling pattern.
- `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj` - sample dependency boundary.
- `tests/Hexalith.Parties.Sample.Tests/**` - focused sample behavior tests to preserve and extend.

## Project Structure Notes

- Keep adopter docs in `README.md`, `docs/getting-started.md`, and existing docs subfolders unless a focused migration note is clearly needed.
- Keep sample production code in `samples/Hexalith.Parties.Sample`.
- Keep sample tests in `tests/Hexalith.Parties.Sample.Tests`.
- Keep source-text docs guardrails in the existing fitness/docs test project if one already exists; otherwise place them in the narrowest relevant test project without creating a broad new test harness.
- Generated `bin/`, `obj/`, screenshots, Aspire dashboard captures, and test result outputs must stay out of commits unless a test explicitly owns them.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-10 | 0.2 | Party-mode review completed; applied gateway-boundary, source-of-truth, scan-scope, MCP, package-boundary, and documentation UX clarifications. | Codex |
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T17:02:14+02:00
- Selected story key: `12-9-sample-and-getting-started-doc-updates`
- Command/skill invocation used: `/bmad-party-mode 12-9-sample-and-getting-started-doc-updates; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Public docs risk freezing guessed contracts while predecessor stories are still in review or blocked.
  - Adopter-facing docs need a hard EventStore gateway boundary and must not present Parties internals as public APIs.
  - Guardrail scans need explicit path scope and allowlist rules to avoid false positives from tests or historical artifacts.
  - Package/reference rules should be allow-list based and parsed where practical.
  - MCP docs need clear `parties-mcp` separation or dated blocker wording.
  - Docs examples need safe placeholder data, narrow GDPR wording, and accessible/culture-neutral presentation.
- Changes applied:
  - Added party-mode review clarifications for contract source of truth, public gateway boundary, EventStore/Parties/`parties-mcp` ownership split, package allow-listing, text guardrail scope, forbidden public positioning, MCP blocker wording, and docs UX expectations.
  - Updated stale Story 12.5 context so current implementation evidence is authoritative and blocked/disputed contracts require placeholders instead of fabricated runnable snippets.
  - Tightened testing guidance around onboarding consistency, parsed project-file checks, and offline relative-link/reference validation.
- Findings deferred:
  - Final command/query payload shape, auth/tenant header shape, RBAC examples, and route mapping examples remain tied to accepted predecessor implementation evidence.
  - Whether historical migration notes may mention retired routes requires product/docs judgment.
  - Whether forbidden literal scanning becomes a shared repo-wide guardrail remains out of scope for this story.
  - Whether EventStore Admin UI screenshots are required now or can stay text-first until the UI stabilizes remains deferred.
- Final recommendation: ready-for-dev
