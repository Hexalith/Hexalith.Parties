# Story 7.1: Compose Admin Console Working View

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want the admin portal to open directly to a working party management console,
so that I can search, browse, and inspect parties without passing through a landing page.

## Acceptance Criteria

1. Given an authorized administrator navigates to `/admin/parties`, when the Parties admin surface loads, then the first viewport shows the working console with search, compact filters, tenant/auth state, result grid, and detail region, and no marketing, landing, hero, or introductory page is shown.
2. Given the admin console layout renders, when list and detail regions are displayed, then they are sibling working regions rather than nested card shells, and the layout remains dense, scannable, and suitable for repeated administration work.
3. Given route support is available in FrontComposer, when a party row is selected, then the detail state can deep-link to `/admin/parties/{partyId}`, and the route uses only the non-PII party id.
4. Given GDPR route support is available in FrontComposer, when an administrator opens party GDPR operations, then the route can use `/admin/parties/{partyId}/gdpr`, and no names, emails, identifiers, consent text, free text, or raw errors are placed in the URL.
5. Given the admin console is rendered across supported viewport sizes, when the layout is inspected, then the toolbar, results, detail region, and status region remain usable without text overlap, and primary workflows are visible or reachable without an explanatory feature page.

## Tasks / Subtasks

- [x] Reconcile the current AdminPortal surface with the Story 7.1 first-viewport contract. (AC: 1, 2, 5)
  - [x] Inspect `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` and preserve the existing `@page "/admin/parties"` working console entry point.
  - [x] Verify the first viewport contains the toolbar, auth/tenant/status region, result grid, and detail region without an introductory page, hero, marketing panel, or decorative shell.
  - [x] Keep list and detail as sibling working regions under the existing `hx-parties-admin__layout`; do not wrap the full page in nested cards.
  - [x] Add or adjust scoped CSS only if needed for responsive usability and no-overlap behavior; keep the admin UI dense and operational.

- [x] Confirm FrontComposer route and manifest integration. (AC: 1, 3, 4)
  - [x] Keep `PartiesAdminPortalManifest.Route` aligned with `/admin/parties`.
  - [x] Preserve `RegisterHexalithPartiesAdminPortal` domain registration through `IFrontComposerRegistry`.
  - [x] If FrontComposer route support for `/admin/parties/{partyId}` exists locally, wire selected-row state to that route using only the non-PII party id.
  - [x] If `/admin/parties/{partyId}/gdpr` route support is not available, record the exact blocker and keep GDPR navigation in bounded in-page state without leaking PII into URLs.

- [x] Preserve accepted query/client boundaries while composing the view. (AC: 1, 3, 4)
  - [x] Continue reading list/search/detail through `IPartiesQueryClient` when registered, with `IQueryService` as the FrontComposer fallback in `PartiesAdminPortalApiClient`.
  - [x] Do not call retired `api/v1/parties`, `api/v1/admin/**`, DAPR actors, projection actors, local search services, or `src/Hexalith.Parties` actor-host internals from the portal.
  - [x] Keep bounded page size and cache discriminator behavior; never include tenant ids, display names, search text, emails, identifiers, consent text, JWTs, claims, or raw ProblemDetails details in storage keys or telemetry dimensions.

- [x] Preserve fail-closed tenant/auth and stale-response behavior. (AC: 1, 5)
  - [x] Keep `AuthenticationStateProvider` plus `Hexalith.Tenants` membership/role derivation through `IAdminPortalAuthorizationService`.
  - [x] Preserve cancellation/version guards in list and detail loading so tenant switch, sign-out, missing tenant, non-admin, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable states clear sensitive state before rendering.
  - [x] Keep `AdminPortalPartyQueryService`, `PartiesAdminListCoordinator`, and `AdminPortalGdprStateCoordinator` wired into reset paths; do not create parallel state services unless the old ones are retired.

- [x] Keep privacy-safe EventStore Admin UI delegation. (AC: 3, 4)
  - [x] Continue generating EventStore Admin UI links through `AdminPortalEventStoreAdminLinks`.
  - [x] Links may use safe identifiers such as aggregate id, stream id, command id, correlation id, or timestamp.
  - [x] Link labels and URLs must not include names, emails, identifiers, consent purposes, free text, raw payloads, raw errors, tenant membership details, or tokens.
  - [x] When `EventStoreAdminUiBaseAddress` is unavailable, render a disabled bounded state rather than embedding a generic stream browser.

- [x] Add or update focused tests. (AC: 1-5)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to assert first-viewport operational content, no hero/landing/marketing text, sibling list/detail regions, no nested card shell, and no text-overlap-prone empty layout.
  - [x] Add route/manifest tests in `PartiesAdminPortalServiceCollectionTests` if route support changes.
  - [x] Preserve existing bUnit coverage for Fluent UI controls, auth/tenant fail-closed states, rich-search gating, XSS encoding, EventStore links, localization, keyboard reachability, coordinator transitions, and tenant-switch cancellation.
  - [x] Extend source guardrails in `tests/Hexalith.Parties.Client.Tests/AdminPortal/**` only if transport or route behavior changes.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` if query/client route behavior changes.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.1 to `7-1-compose-admin-console-working-view`; this supersedes the older historical file `7-1-event-publishing-verification-and-configuration.md`, which belongs to a prior Epic 7 plan and must not be used as the current story source.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 7: Administration Console`]
- Story 7.1 covers FR65 and UX-DR1, UX-DR2, UX-DR21. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.1: Compose Admin Console Working View`]

### Architecture Guardrails

- The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It must register Parties-domain views with the FrontComposer shell, read through EventStore query/client abstractions, route supported commands through the typed Parties client/EventStore command boundary, and delegate generic event/stream browsing to EventStore Admin UI safe deep-links. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- Labels, dates, counts, status messages, validation messages, and operation outcomes must be localized. Focus management, keyboard access, non-color-only state, and polite status announcements are part of the frontend architecture contract. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The dependency record says Epic 7 scheduling is gated by an accepted EventStore-fronted Parties client/gateway contract unless the risk is explicitly accepted. The current repo already contains typed `IPartiesQueryClient`, `IAdminPortalGdprClient`, and `IQueryService` paths from later work; dev must preserve those accepted boundaries instead of reintroducing old REST/admin endpoints. [Source: `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`]

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`

- Current state: renders `@page "/admin/parties"` as a working console with header/status, Fluent UI search input, party type and active filters, search/clear/retry buttons, search capability buttons, `FluentDataGrid`, pagination controls, and a sibling detail region.
- Story change: reconcile this existing component with the new Story 7.1 acceptance criteria, especially first-viewport composition, route/deep-link behavior, and responsive no-overlap layout.
- Preserve: encoded Razor text rendering, Fluent UI controls, cancellation/version guards, tenant/auth reset behavior, rich-search capability gating, status announcements, keyboard handling, and `NormalizeDetail` null-collection protection.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`

- Current state: exposes `PartiesAdminPortalManifest.Route = "/admin/parties"` and a `DomainManifest` for the Parties bounded context.
- Story change: keep route registration aligned with FrontComposer route discovery; add detail/GDPR route support only if FrontComposer local APIs support it.
- Preserve: no duplicate navigation shell and no standalone SPA routing root.

`src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`

- Current state: composes FrontComposer quickstart services, registers options validation, `IPartiesAdminPortalApiClient`, list/GDPR coordinators, EventStore links, query service wrapper, and authorization service.
- Story change: preserve this composition and avoid local-only portal infrastructure that bypasses FrontComposer.
- Preserve: `ValidateOnStart`, FrontComposer service registration, scoped state services, and domain registration through `IFrontComposerRegistry`.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`

- Current state: prefers `IPartiesQueryClient` for list/search/detail, falls back to FrontComposer `IQueryService`, maps failures to bounded `AdminPortalQueryFailureKind`, and routes GDPR operations through `IAdminPortalGdprClient`.
- Story change: no direct transport changes are required unless routing/deep-link work exposes a gap.
- Preserve: typed-client preference, FrontComposer fallback, fail-closed contract-unavailable behavior, bounded skip/page-size logic, rich-search probe parsing, and no retired REST/admin endpoint calls.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: covers dense row rendering, Fluent controls, auth/tenant fail-closed states, Tenants role derivation, search/filter behavior, rich-search capability, tenant switch, detail hydration, XSS encoding, EventStore links, localization, keyboard reachability, non-color-only status badges, coordinator transitions, and query-service cancellation.
- Story change: add assertions for this story's first-viewport and layout-specific rules if not already covered.
- Preserve: scenario breadth; do not weaken existing privacy, accessibility, or tenant-isolation tests.

### Project Structure Notes

- Production portal code belongs in `src/Hexalith.Parties.AdminPortal`.
- Admin portal component/service tests belong in `tests/Hexalith.Parties.AdminPortal.Tests`.
- Admin portal client/transport guardrails belong in `tests/Hexalith.Parties.Client.Tests/AdminPortal`.
- Keep Parties actor-host code in `src/Hexalith.Parties` free of public REST, admin UI, Swagger/OpenAPI, and in-process MCP hosting.
- Do not edit root-level submodules (`Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.Memories`) unless a follow-up explicitly owns that cross-repo change.
- Do not initialize or update nested submodules recursively.

### Technology and Library Requirements

- Runtime: .NET SDK `10.0.300` from `global.json`, target framework `net10.0`.
- Package versions are centrally managed in `Directory.Packages.props`; do not add versions to individual `.csproj` files.
- Relevant current versions: Microsoft Fluent UI Blazor `5.0.0-rc.2-26098.1`, xUnit v3 `3.2.2`, bUnit `2.7.2`, Shouldly `4.3.0`, Dapr `1.17.9`, Aspire `13.3.3`.
- Use existing Fluent UI Blazor components already present in the portal (`FluentTextInput`, `FluentSelect`, `FluentButton`, `FluentDataGrid`) rather than raw input/select/table controls.
- Web research was intentionally not performed for this story because the user explicitly prohibited browsing or web searches.

### Previous Story Intelligence

- Story 10.1 established the read-only admin browse/search/detail foundation and recorded that the portal must not add tenant lifecycle, tenant management, or GDPR mutation scope into the browse story. Preserve that boundary unless Story 7.1 route composition requires only navigation changes. [Source: `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`]
- Story 10.1.1 completed Tenants-backed authorization derivation, rich-search capability detection, Fluent UI migration, and coordinator/query-service lifecycle wiring. Do not regress to host-supplied auth booleans, raw HTML controls, or dead-code state services. [Source: `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md`]
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links. Preserve typed `IPartiesQueryClient` preference, `IQueryService` fallback, `IAdminPortalGdprClient` operations, safe link generation, and the existing UX specification. [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]
- `ux-admin-portal-2026-05-10.md` is the most concrete UX source for dense first-viewport IA, route map, deep-link boundaries, fail-closed states, localization, accessibility, and privacy rules. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`]

### Anti-Patterns to Avoid

- Do not create a landing page, hero, marketing copy, decorative page shell, or feature explainer before the working console.
- Do not build a TypeScript SPA, standalone admin shell, duplicate tenant selector, duplicate authorization model, or generic stream/event browser inside Parties.
- Do not call retired direct Parties REST/admin endpoints, projection actors, DAPR actors, local search services, or `src/Hexalith.Parties` actor-host internals from the portal.
- Do not render party data, ProblemDetails text, consent text, processing summaries, or operator-entered values through `MarkupString`, `AddMarkupContent`, `innerHTML`, JavaScript interpolation, or raw HTML fragments.
- Do not put names, emails, identifiers, consent text, free text, raw errors, tenant ids, tokens, or claim values in URLs, storage keys, telemetry dimensions, filenames, logs, or EventStore Admin UI link labels.
- Do not weaken existing bUnit, privacy, accessibility, localization, tenant-switch, or transport guardrail tests to make layout work easier.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.1: Compose Admin Console Working View`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- [Source: `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`]
- [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md`]
- [Source: `_bmad-output/implementation-artifacts/10-1-1-admin-portal-frontcomposer-and-tenants-integration.md`]
- [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`]
- [Source: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`]

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for current Epic 7 Story 7.1 and reconciled it with existing AdminPortal implementation history. | Codex |
| 2026-05-22 | 0.2 | Implemented first-viewport console verification and privacy-safe detail/GDPR route support; solution build validation remains blocked by local restore/SDK/access errors. | Codex |
| 2026-05-22 | 0.3 | Completed full solution build validation and moved story to review. | Codex |
| 2026-05-22 | 0.4 | Senior Developer Review (AI) auto-fixed route hardening gaps and added GDPR/unsafe-id route coverage; tests 75/75 green. | Claude Opus 4.7 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Read `.agents/skills/bmad-create-story/SKILL.md`, `discover-inputs.md`, `template.md`, and `checklist.md`.
- Resolved workflow customization: no activation prepend/append steps, persistent fact `_bmad-output/project-context.md`, no `on_complete` instruction.
- Loaded current sprint status and confirmed current story key is `7-1-compose-admin-console-working-view`.
- Confirmed `7-1-event-publishing-verification-and-configuration.md` is a historical artifact from an older Epic 7 plan and not the current story.
- Loaded current Epic 7 story source, architecture D20 frontend decision, dependency gate, UX spec, existing AdminPortal implementation, successor stories 10.1/10.1.1/12.7, project context, and recent commit titles.
- Skipped web research per explicit user instruction.
- Resolved `bmad-dev-story` workflow customization: no activation prepend/append steps, persistent fact `_bmad-output/project-context.md`, no `on_complete` instruction.
- Loaded current sprint status and confirmed the active story key is `7-1-compose-admin-console-working-view`; ignored historical `7-1-event-publishing-verification-and-configuration.md`.
- Red check: focused AdminPortal component tests failed to compile because `PartiesAdminPortal.RoutePartyId` did not exist yet.
- Green check: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartiesAdminPortalComponentTests"` passed with 42/42 tests.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed with 72/72 tests.
- Validation: `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` passed with 18/18 tests.
- Earlier blocked validation: `dotnet build Hexalith.Parties.slnx --configuration Release` and `--no-restore` both failed before completing due missing `Aspire.AppHost.Sdk/13.3.3`, NuGet repository-signature SSL/auth failures, and access denied writes under `obj`.
- Recovery validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-1-compose-admin-console-working-view`.
- Checklist review applied: story includes specific acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, tests, validation commands, source references, and the historical 7.1 artifact warning.
- Preserved the existing `/admin/parties` working console entry point and added tests pinning first-viewport toolbar, auth/status region, result grid, detail region, sibling list/detail layout, no landing/hero/marketing shell, and empty-state reachability.
- Added route support for `/admin/parties/{partyId}` and `/admin/parties/{partyId}/gdpr`; selected rows navigate only when the id is a bounded non-PII route token, while tenant-scoped ids remain in in-page detail state and are not written to the URL.
- Added manifest constants for base, detail, and GDPR route templates while preserving `RegisterHexalithPartiesAdminPortal` domain registration through `IFrontComposerRegistry`.
- Preserved query/client boundaries, fail-closed authorization/reset behavior, coordinator wiring, and privacy-safe EventStore Admin UI delegation; no scoped CSS change was required.
- Validation complete: AdminPortal tests, AdminPortal client guardrail tests, and full solution build pass.

### File List

- `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`

## Senior Developer Review (AI)

Reviewer: Claude Opus 4.7 on 2026-05-22

### Findings

Adversarial review applied against AC1–AC5, the Task/Subtask completion claims, and the git-vs-story File List. Findings were auto-fixed in this pass.

- HIGH — No test coverage for the new `@page "/admin/parties/{RoutePartyId}/gdpr"` route variant. The task line "Confirm FrontComposer route and manifest integration" was marked [x] but no assertion proved the GDPR URL resolved to a working detail with the GDPR operations panel rendered. Fix: added `PartiesAdminPortal_GdprRouteVariant_LoadsPartyDetailWithGdprPanelVisible`.
- HIGH — No test that proved an unsafe `RoutePartyId` (e.g., `tenant-a:parties:hostile`) coming from a pasted URL is rejected without firing a detail fetch or echoing the unsafe id into the detail region. Fix: added `PartiesAdminPortal_UnsafeRoutePartyId_RejectsDetailFetchAndDoesNotLeakIdentifier`.
- MEDIUM — `NavigateToPartyDetailRoute` compared `target` against `NavigationManager.ToBaseRelativePath(NavigationManager.Uri)`, which includes `?query`/`#fragment`. Any query string would have forced a redundant navigation and reset state. Fix: strip query/fragment before path comparison in `PartiesAdminPortal.razor`.
- MEDIUM — `IsSafeRoutePartyId` accepted `.` and `..` as full-segment values because the dot character is in the unreserved set. While Blazor would treat them as literals, the values are confusing in logs and have no legitimate use as party ids. Fix: explicitly reject `"."` and `".."` and added `PartiesAdminPortal_PathTraversalRoutePartyId_IsRejected`.
- LOW (follow-up, not auto-fixed) — The `/admin/parties/{partyId}/gdpr` route is satisfied by URL pattern alone (AC4); the route does not yet anchor focus or otherwise differentiate the GDPR panel from the detail route. AC4 strictly only requires the URL pattern and no PII, which is met.

### Validation

- `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` → 75/75 passed.

### Outcome

Approve — moved to `done`. No CRITICAL findings; all HIGH and MEDIUM findings auto-fixed and covered by tests.
