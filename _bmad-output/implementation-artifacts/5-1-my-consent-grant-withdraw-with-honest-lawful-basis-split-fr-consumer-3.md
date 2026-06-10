---
baseline_commit: 02824ce
---

# Story 5.1: My consent - grant / withdraw with honest lawful-basis split (FR-Consumer-3)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to control my consent honestly,
so that I decide what I am opted into without being misled.

## Acceptance Criteria

1. Given `/me/consent`, when the consent controls render, then each consent-based item is a real `FluentSwitch` with `role="switch"` and `aria-checked`, visible purpose text, and purpose plus lawful basis tied to the switch via `aria-describedby`; every consent switch defaults Off and is never pre-checked.
2. Given the data groups, when the page renders, then "Things you control" contains only consent-based toggles, "Things we keep to run your account" contains contract/legal/legitimate-interest read-only items, and legitimate-interest rows offer an Object (Art. 21) action, not a withdraw toggle.
3. Given existing `ConsentRecord` data is loaded, when a consent purpose has an active record, then the matching switch renders On; when no active record exists or the read is unavailable, then the switch renders Off until confirmed data proves active consent.
4. Given a consent flip from Off to On, when I grant consent, then the switch flips optimistically, one polite `aria-live` status announces Saving, `RecordConsent` is issued only through the self-scoped accessor, and projection/refresh confirmation reconciles the switch without stealing focus.
5. Given a consent flip from On to Off, when I withdraw consent, then withdraw is as easy as grant, `RevokeConsent` is issued only through the self-scoped accessor using the active consent id, the switch flips optimistically, and confirmation reconciles the state.
6. Given command rejection, forbidden/unbound access, transient failure, validation rejection, stale read, or erased self, when the consent surface renders, then optimistic state is reverted where needed, the user's intent remains visible, an inline `role="alert"` reason is shown for failures, no PII/raw problem details/ids are exposed, and routine failure does not navigate away from the page.
7. Given Story 5.1 is the consent story only, when files are changed, then no export, erasure request/cancel, processing-records workflow, identity-binding redesign, Parties aggregate/event/projection change, public endpoint, DAPR ACL change, EventStore/Tenants/FrontComposer submodule edit, package upgrade, or new third-party library is introduced.

## Tasks / Subtasks

- [x] Replace the `/me/consent` placeholder with the real My consent surface (AC: 1, 2, 3, 6)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor` so it no longer renders the generic `ConsumerRouteShell` placeholder.
  - [x] Keep `@page "/me/consent"` and `@attribute [Authorize(Policy = "Consumer")]`.
  - [x] Load consent state through a ConsumerPortal-owned no-caller-id port; never accept route/query/hidden-form party ids.
  - [x] Render a cold-load skeleton or calm loading state before awaiting data; cancel in-flight load/save work on dispose.
  - [x] For erased self, block consent changes and show PII-free tombstone/no-data copy only.

- [x] Add a ConsumerPortal consent port and UI-host adapter (AC: 3, 4, 5, 6, 7)
  - [x] Add `IConsumerConsentClient` and narrow request/result DTOs under `src/Hexalith.Parties.ConsumerPortal/Services/`, or an equivalently named ConsumerPortal-owned consent service.
  - [x] The port must expose only no-caller-id methods such as `GetMyConsentOverviewAsync`, `GrantMyConsentAsync`, and `WithdrawMyConsentAsync`; no method may accept `partyId`, return `PagedResult<>`, or expose list/search.
  - [x] The overview result must include the active/revoked consent records plus the bounded self contact-channel metadata needed to issue grants. It may carry channel ids internally for command mapping, but must not render channel ids or contact values as user-facing copy.
  - [x] Add a UI-host adapter under `src/Hexalith.Parties.UI/Services/` that implements the port and delegates only to self-scoped methods such as `ISelfScopedPartiesClient.GetMyPartyAsync`, `GetMyConsentAsync`, `GrantMyConsentAsync`, and `RevokeMyConsentAsync`.
  - [x] Register the adapter as Scoped in `src/Hexalith.Parties.UI/Program.cs` after `AddSelfScopedPartiesClient()`, mirroring the profile data/edit adapters.
  - [x] Keep `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` free of references to `Hexalith.Parties.UI`, Server, Projections, Security, Testing, direct gateway clients, or internal host projects.

- [x] Model consent purposes and lawful-basis groups explicitly (AC: 1, 2, 3, 7)
  - [x] Define the consent toggle catalogue in ConsumerPortal code/resources with stable purpose keys accepted by the backend validator (`^[a-zA-Z0-9\-_]+$`, max 100), for example `marketing_emails` and `product_updates`.
  - [x] Bind each consent purpose to a valid contact channel id from the self-scoped overview; if no suitable channel exists, render the row disabled with generic PII-free copy rather than inventing a channel id.
  - [x] Use `LawfulBasis.Consent` only for rows under "Things you control"; do not issue `RecordConsent` for contract, legal obligation, or legitimate-interest read-only rows.
  - [x] Render contract/legal/legitimate-interest rows as read-only explanations. For legitimate interest, show an Object action stub/command path only if an approved self-scoped object operation exists; otherwise show clear no-PII "contact support" or future-action copy and do not fake a withdraw switch.

- [x] Implement accessible toggle, optimistic save, and reconcile behavior (AC: 1, 3, 4, 5, 6)
  - [x] Use `FluentSwitch` and ensure rendered markup exposes `role="switch"` and `aria-checked`; add `aria-describedby` ids that include both purpose and lawful-basis explanation.
  - [x] Render active backend consent as On only when a matching active `ConsentRecord` exists. Revoked records and absent records are Off.
  - [x] On grant, call the ConsumerPortal consent port, which delegates to `ISelfScopedPartiesClient.GrantMyConsentAsync(channelId, purpose, LawfulBasis.Consent, token)`.
  - [x] On withdraw, call the ConsumerPortal consent port, which delegates to `ISelfScopedPartiesClient.RevokeMyConsentAsync(consentId, token)` for the active consent record.
  - [x] Use exactly one visible status source per consent action. Accepted/processing and freshness are `role="status" aria-live="polite"`; failures and validation rejections are inline `role="alert"`.
  - [x] Do not move focus for routine optimistic saves. Move focus only to a blocking alert when it helps the user recover.
  - [x] Re-read consent records or self party detail after accepted commands and reconcile to the authoritative projection. If projection remains stale/degraded, keep the optimistic state visibly pending rather than claiming it is final.
  - [x] Reset pending optimistic state on route disposal, reload, changed consent catalogue, changed party binding, erased self, capability loss, or authoritative rejection.

- [x] Preserve regulated copy, privacy, and visual posture (AC: 1, 2, 6)
  - [x] Add all consent labels, basis explanations, status text, alert text, and disabled-row copy to `Resources/ConsumerPortalResources.resx` and expose them through `ConsumerPortalLabels`.
  - [x] Keep copy plain: "Grant consent", "Withdraw consent", "Things you control", "Things we keep to run your account", and "Object (Art. 21)" where applicable.
  - [x] Do not show raw consent ids, channel ids, party ids, tenant ids, operator ids, correlation ids, command payloads, exception text, contact values in alerts, or backend problem details.
  - [x] Use Fluent/FrontComposer tokens and CSS isolation only if styling is needed. No raw hex/rgb/hsl colors, no raw `#0097A7`, no gradient/orb decoration, and no separate Consumer brand.
  - [x] Preserve phone-first, roomy, single-column Consumer layout and minimum target/focus rules.

- [x] Add focused tests and static guards (AC: 1-7)
  - [x] Add `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs` covering loading, active-consent On state, default-Off when no active record exists, read-only non-consent bases, legitimate-interest Object action, optimistic grant, optimistic withdraw, rejection revert, erased self, stale/degraded read, and PII-free alerts.
  - [x] Add ConsumerPortal service/DTO tests proving no method accepts or contains `partyId`, no list/search/direct gateway surface exists, and consent requests carry stable purpose/lawful-basis values.
  - [x] Update `ConsumerPortalPackagingTests` to allow only the approved consent port while continuing to forbid `Hexalith.Parties.UI`, `ISelfScopedPartiesClient`, `IPartiesQueryClient`, `IAdminPortalGdprClient`, `ListPartiesAsync`, `SearchPartiesAsync`, direct `GetPartyAsync(`, and public command clients inside ConsumerPortal.
  - [x] Add UI-host adapter tests proving Scoped registration and delegation to `ISelfScopedPartiesClient` consent methods without caller-supplied party ids.
  - [x] Keep `SelfScopedPartiesClientSurfaceTests` passing: no list/search members, no `PagedResult<>`, no party-id parameters, and no request type with a party-id shaped property.
  - [x] Add source/static checks that `/me/consent` does not log, trace, meter, or display PII/ids/raw details, and that only one status source renders per action.
  - [x] Update e2e specs for `/me/consent` route behavior where fixtures support it; label Playwright typecheck/spec discovery separately from browser runtime proof if sandbox sockets block execution.

- [x] Validate the focused implementation (AC: 1-7)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run compiled focused xUnit v3 executables for ConsumerPortal and UI tests when `dotnet test`/MTP is unreliable.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record exact wrapper/MTP/socket limitations instead of claiming success.

## Dev Notes

### Current Implementation State

- Sprint status had Epic 5 and Story 5.1 in backlog before this story file was created; Story 5.1 is the first Epic 5 story. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 5.1 covers FR-Consumer-3: My consent, opt-in default-Off, honest lawful-basis split, Object (Art. 21), and optimistic-then-reconcile. [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.1-My-consent-grant-withdraw-with-honest-lawful-basis-split-FR-Consumer-3`]
- `/me/consent` currently renders only the generic `ConsumerRouteShell` placeholder with static next-step text. It has no data load, no switch, no self-scoped command path, and no reconcile behavior. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`]
- `/me/privacy` remains a placeholder for later Epic 5 stories. Do not implement export, erasure, or processing-records behavior in Story 5.1. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- `/me` and `/me/edit` are real ConsumerPortal pages from Epic 4. They use ConsumerPortal-owned ports plus UI-host adapters, keep erased-profile PII suppressed, and keep status/freshness behavior local to the RCL. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`] [Source: `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`]
- `ISelfScopedPartiesClient` already exposes the required consent methods: `GetMyConsentAsync`, `GrantMyConsentAsync`, and `RevokeMyConsentAsync`. It is the single Consumer own-data gateway path and accepts no caller-supplied party id. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- `SelfScopedPartiesClient` resolves the current principal's `party_id` through `PartyIdClaimResolver`, fails closed for missing/ambiguous binding, and delegates consent methods to `IAdminPortalGdprClient` with the resolved id. [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `HttpAdminPortalGdprClient` posts `RecordConsent` and `RevokeConsent` through the EventStore command gateway and reads consent from `PartyDetail.ConsentRecords`; no new public endpoint is needed. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor` (UPDATE)
  - Current state: protected `/me/consent` placeholder shell.
  - What this story changes: real consent load, lawful-basis grouping, accessible switches, optimistic grant/withdraw, failure handling, and reconcile.
  - Preserve: route, Consumer policy, resource-backed Consumer copy.
- `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor` and `.razor.css` (READ/possibly UPDATE)
  - Current state: generic placeholder used by `/me/privacy` after this story.
  - What this story may change: leave it for privacy placeholder or adjust carefully without regressing `/me/privacy`.
  - Preserve: polite placeholder status, Consumer area posture.
- `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor` and profile components (READ/possibly REUSE)
  - Current state: RCL-local freshness/status display patterns.
  - What this story may change: reuse or extend local status conventions without adding a UI-host dependency.
  - Preserve: dot plus word, polite freshness, no color-only state.
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs` and `Resources/ConsumerPortalResources.resx` (UPDATE)
  - Current state: resource-backed profile/edit/placeholder copy.
  - What this story changes: consent labels, basis explanations, statuses, alerts, disabled copy.
  - Preserve: no regulated/user-facing inline copy in feature components.
- `src/Hexalith.Parties.ConsumerPortal/Services/*` (NEW/UPDATE)
  - Current state: profile data/edit ports and DTOs exist; no consent port exists.
  - What this story changes: add the consent-specific no-caller-id port and DTOs, including an overview shape that can carry internal command-mapping ids without displaying them.
  - Preserve: ConsumerPortal must not reference UI host, direct gateway clients, or internal projects.
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` and `SelfScopedPartiesClient.cs` (READ; UPDATE only if required)
  - Current state: already has no-id consent methods.
  - What this story likely changes: no change unless a missing result shape forces a narrow additive method.
  - Preserve: no list/search, no `PagedResult<>`, no party-id parameters, fail-closed generic errors, Scoped lifetime.
- `src/Hexalith.Parties.UI/Services/*Consent*` or new adapter file (NEW)
  - Current state: profile adapters exist (`ConsumerProfileDataClient`, `ConsumerProfileEditClient`); consent adapter does not.
  - What this story changes: add adapter from ConsumerPortal consent port to `ISelfScopedPartiesClient`, using `GetMyPartyAsync` only when needed to resolve eligible self contact channels for consent grants.
  - Preserve: no business rules beyond mapping/result translation; no PII logging.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers self-scoped accessor and ConsumerPortal profile adapters.
  - What this story changes: register consent adapter as Scoped after `AddSelfScopedPartiesClient()`.
  - Preserve: `ValidateScopes=true`, no browser tokens, no DAPR sidecar, no eager resolution in degraded/test boot.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs` (UPDATE)
  - Current state: static boundary guard for ConsumerPortal.
  - What this story changes: allow only the new consent port/DTOs and keep direct host/gateway calls forbidden.
  - Preserve: source scans exclude `bin/` and `obj/`.
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` and consent adapter tests (READ/UPDATE)
  - Current state: tripwire tests pin no list/search/no party-id self-scope surface.
  - What this story changes: ensure Epic 5 additions do not weaken those tripwires.
  - Preserve: reflection tests remain strict.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It must not reference `Hexalith.Parties.UI` to consume `ISelfScopedPartiesClient`, `StatusKind`, `OptimisticReconcile`, shared UI components, or host-owned services. Use a ConsumerPortal-owned port with a UI-host adapter. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`] [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-06-10.md#Commitments`]
- Consumer data access is own-data-only. The UI BFF resolves the Consumer's `party_id` and injects it; ConsumerPortal must never accept route ids, query ids, hidden form party ids, arbitrary ids, list/search, or direct `GetPartyAsync(partyId)`. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`] [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- The browser talks only to the UI host. Do not add public ConsumerPortal APIs, direct browser calls to EventStore, direct actor-host calls, DAPR ACLs, or browser bearer-token flows. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Use the existing EventStore gateway typed client path for consent. `RecordConsent`/`RevokeConsent` already exist; this story should not add commands, events, projections, validators, controllers, gateway routes, or actor-host endpoints. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`] [Source: `src/Hexalith.Parties.Contracts/Commands/RecordConsent.cs`] [Source: `src/Hexalith.Parties.Contracts/Commands/RevokeConsent.cs`]
- Consent management is allowed while processing is restricted because the aggregate intentionally has no restriction check for consent operations (Art. 18(3)). Do not disable consent solely because `IsRestricted` is true. [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-RecordConsent`] [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-RevokeConsent`]
- Erasure pending/in-progress rejects consent mutations. UI must disable or revert with a generic alert when the self party is erased or erasure is in progress. [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-RecordConsent`]
- Use Central Package Management. Do not add package versions to csproj files, bump packages, or add a third-party state/consent library. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Domain and Contract Guidance

- `ConsentRecord` fields are `ConsentId`, `ChannelId`, `Purpose`, `LawfulBasis`, `GrantedAt`, `GrantedBy`, `Source`, revoke metadata, and computed `IsActive`. Treat ids/operator/source fields as backend/audit data, not user-facing copy. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs`]
- `LawfulBasis` values are `Consent`, `LegitimateInterest`, `ContractualNecessity`, and `LegalObligation`. Only `Consent` rows get switches. [Source: `src/Hexalith.Parties.Contracts/Security/LawfulBasis.cs`]
- `RecordConsent` requires `PartyId`, `TenantId`, `ChannelId`, `Purpose`, and `LawfulBasis`; the self-scoped accessor supplies party id through the underlying client. [Source: `src/Hexalith.Parties.Contracts/Commands/RecordConsent.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `RevokeConsent` requires `ConsentId`; the UI must use the active `ConsentRecord.ConsentId` for the selected purpose and must not synthesize a revoke id when no active consent exists. [Source: `src/Hexalith.Parties.Contracts/Commands/RevokeConsent.cs`] [Source: `src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs`]
- `GrantMyConsentAsync` still needs a backend `channelId`. Resolve that id from the current user's self party through the UI-host adapter and keep it out of visible copy, URLs, alerts, logs, and telemetry. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/ContactChannel.cs`] [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- The aggregate derives deterministic consent ids as `"{channelId}:{purpose}".ToLowerInvariant()`, no-ops on already-active grant, and no-ops on already-revoked revoke. UI should still reconcile from projection rather than assuming command result means final projection state. [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-RecordConsent`]
- `PartyDetailProjectionHandler` adds consent records on `ConsentRecorded` and marks revoke metadata on `ConsentRevoked`; consent reads come from the projected `PartyDetail`. [Source: `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs#HandleConsentRecorded`]

### UX and Accessibility Guardrails

- Consumer consent controls must be real switches, not styled divs. They need visible labels and `aria-describedby` tying purpose plus lawful basis to the control. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns`]
- Consent defaults Off and is never pre-checked. Existing active backend consent may render On after data loads; absent/unavailable consent must not appear as On. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Split "Things you control" from "Things we keep to run your account". Contract/legal/legitimate-interest bases are read-only; legitimate interest uses Object (Art. 21), not Withdraw. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Accepted/processing status and freshness use `role="status" aria-live="polite"` with no focus steal. Validation/failure/rejection uses inline `role="alert"`. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]
- Withdraw must be as easy as grant. Do not add confirm-shaming, extra friction, modal stacking, or asymmetrical copy that makes staying opted in easier than withdrawing. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Inspiration--Anti-patterns`]
- Consumer pages are phone-first, single-column, roomy, and 16px body text. This is the working product surface, not a landing page. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- Do not use raw teal `#0097A7`, raw hex/rgb/hsl colors, color-only state, spinner-only screens, native `alert()`/`confirm()`/`prompt()`, or success-green deletion/withdraw language. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]

### Previous Story Intelligence

- Epic 4 established the safe ConsumerPortal pattern: RCL-owned ports plus UI-host adapters. Reuse that for consent instead of exposing `ISelfScopedPartiesClient` or gateway clients to the RCL. [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-06-10.md#Next-Epic-Preview-Epic-5-Consumer-Consent-Data-Export--Erasure`]
- Story 4.5 added `IConsumerProfileEditClient`, a UI-host adapter, no-caller-id self-scoped write behavior, one-status-source rules, and strict packaging guards. Mirror those conventions for consent. [Source: `_bmad-output/implementation-artifacts/4-5-edit-my-profile-fr-consumer-2.md#Completion-Notes-List`]
- Story 4.4 added `IConsumerProfileDataClient`, `ConsumerProfileDataClient`, `FreshnessStatus`, `ProfileField`, and real `/me` rendering. Reuse RCL-local display conventions and avoid a shared UI package refactor in this story. [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md#Previous-Story-Intelligence`]
- Story 3.3 hardened Admin consent with bounded display, no raw consent/channel/operator ids, single confirmation for revoke, and shared outcome mapping. Consumer consent is a different copy register, but the bounded-display and stale-state lessons still apply. [Source: `_bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md#Senior-Developer-Review-AI`]
- Story 4.5 validation showed focused builds and compiled xUnit v3 executables pass reliably while `dotnet test`/MTP can fail in this sandbox with no C# diagnostics. Keep evidence labels precise. [Source: `_bmad-output/implementation-artifacts/4-5-edit-my-profile-fr-consumer-2.md#Debug-Log-References`]
- Recent commits are sequential and scoped: Story 4.1 ADR, 4.2 binding provisioning, 4.3 ConsumerPortal stand-up, 4.4 read-only profile, and 4.5 edit profile. Keep Story 5.1 focused on consent only. [Source: `git log -5`]

### Testing and Validation Guidance

- Use xUnit v3, bUnit, Shouldly, and NSubstitute. Do not introduce Moq, FluentAssertions, raw `Assert.*`, Verify snapshots unless needed, package versions in csproj files, or new third-party libraries. [Source: `_bmad-output/project-context.md#Testing-Rules`]
- Required ConsumerPortal component tests:
  - `/me/consent` loading state renders a calm skeleton/status before data resolves.
  - No active consent renders switches Off and never pre-checked.
  - Active matching consent renders the matching switch On.
  - Revoked consent renders Off.
  - Purpose and lawful basis are connected to each switch by `aria-describedby`.
  - Contract/legal/legitimate-interest rows are read-only; legitimate interest offers Object, not Withdraw.
  - Grant flips optimistically, shows one polite Saving status, delegates through the consent port, and reconciles.
  - Withdraw flips optimistically, uses the active consent id, shows one polite Saving status, and reconciles.
  - Rejection/transient failure reverts or marks pending, preserves intent, shows inline `role="alert"`, and does not leak ids or exception text.
  - Stale/degraded reads keep last-known consent rows visible with quiet status.
  - Erased self blocks switches and renders no personal values/ids.
- Required boundary/static tests:
  - ConsumerPortal has no reference to `Hexalith.Parties.UI`.
  - ConsumerPortal contains no `IPartiesQueryClient`, `IPartiesCommandClient`, `IAdminPortalGdprClient`, `ISelfScopedPartiesClient`, direct `GetPartyAsync(`, `ListPartiesAsync`, `SearchPartiesAsync`, public endpoint code, or methods/DTOs with party-id shaped properties.
  - UI host registers the consent adapter as Scoped and delegates only to the self-scoped consent methods.
  - Self-scope surface tripwires remain green.
  - Source scans exclude `bin/` and `obj/`.
- Required E2E updates:
  - `/me/consent` route is reachable for bound Consumers and not shown to Admin-only/unauthenticated users.
  - Basic switch semantics and no-regulated-copy regressions can be asserted through fixtures if browser runtime is available.
  - Keep unbound Consumer tests proving `NoPartyBinding` wins and no consent data appears.

### Latest Technical Information

- Microsoft Learn's current Blazor forms guidance says `EditForm` supports `Model`/`EditContext`, `OnSubmit`, `OnValidSubmit`, and `OnInvalidSubmit`, and `FormName` should be unique. Use the same pattern if a consent form wrapper is needed. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor lifecycle guidance says an async lifecycle method must leave the component in a valid render state before awaiting incomplete work. Apply this to consent load/save state. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor DI guidance describes server-side Scoped services as circuit-scoped and warns against singleton capture of scoped services. Keep consent adapters Scoped and compatible with `ValidateScopes=true`. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET SDK `10.0.300`, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, bUnit, Shouldly, and NSubstitute. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerConsentClient.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentOverview.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentItem.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentOperationResult.cs`
  - `src/Hexalith.Parties.UI/Services/ConsumerConsentClient.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerConsentClientTests.cs`
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` only if needed by new shapes
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
  - `tests/e2e/specs/consumer-party-binding.spec.ts`
- Keep generated/build artifacts out of the repo. Do not edit or commit `bin/`, `obj/`, or `obj/**/generated/**`.

### Out of Scope

- No export, erasure request/cancel, or processing-records UI; Stories 5.2-5.4 own `/me/privacy`.
- No identity binding redesign, self-registration, IdP federation, binding-store persistence, or Keycloak mapper changes.
- No Parties aggregate/event/projection/actor/validator changes.
- No EventStore, Tenants, FrontComposer, Memories, DAPR ACL, deployment, or production KMS changes.
- No public UI API endpoint, direct EventStore browser call, actor-host endpoint, browser token flow, or new app host sidecar.
- No package upgrades and no new third-party libraries.
- No broad shared UI package refactor unless a build-breaking dependency issue proves it is strictly required.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.1-My-consent-grant-withdraw-with-honest-lawful-basis-split-FR-Consumer-3`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-06-10.md`]
- [Source: `_bmad-output/implementation-artifacts/4-5-edit-my-profile-fr-consumer-2.md`]
- [Source: `_bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Security/LawfulBasis.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/RecordConsent.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/RevokeConsent.cs`]
- [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`]
- [Source: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]

## Validation Summary

- Source discovery loaded the BMAD create-story skill, discovery protocol, template, checklist, BMad config, sprint status, root and sibling project-context facts, planning epics, architecture, UX experience/review/validation files, Epic 4 retrospective, Story 4.3, Story 4.4, Story 4.5, Story 3.3, current ConsumerPortal source/tests, UI self-scope source/tests, consent contracts, aggregate/projection consent logic, Admin GDPR client seams, recent git history, and current Microsoft Blazor docs.
- Checklist fixes applied before finalizing: made the ConsumerPortal port/adapter boundary explicit, required default-Off semantics while still rendering active backend consent On, prevented fake withdraw toggles for non-consent bases, required active consent id usage for revoke, carried forward one-status-source and PII-free alert rules, preserved static self-scope/packaging guard tripwires, and kept `/me/privacy` out of scope.
- Latest-technology review found no dependency upgrade requirement; implementation should use normal Blazor lifecycle, DI, and component patterns on the pinned local stack.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` red phase failed on missing consent port/DTO types, then passed after implementation.
- `git diff --check` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -parallel none -longRunning 30` passed: 53 total, 0 failed.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -parallel none -longRunning 30` passed: 297 total, 0 failed.
- `npm run typecheck` in `tests/e2e` passed.
- `PLAYWRIGHT_SKIP_WEBSERVER=1 npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium --list` passed and listed 11 tests.
- `npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium --reporter=list` could not run browser proof because sandbox socket permissions blocked Kestrel bind with `System.Net.Sockets.SocketException (13): Permission denied`.
- `pwsh scripts/test.ps1 -Lane unit` returned process exit code 0 but repeatedly printed `Build failed with exit code: 1` after restore discovery and emitted no C# diagnostics; focused builds and compiled xUnit executables above were used as reliable evidence.

### Completion Notes List

- Replaced `/me/consent` placeholder with a real ConsumerPortal consent surface using `FluentSwitch`, accessible switch attributes, default-Off behavior, active-record On reconciliation, read-only non-consent lawful-basis rows, and Object (Art. 21) action copy.
- Added a ConsumerPortal-owned consent port/DTO surface plus a UI-host `ConsumerConsentClient` adapter that delegates to `ISelfScopedPartiesClient` consent methods and carries contact channel ids only internally for command mapping.
- Added resource-backed consent copy and token-based CSS isolation with no raw color literals or separate Consumer branding.
- Added component, static packaging, UI adapter, host registration, self-scope tripwire, and e2e route/spec-discovery coverage for consent grant/withdraw behavior and boundary constraints.
- Updated the e2e fixture so the bound Consumer has a bounded synthetic contact channel and consent reads reflect fixture add/revoke mutations.

### File List

- `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentChannel.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentGrantRequest.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentOperationOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentOperationResult.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentOverview.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerConsentWithdrawRequest.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerConsentClient.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerConsentClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerConsentClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Change Log

- 2026-06-10: Implemented Story 5.1 Consumer consent grant/withdraw surface, ConsumerPortal consent port, UI-host adapter, tests, e2e spec updates, and validation evidence.
- 2026-06-10: Senior developer review auto-fixed active-consent withdrawal when the matching contact channel is unavailable, added regression coverage, and marked the story done.

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

### Outcome

Approved after auto-fix. Story 5.1 status moved to `done`; sprint status synced to `done`.

### Findings Fixed

- [HIGH] Active consent could become non-withdrawable when the current self party no longer exposed a matching contact channel. The component disabled every switch with a missing channel id and the event handler returned before withdrawal, even though revoke only requires the active consent id. Fixed `MyConsentPage.razor` so channel ids are required only for grants, while active consent remains withdrawable by consent id. Added `MyConsentPage_ActiveConsentWithoutChannel_AllowsWithdrawByActiveConsentId`.

### Review Notes

- Acceptance Criteria 1-7 were cross-checked against the implementation surface: `/me/consent` uses `FluentSwitch` with switch semantics and `aria-describedby`, separates consent from contract/legal/legitimate-interest rows, reconciles active `ConsentRecord` state, delegates through the ConsumerPortal port and self-scoped UI adapter, keeps failure copy PII-free, and does not introduce backend/public endpoint/package/submodule changes.
- File List matches the story-relevant source and test changes reviewed. The worktree also contains pre-existing documentation/orchestration changes outside the story source surface; those were not reviewed as application implementation.
- Official documentation fallback checked: Microsoft Learn Blazor lifecycle guidance and Fluent UI Blazor switch documentation. No dependency or package update was required.

### Validation

- `git diff --check` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -parallel none -longRunning 30` passed: 54 total, 0 failed.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -parallel none -longRunning 30` passed: 297 total, 0 failed.
- `npm run typecheck` in `tests/e2e` passed.
- `PLAYWRIGHT_SKIP_WEBSERVER=1 npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium --list` passed and listed 13 tests.
- `npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium --reporter=list` could not run browser proof because sandbox socket permissions blocked Kestrel bind with `System.Net.Sockets.SocketException (13): Permission denied`.
