# Story 12.9: Sample and Getting-Started Doc Updates

Status: done

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

## Advanced Elicitation Clarifications

- Evidence freeze rule: before publishing runnable command/query or MCP examples, capture dated evidence from Stories 12.5 and 12.6 showing the accepted route, domain, auth/tenant header, payload, response, and tool-name contracts. If evidence is still in review, disputed, or blocked, publish a dated blocker or non-runnable placeholder instead of guessed snippets.
- Snippet status rule: every copied command/query/MCP example in `README.md` or `docs/getting-started.md` should be clearly treated by tests as either runnable against the accepted EventStore/`parties-mcp` surface or intentionally blocked with a date and predecessor story reference.
- Gateway configuration rule: sample options, comments, launch settings, and docs must name the configured endpoint as the EventStore gateway or typed client boundary. Avoid ambiguous `Parties` host wording that could send adopters to the actor host.
- Event subscriber contract rule: subscriber examples may model only the additive EventStore-published envelope fields they consume, must acknowledge unknown or future events without redelivery loops, and must not imply DAPR actor or service-invocation access to Parties.
- Troubleshooting rule: authorization, tenant, projection-lag, EventStore-readiness, and MCP-unavailable guidance must stay outside backend internals. Safe troubleshooting points to gateway status, tenant/RBAC configuration, projection freshness, and dated blockers rather than Parties actor endpoints, stack traces, or protected payloads.
- Test stability rule: source-text guardrails should parse project files and checked snippets where practical, strip generated `bin`/`obj` output, and allow forbidden literals only in explicitly named negative tests or historical artifacts so the story does not create brittle repo-wide scans.

## Tasks / Subtasks

- [x] Confirm predecessor gates and current contract state. (AC: 1-10)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md` and `_bmad-output/implementation-artifacts/12-8-picker-rewrite.md` for consumer migration language.
  - [x] Record the dated source artifact and status used as evidence for command/query, auth/tenant, RBAC, response-mapping, and MCP tool contracts.
  - [x] If Story 12.5 is still blocked and no formal EventStore client/gateway contract is frozen, limit implementation to red guardrail tests, documentation placeholders with explicit blockers, and source-text cleanup that does not fabricate working commands.
  - [x] If Story 12.6 is not implemented or frozen, do not publish final MCP setup instructions; record a dated blocker naming the missing `parties-mcp` endpoint.

- [x] Update the sample project boundary. (AC: 1, 2, 3, 9)
  - [x] Inspect `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj`.
  - [x] Add `Hexalith.Parties.ServiceDefaults` only if the sample needs standard host health/telemetry wiring; keep the sample free of Parties service/server/projection references.
  - [x] Keep `Dapr.AspNetCore` or its accepted replacement only for the subscriber endpoint; do not add DAPR actor, DAPR client, or sidecar invocation dependencies for command/query traffic.
  - [x] Keep command/query calls behind `IPartiesCommandClient` and `IPartiesQueryClient`; do not call `HttpClient` directly for old Parties routes or EventStore DTOs unless Story 12.5 explicitly requires a lower-level seam.
  - [x] Update configuration names and comments so `Parties:BaseUrl` or its replacement clearly points to the EventStore gateway/client boundary, not the Parties actor host.
  - [x] Check launch settings, options binding, sample comments, and test fixtures for ambiguous endpoint names that could be interpreted as a direct Parties actor-host URL.

- [x] Rewrite sample walkthrough code and comments. (AC: 2, 3, 6, 9)
  - [x] Update `samples/Hexalith.Parties.Sample/Program.cs` comments to remove old "REST API and MCP" positioning.
  - [x] Replace the bottom MCP comment block that points to `https://localhost:5001/mcp` with the Story 12.6 `parties-mcp` host guidance or a dated blocker.
  - [x] Keep demo output bounded and non-sensitive: correlation ids, party ids, counts, and generic statuses are acceptable; avoid printing tokens, raw payload JSON, tenant membership state, or backend ProblemDetails details.
  - [x] Keep the event handler sample idempotent and tolerant of unknown events. Do not turn subscriber code into command/query integration code.
  - [x] If sample event payload examples are updated, preserve tolerant deserialization and document that subscriber apps own their own local envelope models.

- [x] Rewrite README for the pivoted topology. (AC: 4, 6, 7, 8, 9)
  - [x] Reframe the service as EventStore-fronted: EventStore is the public command/query gateway; `parties` is the domain actor/projection host.
  - [x] Replace key feature bullets for REST API, in-process MCP, and OpenAPI with EventStore command/query gateway, separate `parties-mcp` host, typed Parties client, DAPR event subscription, and EventStore Admin UI stream browsing.
  - [x] Update Quick Start to verify the five AppHost resources from Story 12.1 and direct users to EventStore endpoints for commands/queries.
  - [x] Keep the GDPR notice aligned with Story 12.2: startup warning only, no `X-GDPR-Warning` response header claim.
  - [x] Keep links to `docs/getting-started.md`, Tenants access projection, picker docs, architecture, and EventStore/Admin UI guidance accurate.

- [x] Rewrite the getting-started command/query path. (AC: 5, 7, 8, 9)
  - [x] Replace `POST /api/v1/parties`, `GET /api/v1/parties/{id}`, `GET /api/v1/parties/search`, and `GET /api/v1/parties` examples with EventStore command/query gateway examples.
  - [x] Use `Domain="party"` and the command/query type names accepted by Story 12.5 or the frozen Wave 1 contract. Do not use the sprint proposal's older `Domain="Parties"` wording unless a later accepted decision changes the domain.
  - [x] Explain command acceptance versus projection/query availability without promising read-your-write behavior unless the accepted EventStore/client contract proves it.
  - [x] Label any non-runnable example as a dated blocker tied to the predecessor story whose contract is missing; do not mix blocker text with copy-pastable commands.
  - [x] Keep the non-.NET path focused on EventStore HTTP gateway calls, not old direct Parties REST.
  - [x] Replace API overview tables so they list command/query gateway shapes and typed client methods instead of old Parties routes.
  - [x] Update troubleshooting for `401`, `403`, projection lag, Tenants projection lag, and EventStore gateway readiness without telling adopters to call Parties internals.

- [x] Update MCP and event subscription onboarding. (AC: 3, 6, 9)
  - [x] Point MCP clients to the separate `parties-mcp` resource when Story 12.6 lands.
  - [x] Preserve canonical tool names from Story 12.6 where applicable: `create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`, plus any explicit `get_party_name_at` decision.
  - [x] State that event subscribers consume EventStore-published DAPR events and must implement idempotent handlers.
  - [x] Keep `PartyErased` and future additive event guidance tolerant; unknown events should be acknowledged without redelivery loops unless the subscriber explicitly owns them.

- [x] Add docs/sample regression guardrails. (AC: 1, 2, 4, 5, 6, 10)
  - [x] Add or update source-text tests that scan `README.md`, `docs/getting-started.md`, `samples/Hexalith.Parties.Sample/**`, and sample tests for retired route literals: `api/v1/parties`, `api/v1/parties/search`, `api/v1/admin`, `openapi/v1.json`, `Swagger`, and actor-host `/mcp` setup guidance.
  - [x] Allow those literals only in explicitly labeled historical story artifacts, not in current adopter docs or sample production code.
  - [x] Add package/reference tests proving the sample does not reference Parties service/server/projection, DAPR actor, MediatR, FluentValidation, MVC controller, Swagger/OpenAPI, or EventStore server assemblies.
  - [x] Update existing sample tests under `tests/Hexalith.Parties.Sample.Tests/**` so they validate EventStore-fronted comments/config and subscriber behavior rather than old direct Parties endpoint assumptions.
  - [x] Add a doc command-shape test or checked snippet inventory so README/getting-started examples do not drift back to old routes.
  - [x] Add a blocker-snippet test path so docs fail if a predecessor contract is unresolved but the docs present runnable commands, and fail if accepted contracts exist but the docs still present only blocker placeholders.

- [x] Verify the docs and sample update. (AC: 1-10)
  - [x] Run `dotnet test tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release`.
  - [x] Run the focused docs/source fitness tests added or updated by this story.
  - [x] Run `dotnet build samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If `dotnet aspire run --project src/Hexalith.Parties.AppHost` or `dotnet aspire` is unavailable, record the limitation and rely on static docs/source tests plus build verification.

### Review Findings

_Code review performed 2026-05-13 (bmad-code-review). 2 decisions resolved, 12 patches applied, 6 deferred, 12 dismissed. Sample tests 52/52, full Release solution build clean. See `_bmad-output/implementation-artifacts/deferred-work.md` for deferred items._

#### Decisions (resolved 2026-05-13)

- [x] [Review][Decision] README Project Structure label overlap — RESOLVED: annotate `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections` as internal to the actor host (not adopter-facing dependencies). Folded into patch P7 below.
- [x] [Review][Decision] Sample `appsettings.json` `BaseUrl` value — RESOLVED: replace with the placeholder `"https://localhost:<eventstore-port>"` and add a short comment in `Program.cs` directing adopters to copy the actual port from the Aspire dashboard. Folded into patch P1 below.

#### Patches

- [x] [Review][Patch] `samples/Hexalith.Parties.Sample/appsettings.json` `BaseUrl` is stale legacy port [`samples/Hexalith.Parties.Sample/appsettings.json:3`] — Resolved-as-patch under D2. Replace `https://localhost:5001` with a clearly non-runnable placeholder (`https://localhost:<eventstore-port>`) or document that the value must be updated to the AppHost-assigned EventStore port.
- [x] [Review][Patch] Guardrail `ShouldAllBe` is vacuously true on empty collections [`tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs:82-98`] — A sample csproj with zero `ProjectReference` or zero `PackageReference` would pass the guardrail. Add `projectReferences.ShouldNotBeEmpty()` and `packageReferences.ShouldNotBeEmpty()` before the `ShouldAllBe` calls.
- [x] [Review][Patch] Demo exception logger drops exception entirely [`samples/Hexalith.Parties.Sample/Program.cs:144`] — `logger.LogError("Demo failed with {ExceptionType}", ex.GetType().Name)` discards the stack trace and inner exception from structured logs. Change to `logger.LogError(ex, "Demo failed")`; keep the bounded `Console.WriteLine` text as-is for PII safety. Structured loggers can then capture full detail without printing it to the demo console.
- [x] [Review][Patch] Forbidden literal scan is case-sensitive [`tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs:38,64`] — `text.ShouldNotContain(forbidden)` uses ordinal case-sensitive match. Variants like `SWAGGER`, `OpenAPI`, `Api/V1/Parties` slip past. Use case-insensitive comparison (`text.ShouldNotContain(forbidden, Case.Insensitive)` or lowercase normalization).
- [x] [Review][Patch] Markdown table row uses broken nested backticks [`docs/getting-started.md:333`] — `` `parties-mcp` `/mcp` `` renders broken because the backticks pair across the space. Rewrite as `` `parties-mcp /mcp` `` or split into two columns.
- [x] [Review][Patch] README claims `parties-mcp` is "present when the MCP host is included in the local AppHost" [`README.md:25`] — Story 12.1 AppHost `Program.cs:42` unconditionally adds `partiesMcp`. The conditional wording is misleading. Reword to state `parties-mcp` runs alongside `parties` as a separate resource.
- [x] [Review][Patch] README Project Structure lists internal projects without annotation [`README.md:46-50`] — `Hexalith.Parties.Projections` and `Hexalith.Parties.Server` are not adopter-facing dependencies but appear in the structure tree at the same level as `Hexalith.Parties.Client`. Annotate them as "internal to actor host — not adopter-facing dependencies" or move under an "Internal modules" subsection.
- [x] [Review][Patch] Step 2 Bash drops the Keycloak token-fetch example [`docs/getting-started.md:64-76`] — Old doc had a complete `curl -X POST .../token` example; new doc replaces with `export TOKEN=<access-token>` and a paragraph "Keycloak is enabled by default... use the repository's symmetric-key development token settings". Add a short token-fetch snippet (curl against the local Keycloak realm + client) or link to a sibling doc so first-run adopters can complete Step 3.
- [x] [Review][Patch] MCP JSON config examples have angle-bracket placeholders without "replace these" guidance [`docs/getting-started.md:293-306`, `samples/Hexalith.Parties.Sample/Program.cs:183,198`] — `<parties-mcp-port>`, `<token>`, `<user-id>` are angle-bracket placeholders. Add a one-line "Replace `<parties-mcp-port>` with the actual port from the Aspire dashboard, `<token>` with your bearer token, `<user-id>` with your authenticated user id."
- [x] [Review][Patch] Curl examples lack `--fail` / HTTP-status capture / dev-cert troubleshooting [`docs/getting-started.md` Step 3-4 curl blocks; Troubleshooting section] — `curl -s ... | jq` swallows non-JSON responses with a cryptic jq parse error. Add `-w '\n[HTTP %{http_code}]\n'` (or `--fail-with-body`), and add a Troubleshooting line for "SSL certificate problem" pointing to `dotnet dev-certs https --trust`.
- [x] [Review][Patch] Guardrail forbidden literal list missing variants [`tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs:21-30,47-55`] — Spec line 97 enumerates retired routes including `api/v1/parties/search`. While `api/v1/parties` substring covers it, `OpenAPI` (different from `openapi/v1.json`), `/openapi`, and `PartyActor` are not in the list. Add `OpenAPI`, `/openapi`, `PartyActor`, `PartyDetailProjectionActor`, `PartyIndexProjectionActor` (case-insensitive per P4).
- [x] [Review][Patch] Guardrail test `SampleTests_DoNotAssertRetiredPartiesRoutes` enumerates `*.cs` under `tests/Hexalith.Parties.Sample.Tests/**` including `obj/` and `bin/` paths [`tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs:57-58`] — On a freshly built dev machine, MSBuild may emit generated `.cs` files under `obj/Debug/net10.0/` containing forbidden substrings (e.g., generator output referencing `openapi/v1.json` or attribute strings). Exclude `obj/` and `bin/` segments from the enumeration.

#### Deferred

- [x] [Review][Defer] Bash `'"$TENANT_ID"'` interpolation pattern [`docs/getting-started.md:97,160`] — deferred, valid Bash idiom; copy-paste errors are a general shell concern. Pre-existing pattern carried across multiple stories.
- [x] [Review][Defer] Subscription topic `tenant-a.parties.events` naming convention [`samples/Hexalith.Parties.Sample/Program.cs:21`, `docs/getting-started.md:317`] — deferred, the topic name is owned by the sample's pre-existing DAPR subscription file and the EventStore publication contract. Not introduced by this story; revisit when subscription guidance is consolidated.
- [x] [Review][Defer] `aggregateId="parties"` envelope convention for `PartySearch`/`PartyIndex` queries [`docs/getting-started.md:194,214`] — deferred, the lower-case `"party"` domain plus collection-style `"parties"` aggregateId for non-aggregate queries is a Story 12.5 envelope convention. Document or rename as part of the Client/Gateway documentation update, not this docs story.
- [x] [Review][Defer] `get_party_name_at` MCP tool lacks parameter schema in adopter docs [`README.md:12`, `docs/getting-started.md:289`] — deferred, MCP tool parameter schemas are owned by the `parties-mcp` host (Story 12.6) and exposed via its OpenAPI/MCP manifest. Adopter docs need only list the tool name.
- [x] [Review][Defer] Cross-shell consistency between Bash and PowerShell examples — deferred, current snippets are functional in their respective shells.
- [x] [Review][Defer] Sample csproj does not reference `Hexalith.Parties.ServiceDefaults` [`samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj`] — deferred, task said "only if needed". Sample does not currently require shared host health/telemetry wiring; revisit if Aspire integration is added to the sample.

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
  - Checked command/query snippets prove `POST /api/v1/commands` and `POST /api/v1/queries` examples use the accepted `Domain="party"` casing and do not include retired route literals in URLs, comments, or nearby explanatory text.
  - Blocker-placeholder tests distinguish final runnable snippets from unresolved predecessor contracts, so docs cannot accidentally publish guessed command/query or MCP examples.
  - Troubleshooting tests or source checks verify that tenant/auth/projection guidance points to EventStore gateway, tenant/RBAC configuration, and projection freshness rather than Parties actor-host internals.
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

Codex GPT-5

### Debug Log References

- 2026-05-11: Confirmed predecessor evidence. Stories 12.0 through 12.6 are `done`; Stories 12.5 and 12.6 provide accepted EventStore command/query and `parties-mcp` contracts. Stories 12.7 and 12.8 remain blocked consumer migrations and were used only for wording/context.
- 2026-05-11: Evidence source for command/query contract: Story 12.5 v1.1 and current `HttpPartiesCommandClient` / `HttpPartiesQueryClient` tests. Accepted routes are `POST /api/v1/commands` and `POST /api/v1/queries`; accepted domain is `party`; accepted query types are `PartyDetail`, `PartyIndex`, and `PartySearch`.
- 2026-05-11: Evidence source for auth/tenant/RBAC/response ownership: Stories 12.2 and 12.3. EventStore owns public authentication, tenant validation, RBAC, routing, and generic response mapping; Parties owns domain execution and projections behind the actor host.
- 2026-05-11: Evidence source for MCP contract: Story 12.6 v1.0 plus 2026-05-11 sprint close-out for review patches. `parties-mcp` is the separate host and canonical tools are `create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`, and `get_party_name_at`.
- 2026-05-11: Red/guardrail phase added `SampleOnboardingGuardrailTests`; focused filter initially failed during compilation because `ShouldNotContain` custom-message overload usage was invalid. Fixed the assertions and reran the focused guardrail filter successfully (6/6).
- 2026-05-11: Focused sample validation passed: `dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` (52/52).
- 2026-05-11: Sample build passed: `dotnet build samples\Hexalith.Parties.Sample\Hexalith.Parties.Sample.csproj --configuration Release --no-restore -p:UseSharedCompilation=false`.
- 2026-05-11: Solution build passed: `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -p:UseSharedCompilation=false`.
- 2026-05-11: `dotnet aspire --version` failed because `dotnet-aspire` is not installed on PATH; runtime Aspire proof was unavailable.
- 2026-05-11: First full no-build regression attempt timed out after 10 minutes; rerun with a longer timeout passed: `dotnet test Hexalith.Parties.slnx --configuration Release --no-build -p:UseSharedCompilation=false` (973 passed, 6 expected health E2E skips).

### Completion Notes List

- Rewrote `README.md` around the EventStore-fronted topology: `eventstore` is the public command/query gateway, `parties` is the actor/projection host, `parties-mcp` is a separate MCP host, and EventStore Admin UI owns generic stream/event browsing.
- Replaced `docs/getting-started.md` direct Parties REST examples with EventStore gateway examples for `POST /api/v1/commands` and `POST /api/v1/queries` using `Domain="party"` and accepted command/query type names.
- Removed adopter-facing OpenAPI/Swagger, direct Parties REST/admin, actor-host `/mcp`, and retired GDPR response-header positioning from current docs and sample production code.
- Updated the sample walkthrough so command/query traffic remains behind `IPartiesCommandClient` and `IPartiesQueryClient`, `Parties:BaseUrl` is documented as the EventStore gateway URL, `Parties:Tenant` is configured, demo output is bounded to ids/counts/status, and failures do not print backend details.
- Preserved the sample's subscriber-owned DAPR event handling pattern, idempotency, tolerant deserialization, unknown-event acknowledgement, and local envelope model.
- Added sample onboarding guardrails that scan current adopter docs, sample production code, and sample tests for retired route literals and verify the sample project reference/package boundary, EventStore topology language, runnable snippet shape, and `parties-mcp` guidance.

### File List

- `_bmad-output/implementation-artifacts/12-9-sample-and-getting-started-doc-updates.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `README.md`
- `docs/getting-started.md`
- `samples/Hexalith.Parties.Sample/Program.cs`
- `samples/Hexalith.Parties.Sample/appsettings.json`
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-11 | 1.0 | Rewrote sample/docs onboarding around the EventStore gateway, separate `parties-mcp` host, typed client configuration, subscriber-owned event handling, and guardrail tests; validation passed and story moved to review. | Codex |
| 2026-05-10 | 0.3 | Advanced elicitation completed; applied evidence-freeze, snippet-status, gateway-config, subscriber-contract, troubleshooting, and test-stability clarifications. | Codex |
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

## Advanced Elicitation

- Date/time: 2026-05-10T19:03:38+02:00
- Selected story key: `12-9-sample-and-getting-started-doc-updates`
- Command/skill invocation used: `/bmad-advanced-elicitation 12-9-sample-and-getting-started-doc-updates`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The story needed a stricter evidence freeze so documentation does not publish guessed runnable EventStore or MCP contracts while predecessor stories remain in review or disputed.
  - Docs and tests need to distinguish accepted runnable snippets from intentionally blocked placeholder text.
  - Sample configuration names can accidentally point adopters to the Parties actor host unless endpoint ownership is explicit.
  - Event subscriber examples need an additive-envelope and unknown-event acknowledgement rule to avoid brittle consumer guidance.
  - Troubleshooting guidance must keep users at gateway, tenant/RBAC, projection freshness, and blocker evidence levels rather than backend internals.
  - Guardrail tests should be narrow and parse structured files/snippets where practical to avoid brittle false positives.
- Changes applied:
  - Added advanced elicitation clarifications for evidence freeze, snippet status, EventStore gateway configuration, subscriber contract, troubleshooting, and test stability.
  - Added tasks to record dated predecessor evidence, scan ambiguous endpoint names, label blocked snippets, and test blocker-vs-runnable snippet behavior.
  - Expanded testing guidance for command/query snippet shape, blocker placeholders, and internal-troubleshooting regressions.
- Findings deferred:
  - Final runnable command/query payload examples remain deferred to accepted Story 12.5 evidence.
  - Final MCP endpoint and tool setup examples remain deferred to accepted Story 12.6 evidence.
  - Any product-level decision to include historical migration notes that mention retired routes remains deferred.
  - Shared repo-wide docs guardrails remain out of scope unless a later story accepts that broader test policy.
- Final recommendation: ready-for-dev
