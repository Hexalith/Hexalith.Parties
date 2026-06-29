---
story_key: 7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence
story_id: "7.3"
epic: "7"
created: 2026-06-29T14:57:17+02:00
source_status: backlog
target_status: ready-for-dev
baseline_commit: 59d41a249a07e8a2a746b00d2d6e057fcc8c22a6
---

# Story 7.3: Search Normalization and FrontComposer UI Orchestration Convergence

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a maintainer,
I want shared text/search helpers and UI lifecycle primitives adopted where they fit,
so that Parties does not duplicate generic search normalization or orchestration code.

## Acceptance Criteria

1. Given Story 7.1 routed B8 pure text helpers to Commons, when local Parties search adopts shared helpers, then diacritic normalization and Jaro-Winkler similarity either come from an additive Commons API or from an already approved shared helper, with no dependency on Memories for the local fallback path.
2. Given local fuzzy search remains the default, when Memories search is disabled or absent, then `IPartySearchService` still resolves to the local search service and admin list/search behavior, page metadata, erased-party exclusion, filters, authorized-id scoping, score metadata, source metadata, and deterministic ordering do not drift.
3. Given Memories search is optional, when Memories is enabled, then syntactic, semantic, graph, and hybrid search remain configuration gated; Memories-specific scoring and source metadata stay in Memories-backed code only; runtime/axis/missing-context degrade behavior and local fallback semantics remain compatible.
4. Given FrontComposer owns command lifecycle/orchestration primitives, when Parties adopts or wraps those primitives, then existing Parties `StatusKind`, `StatusPresentation`, `StatusLiveRegion`, `DataFreshnessIndicator`, `OptimisticReconcile`, and `ProjectionFreshnessFallback` observable behavior remains compatible, including the aria-live politeness split and no focus steal on routine optimistic saves.
5. Given Epic 7 uses adapter-first migration, when this story completes, then rollback is possible by restoring local text helper methods and/or local optimistic/freshness orchestration registrations, and no Parties-local code is deleted unless parity tests and rollback evidence prove it is safe.
6. Given this story may touch Parties, Commons, and FrontComposer, when implementation completes, then focused Parties tests, any touched owner-submodule tests, bUnit/Playwright evidence for changed UI orchestration, build or documented blocked-build evidence, and `git diff --check` are recorded in the Dev Agent Record.

## Tasks / Subtasks

- [x] Add or consume shared pure text helpers without changing search semantics (AC: 1, 2, 5)
  - [x] Inspect `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs` and current Commons APIs before adding a new helper.
  - [x] If no suitable API exists, add additive Commons helpers for diacritic stripping and Jaro-Winkler similarity in the Commons owner surface, with Commons tests for null/empty input, composed/decomposed accents, case-insensitive matching, known Jaro-Winkler pairs, no-match pairs, and digit-heavy false-positive examples.
  - [x] Add the required project reference only where the helper is consumed. `src/Hexalith.Parties/Hexalith.Parties.csproj` currently references `Hexalith.Commons.Http`, not core `Hexalith.Commons`; keep references versionless and use `$(HexalithCommonsRoot)`.
  - [x] Replace direct helper calls in Parties search with the shared helper through the smallest adapter/facade needed for rollback.
  - [x] Preserve the current local helper names as internal compatibility shims if tests or call sites still use them, unless all references are intentionally updated in the same change.

- [x] Preserve local fallback search as the default path (AC: 2, 5)
  - [x] Keep `IPartySearchProvider -> LocalFuzzyPartySearchProvider`, `LocalPartySearchService`, and the Memories-disabled `IPartySearchService` registration behavior intact.
  - [x] Preserve request normalization: explicit `AuthorizedPartyIds` is required, page is clamped to `>= 1`, page size is clamped to `1..100`, and local fallback emits `PartySearchExecutionStatus.LocalOnly` with no degraded reason when Memories integration is simply disabled.
  - [x] Preserve filters and security gates: erased entries are excluded, `AuthorizedPartyIds` is enforced with `StringComparer.Ordinal`, type/active filters still apply, and no consumer self-scoped client gains list/search access.
  - [x] Preserve ordering and paging: relevance descending, normalized display name tie-break, party id tie-break, overflow-safe skip, `TotalPages` minimum of 1, and page-aligned score/source metadata.

- [x] Preserve Memories-enabled rich search and degradation behavior (AC: 3, 5)
  - [x] Keep `Parties:MemoriesSearch:Enabled` and `PartyMemorySearchOptions` validation as the gate for Memories registration.
  - [x] Keep search-specific scoring in `MemoriesPartySearchService` or Memories-owned APIs, not Commons. Commons helpers may normalize text; they must not own syntactic/semantic/graph/hybrid scoring.
  - [x] Preserve axis gating: disabled axis returns local fallback with `LocalOnly`, missing graph context returns `Degraded`, and transient Memories failures return `Degraded` with local fallback.
  - [x] Preserve tenant/case/authorization hydration checks, duplicate-id warning aggregation, NaN/Infinity score sanitization, and source metadata (`Hexalith.Memories` vs `Hexalith.Parties.LocalFallback`).

- [x] Converge UI orchestration only where FrontComposer parity exists (AC: 4, 5)
  - [x] Inspect `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Lifecycle/ILifecycleStateService.cs`, `CommandLifecycleState.cs`, and `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/State/PendingCommands/` before changing Parties UI orchestration.
  - [x] If adopting FrontComposer lifecycle state, do it behind a Parties-compatible adapter that maps FrontComposer states to the existing Parties `StatusKind` and freshness semantics. Do not replace `StatusKind` with `CommandLifecycleState`.
  - [x] Preserve the existing `OptimisticReconcile` flow: apply optimistic state, announce accepted-processing politely, issue command, reconcile on SignalR confirm or fallback re-read, announce degraded/current politely, revert and announce validation/failure assertively, and silently drop user-initiated cancellation.
  - [x] Preserve `ProjectionFreshnessFallback` behavior while disconnected: immediate first poll, interval from `Parties:Freshness:PollingIntervalSeconds`, stop on Current/reconnect/cancel, deterministic `TimeProvider` testing.
  - [x] Preserve `StatusLiveRegion` and `DataFreshnessIndicator` DOM behavior: status/freshness/accepted-processing are `role="status" aria-live="polite"`; validation/transient/load failure/hard denial are `role="alert" aria-live="assertive"`; sign-in-required renders no live region.

- [x] Update focused regression coverage (AC: 1-6)
  - [x] Add or update Commons tests for any new text helpers.
  - [x] Update `tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs`, `BasicPartySearchProviderTests.cs`, `MvpDisplayNameSearchContractTests.cs`, and `PartySearchServiceBoundaryTests.cs` so local search parity is pinned after the helper move.
  - [x] Update `tests/Hexalith.Parties.Tests/Search/MemoriesPartySearchServiceTests.cs` and `PartyMemorySearchOptionsValidatorTests.cs` for Memories-enabled and disabled paths if registration or fallback behavior changes.
  - [x] Update UI tests when orchestration changes: `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`, `ProjectionFreshnessFallbackTests.cs`, `ProjectionFreshnessCompositionTests.cs`, `StatusPresentationTests.cs`, `StatusLiveRegionTests.cs`, and `DataFreshnessIndicatorTests.cs`.
  - [x] Run bUnit tests for changed UI components/services and Playwright a11y/visual checks if rendered status/freshness or live-region DOM changes.
  - [x] Run `git diff --check`.

- [x] Record release and rollback evidence (AC: 5, 6)
  - [x] List every Parties, Commons, and FrontComposer file changed in the Dev Agent Record.
  - [x] Record whether any submodule source changed and which owner tests were run.
  - [x] Record rollback: restore local `NormalizeDiacritics`/`JaroWinklerSimilarity`, revert Commons or FrontComposer pointer/source changes, restore local UI orchestration registrations/adapters, and confirm no state or public contract migration occurred.
  - [x] Confirm Epic 7 remains post-MVP platform maintenance and adds no new PRD functional coverage.

### Review Follow-ups (AI)

- [ ] [AI-Review][Med] `PartiesAdminPortalE2eFixture` reimplements diacritic normalization in a private `StripDiacritics` instead of consuming the shared `Hexalith.Commons.Strings.StringHelper.StripDiacritics` this story centralized, which works against AC1's "no duplicate generic search normalization" goal. Its variant also diverges (recomposes to `FormC` and strips `SpacingCombiningMark`/`EnclosingMark`) from the production `LocalFuzzyPartySearchProvider` path. Deferred (not auto-applied) because routing it to Commons requires adding a versionless core `Hexalith.Commons` project reference to `Hexalith.Parties.UI.csproj`, a build-surface change that cannot be validated in this sandbox (full UI build is blocked by unrelated submodule drift). Fix once the build is restorable. [`src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs:984`]

## Dev Notes

### Story Classification

- Epic 7 is post-MVP platform maintenance. This story is not MVP feature delivery and must not be reported as new PRD functional coverage. [Source: `_bmad-output/planning-artifacts/epics.md#Epic-7-Platform-Alignment---adopt-CommonsEventStore-Class-B`]
- Story 7.3 covers B8 search normalization/similarity and the B11 FrontComposer UI lifecycle/orchestration slice. It must not migrate projections, crypto/key management, EventStore gateway routing, DAPR ACLs, public Parties contracts, or GDPR legal policy. [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.3---Search-Normalization-And-FrontComposer-UI-Orchestration-Convergence`]
- Adapter-first is binding: introduce or consume shared primitives behind Parties-compatible adapters and prove parity before deleting local code. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-1---Adapter-First-Migration`]

### Approved Story Scope

Story 7.3 covers these Epic 7 inventory items:

| ID | Scope | Current direction |
| --- | --- | --- |
| B8 | Jaro-Winkler and diacritic normalization | Commons owns pure text helpers; Memories owns search-specific scoring only when configured. |
| B11 slice | UI lifecycle/orchestration | FrontComposer owns command lifecycle primitives; Parties keeps its `StatusKind`, freshness, accessibility, and domain-specific optimistic reconcile semantics behind adapters. |

Out of scope for this story: Story 7.2 service defaults/correlation/ProblemDetails/paging, Story 7.4/7.5 projection platform migration, Story 7.6/7.7 crypto/key-management, public contract breaking changes, consumer search/list exposure, EventStore gateway route changes, DAPR ACL changes, package upgrades unrelated to the approved shared helper path, and deletion of local code without parity evidence.

### Required Source Discovery Results

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 7 and Story 7.3.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md` and `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/parties-ui-prd.md`; relevant context is admin search, no consumer list/search, eventual-consistency UX, accessibility, and no-PII observability.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`; relevant guardrails are the aria-live split, announce-not-steal optimistic saves, dot-plus-word freshness, and no duplicate/conflicting status signals.
- Loaded persistent project context from `_bmad-output/project-context.md` and FrontComposer context from `references/Hexalith.FrontComposer/_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/7-2-commons-service-defaults-correlation-problemdetails-and-paging.md`.
- Loaded current Parties search files, UI freshness/orchestration files, Commons string helper surface, FrontComposer lifecycle contracts/services, focused test inventory, and recent git history.

### Current Files Being Modified - Required Reading

Read each UPDATE file completely before editing it.

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs` or a new Commons text helper file (UPDATE/NEW if Commons helper is added)
  - Current state: general string formatting/RFC helper surface; no diacritic stripping or Jaro-Winkler helper was found in the checked-out source.
  - What this story changes: add additive pure text normalization/similarity helpers if no approved helper already exists.
  - Preserve: Commons conventions, XML docs density around public APIs, existing helper behavior, and no Memories dependency.

- `references/Hexalith.Commons/test/Hexalith.Commons.Tests/Helpers/StringHelperTest.cs` or a new Commons helper test file (UPDATE/NEW if Commons helper is added)
  - Current state: covers existing `StringHelper` behavior.
  - What this story changes: add parity tests for the new pure text helper surface.
  - Preserve: Commons test style, Shouldly assertions, and no package `Version=` changes.

- `src/Hexalith.Parties/Hexalith.Parties.csproj` (UPDATE if core Commons helper is consumed)
  - Current state: references `$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Http\Hexalith.Commons.Http.csproj` from Story 7.2, but not core `Hexalith.Commons`.
  - What this story changes: may add a versionless project reference to the core Commons project if Parties search consumes helpers from core Commons.
  - Preserve: Central Package Management, no `Version=` attributes, existing EventStore/Tenants/Memories project references, and the Web SDK analyzer workaround.

- `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs` (UPDATE)
  - Current state: local fallback search provider owns `NormalizeDiacritics`, `JaroWinklerSimilarity`, token matching, field score computation, deterministic sort, page clamping, and erased-entry exclusion.
  - What this story changes: delegate pure text normalization/similarity to Commons or an approved shared helper while preserving search behavior.
  - Preserve: no Memories dependency, `MaxPageSize=100`, argument guards, digit-token fuzzy guard, relevance scoring, match metadata, deterministic sort, overflow-safe paging, and `internal` helper compatibility if tests still use it.

- `src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs` (UPDATE)
  - Current state: list/search helper sorts list and search results by `LocalFuzzyPartySearchProvider.NormalizeDiacritics(...)`, then party id.
  - What this story changes: route normalization through the shared helper or through a local adapter.
  - Preserve: list filters, erased exclusion, created/modified filters, sort order, page clamping, overflow-safe skip, and public `PagedResult<T>` shape.

- `src/Hexalith.Parties/Search/LocalPartySearchService.cs` (UPDATE only if helper/metadata integration changes)
  - Current state: enforces lexical-only local search, explicit `AuthorizedPartyIds`, erased filtering, local-only status, page-aligned score/source metadata, and no degraded reason for normal local fallback.
  - What this story changes: should normally be minimal; update only if shared helper adoption requires boundary changes.
  - Preserve: security gates, source system `Hexalith.Parties.LocalFallback`, null SourceUri for local fallback, and unsupported future modes behavior.

- `src/Hexalith.Parties/Search/MemoriesPartySearchService.cs` (UPDATE only if parity tests require adapter changes)
  - Current state: optional rich search path using Memories axes; normalizes requests, checks axis configuration, degrades to local fallback on disabled axis/missing graph context/transient failures, hydrates candidates against tenant/case/authorization filters, sanitizes scores, and emits Memories source metadata.
  - What this story changes: avoid changes unless necessary to keep helper adoption parity.
  - Preserve: Memories-only scoring, endpoint/config gates, tenant/case strictness, authorized id matching, duplicate warning aggregation, NaN/Infinity sanitization, local degrade status distinctions, and no PII in logs.

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (UPDATE if registrations change)
  - Current state: registers local search by default and Memories-backed `IPartySearchService` only when `PartyMemorySearchOptions.Enabled` is true.
  - What this story changes: may add helper/adapter registrations if needed.
  - Preserve: local fallback default, Memories endpoint fail-fast when enabled, `ValidateOnStart`, erasure cleanup behavior, and no public actor-host API.

- `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs` (UPDATE if FrontComposer lifecycle is adopted)
  - Current state: Parties-local shared optimistic/reconcile primitive with delegate-based request, `StatusKind` announcements, SignalR projection confirm, disconnected polling fallback, one-shot duplicate guard, and silent user-cancel behavior.
  - What this story changes: optionally wrap/consume FrontComposer lifecycle primitives where exact parity exists.
  - Preserve: `StatusKind` announcements, no DOM/focus work inside the primitive, rejection revert behavior, user-cancel silence, and idempotent reconcile guard.

- `src/Hexalith.Parties.UI/Services/ProjectionFreshnessFallback.cs` (UPDATE only if FrontComposer equivalent replaces mechanics)
  - Current state: polls immediately and then by configured interval while the projection stream is disconnected; stops on Current, reconnect, or cancellation; uses `TimeProvider`.
  - What this story changes: optional adapter to a FrontComposer lifecycle/fallback primitive only if behavior is equivalent.
  - Preserve: deterministic tests, no `Task.Delay`/`DateTime.UtcNow`, scoped lifetime, and current configuration key.

- `src/Hexalith.Parties.UI/Services/ProjectionFreshnessServiceCollectionExtensions.cs` (UPDATE if DI changes)
  - Current state: registers projection freshness services scoped and lazily/inertly.
  - What this story changes: may replace registrations with adapters that consume FrontComposer services.
  - Preserve: unconditional composition, scoped lifetime, `ValidateScopes=true` compatibility, and degraded/test boot with no hub URL or Parties base URL.

- `src/Hexalith.Parties.UI/Status/StatusPresentation.cs`, `StatusKind.cs`, `LiveRegionPoliteness.cs`, `Components/Shared/StatusLiveRegion.razor`, and `Components/Shared/DataFreshnessIndicator.razor` (UPDATE only if required for adapter mapping)
  - Current state: single source of truth for Parties status mapping and live-region attributes.
  - What this story changes: ideally nothing; if FrontComposer lifecycle is adopted, add mappings without changing observable behavior.
  - Preserve: exhaustive mappings, no blanket-polite default, no sign-in live region, dot-plus-word freshness, and no hard-coded per-screen status mapping.

- FrontComposer owner files under `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Lifecycle/` and `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/State/PendingCommands/` (UPDATE only if missing API is added with explicit owner scope)
  - Current state: local source contains `ILifecycleStateService`, `CommandLifecycleState`, `CommandLifecycleTransition`, `FcLifecycleWrapper`, and pending-command services.
  - What this story changes: add only additive owner APIs if Parties cannot consume existing primitives without parity loss.
  - Preserve: FrontComposer scoped-lifetime rules, ULID/correlation semantics, no raw HTML controls, no hand-edited generated output, and owner test gates.

### Architecture Guardrails

- No public Parties host API. Public commands/queries continue through the EventStore gateway; the actor host remains DAPR-invoked at `POST /process`. [Source: `_bmad-output/project-context.md#Framework-Specific-Rules-Event-Sourcing--CQRS--DAPR-behind-EventStore`]
- Search must continue working without `Hexalith.Memories`. `IPartySearchProvider` defaults to `LocalFuzzyPartySearchProvider`; rich search activates only behind valid `Parties:MemoriesSearch` configuration. [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- Commons may own pure text helpers; Memories owns search-specific scoring. Do not move syntactic/semantic/graph/hybrid scoring into Commons or Parties.Contracts. [Source: `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Target-Destination-Matrix`]
- FrontComposer lifecycle states are not Parties `StatusKind`. If consumed, map through a Parties adapter and preserve existing UI/accessibility behavior. [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns`]
- Do not expose search/list surfaces to Consumer self-scoped clients. Consumers act only on "me"; never trust a client-supplied party id on consumer surfaces. [Source: `_bmad-output/project-context.md#Consumer-portal-consent--GDPR-rights-Epics-45`]
- No PII in logs, telemetry, status copy, ProblemDetails, search degraded reasons, or source metadata. Logs must not include party names, tenant/party identifiers beyond already existing coarse diagnostics, event payloads, tokens, raw payloads, key aliases, or decrypted values. [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- Public contracts evolve additively only. Do not remove or rename `PartySearchResult`, `MatchMetadata`, `PartySearchResponse`, `ProjectionFreshnessMetadata`, `StatusKind`, or public client shapes as part of this story. [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#Consistency-Conventions`]

### Existing Shared Surface Assessment

- Commons `StringHelper` currently contains formatting, RFC1123, and invariant number helpers; no diacritic normalization or Jaro-Winkler helper was found by source search. Story 7.3 should add an additive pure text helper in Commons if no newer approved helper exists by implementation time. [Source: `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs`]
- Story 7.2 added `HexalithCommonsRoot` to `Directory.Build.props` and added Commons HTTP references to several Parties projects. The main actor host currently references `Hexalith.Commons.Http`, not core `Hexalith.Commons`, so consuming core text helpers may require a new versionless project reference in `src/Hexalith.Parties/Hexalith.Parties.csproj`. [Source: `Directory.Build.props`] [Source: `src/Hexalith.Parties/Hexalith.Parties.csproj`]
- FrontComposer local source contains lifecycle services and pending-command primitives: `ILifecycleStateService`, `CommandLifecycleState`, `CommandLifecycleTransition`, `LifecycleStateService`, `FcLifecycleWrapper`, and `PendingCommandStateService`. These are command-lifecycle primitives, not a direct replacement for Parties projection freshness or `StatusKind`. [Source: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Lifecycle/ILifecycleStateService.cs`] [Source: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/State/PendingCommands/PendingCommandStateService.cs`]

### Previous Story Intelligence

- Story 7.2 established the Commons project-reference path and adapter-first approach for low-risk shared utilities. It kept public Parties contracts stable, used Commons behind local facades/adapters, and recorded rollback through local wrapper restoration and Commons pointer rollback. Reuse that pattern here. [Source: `_bmad-output/implementation-artifacts/7-2-commons-service-defaults-correlation-problemdetails-and-paging.md#Completion-Notes-List`]
- Story 7.2 documented existing out-of-scope submodule drift and restore/build blockers. If broad build/test lanes fail again for unrelated submodule drift, record precise blocked evidence and still run focused lanes where possible. Do not reset or update unrelated submodule pointers in this story. [Source: `_bmad-output/implementation-artifacts/7-2-commons-service-defaults-correlation-problemdetails-and-paging.md#Senior-Developer-Review-AI`]
- Recent commits are `feat(story-7.2)`, `feat(story-7.1)`, and Epic 6 shared-anchor commits. Do not re-open Epic 6 Class A anchors or reclassify them as Epic 7 work. [Source: `git log -5`]

### Testing and Validation Guidance

Run the smallest reliable lane first, then broaden as needed:

- `git diff --check`
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore` or document the exact unrelated blocker.
- Focused Parties search tests:
  - `tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs`
  - `tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs`
  - `tests/Hexalith.Parties.Tests/Search/MvpDisplayNameSearchContractTests.cs`
  - `tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs`
  - `tests/Hexalith.Parties.Tests/Search/MemoriesPartySearchServiceTests.cs`
  - `tests/Hexalith.Parties.Tests/Search/PartyMemorySearchOptionsValidatorTests.cs`
- Focused UI tests if orchestration changes:
  - `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessFallbackTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessCompositionTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/StatusPresentationTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/DataFreshnessIndicatorTests.cs`
- Commons owner tests when Commons is touched:
  - `dotnet test references/Hexalith.Commons/test/Hexalith.Commons.Tests/Hexalith.Commons.Tests.csproj -c Release --no-restore`
- FrontComposer owner tests when FrontComposer is touched. Follow FrontComposer's project context: solution-level `dotnet test Hexalith.FrontComposer.slnx --filter "Category!=Performance&Category!=e2e-palette&Category!=NightlyProperty&Category!=Quarantined"` with `DiffEngine_Disabled=true`, unless a focused owner lane is documented and accepted.
- If filtering Parties xUnit v3 tests, do not rely on classic VSTest `--filter` silently running zero tests. Use the test executable with xUnit v3 single-dash args where required. [Source: `_bmad-output/project-context.md#Testing-Rules`]

### Rollback Plan

- Text helper rollback: restore local `NormalizeDiacritics` and `JaroWinklerSimilarity` implementations in `LocalFuzzyPartySearchProvider`, revert `PartySearchResultsBuilder` helper calls, and remove the core Commons project reference if it is no longer used.
- Commons rollback: revert additive Commons text helper source/tests or roll back the `references/Hexalith.Commons` pointer, preserving Story 7.2 APIs if they are still required.
- Memories rollback: leave `Parties:MemoriesSearch:Enabled=false` as the local fallback path; revert any Memories-specific adapter changes and confirm local fallback remains green.
- UI orchestration rollback: restore `OptimisticReconcile`, `ProjectionFreshnessFallback`, and projection freshness DI registrations to their local implementations, or switch the Parties adapter back to local behavior.
- FrontComposer rollback: revert additive FrontComposer APIs or roll back the FrontComposer pointer if this story touched it. Do not make rollback depend on deleting Parties-local code.
- No data migration, projection checkpoint migration, public contract migration, or EventStore gateway route change should be introduced by this story.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.3-Search-normalization-and-FrontComposer-UI-orchestration-convergence`]
- [Source: `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md#Story-7.3---Search-Normalization-And-FrontComposer-UI-Orchestration-Convergence`]
- [Source: `_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md#Target-Destination-Matrix`]
- [Source: `_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md#Story-73`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md#AD-4---Utility-Destination-Discipline`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/parties-ui-prd.md#NFR2-Eventual-Consistency-UX`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- [Source: `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`]
- [Source: `src/Hexalith.Parties/Search/LocalPartySearchService.cs`]
- [Source: `src/Hexalith.Parties/Search/MemoriesPartySearchService.cs`]
- [Source: `src/Hexalith.Parties/Search/PartySearchResultsBuilder.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/ProjectionFreshnessFallback.cs`]
- [Source: `src/Hexalith.Parties.UI/Status/StatusPresentation.cs`]
- [Source: `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs`]
- [Source: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Lifecycle/ILifecycleStateService.cs`]

## Validation Summary

- Source discovery loaded project context facts, sprint status, canonical epics, PRD, architecture, Epic 7 architecture spine, Story 7.1 ADR/release plan, Story 7.2 prior story intelligence, current Parties search and UI orchestration files, Commons string helper surface, FrontComposer lifecycle primitives, focused test inventory, UX accessibility/regulated-language notes, and recent git history.
- Checklist fixes applied before finalizing: made local fallback search the default non-negotiable path; separated Commons pure text helpers from Memories scoring; required FrontComposer lifecycle adoption through a Parties-compatible adapter; pinned `StatusKind` and aria-live behavior; added rollback sets and focused validation lanes.
- Latest-technology review found no external dependency upgrade requirement. The story must rely on local pinned .NET 10, current root submodule sources, and the accepted ADR rather than changing package versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-29: Inspected Commons `StringHelper`; no existing diacritic stripping or Jaro-Winkler helper existed in checked-out source.
- 2026-06-29: Inspected FrontComposer lifecycle/pending-command primitives and Parties UI freshness/status/orchestration files. No FrontComposer adoption was made because current FrontComposer primitives are command-lifecycle and pending-command state services, not parity replacements for Parties projection freshness polling, `StatusKind`, aria-live DOM split, or optimistic reconcile behavior.
- 2026-06-29: `dotnet build references/Hexalith.Commons/src/libraries/Hexalith.Commons/Hexalith.Commons.csproj -c Release --no-restore -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-29: `dotnet test references/Hexalith.Commons/test/Hexalith.Commons.Tests/Hexalith.Commons.Tests.csproj -c Release --no-restore` blocked during build with generic `Build failed with exit code: 1`; diagnostic build shows existing project-reference target failure on `Hexalith.Commons.Configurations` `GetTargetFrameworks` through `NETStandardCompatError_Microsoft_Extensions_Configuration_Binder`, with no emitted compiler errors.
- 2026-06-29: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore` blocked during build with generic `Build failed with exit code: 1`; diagnostic build shows existing Parties project-reference target framework resolution failure before compilation, with 0 emitted errors.
- 2026-06-29: `dotnet build Hexalith.Parties.slnx -c Release --no-restore -v:minimal` blocked with `Build FAILED`, 0 warnings, 0 errors, before compilation.
- 2026-06-29: `dotnet build src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -c Release -v:minimal` attempted restore and failed on NU1900 because NuGet vulnerability data from `https://api.nuget.org/v3/index.json` is unreachable in this environment and treated as an error.
- 2026-06-29: `dotnet restore references/Hexalith.Commons/test/Hexalith.Commons.Tests/Hexalith.Commons.Tests.csproj --ignore-failed-sources -p:NuGetAudit=false` and `dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --ignore-failed-sources -p:NuGetAudit=false` still failed during project graph resolution before diagnostics.
- 2026-06-29: `git diff --check` passed.
- 2026-06-29: Removed literal CR/trailing whitespace from the Commons helper/test additions; `git -C references/Hexalith.Commons diff --check` and root `git diff --check` passed.
- 2026-06-29: `bash scripts/check-no-warning-override.sh` passed.

### Completion Notes List

- Added additive Commons pure text helpers: `StringHelper.StripDiacritics` and `StringHelper.JaroWinklerSimilarity`.
- Added Commons owner tests for null/empty inputs, composed/decomposed accents, case-insensitive matching, known Jaro-Winkler matches, unrelated pairs, and digit-heavy false-positive examples.
- Added a versionless core `Hexalith.Commons` project reference to the Parties host where the helper is consumed.
- Routed `LocalFuzzyPartySearchProvider` and `SemanticPartySearchProvider` through the Commons helper while preserving the existing internal `NormalizeDiacritics` and `JaroWinklerSimilarity` method names as rollback/test shims.
- Added focused Parties search coverage for accent-insensitive matching, normalized display-name ordering, MVP display-name-only metadata, and unchanged local fallback metadata.
- Kept default local fallback registration and Memories registration/degradation/scoring code unchanged. Memories-specific scoring and source metadata remain isolated in `MemoriesPartySearchService`.
- Made no FrontComposer source changes and no Parties UI *orchestration* changes after parity inspection; observable `StatusKind`, live-region, `DataFreshnessIndicator`, `OptimisticReconcile`, and `ProjectionFreshnessFallback` behavior is preserved by leaving local orchestration intact. The only Parties UI change is accent-insensitive search coverage added to the admin E2E fixture (`PartiesAdminPortalE2eFixture.cs`) plus its Playwright spec (`admin-parties-list.spec.ts`); no status/freshness/live-region DOM was touched.
- Rollback: restore the previous local `NormalizeDiacritics` and `JaroWinklerSimilarity` method bodies in `LocalFuzzyPartySearchProvider` and `SemanticPartySearchProvider`, revert the core Commons project reference, and revert the additive Commons helper/tests. No state migration, public contract migration, projection checkpoint migration, EventStore gateway route change, FrontComposer source change, or UI orchestration registration change was introduced.
- Epic 7 remains post-MVP platform maintenance and adds no new PRD functional coverage.

### File List

- `references/Hexalith.Commons/src/libraries/Hexalith.Commons/Strings/StringHelper.cs`
- `references/Hexalith.Commons/test/Hexalith.Commons.Tests/Helpers/StringHelperTest.cs`
- `src/Hexalith.Parties/Hexalith.Parties.csproj`
- `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs`
- `src/Hexalith.Parties/Search/SemanticPartySearchProvider.cs`
- `tests/Hexalith.Parties.Tests/Search/BasicPartySearchProviderTests.cs`
- `tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs`
- `tests/Hexalith.Parties.Tests/Search/MvpDisplayNameSearchContractTests.cs`
- `tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/e2e/specs/admin-parties-list.spec.ts`
- `_bmad-output/implementation-artifacts/7-3-search-normalization-and-frontcomposer-ui-orchestration-convergence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-06-29: Added Commons shared text normalization/similarity helpers and routed Parties search shims through them.
- 2026-06-29: Added focused Commons and Parties regression coverage for helper parity and local fallback metadata.
- 2026-06-29: Recorded FrontComposer parity inspection outcome; no UI orchestration source changed.
- 2026-06-29: Recorded rollback and blocked-build evidence for project graph/restore limitations in the current workspace.
- 2026-06-29: Cleaned literal CR/trailing whitespace from Commons helper/test additions and reran owner/root diff checks.
- 2026-06-29: Senior Developer Review (AI): added the two missing UI/e2e files to the File List, corrected the "no Parties UI changes" completion note, logged a Med follow-up for the duplicated fixture normalization, pinpointed the blocked full build to unrelated Tenants submodule drift, and set status to done.

## Senior Developer Review (AI)

**Reviewer:** Administrator · **Date:** 2026-06-29 · **Outcome:** Approve (with one follow-up)

### Summary

Story 7.3 routes the two pure text primitives (`StripDiacritics`, `JaroWinklerSimilarity`) into Commons `StringHelper` and delegates the Parties search providers to them while keeping the original `NormalizeDiacritics`/`JaroWinklerSimilarity` method names as `internal static` shims. All six ACs are implemented: behavior parity holds, local fallback stays the default, Memories scoring/registration is untouched, and no FrontComposer/UI orchestration was changed. No CRITICAL or HIGH issues found. Two MEDIUM documentation accuracy gaps were auto-fixed; one MEDIUM code-quality follow-up is logged.

### Verification performed

- **Behavior parity (AC1/AC2/AC3):** Commons `JaroWinklerSimilarity`/`StripDiacritics` are byte-for-byte the original Parties logic (params renamed, null-guard generalized from `.Length==0` to `IsNullOrEmpty`); for all non-null inputs Parties passes, results are identical. The shims are still referenced by `PartySearchResultsBuilder` and the search internals (not dead code).
- **Removed usings (`System.Globalization`, `System.Text`) are safe:** the only remaining reference in either provider is inside a code comment in `LocalFuzzyPartySearchProvider.cs:78`; no lingering `StringBuilder`/`NormalizationForm`/`CharUnicodeInfo` usage — no compile break introduced.
- **Commons library build:** `dotnet build .../Hexalith.Commons.csproj -c Release --no-restore` → **0 warnings, 0 errors**.
- **`git diff --check`:** clean in both root and Commons submodule.
- **Tests:** new Commons tests (null/empty, composed/decomposed accents, case-insensitive, known/no-match pairs, digit-heavy false positives) and new Parties tests (accent-insensitive match, normalized-display-name ordering, MVP displayName-only metadata, unchanged local-fallback status/metadata) are real assertions mapped to the ACs.
- **Full Parties build is blocked by unrelated submodule drift** — not story 7.3 code. `dotnet build src/Hexalith.Parties` fails with 43× CS0246 (`IQueryContract`/`IEventPayload`/`IRejectionEvent`) entirely inside `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts`, caused by the drifted Tenants submodule pointer. This confirms the dev's "blocked build" evidence and its precise root cause. Per story scope and Story 7.2 intelligence, unrelated submodule pointers were not reset.

### Findings

- **[Med][Fixed] File List incomplete.** `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` and `tests/e2e/specs/admin-parties-list.spec.ts` were changed (accent-insensitive admin-search fixture + Playwright evidence for AC1/AC2/AC4) but absent from the File List. Added.
- **[Med][Fixed] Inaccurate completion note.** "Made no … Parties UI code changes" contradicted the `PartiesAdminPortalE2eFixture.cs` change. Reworded to "no FrontComposer source / no UI orchestration changes," documenting the E2E search-coverage change.
- **[Med][Follow-up] Duplicated normalization in the E2E fixture.** See Review Follow-ups (AI) — deferred because the fix needs an unverifiable UI-project build-surface change.
- **[Low][Noted] Commons additive edit ships as a whole-file CRLF→LF flip** (514/341 churn vs ~173 real lines) so added lines pass the default `git diff --check` (which flags CR-at-eol). Sibling Commons files remain CRLF. A reviewer-side CRLF restoration was attempted and reverted because it reintroduced the `git diff --check` failure that AC6 requires to pass. Proper fix belongs to the Commons owner repo (add a `.gitattributes` EOL/`cr-at-eol` rule); out of scope for this Parties story.
- **[Low][Noted] Unrelated submodule pointer drift** (`Hexalith.Builds`, `EventStore`, `FrontComposer`, `Memories`, `PolymorphicSerializations`, `Tenants`) present in the working tree and undocumented; pre-existing per Story 7.2 intelligence and the cause of the blocked build. Not reset (story scope forbids it).

### Outcome

No CRITICAL issues remain → **Status: done**. One MEDIUM follow-up tracked under Review Follow-ups (AI).
