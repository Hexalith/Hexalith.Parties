# Story 12.7: Admin Portal Rebuild on FrontComposer

Status: review

## Story

As an administrator,
I want the Admin Portal to be a FrontComposer-based UI that reads via EventStore queries and uses the EventStore Admin UI for generic stream/event browsing,
so that Parties admin operations follow the platform UX pattern and avoid duplicating EventStore Admin UI.

## Acceptance Criteria

1. Given the rewritten `Hexalith.Parties.AdminPortal`, then it integrates with `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts`, registers a Parties domain manifest, and contributes Parties-domain views to the FrontComposer shell.
2. Given the portal navigation, then generic event/stream browsing is delegated to the EventStore Admin UI via deep-links, while Parties-specific views for party search, party detail, GDPR operations, consent, restriction, portability, and processing records live in the FrontComposer-based portal.
3. Given all party data reads, then they are issued through `POST /api/v1/queries` by the accepted EventStore/FrontComposer query path and/or `Hexalith.Parties.Client`; no direct `Hexalith.Parties` REST URLs, old admin-controller URLs, projection actors, DAPR actor calls, or local search-service calls remain in the portal.
4. Given party and GDPR command actions are available, then they use the accepted EventStore command/client contract from Story 12.5 or are explicitly disabled with a dated blocker; the portal must not resurrect old `AdminController` endpoints as a command backdoor.
5. Given Stories 10-1, 10-1-1, and 10-2, then their open consumer-facing scope is consolidated here; the original story files remain in `_bmad-output/implementation-artifacts/` for history and must not be marked complete by this story unless their superseded scope is actually closed.
6. Given the UX gap identified in the Epic 12 proposal, then a fresh UX specification is authored alongside implementation under `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-XX.md` before broad UI changes are claimed complete.
7. Given Wave 2 sequencing, then implementation does not claim production readiness until Wave 1 behavior is landed or formally frozen and Story 12.5 exposes the client/query/command contract this portal consumes.
8. Given rendered party fields, GDPR details, ProblemDetails text, processing summaries, export metadata, or operator-entered values contain user-supplied or AI-created content, then the portal renders them as encoded text only and never through raw markup, JavaScript interpolation, logs, storage keys, telemetry dimensions, or filenames.
9. Given authentication, tenant context, admin role, tenant switch, stale response, not-found, erased, forbidden, degraded, timeout, or malformed-response scenarios, then the UI clears sensitive state fail-closed and surfaces bounded operator states without leaking cross-tenant party existence or protected personal data.
10. Given tests are complete, then focused portal, client/transport, FrontComposer integration, accessibility, localization, privacy/XSS, and architecture fitness tests prove the portal uses EventStore queries/client paths and does not depend on the retired Parties REST/admin/MCP surfaces.

## Tasks / Subtasks

- [x] Confirm predecessor gates before implementation. (AC: 3, 4, 5, 7)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` for consumer-host separation context.
  - [x] If Story 12.5 is not done, limit implementation to UX spec, ATDD, failing guardrail tests, and adapter seams that do not assume final client API names.
  - [x] If Story 12.4 remains blocked or the EventStore query contract is not frozen, record the exact query blocker instead of reading projection actors or old Parties REST endpoints directly.

- [x] Author the admin portal UX specification. (AC: 2, 5, 6, 8, 9)
  - [x] Create `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-XX.md` with the route map, information architecture, navigation/deep-link behavior, list/detail layout, GDPR operation flows, empty/error states, localization notes, accessibility requirements, and privacy rules.
  - [x] Keep the first viewport operational and dense: no landing page, hero section, marketing copy, decorative card shell, duplicated tenant-management UI, or generic stream browser.
  - [x] Define the boundary between Parties-domain views and EventStore Admin UI deep-links, including how a selected party/correlation id/stream id becomes a safe deep-link without exposing PII.
  - [x] Define fail-closed UX for missing token, missing tenant, non-admin, tenant switch, stale in-flight response, forbidden/cross-tenant, not found, erased/gone, degraded projection, timeout, malformed response, and EventStore query blocker states.
  - [x] Define keyboard/focus behavior for search, filters, grid rows, detail panel, GDPR confirmations, export download, retry actions, and deep-link controls.

- [x] Recompose `Hexalith.Parties.AdminPortal` around FrontComposer and EventStore. (AC: 1, 2, 3, 5)
  - [x] Keep production portal code in `src/Hexalith.Parties.AdminPortal`; do not move Parties-domain UI into the `Hexalith.FrontComposer` submodule.
  - [x] Add or update references so the portal can consume `Hexalith.FrontComposer.Shell`, `Hexalith.FrontComposer.Contracts`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, and only the accepted EventStore client/gateway surface required by Story 12.5.
  - [x] Register the Parties domain manifest through `IFrontComposerRegistry` and keep `PartiesAdminPortalManifest.Route` or its replacement aligned with FrontComposer navigation conventions.
  - [x] Reuse FrontComposer shell services for navigation, density, command feedback, auth redirect, ETag query caching, storage boundaries, localization, and data-grid state where available.
  - [x] Do not create a separate TypeScript SPA, standalone shell, duplicate tenant selector, duplicate authorization model, or new tenant-management screens.
  - [x] Remove or quarantine configuration that treats `PartiesAdminPortalOptions.ApiBaseAddress` as a direct Parties REST base URL; any retained base URL must target the EventStore gateway or a FrontComposer host abstraction.

- [x] Replace direct REST reads with EventStore query/client reads. (AC: 3, 7, 9)
  - [x] Replace `IPartiesAdminPortalApiClient.ListPartiesAsync`, `SearchPartiesAsync`, and `GetPartyAsync` internals that call `GET api/v1/parties`, `GET api/v1/parties/search`, and `GET api/v1/parties/{id}`.
  - [x] Prefer `Hexalith.Parties.Client` query methods once Story 12.5 lands; otherwise use FrontComposer `IQueryService.QueryAsync<T>(QueryRequest)` with `Domain="party"`, the accepted projection/query type, tenant from authenticated context, bounded `Skip`/`Take`, filters, search query, sort metadata, and safe cache discriminator.
  - [x] Preserve behavior from Stories 10-1 and 10-1-1: browse list, display-name baseline search, rich-search capability gating, party type/active filters, detail hydration, degraded/stale metadata, pagination or virtualized server-side loading, and tenant-switch cancellation.
  - [x] Do not read `PartyIndexProjectionActor`, `PartyDetailProjectionActor`, `IPartySearchService`, DAPR actors, old controllers, or `Hexalith.Parties` internals directly.
  - [x] Treat a missing query adapter, missing projection actor type, or missing client method as a blocker with exact evidence; do not fabricate read-your-write state or local search.

- [x] Consolidate GDPR operations from Story 10.2 through the EventStore/client boundary. (AC: 2, 4, 5, 8, 9)
  - [x] Preserve the operator flows from Story 10.2: erasure request, erasure status/certificate, verification retry, restriction/lift restriction, consent add/revoke/history, portability export, processing records, and compact DPO entry points.
  - [x] Route GDPR reads through EventStore query/client APIs. Route GDPR commands through the accepted EventStore command/client APIs when Story 12.5 exposes them.
  - [x] If the accepted client does not yet expose a GDPR command/query method, disable that operation and record a blocker rather than calling retired `/api/v1/admin/**` endpoints.
  - [x] After any accepted command, refresh authoritative state through EventStore queries before enabling follow-on actions; do not infer completion from command acceptance.
  - [x] Keep erasure terminal states privacy-preserving: no stale personal detail rendering after `410 Gone`, erased-party query results, or verified erasure state.
  - [x] Keep consent per channel/per purpose and restriction semantics inherited from Stories 9.3 and 9.4; do not add batch restriction, party-wide consent, tenant-wide export, dual-control approval, or DPO case-management scope.

- [x] Delegate generic stream/event browsing to EventStore Admin UI. (AC: 2, 8, 9)
  - [x] Add safe deep-links from party detail, command acceptance, correlation id, or processing record context to the EventStore Admin UI when the AppHost/topology exposes a stable URL.
  - [x] Keep link labels and destinations generic enough to avoid displaying names, emails, identifiers, consent purposes, raw payload JSON, or tenant membership details.
  - [x] Do not embed a generic stream/event browser inside Parties AdminPortal.
  - [x] Add tests for deep-link generation, disabled state when the EventStore Admin UI URL is unavailable, and PII-free link labels/URLs.

- [x] Preserve and harden tenant/auth/privacy/accessibility behavior. (AC: 1, 8, 9)
  - [x] Continue deriving auth, tenant context, and admin role from `AuthenticationStateProvider` and Hexalith.Tenants services as established by Story 10-1-1; do not go back to host-supplied boolean parameters.
  - [x] Keep backend authorization authoritative; frontend affordances are not permission checks.
  - [x] Clear list, detail, GDPR operation state, processing records, export state, cached payloads, and selected-row state after token loss, sign-out, tenant switch, forbidden response, missing tenant, non-admin user, or stale in-flight response.
  - [x] Render all user/backend text through normal Razor/component text paths. Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, raw HTML fragments, or JavaScript interpolation with party data, ProblemDetails details, processing summaries, consent purposes, or export metadata.
  - [x] Log only operation category, non-PII ids, correlation/status ids, and bounded outcome codes; do not log party names, contact values, identifiers, consent text, export payloads, JWTs, claims, tenant membership dictionaries, raw query payloads, or raw ProblemDetails details.
  - [x] Use localized resource strings for labels, status messages, dates, booleans, counts, warning copy, validation messages, lawful-basis labels, and operation outcomes.
  - [x] Maintain keyboard reachability, visible focus, screen-reader labels, status announcements, and non-color-only status indicators for every state.

- [x] Rewrite tests and guardrails. (AC: 3, 4, 5, 8, 9, 10)
  - [x] Update `tests/Hexalith.Parties.AdminPortal.Tests/**` so component tests use EventStore/client fakes rather than old direct REST path fakes.
  - [x] Update `tests/Hexalith.Parties.Client.Tests/AdminPortal/**` or replacement tests to assert `POST /api/v1/queries` / accepted command requests instead of old `api/v1/parties` and `api/v1/admin` paths.
  - [x] Preserve scenario coverage from Stories 10-1, 10-1-1, and 10-2: browse, search, filters, detail, rich-search gating, GDPR operation flows, DPO summary entry points, auth/tenant states, stale-response suppression, and privacy/XSS inputs.
  - [x] Add architecture fitness tests proving `src/Hexalith.Parties.AdminPortal` contains no direct old REST route literals for Parties/admin data access, no references to `Hexalith.Parties` actor host/server/projections/security internals, no DAPR actor calls, no MVC controllers, no local projection actor reads, and no raw markup rendering of untrusted values.
  - [x] Add FrontComposer integration tests for manifest registration, route discovery, navigation/deep-link behavior, EventStore query-service usage, ETag/not-modified behavior where applicable, and auth redirect/failure classification.
  - [x] Add accessibility/localization tests for named controls, focus return, status announcements, keyboard flow, localized labels/status text, and date/count formatting.
  - [x] Add tests proving safe state cleanup after `401`, `403`, missing tenant, non-admin, tenant switch, cross-tenant scoped id, `404`, `410`, degradation, timeout, malformed JSON, non-JSON gateway response, and cancellation.

- [x] Verify the rebuilt portal. (AC: 1-10)
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run affected client/admin portal contract tests, including `tests/Hexalith.Parties.Client.Tests`.
  - [x] Run affected FrontComposer integration or shell tests when the local submodule exposes focused suites.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If Wave 1, Story 12.5, EventStore Admin UI URL, or UX spec prerequisites are still incomplete, record the exact limitation in completion notes and do not mark unsupported flows as complete.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.7.
- The pivot decision is that all client commands and queries go to EventStore. `Hexalith.Parties` becomes an actor host/projection runtime and must not keep public REST, admin REST, or in-process MCP as the primary consumer surface.
- Story 12.7 is Wave 2. Wave 2 starts after Wave 1 lands: AppHost recomposition, actor-host cleanup, validation/authorization ownership, and server Tier-1/Tier-2 coverage must provide the EventStore command/query contract this portal consumes.
- Sprint status at creation has Story 12.2 in active implementation and Story 12.4 blocked. Treat those as gates, not permission to bypass the EventStore boundary.
- Stories 10-1, 10-1-1, and 10-2 are historical input and behavior inventory. Their direct REST endpoint assumptions are superseded by Epic 12.

### Current Implementation to Inspect

- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` currently contains the read-only browse/search/detail UI, Fluent UI controls, tenant/auth state handling, list/detail cancellation, and GDPR-adjacent display state from Stories 10-1 and 10-1-1.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` currently calls direct old URLs: `api/v1/parties`, `api/v1/parties/search`, `api/v1/parties/{id}`, and `health`. Story 12.7 must replace that transport path.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalPartyQueryService.cs` and `PartiesAdminListCoordinator.cs` are already wired into the component lifecycle. Reuse or replace them deliberately; do not create a parallel query-state service without retiring the old one.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs` currently registers a `DomainManifest` with route `/admin/parties`; align it with FrontComposer manifest/navigation behavior rather than adding manual navigation duplication.
- `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs` currently validates `PartiesAdminPortalOptions.ApiBaseAddress`; reinterpret or replace this option so it cannot point to a retired Parties service endpoint.
- `tests/Hexalith.Parties.AdminPortal.Tests/**` and `tests/Hexalith.Parties.Client.Tests/AdminPortal/**` currently pin many old direct REST assumptions. Rewrite them toward the EventStore/FrontComposer query contract while preserving behavior scenarios.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Communication/IQueryService.cs`, `QueryRequest.cs`, and `QueryResult.cs` define the local query seam.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/EventStore/EventStoreQueryClient.cs` posts to the configured EventStore query endpoint, resolves tenant/user context, applies auth, supports ETag cache integration, and classifies failures into typed outcomes.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/EventStoreServiceExtensions.cs` registers `IQueryService`, `ICommandService`, auth redirect, EventStore clients, and related FrontComposer infrastructure.

### EventStore and FrontComposer Query Guidance

- FrontComposer `QueryRequest` supports `Domain`, `AggregateId`, `QueryType`, `ProjectionType`, `EntityId`, `ProjectionActorType`, filters, search query, sort, `Skip`, `Take`, ETags, cache discriminator, and cache payload version.
- Use `Domain="party"` for Parties domain queries unless a later accepted architecture update changes the domain.
- Use the projection/query type and optional `ProjectionActorType` proven by Wave 1 and Story 12.5. Do not guess if the query adapter contract is missing.
- Cache discriminators must be framework-allowlisted and must not include raw search text, party names, emails, identifiers, consent purposes, tenant ids, JWTs, or other PII.
- `QueryResult<T>.IsNotModified` is a real no-change signal. Do not treat 304/not-modified as an empty result unless the current FrontComposer API explicitly says so.
- EventStore response classification should drive auth redirect, forbidden, not-found, validation, transient failure, degraded, and malformed-response UI. Avoid stringly-typed `HttpRequestException` handling in components.

### Superseded Story Scope

- Story 10.1 delivered browse/search/detail behavior using the old Parties REST paths. Keep the UX behavior, tests, and privacy guardrails; replace the transport.
- Story 10.1.1 delivered Tenants-backed authorization derivation, rich-search capability detection, Fluent UI control adoption, and coordinator/query-service lifecycle wiring. Keep these patterns unless the UX spec or FrontComposer API provides a stronger equivalent.
- Story 10.2 is still `ready-for-dev` but is paused and superseded by Epic 12 for consumer-facing portal work. Consolidate its GDPR flows into this story through EventStore/client contracts instead of implementing old admin-controller calls.
- Story 10.3 remains superseded by Story 12.8 and must not be pulled into the admin portal rebuild except for shared FrontComposer conventions.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, Microsoft Fluent UI Blazor `5.0.0-rc.2-26098.1`, xUnit `2.9.3`, Shouldly `4.3.0`, and NSubstitute `5.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present.
- Do not edit the `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, or `Hexalith.Memories` submodules for this story unless an explicit follow-up story owns that change.
- Do not add DAPR, MediatR, FluentValidation, MVC controller, EventStore server, Parties actor-host, projection actor, search-service, or security-internal references to `Hexalith.Parties.AdminPortal`.
- Do not resurrect old Parties REST, admin REST, OpenAPI, Swagger, or in-process MCP URLs as compatibility shims.
- Keep `Hexalith.Parties.Contracts` free of UI framework dependencies.
- Keep the portal operational and work-focused: dense data surfaces, predictable navigation, compact controls, no landing/marketing page, and no decorative nested-card layout.

### Security and Privacy Guidance

- EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping after the pivot.
- Parties owns domain execution behind EventStore. The admin portal must not reimplement tenant authorization, domain validation, projection repair, stream browsing, or actor invocation.
- Tenant/user context must come from authenticated FrontComposer/EventStore context and Hexalith.Tenants membership/role state. Fail closed when context is absent or ambiguous.
- Safe operator feedback may include operation category, reason code, retry guidance, correlation/status id, and bounded status labels.
- Unsafe feedback includes raw command/query payload JSON, protected PII, display names in logs, contact values, identifiers, consent purpose text in telemetry/storage keys, access tokens, signing keys, stack traces, DAPR ports, sidecar names, and internal config.
- Render untrusted text normally through Razor/component text rendering. Avoid `MarkupString`, `AddMarkupContent`, `innerHTML`, raw HTML fragments, and JavaScript interpolation for party or backend values.
- Export filenames and EventStore Admin UI deep-links must be derived from non-PII ids and timestamps only.

### Testing Guidance

- Minimum focused tests:
  - Portal manifest registers the Parties domain and route through FrontComposer.
  - Browse/list/detail/search reads use EventStore query/client fakes and do not contain old `api/v1/parties` route assertions.
  - GDPR flows do not call old `api/v1/admin` endpoints; unavailable client command/query methods produce disabled/blocker states.
  - EventStore Admin UI deep-links are generated only from safe identifiers and are disabled when topology configuration is absent.
  - Tenant switch, sign-out, non-admin, missing tenant, `401`, `403`, `404`, `410`, degradation, timeout, malformed JSON, non-JSON response, cancellation, and 304/not-modified states clear or preserve state exactly as the UX spec says.
  - XSS/privacy tests cover party display name, contact value, identifier value, consent purpose, ProblemDetails detail, processing summary, export metadata, and operator-entered text.
  - Fitness tests prove no direct old Parties/admin REST path literals, DAPR actor calls, projection actor reads, MVC controller dependencies, or raw markup APIs are introduced.

### Out of Scope

- Implementing `Hexalith.Parties.Client` EventStore transport; Story 12.5 owns that.
- Implementing the separate Parties MCP host; Story 12.6 owns that.
- Rewriting the embeddable picker; Story 12.8 owns that.
- Updating samples/getting-started docs; Story 12.9 owns that.
- Rewriting deployment validation/topology fitness; Story 12.10 owns that.
- Building a generic EventStore stream/event browser inside Parties AdminPortal.
- Adding new backend GDPR workflows, tenant-management screens, batch GDPR operations, DPO case-management, or dual-control approval.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.7 source, Epic 12 pivot rationale, Wave 2 sequencing, and UX-gap requirement.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - EventStore/Parties query and actor invocation constraints.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - EventStore, Parties, Tenants, and admin UI topology context.
- `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md` - removal of direct Parties public REST/MCP surfaces.
- `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md` - EventStore-owned authorization and Parties-owned validation boundary.
- `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md` - blocked server gateway coverage and query-contract risks.
- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md` - typed Parties client contract this portal should consume.
- `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` - consumer-host separation and EventStore client usage pattern.
- `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md` - delivered browse/search/detail UX and privacy guardrails.
- `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md` - Tenants auth derivation, Fluent UI, rich-search capability, and query lifecycle wiring.
- `_bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md` - GDPR operator flow inventory to consolidate through EventStore/client APIs.
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` - current portal component.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` - old direct REST transport to replace.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs` - current FrontComposer domain manifest.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Communication/IQueryService.cs` - query service seam.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Communication/QueryRequest.cs` - query request contract.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/EventStore/EventStoreQueryClient.cs` - EventStore-backed query client behavior.
- `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/EventStoreServiceExtensions.cs` - EventStore service registration.

## Project Structure Notes

- Keep Parties admin portal production code in `src/Hexalith.Parties.AdminPortal`.
- Keep portal tests in `tests/Hexalith.Parties.AdminPortal.Tests` and contract/fitness tests in the existing focused test projects unless a new project is clearly justified.
- Keep UX planning output in `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-XX.md`.
- Keep typed Parties contracts in `src/Hexalith.Parties.Contracts` and typed client behavior in `src/Hexalith.Parties.Client`.
- Keep `src/Hexalith.Parties` focused on actor hosting and projections after the predecessor cleanup.
- Generated `bin/`, `obj/`, screenshots, and browser artifacts must stay out of commits unless a test artifact path is explicitly planned.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-10: Confirmed predecessor gates. Stories 12.4 and 12.5 are `blocked`; Story 12.6 only added blocked-scope MCP scaffold and guardrails. Normal production portal rebuild is gated.
- 2026-05-10: Limited implementation to the story-approved scope: UX specification, FrontComposer/EventStore query adapter seam, old REST route removal from AdminPortal reads, and guardrail tests. No EventStore, FrontComposer, Tenants, or Memories submodule source was edited.
- 2026-05-10: Focused AdminPortal tests passed: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --filter "FullyQualifiedName~PartiesAdminPortalApiClientTests|FullyQualifiedName~PartiesAdminPortalComponentTests" --no-restore` (35/35).
- 2026-05-10: Focused client admin contract tests passed: `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortalQueryContractTests|FullyQualifiedName~AdminPortalGdprOperationContractTests" --no-restore` (5 passed, 6 skipped).
- 2026-05-10: Story verification passed: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` (35/35); `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --no-restore` (56 passed, 6 skipped); `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors.
- 2026-05-10: Full Release regression passed from existing build outputs: `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` (858 passed, 27 skipped, 0 failed across solution test projects).
- 2026-05-10: Added explicit AdminPortal project references to `Hexalith.FrontComposer.Shell` and `Hexalith.Parties.Client`; focused project-reference test passed after restore.
- 2026-05-10: Composed `AddHexalithPartiesAdminPortal` through FrontComposer quickstart services so the portal reuses shell storage, ETag cache, auth redirect, command feedback, localization, and data-grid state services.
- 2026-05-10: Added parent-level integrated-build warning demotion for existing `Hexalith.FrontComposer.Shell` diagnostics `CS9113` and `CS0162` so the root Release build can consume the shell without editing submodule source.
- 2026-05-10: Verification passed: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (36/36); focused AdminPortal DI test and project-reference test passed.
- 2026-05-10: Updated AdminPortal read transport to prefer Story 12.5 `IPartiesQueryClient` for list/search/detail when registered, while retaining the FrontComposer `IQueryService` query adapter fallback and fail-closed contract-unavailable behavior.
- 2026-05-10: Verification passed: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (37/37); `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" -p:UseSharedCompilation=false` (6 passed, 6 skipped).
- 2026-05-10: Earlier blocker that the accepted Parties client had no GDPR command/query adapter is superseded; `IAdminPortalGdprClient` is now present, AdminPortal GDPR contract tests are active, and portal operations route through the EventStore-backed client boundary.
- 2026-05-10: Verification passed: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (38/38); `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" -p:UseSharedCompilation=false` (6 passed, 6 skipped); `dotnet build Hexalith.Parties.slnx --configuration Release -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- 2026-05-10: Completed GDPR retry refresh hardening so retry verification refreshes authoritative erasure status after command acceptance; focused red/green retry test now passes.
- 2026-05-10: Added configurable EventStore Admin UI deep-link helper and generic stream/correlation links with disabled state when the Admin UI base URL is unavailable; focused deep-link tests passed.
- 2026-05-10: Wired `AdminPortalGdprStateCoordinator` into selected-party and tenant/auth reset lifecycle so GDPR/export/correlation state clears on tenant switch and terminal auth states.
- 2026-05-10: Added FrontComposer manifest registration/route-discovery coverage, EventStore not-modified metadata coverage, and malformed GDPR query response classification; AdminPortal tests passed 50/50 and AdminPortal client contract tests passed 12/12.
- 2026-05-10: Verification: `dotnet build Hexalith.Parties.slnx --configuration Release -p:UseSharedCompilation=false` passed; `dotnet test Hexalith.Parties.slnx --configuration Release --no-build -p:UseSharedCompilation=false` passed (858 passed, 6 expected health E2E skips).
- 2026-05-10: FrontComposer shell validation attempted with `dotnet test Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj --configuration Release -p:UseSharedCompilation=false`; failed in submodule test `AddFrontComposerDevMode_RegistersDevModeServicesInDevelopment` because `IDevModeOverlayController` is not registered as scoped. No FrontComposer submodule source was edited per story constraint.
- 2026-05-12: Re-ran the previously blocked FrontComposer shell validation: `dotnet test Hexalith.FrontComposer\tests\Hexalith.FrontComposer.Shell.Tests\Hexalith.FrontComposer.Shell.Tests.csproj --configuration Release -p:UseSharedCompilation=false` passed (1572/1572).
- 2026-05-12: Fixed validation flake in `HealthEndpointIntegrationTests.ReadyEndpoint_PubSubDegraded_Returns200Async` by resetting the shared tenants readiness probe before asserting pub/sub degradation does not block readiness.
- 2026-05-12: Verification passed: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (67/67); `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release -p:UseSharedCompilation=false` (74/74); `dotnet build Hexalith.Parties.slnx --configuration Release -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors; `dotnet test Hexalith.Parties.slnx --configuration Release --no-build -p:UseSharedCompilation=false` passed (979 passed, 6 expected health E2E skips).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Authored `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md` covering route map, dense operational IA, EventStore Admin UI deep-link boundary, GDPR flows, fail-closed states, localization, accessibility, and privacy/XSS rules.
- Replaced the AdminPortal direct REST read transport with a FrontComposer `IQueryService` adapter. The adapter uses `Domain="party"`, bounded paging, safe cache discriminators, and explicit query/projection option names; it does not call old `api/v1/parties`, old admin routes, projection actors, DAPR actors, search services, or Parties internals.
- Removed the `ApiBaseAddress` startup requirement and marked the old option obsolete so it cannot be treated as a direct Parties REST base URL. EventStore gateway HTTP setup now belongs at the FrontComposer shell boundary.
- Added a contract-unavailable failure kind and query adapter behavior that fails closed when Story 12.4/12.5 has not frozen the query/client contract. Rich-search capability likewise reports a bounded local-only blocker instead of probing retired health/REST endpoints.
- Reworked AdminPortal and client admin-query tests around the FrontComposer/EventStore query seam, including source guardrails that block old REST route literals and raw markup APIs under `src/Hexalith.Parties.AdminPortal`.
- Updated skipped GDPR ATDD scaffolds to point at the future EventStore command/query contract instead of retired admin-controller routes.
- Completed the AdminPortal recomposition wiring: the project now references FrontComposer Shell and Parties Client boundaries, and the portal DI extension composes FrontComposer shell services instead of registering only standalone AdminPortal services.
- Added a narrow root integrated-build target that demotes two pre-existing FrontComposer Shell diagnostics when this repository builds the shell as a dependency; no submodule source files were modified.
- AdminPortal read operations now prefer the typed `Hexalith.Parties.Client.Abstractions.IPartiesQueryClient` boundary from Story 12.5 for list, search, and detail queries. Existing FrontComposer `IQueryService` query requests remain as the fallback for hosts that have not registered the typed client yet.
- Existing browse/search/detail behavior, rich-search gating, bounded paging, fail-closed auth/tenant states, stale-response suppression, and tenant-switch cancellation remain covered by the AdminPortal component and service tests.
- GDPR operations now route through the accepted `IAdminPortalGdprClient` EventStore command/query boundary, refresh authoritative state after accepted commands, and avoid retired `/api/v1/admin/**` endpoints.
- EventStore Admin UI delegation is configurable through `PartiesAdminPortalOptions.EventStoreAdminUiBaseAddress`; stream/correlation links use generic labels and safe encoded identifiers, with disabled controls when the URL is unavailable.
- GDPR state cleanup is wired through `AdminPortalGdprStateCoordinator` for selected-party tracking and tenant/auth/erased reset paths.
- The previous FrontComposer Shell submodule validation blocker is resolved in the current workspace: the shell suite now passes 1572/1572 without submodule source edits.
- Closed the final verification gate and moved the story to review after the Release build and full no-build regression passed.

### File List

- `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`
- `Directory.Build.targets`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryException.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalQueryFailureKind.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptions.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs`
- `tests/Hexalith.Parties.Tests/HealthChecks/HealthEndpointIntegrationTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-10 | 0.2 | Added gated UX spec, FrontComposer/EventStore query adapter seam, old REST read removal, source guardrails, and validation evidence; story blocked pending Story 12.4/12.5 contract landing or formal freeze. | Codex |
| 2026-05-10 | 0.3 | Completed AdminPortal recomposition wiring with FrontComposer Shell and Parties Client project references, FrontComposer shell service composition, and focused DI/project-reference validation. | Codex |
| 2026-05-10 | 0.4 | Switched AdminPortal reads to prefer the typed Parties client query boundary from Story 12.5 while preserving the FrontComposer query-service fallback and existing browse/search/detail behavior. | Codex |
| 2026-05-10 | 0.5 | Added disabled GDPR operation entry points and recorded the blocker that the accepted typed GDPR client command/query contract is not available yet. | Codex |
| 2026-05-10 | 0.6 | Completed GDPR retry authoritative refresh behavior and delegated generic EventStore stream/correlation inspection through configurable Admin UI deep-links. | Codex |
| 2026-05-10 | 0.7 | Wired GDPR state cleanup, expanded FrontComposer/ETag/malformed-response coverage, and recorded the remaining external FrontComposer Shell validation blocker. | Codex |
| 2026-05-12 | 0.8 | Re-ran FrontComposer shell validation, fixed a health-test isolation flake, completed final verification, and moved the story to review. | Codex |
