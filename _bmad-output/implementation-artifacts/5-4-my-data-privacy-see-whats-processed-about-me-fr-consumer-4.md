---
baseline_commit: 7abfea9
---

# Story 5.4: My data & privacy - see what's processed about me (FR-Consumer-4)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to see what is processed about me,
so that I have transparency over my data.

## Acceptance Criteria

1. Given `/me/privacy`, when the processing summary renders, then it is sourced only from self-scoped `GetProcessingRecords`, uses no caller-supplied party id, and shows plain-language bounded audit metadata only.
2. Given processing records contain backend metadata, when the ConsumerPortal maps and renders them, then no `PartyId`, `TenantId`, `ActorId`, `CorrelationId`, raw command/query payload, exported JSON content, contact value, identifier, display name, restriction reason, token, claim, exception detail, or ProblemDetails text appears in copy, DOM, URLs, logs, tests, or component attributes.
3. Given processing records are available, when the page renders, then the summary includes operation category, outcome, timestamp, and safe summary text, plus a freshness/status line using `role="status" aria-live="polite"` without creating duplicate export, erasure, or processing status sources for the same state.
4. Given no processing records are available, when the page renders, then the processing section remains visible with a bounded empty state and the existing export and erasure sections remain usable.
5. Given processing records fail to load because of unbound/forbidden, unauthenticated, unavailable, stale/degraded, transient/server/timeout, erased, or circuit-disconnect states, when `/me/privacy` renders, then the page keeps export and erasure content visible where possible, maps the failure to bounded PII-free copy, does not navigate away for routine failures, and does not expose raw ids/details.
6. Given the privacy card offers "Manage all consent", when I follow it, then it links to `/me/consent` as the full consent surface; the `/me/privacy` card is only a summary and must not duplicate consent grant/withdraw controls.
7. Given this story is the processing-summary slice, when files are changed, then no consent workflow rewrite, erasure workflow rewrite, export download rewrite, identity-binding redesign, public browser endpoint, direct EventStore browser call, DAPR ACL change, EventStore/Tenants/FrontComposer submodule edit, package upgrade, or new third-party library is introduced.

## Tasks / Subtasks

- [x] Add a ConsumerPortal-owned processing-summary port and DTOs (AC: 1, 2, 4, 5, 7)
  - [x] Add `IConsumerPrivacyProcessingClient` under `src/Hexalith.Parties.ConsumerPortal/Services/` with `GetMyProcessingSummaryAsync(CancellationToken)` and no caller-supplied ids.
  - [x] Add bounded ConsumerPortal DTO/result types, for example `ConsumerPrivacyProcessingResult`, `ConsumerPrivacyProcessingOutcome`, and `ConsumerPrivacyProcessingRecord`.
  - [x] Include only fields the UI may render: safe operation category, safe outcome, timestamp, safe summary, and optional bounded freshness/status. Do not include party, tenant, actor, correlation, raw payload, problem detail, or exported package fields.
  - [x] Keep ConsumerPortal free of `ISelfScopedPartiesClient`, `IAdminPortalGdprClient`, `IPartiesQueryClient`, `IPartiesCommandClient`, `GetPartyAsync`, list/search, `PagedResult<>`, public endpoint code, and UI-host/internal references.

- [x] Add the UI-host adapter and DI registration (AC: 1, 2, 5, 7)
  - [x] Add `ConsumerPrivacyProcessingClient` under `src/Hexalith.Parties.UI/Services/`.
  - [x] Implement it by delegating only to `ISelfScopedPartiesClient.GetMyProcessingRecordsAsync(CancellationToken)`.
  - [x] Map backend `ProcessingActivityRecord` to the bounded ConsumerPortal DTO and drop `PartyId`, `TenantId`, `ActorId`, and `CorrelationId`.
  - [x] Catch `InvalidOperationException` from self-scope as forbidden/unbound and catch client/transient failures as bounded failure outcomes; preserve `OperationCanceledException` when the caller token is canceled.
  - [x] Register the adapter as Scoped in `src/Hexalith.Parties.UI/Program.cs` after `AddSelfScopedPartiesClient()`, matching profile, edit, consent, export, and erasure adapters.

- [x] Render the processing summary on `/me/privacy` without regressing export or erasure (AC: 1-6)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`; keep `@page "/me/privacy"`, `[Authorize(Policy = "Consumer")]`, `IConsumerPrivacyExportClient`, `IConsumerPrivacyErasureClient`, JS download behavior, and all Story 5.3 erasure states.
  - [x] Inject the new ConsumerPortal processing port and load processing records through it using a component-owned `CancellationTokenSource`.
  - [x] Leave the component in a valid render state before awaiting lifecycle work: show a bounded loading/status message and do not blank the export/erasure sections.
  - [x] Add a processing section below or between existing privacy sections with a heading such as "What we process about you", safe row text, and an empty state.
  - [x] Render only safe fields from the ConsumerPortal DTO. Do not render backend ids, actor ids, correlation ids, raw `EventType` if it is too implementation-shaped for consumers, raw problem details, payloads, names, contacts, identifiers, or exported JSON content.
  - [x] Add a "Manage all consent" link to `/me/consent` and make clear in copy that `/me/privacy` is a summary, not the consent-management surface.
  - [x] Preserve one visible status source per action/state: export status stays export-specific; erasure status stays erasure-specific; processing load/freshness gets its own polite status only when it is the processing state being announced. Failure messages use `role="alert"`.

- [x] Add resource-backed copy and token-based styling only (AC: 2-6)
  - [x] Add processing-summary labels to `Resources/ConsumerPortalResources.resx` and expose them through `ConsumerPortalLabels`.
  - [x] Required copy concepts: processing transparency, bounded audit metadata, no raw payloads, empty state, unavailable/retry state, and "Manage all consent".
  - [x] Keep all regulated/user-facing copy in resources, not inline in Razor markup.
  - [x] If CSS is needed in `MyPrivacyPage.razor.css`, use Fluent/FrontComposer design tokens only; no raw hex/rgb/hsl colors, no raw `#0097A7`, no color-only state, no nested cards, and no spinner-only screen.

- [x] Preserve and prove self-scoped backend/client behavior (AC: 1, 2, 5, 7)
  - [x] Do not change the existing `ISelfScopedPartiesClient.GetMyProcessingRecordsAsync` method signature; it already injects the resolved party id and delegates to `IAdminPortalGdprClient.GetProcessingRecordsAsync`.
  - [x] Do not change `HttpAdminPortalGdprClient.GetProcessingRecordsAsync` unless a failing test proves the current gateway query shape is wrong. It currently posts `GetProcessingRecords` to `/api/v1/queries` with `ProjectionType="PartyDetail"` and `PartyDetailProjectionQueryActor`.
  - [x] Do not alter processing-record domain/query contracts unless an implementation blocker is found. `ProcessingActivityRecord` is backend/audit metadata and contains fields ConsumerPortal must filter, not display wholesale.

- [x] Add focused component, adapter, boundary, and E2E tests (AC: 1-7)
  - [x] Extend `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs` for processing-summary loading, populated records, empty state, transient failure, forbidden/unbound failure, erased self retaining processing records, PII-free DOM, "Manage all consent" link to `/me/consent`, and export/erasure sections remaining visible.
  - [x] Add `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyProcessingClientTests.cs` proving delegation to `GetMyProcessingRecordsAsync`, metadata filtering, bounded outcome mapping, no id leakage, and cancellation behavior.
  - [x] Extend `PartiesUiHostCompositionTests.Program_RegistersConsumerPortalAdaptersAfterSelfScopedClient` for `IConsumerPrivacyProcessingClient`.
  - [x] Extend `ConsumerPortalPackagingTests` to require the new processing port while continuing to forbid UI-host/internal clients, list/search, id-shaped public properties, direct gateway clients, raw ids, and `PagedResult<>` inside ConsumerPortal.
  - [x] Extend `SelfScopedPartiesClientTests` only if coverage is missing; current tests already cover `GetMyProcessingRecordsAsync` bound, unbound, ambiguous, and no-underlying-call behavior.
  - [x] Extend `tests/e2e/specs/consumer-portal-routes.spec.ts`: bound Consumer sees the processing summary on `/me/privacy`, the request capture records `processing-records` for `party-bound-001`, the browser sees no `/api/v1/commands` or `/api/v1/queries`, the "Manage all consent" link reaches `/me/consent`, and forbidden ids/details are absent from the page.
  - [x] If fixture data needs more detail, extend `PartiesAdminPortalE2eFixture` narrowly; it already captures `ProcessingRecordRequests` and returns a synthetic `ProcessingActivityRecord`.

- [x] Validate the focused implementation (AC: 1-7)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run the focused xUnit v3 executables for ConsumerPortal and UI tests when `dotnet test`/MTP is unreliable.
  - [x] Run the focused Playwright spec for `consumer-portal-routes.spec.ts` if the local fixture host is available; otherwise record the exact host/browser limitation.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record exact wrapper/MTP/socket limitations instead of claiming success.

## Dev Notes

### Current Implementation State

- Sprint status has Epic 5 in progress, Stories 5.1-5.3 done, and Story 5.4 in backlog before this story file was created. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 5.4 covers FR-Consumer-4's processing-transparency slice: `/me/privacy` shows what is processed about the signed-in consumer, sourced from `GetProcessingRecords`, and the "Manage all consent" link targets `/me/consent`. [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.4-My-data--privacy--see-whats-processed-about-me-FR-Consumer-4`]
- `/me/privacy` currently has working export and erasure sections from Stories 5.2 and 5.3. It injects `IConsumerPrivacyExportClient`, `IConsumerPrivacyErasureClient`, and `IJSRuntime`, uses `DotNetStreamReference` for downloads, and maintains separate export and erasure status/failure states. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- The UI host already exposes `ISelfScopedPartiesClient.GetMyProcessingRecordsAsync(CancellationToken)`, which resolves the current principal's bound party id and delegates to `IAdminPortalGdprClient.GetProcessingRecordsAsync`. Do not add a caller-id method to ConsumerPortal. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `IAdminPortalGdprClient.GetProcessingRecordsAsync(string partyId, CancellationToken)` and `HttpAdminPortalGdprClient.GetProcessingRecordsAsync` already exist and use the EventStore gateway query `GetProcessingRecords`. [Source: `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`] [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]
- `ProcessingActivityRecord` includes `SequenceNumber`, `PartyId`, `TenantId`, `ActorId`, `CorrelationId`, `OperationCategory`, `Outcome`, `EventType`, `Timestamp`, and `Summary`. ConsumerPortal must not render this record wholesale. [Source: `src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs`]
- Processing activity records intentionally exclude raw command payloads, query payloads, exported package content, tokens, claim dictionaries, contact values, identifiers, names, and restriction reason text. Erased parties retain processing records because they are audit metadata and do not need decrypted personal data. [Source: `docs/gdpr-processing-activity-records.md`]
- The E2E fixture already captures processing-record requests and returns a synthetic `ProcessingActivityRecord` with backend ids and correlation metadata. This is useful for proving the UI filters those values. [Source: `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs#CaptureProcessingRecords`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor` (UPDATE)
  - Current state: export and erasure are real, self-scoped features.
  - What this story changes: add processing-summary section and load state.
  - Preserve: route, Consumer policy, export download behavior, erasure request/cancel behavior, one-status-source discipline, PII-free DOM.
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css` (UPDATE only if needed)
  - Current state: token-based, phone-first, single-column privacy layout.
  - What this story changes: processing list/summary styling if needed.
  - Preserve: no raw colors, no nested cards, no one-hue decorative theme, no text overlap on phone.
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx` and `Services/ConsumerPortalLabels.cs` (UPDATE)
  - Current state: resource-backed profile/edit/consent/export/erasure copy.
  - What this story changes: processing summary, empty, loading, failure, and consent-link copy.
  - Preserve: regulated copy never inline in components.
- `src/Hexalith.Parties.ConsumerPortal/Services/*Privacy*Processing*` (NEW)
  - Current state: no ConsumerPortal processing port exists.
  - What this story changes: add no-caller-id port and bounded DTOs.
  - Preserve: ConsumerPortal cannot know about UI host, self-scope implementation, AdminPortal client, or backend ids.
- `src/Hexalith.Parties.UI/Services/ConsumerPrivacyProcessingClient.cs` (NEW)
  - Current state: export, erasure, consent, profile adapters show the pattern.
  - What this story changes: map self-scoped processing records to safe ConsumerPortal records.
  - Preserve: Scoped lifetime, no PII logging, no raw detail passthrough.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers `AddSelfScopedPartiesClient()` before ConsumerPortal adapters.
  - What this story changes: register processing adapter after self-scope.
  - Preserve: no eager gateway resolution in degraded/test boot; `ValidateScopes=true`.
- Tests (UPDATE/NEW)
  - Current state: MyPrivacyPage tests cover export and erasure; packaging tests enforce ConsumerPortal boundaries; UI tests cover adapter registration and self-scope.
  - What this story changes: add processing summary coverage and update tripwires.
  - Preserve: source scans exclude `bin/` and `obj/`.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It owns narrow, caller-id-free ports; the UI host registers adapters that delegate to `ISelfScopedPartiesClient`. Do not make ConsumerPortal reference `Hexalith.Parties.UI` just to reach host primitives. [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns--Consistency-Rules`]
- Consumer data access is own-data-only through the self-scoped accessor. ConsumerPortal must never accept route ids, query ids, hidden form party ids, arbitrary ids, list/search, direct `GetPartyAsync(partyId)`, or direct EventStore gateway clients. [Source: `_bmad-output/planning-artifacts/architecture.md#Process-Patterns`]
- Browser traffic talks only to the UI host. Do not add browser-visible command/query APIs, direct browser calls to EventStore, actor-host calls, DAPR ACLs, CORS-dependent processing-record URLs, or browser bearer-token flows. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- GDPR reads use the EventStore gateway typed client path. `GetProcessingRecords` is already a query on the party-detail projection actor. Do not add public `parties` endpoints for this story. [Source: `docs/api-contracts.md#Queries--POST-apiv1queries`]
- Reads are eventually consistent and carry freshness. Render last-known or bounded stale/degraded states; do not throw, blank the page, or hide export/erasure when processing summary is unavailable. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Central Package Management is on. Do not add package versions to `.csproj`, bump packages, or add a third-party table/dialog/state library. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Domain and Contract Guidance

- `GetProcessingRecords` returns an array of `ProcessingActivityRecord` for one aggregate. It is tenant/party scoped by the gateway and self-scoped accessor; ConsumerPortal only sees the bounded adapter result. [Source: `docs/api-contracts.md#Queries--POST-apiv1queries`]
- The backend record's `Summary` is intended to be stable and non-PII, but the adapter should still treat it as backend-provided text: render only after null/empty handling and never combine it with ids or raw details. [Source: `docs/gdpr-processing-activity-records.md`]
- Do not render `SequenceNumber` unless the UX needs ordering. If used internally for stable ordering or keys, do not expose it as user-facing copy because it is audit implementation detail.
- Safe display fields for the first implementation: `Summary`, `OperationCategory`, `Outcome`, and a localized timestamp. Consider mapping known categories/outcomes to resource-backed labels; unknown values should degrade to bounded neutral copy instead of raw technical labels.
- Erased self still gets processing records. Do not use erasure state as a reason to hide the processing-summary section; show audit metadata only. [Source: `docs/gdpr-processing-activity-records.md`]

### UX and Accessibility Guardrails

- Consumer privacy is phone-first, single-column, roomy, and 16px body text. Do not turn this into an admin-style dense audit table on small screens. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- The privacy card is a summary. "Manage all consent" must route to `/me/consent`, where grant/withdraw parity lives. Do not add consent toggles to `/me/privacy`. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Status/freshness/accepted processing states use `role="status" aria-live="polite"`; validation, transient failure, and load failure use `role="alert"`. Never blanket all messages as polite. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Banned UI behavior remains banned: native `alert()`/`confirm()`/`prompt()`, spinner-only screens, color-only state, raw 500/problem details, hard 30-day erasure promises, "under one minute" export promises, and success-green deletion language. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Interaction-Primitives`]

### Previous Story Intelligence

- Story 5.3 added real erasure request/cancel behavior and already changed backend contracts. Story 5.4 must not reopen erasure semantics, cancel command behavior, or erasure status mapping unless a regression blocks the processing summary. [Source: `_bmad-output/implementation-artifacts/5-3-my-data-privacy-request-cancel-erasure-fr-consumer-4.md#Dev-Notes`]
- Story 5.2 established the export pattern: ConsumerPortal-owned port, UI-host adapter, resource-backed copy, PII-free DOM, and JS download isolated at the host boundary. Mirror that adapter shape for processing records instead of coupling ConsumerPortal to self-scope or AdminPortal clients. [Source: `_bmad-output/implementation-artifacts/5-2-my-data-privacy-export-my-data-fr-consumer-4.md#Dev-Notes`]
- Story 5.1 established regulated-copy and consent patterns: "Things you control" lives on `/me/consent`, resource-backed labels, strict packaging guards, one status source, and no raw consent/channel/operator ids in DOM/copy. Reuse this posture for the `/me/privacy` summary link. [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md#Dev-Notes`]
- Existing self-scope tests already include `GetMyProcessingRecordsAsync` in bound/unbound/ambiguous method coverage. Do not weaken those reflection or fail-closed tests to fit the UI. [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`]
- Recent commits are sequential and scoped: Story 5.1 consent, Story 5.2 export, Story 5.3 erasure. Keep Story 5.4 focused on processing transparency. [Source: `git log -5 --oneline`]

### Latest Technical Information

- Microsoft Learn's current Blazor lifecycle guidance says a component must be left in a valid render state before awaiting incomplete lifecycle work. Use this for processing-record load so `/me/privacy` renders a bounded intermediate state. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor DI guidance says server-side Scoped services are circuit-scoped and not reconstructed during client-side navigation. Keep the processing adapter Scoped and do not capture it from singletons. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]
- Microsoft Learn's routing guidance confirms route templates are component-level concerns. Keep the full consent surface at the existing `/me/consent` route and link to it directly; do not create a duplicate privacy sub-route for consent. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET SDK `10.0.302`, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, bUnit, Shouldly, and NSubstitute. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyProcessingClient.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingResult.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingOutcome.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingRecord.cs`
  - `src/Hexalith.Parties.UI/Services/ConsumerPrivacyProcessingClient.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyProcessingClientTests.cs`
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css` if needed
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
  - `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` only if richer processing fixture data is needed

### References

- `_bmad-output/planning-artifacts/epics.md#Story-5.4-My-data--privacy--see-whats-processed-about-me-FR-Consumer-4`
- `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns--Consistency-Rules`
- `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Information-Architecture`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`
- `docs/api-contracts.md#Queries--POST-apiv1queries`
- `docs/gdpr-processing-activity-records.md`
- `src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs`
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `git diff --check` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: 82 tests, 0 failed.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: 330 tests, 0 failed.
- `npm run typecheck` in `tests/e2e` passed.
- `npm run test -- specs/consumer-portal-routes.spec.ts --project=chromium` attempted; local fixture host failed to start because Kestrel socket bind is denied in this sandbox (`System.Net.Sockets.SocketException (13): Permission denied`).
- `pwsh scripts/test.ps1 -Lane unit` attempted; wrapper/dotnet test path reported `Build failed with exit code: 1` during restore/build without detailed diagnostics. Focused xUnit v3 executables were used for reliable test execution.
- ConsumerPortal source scan for forbidden processing metadata/client references passed.
- AI review auto-fix validation on 2026-06-10: `git diff --check`, ConsumerPortal/UI Release builds, ConsumerPortal test executable (82 tests, 0 failed), and UI test executable (330 tests, 0 failed) passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added the ConsumerPortal-owned processing port/result/record/category/outcome DTOs with no caller-supplied identity and no UI-host or gateway client dependency.
- Added the UI-host scoped `ConsumerPrivacyProcessingClient` adapter, delegating only through `ISelfScopedPartiesClient.GetMyProcessingRecordsAsync`, mapping backend metadata to bounded enums/summaries, and preserving caller cancellation.
- AI review fixed the adapter to drop unsafe backend summary text containing raw IDs/detail markers and to map typed gateway status failures to bounded ConsumerPortal processing outcomes.
- Updated `/me/privacy` with a resource-backed processing summary section, independent processing status/alert handling, safe rows, empty state, and `Manage all consent` link to `/me/consent` without changing export or erasure workflows.
- Added component, adapter, composition, packaging, and Playwright coverage for processing summary behavior, bounded metadata, no browser-visible gateway calls, and consent navigation.

### File List

- `_bmad-output/implementation-artifacts/5-4-my-data-privacy-see-whats-processed-about-me-fr-consumer-4.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingCategory.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingRecord.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingRecordOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyProcessingResult.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyProcessingClient.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerPrivacyProcessingClient.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyProcessingClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Change Log

- 2026-06-10: Implemented Story 5.4 processing transparency summary for `/me/privacy`; added bounded ConsumerPortal port/DTOs, UI-host adapter, resource-backed UI copy/styling, focused unit/component/boundary tests, and E2E coverage updates.
- 2026-06-10: Senior developer review auto-fixed unsafe processing-summary pass-through and typed client failure mapping; added focused adapter regression tests and marked story done.

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after auto-fix.

Findings fixed:

- HIGH: `ConsumerPrivacyProcessingClient` truncated backend summaries but still rendered unsafe summary content if a `ProcessingActivityRecord.Summary` included `PartyId`, `TenantId`, `ActorId`, `CorrelationId`, raw structured content, `ProblemDetails`, token/claim markers, display-name markers, or raw backend event type text. Fixed by treating unsafe summaries as empty so the ConsumerPortal resource-backed fallback renders instead.
- MEDIUM: typed gateway failures from `PartiesClientException` were collapsed to transient failure, losing bounded unauthenticated, forbidden, unavailable, and erased states required by the story. Fixed explicit 401/403/404/410/501/503/status-family mapping.

Validation:

- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: 330 tests, 0 failed.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: 82 tests, 0 failed.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `git diff --check` passed.
