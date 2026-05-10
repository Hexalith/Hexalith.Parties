# Story 12.7: Admin Portal Rebuild on FrontComposer

Status: ready-for-dev

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

- [ ] Confirm predecessor gates before implementation. (AC: 3, 4, 5, 7)
  - [ ] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [ ] Read `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` for consumer-host separation context.
  - [ ] If Story 12.5 is not done, limit implementation to UX spec, ATDD, failing guardrail tests, and adapter seams that do not assume final client API names.
  - [ ] If Story 12.4 remains blocked or the EventStore query contract is not frozen, record the exact query blocker instead of reading projection actors or old Parties REST endpoints directly.

- [ ] Author the admin portal UX specification. (AC: 2, 5, 6, 8, 9)
  - [ ] Create `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-XX.md` with the route map, information architecture, navigation/deep-link behavior, list/detail layout, GDPR operation flows, empty/error states, localization notes, accessibility requirements, and privacy rules.
  - [ ] Keep the first viewport operational and dense: no landing page, hero section, marketing copy, decorative card shell, duplicated tenant-management UI, or generic stream browser.
  - [ ] Define the boundary between Parties-domain views and EventStore Admin UI deep-links, including how a selected party/correlation id/stream id becomes a safe deep-link without exposing PII.
  - [ ] Define fail-closed UX for missing token, missing tenant, non-admin, tenant switch, stale in-flight response, forbidden/cross-tenant, not found, erased/gone, degraded projection, timeout, malformed response, and EventStore query blocker states.
  - [ ] Define keyboard/focus behavior for search, filters, grid rows, detail panel, GDPR confirmations, export download, retry actions, and deep-link controls.

- [ ] Recompose `Hexalith.Parties.AdminPortal` around FrontComposer and EventStore. (AC: 1, 2, 3, 5)
  - [ ] Keep production portal code in `src/Hexalith.Parties.AdminPortal`; do not move Parties-domain UI into the `Hexalith.FrontComposer` submodule.
  - [ ] Add or update references so the portal can consume `Hexalith.FrontComposer.Shell`, `Hexalith.FrontComposer.Contracts`, `Hexalith.Parties.Contracts`, `Hexalith.Parties.Client`, and only the accepted EventStore client/gateway surface required by Story 12.5.
  - [ ] Register the Parties domain manifest through `IFrontComposerRegistry` and keep `PartiesAdminPortalManifest.Route` or its replacement aligned with FrontComposer navigation conventions.
  - [ ] Reuse FrontComposer shell services for navigation, density, command feedback, auth redirect, ETag query caching, storage boundaries, localization, and data-grid state where available.
  - [ ] Do not create a separate TypeScript SPA, standalone shell, duplicate tenant selector, duplicate authorization model, or new tenant-management screens.
  - [ ] Remove or quarantine configuration that treats `PartiesAdminPortalOptions.ApiBaseAddress` as a direct Parties REST base URL; any retained base URL must target the EventStore gateway or a FrontComposer host abstraction.

- [ ] Replace direct REST reads with EventStore query/client reads. (AC: 3, 7, 9)
  - [ ] Replace `IPartiesAdminPortalApiClient.ListPartiesAsync`, `SearchPartiesAsync`, and `GetPartyAsync` internals that call `GET api/v1/parties`, `GET api/v1/parties/search`, and `GET api/v1/parties/{id}`.
  - [ ] Prefer `Hexalith.Parties.Client` query methods once Story 12.5 lands; otherwise use FrontComposer `IQueryService.QueryAsync<T>(QueryRequest)` with `Domain="party"`, the accepted projection/query type, tenant from authenticated context, bounded `Skip`/`Take`, filters, search query, sort metadata, and safe cache discriminator.
  - [ ] Preserve behavior from Stories 10-1 and 10-1-1: browse list, display-name baseline search, rich-search capability gating, party type/active filters, detail hydration, degraded/stale metadata, pagination or virtualized server-side loading, and tenant-switch cancellation.
  - [ ] Do not read `PartyIndexProjectionActor`, `PartyDetailProjectionActor`, `IPartySearchService`, DAPR actors, old controllers, or `Hexalith.Parties` internals directly.
  - [ ] Treat a missing query adapter, missing projection actor type, or missing client method as a blocker with exact evidence; do not fabricate read-your-write state or local search.

- [ ] Consolidate GDPR operations from Story 10.2 through the EventStore/client boundary. (AC: 2, 4, 5, 8, 9)
  - [ ] Preserve the operator flows from Story 10.2: erasure request, erasure status/certificate, verification retry, restriction/lift restriction, consent add/revoke/history, portability export, processing records, and compact DPO entry points.
  - [ ] Route GDPR reads through EventStore query/client APIs. Route GDPR commands through the accepted EventStore command/client APIs when Story 12.5 exposes them.
  - [ ] If the accepted client does not yet expose a GDPR command/query method, disable that operation and record a blocker rather than calling retired `/api/v1/admin/**` endpoints.
  - [ ] After any accepted command, refresh authoritative state through EventStore queries before enabling follow-on actions; do not infer completion from command acceptance.
  - [ ] Keep erasure terminal states privacy-preserving: no stale personal detail rendering after `410 Gone`, erased-party query results, or verified erasure state.
  - [ ] Keep consent per channel/per purpose and restriction semantics inherited from Stories 9.3 and 9.4; do not add batch restriction, party-wide consent, tenant-wide export, dual-control approval, or DPO case-management scope.

- [ ] Delegate generic stream/event browsing to EventStore Admin UI. (AC: 2, 8, 9)
  - [ ] Add safe deep-links from party detail, command acceptance, correlation id, or processing record context to the EventStore Admin UI when the AppHost/topology exposes a stable URL.
  - [ ] Keep link labels and destinations generic enough to avoid displaying names, emails, identifiers, consent purposes, raw payload JSON, or tenant membership details.
  - [ ] Do not embed a generic stream/event browser inside Parties AdminPortal.
  - [ ] Add tests for deep-link generation, disabled state when the EventStore Admin UI URL is unavailable, and PII-free link labels/URLs.

- [ ] Preserve and harden tenant/auth/privacy/accessibility behavior. (AC: 1, 8, 9)
  - [ ] Continue deriving auth, tenant context, and admin role from `AuthenticationStateProvider` and Hexalith.Tenants services as established by Story 10-1-1; do not go back to host-supplied boolean parameters.
  - [ ] Keep backend authorization authoritative; frontend affordances are not permission checks.
  - [ ] Clear list, detail, GDPR operation state, processing records, export state, cached payloads, and selected-row state after token loss, sign-out, tenant switch, forbidden response, missing tenant, non-admin user, or stale in-flight response.
  - [ ] Render all user/backend text through normal Razor/component text paths. Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, raw HTML fragments, or JavaScript interpolation with party data, ProblemDetails details, processing summaries, consent purposes, or export metadata.
  - [ ] Log only operation category, non-PII ids, correlation/status ids, and bounded outcome codes; do not log party names, contact values, identifiers, consent text, export payloads, JWTs, claims, tenant membership dictionaries, raw query payloads, or raw ProblemDetails details.
  - [ ] Use localized resource strings for labels, status messages, dates, booleans, counts, warning copy, validation messages, lawful-basis labels, and operation outcomes.
  - [ ] Maintain keyboard reachability, visible focus, screen-reader labels, status announcements, and non-color-only status indicators for every state.

- [ ] Rewrite tests and guardrails. (AC: 3, 4, 5, 8, 9, 10)
  - [ ] Update `tests/Hexalith.Parties.AdminPortal.Tests/**` so component tests use EventStore/client fakes rather than old direct REST path fakes.
  - [ ] Update `tests/Hexalith.Parties.Client.Tests/AdminPortal/**` or replacement tests to assert `POST /api/v1/queries` / accepted command requests instead of old `api/v1/parties` and `api/v1/admin` paths.
  - [ ] Preserve scenario coverage from Stories 10-1, 10-1-1, and 10-2: browse, search, filters, detail, rich-search gating, GDPR operation flows, DPO summary entry points, auth/tenant states, stale-response suppression, and privacy/XSS inputs.
  - [ ] Add architecture fitness tests proving `src/Hexalith.Parties.AdminPortal` contains no direct old REST route literals for Parties/admin data access, no references to `Hexalith.Parties` actor host/server/projections/security internals, no DAPR actor calls, no MVC controllers, no local projection actor reads, and no raw markup rendering of untrusted values.
  - [ ] Add FrontComposer integration tests for manifest registration, route discovery, navigation/deep-link behavior, EventStore query-service usage, ETag/not-modified behavior where applicable, and auth redirect/failure classification.
  - [ ] Add accessibility/localization tests for named controls, focus return, status announcements, keyboard flow, localized labels/status text, and date/count formatting.
  - [ ] Add tests proving safe state cleanup after `401`, `403`, missing tenant, non-admin, tenant switch, cross-tenant scoped id, `404`, `410`, degradation, timeout, malformed JSON, non-JSON gateway response, and cancellation.

- [ ] Verify the rebuilt portal. (AC: 1-10)
  - [ ] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [ ] Run affected client/admin portal contract tests, including `tests/Hexalith.Parties.Client.Tests`.
  - [ ] Run affected FrontComposer integration or shell tests when the local submodule exposes focused suites.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [ ] If Wave 1, Story 12.5, EventStore Admin UI URL, or UX spec prerequisites are still incomplete, record the exact limitation in completion notes and do not mark unsupported flows as complete.

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
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
