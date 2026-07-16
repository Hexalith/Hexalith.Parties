---
baseline_commit: b57afcdc9493690b6d0d21fd84a0f75948976389
---

# Story 4.5: Edit My Profile (FR-Consumer-2)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to correct my own data,
so that what is held about me is accurate.

## Acceptance Criteria

1. Given a signed-in, bound Consumer opens `/me/edit`, when the edit page loads, then it fetches the current user's party only through the existing ConsumerPortal profile data path, pre-fills the form with exactly the stored values used by `/me`, and never accepts or displays a caller-supplied party id.
2. Given the current party is a Person or Organization, when the edit form renders, then it exposes only editable profile fields for that party type, uses real labels and accessible descriptions, preserves Consumer roomy single-column layout, and keeps all copy in ConsumerPortal resources.
3. Given the user submits a valid correction, when the save command is issued, then the UI-host self-scoped accessor injects the resolved `party_id` and calls the validated `Update*` command path, preferably `UpdatePartyCompositeWithResultAsync` through a new no-party-id self-scoped method, without list/search, public endpoints, browser tokens, DAPR ACL changes, or direct actor-host calls.
4. Given the command is accepted, when the gateway returns an updated detail or the projection later confirms, then `/me/edit` shows the changed value optimistically, exposes exactly one polite status source such as "Saving..." or "Saved - updating", reconciles to the confirmed `ProjectionFreshnessStatus.Current` state, and never moves focus to a toast for routine success.
5. Given client-side validation fails or a `PartyCommandValidationRejected`/400/422 result is returned, when the form renders the error, then the user's typed input is preserved, the inline error is tied to the affected field via `aria-describedby`, the alert is assertive (`role="alert"`), and no simultaneous "Saved" and "Saving" status is shown.
6. Given the profile load is stale, degraded, unavailable, rebuilding, local-only, fails closed, or the self party is erased, when `/me/edit` renders, then it keeps last-known non-erased data visible where available, blocks edits for erased profiles with a PII-free tombstone, shows generic PII-free failure copy, and never falls back to list/search or arbitrary route ids.
7. Given Story 4.5 is the profile-edit story only, when files are changed, then no consent grant/withdraw, export, erasure request/cancel, processing-records workflow, identity-binding redesign, Parties aggregate/event/projection change, EventStore/Tenants/FrontComposer submodule change, package upgrade, or new third-party library is introduced.

## Tasks / Subtasks

- [x] Replace the `/me/edit` placeholder with a real edit profile page (AC: 1, 2, 4, 5, 6)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` so it no longer renders the generic `ConsumerRouteShell` placeholder.
  - [x] Load the current self party through `IConsumerProfileDataClient.GetMyPartyAsync(...)` before initializing the edit model.
  - [x] Leave the component in a valid loading/skeleton state before awaiting data, and cancel in-flight work on dispose.
  - [x] Pre-fill the edit model from `PartyDetail` without silent transformation drift: displayed view value and edit input value must match until the user types.
  - [x] Render Person fields only when `PartyDetail.Type == Person`; render Organization fields only when `PartyDetail.Type == Organization`; do not offer party type switching in the Consumer screen.
  - [x] Keep internal ids, tenant ids, sort names, raw command ids, correlation ids, event ids, and diagnostics out of visible copy.

- [x] Add the narrow ConsumerPortal edit data port and UI-host adapter (AC: 1, 3, 7)
  - [x] Extend the existing `IConsumerProfileDataClient` only if the name remains accurate, or add a sibling `IConsumerProfileEditClient` in `src/Hexalith.Parties.ConsumerPortal/Services/`.
  - [x] The ConsumerPortal port must expose caller-id-free methods only, for example `Task<ConsumerProfileUpdateResult> UpdateMyProfileAsync(ConsumerProfileUpdateRequest request, CancellationToken cancellationToken = default)`.
  - [x] Add a UI-host adapter in `src/Hexalith.Parties.UI/Services/` that implements the ConsumerPortal port and delegates only to a new self-scoped write method on `ISelfScopedPartiesClient`.
  - [x] Register the adapter as Scoped in `src/Hexalith.Parties.UI/Program.cs` after `AddSelfScopedPartiesClient()`, mirroring the current `IConsumerProfileDataClient` registration.
  - [x] Keep `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` free of references to `Hexalith.Parties.UI`, Server, Projections, Security, Testing, or internal host projects.

- [x] Extend the self-scoped accessor for profile writes without weakening its tripwires (AC: 3, 7)
  - [x] Add one no-party-id profile update method to `ISelfScopedPartiesClient`; it must not accept any parameter named or shaped like `partyId`.
  - [x] Implement it in `SelfScopedPartiesClient` by resolving the current bound party id fail-closed and injecting it into `IPartiesCommandClient.UpdatePartyCompositeWithResultAsync(...)` or the narrow `UpdatePersonDetailsWithResultAsync`/`UpdateOrganizationDetailsWithResultAsync` path.
  - [x] Add `IPartiesCommandClient` to `SelfScopedPartiesClient` constructor only if needed; preserve Scoped lifetime and lazy resolution behavior.
  - [x] Update `AddPartiesClient` composition assumptions/tests so the command client is registered only under the existing `Parties:BaseUrl` gate and no degraded/test boot is broken by eager resolution.
  - [x] Keep `ISelfScopedPartiesClient` free of list/search members, `PagedResult<>` returns, and caller-supplied party ids; update `SelfScopedPartiesClientSurfaceTests` rather than removing them.

- [x] Build the profile edit form and command mapping (AC: 1, 2, 3, 5)
  - [x] Reuse the AdminPortal `CreateEditPartyPage` validation/message-store patterns where they fit, but do not copy tenant/admin routing behavior or route party ids.
  - [x] Use `EditForm`/`EditContext` with a unique `FormName`; keep model state in the component so failed submissions preserve user input.
  - [x] Map Person updates to `PersonDetails` (`FirstName`, `LastName`, `DateOfBirth`, `Prefix`, `Suffix`) and Organization updates to `OrganizationDetails` (`LegalName`, `TradingName`, `LegalForm`, `RegistrationNumber`, `IsNaturalPerson`).
  - [x] If contact channel or identifier edits are included in scope, update existing items through `UpdateContactChannels` using existing ids from the loaded `PartyDetail`; do not convert existing records into add-only commands that duplicate email/identifier values.
  - [x] If contact/identifier editing would exceed the story safely, leave them read-only on `/me/edit` and document that choice in completion notes; do not fake support with add-only behavior.
  - [x] Trim user-entered text consistently with existing AdminPortal command builders and map server validation property names back to exact fields.

- [x] Implement status, validation, and optimistic reconcile behavior (AC: 4, 5, 6)
  - [x] Use exactly one visible status source per save operation; choose either an inline status region or one toast, not both.
  - [x] Render accepted-processing status through `role="status" aria-live="polite"` and leave focus on the active field/button.
  - [x] Render validation and blocking failures through a focusable `role="alert"` and set `aria-describedby` on invalid controls.
  - [x] Preserve typed values after client validation failure, server validation rejection, transient failure, and failed reconcile.
  - [x] Reuse `OptimisticReconcile` from the UI host where it can be consumed without reversing dependencies; otherwise keep the RCL port/adaptor boundary clean and implement a minimal caller-side flow that delegates the actual optimistic/reconcile orchestration to the host adapter.
  - [x] On accepted command result with `PartyDetail`, normalize/update local page state from the returned detail; then re-read/reconcile until `Freshness == Current` or show polite degraded state.

- [x] Handle stale, failed, and erased states safely (AC: 5, 6)
  - [x] Keep last-known non-erased profile content visible for stale/degraded freshness and show the existing `FreshnessStatus` behavior or equivalent.
  - [x] For erased self-party, block the form and show PII-free tombstone copy only; do not render display name, person details, organization details, contact channels, identifiers, name history, party id, or tenant id.
  - [x] For load or save failure, show generic localized copy; never include exception text, party id, tenant, claim values, display name, contact values, identifiers, command payloads, or correlation ids.
  - [x] Do not inject `ILogger`, `Console`, `Debug`, `ActivitySource`, or metrics APIs into the edit page unless tests prove no PII can be written.

- [x] Update resources and styles through existing ConsumerPortal conventions (AC: 2, 4, 5, 6)
  - [x] Add edit labels, validation text, save/cancel/status text, stale/degraded text, and erased/failure text to `Resources/ConsumerPortalResources.resx`.
  - [x] Expose resource entries through `Services/ConsumerPortalLabels.cs`; do not inline regulated or user-facing copy in `.razor` files.
  - [x] Add CSS isolation under `Components/EditMyProfilePage.razor.css` if needed, using Fluent/FrontComposer tokens and system colors only.
  - [x] Preserve Consumer 16px body, roomy density, narrow single-column measure, focus rings, forced-colors compatibility, and reduced-motion behavior.

- [x] Add focused bUnit, composition, and static guard tests (AC: 1-7)
  - [x] Add `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs` covering loading, prefill parity for Person, prefill parity for Organization, valid submit, client validation, server validation rejection, accepted optimistic status, stale/degraded display, erased tombstone, failure alert, retry, and preserved input.
  - [x] Use xUnit v3, bUnit, Shouldly, and NSubstitute; do not introduce Moq, FluentAssertions, raw `Assert.*`, or csproj package versions.
  - [x] Add a fake/stub ConsumerPortal edit client in tests; assert no caller-supplied party id is needed.
  - [x] Update `ConsumerPortalPackagingTests` so ConsumerPortal may use only the approved profile edit data port and remains forbidden from `ISelfScopedPartiesClient`, `IPartiesQueryClient`, `IAdminPortalGdprClient`, `ListPartiesAsync`, `SearchPartiesAsync`, direct `GetPartyAsync(`, and `Hexalith.Parties.UI`.
  - [x] Update `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` and composition tests to prove the new write method still accepts no party id and delegates through the resolved-id self-scoped command path.
  - [x] Add UI-host adapter tests proving Scoped registration and delegation to the new `ISelfScopedPartiesClient` write method.
  - [x] Add source guards for one-status-source behavior and for no PII logging/telemetry in the edit page.
  - [x] Update `tests/e2e/specs/consumer-portal-routes.spec.ts` and `consumer-party-binding.spec.ts` expectations from setup placeholder copy to real `/me/edit` behavior where fixtures support it.

- [x] Validate the focused implementation (AC: 1-7)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run compiled focused xUnit v3 executables for ConsumerPortal tests and relevant UI host/self-scope tests when available.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record exact wrapper/build/MTP limitations instead of claiming success.

## Dev Notes

### Current Implementation State

- Sprint status has Epic 4 in progress, Stories 4.1-4.4 done, and `4-5-edit-my-profile-fr-consumer-2` in backlog before this story file was created. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 4.5 covers FR-Consumer-2: Consumer edit profile, validated correction, command accepted/validation states, and optimistic reconcile. [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.5-Edit-my-profile-FR-Consumer-2`]
- `/me/edit` currently renders a placeholder `ConsumerRouteShell` with no data load, no form, no command, and no status/validation model. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`]
- `/me` is now a real read-only profile page. It uses `IConsumerProfileDataClient.GetMyPartyAsync(...)`, renders `PartyDetail`, maps freshness through `FreshnessStatus`, handles erased profiles with a PII-free tombstone, and avoids internal identifiers. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`] [Source: `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor`]
- The existing ConsumerPortal data port is read-only and caller-id-free. Do not make ConsumerPortal reference `Hexalith.Parties.UI` to reach host services. [Source: `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileDataClient.cs`] [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- The UI-host adapter `ConsumerProfileDataClient` delegates only to `ISelfScopedPartiesClient.GetMyPartyAsync(...)`, and Program registers it after `AddSelfScopedPartiesClient()`. [Source: `src/Hexalith.Parties.UI/Services/ConsumerProfileDataClient.cs`] [Source: `src/Hexalith.Parties.UI/Program.cs`]
- `ISelfScopedPartiesClient` currently states profile write is intentionally absent and belongs to Story 4.5. This is the intended extension point. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor` (UPDATE)
  - Current state: `/me/edit`, `[Authorize(Policy = "Consumer")]`, placeholder shell with two next-step labels.
  - What this story changes: real load, form, validation, save, optimistic/reconcile, stale/failure/erased states.
  - Preserve: route, Consumer policy, Consumer area resource-backed copy.
- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor` (READ/possibly UPDATE)
  - Current state: read-only profile page and safest source for display-to-edit prefill parity.
  - What this story may change: extract shared formatting/model helpers only if it avoids real duplication without expanding scope.
  - Preserve: erased tombstone PII suppression, freshness behavior, no logging/telemetry.
- `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor` and `ProfileField.razor` (READ/possibly UPDATE)
  - Current state: RCL-local safe display components.
  - What this story may change: reuse from edit page; avoid depending on UI-host shared components.
  - Preserve: status dot plus word; polite status live region.
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileDataClient.cs` (UPDATE or add sibling port)
  - Current state: caller-id-free read port with `GetMyPartyAsync`.
  - What this story changes: add a caller-id-free edit operation or add a separate edit port.
  - Preserve: no `partyId`, no `GetPartyAsync`, no list/search/direct gateway client exposure.
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs` and `Resources/ConsumerPortalResources.resx` (UPDATE)
  - Current state: resource-backed placeholder and profile copy.
  - What this story changes: edit form labels, validation, one status source, failure, erased, save/retry/cancel copy.
  - Preserve: regulated/user-facing copy centralized and auditable.
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` and `SelfScopedPartiesClient.cs` (UPDATE)
  - Current state: no-party-id own-data read/GDPR accessor; no list/search; profile writes intentionally absent.
  - What this story changes: add a no-party-id profile update method and implementation resolving the bound id fail-closed.
  - Preserve: no list/search members, no `PagedResult<>`, no caller-supplied party ids, generic fail-closed errors, Scoped lifetime.
- `src/Hexalith.Parties.UI/Services/ConsumerProfileDataClient.cs` or new adapter (UPDATE/NEW)
  - Current state: read adapter.
  - What this story changes: delegate edit operation from ConsumerPortal port to the self-scoped accessor.
  - Preserve: no business rules in adapter; no PII logging.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers self-scoped accessor, read adapter, identity binding, projection freshness, and typed clients behind the existing gates.
  - What this story changes: register any new edit adapter as Scoped after `AddSelfScopedPartiesClient()`.
  - Preserve: `ValidateScopes=true`, no browser tokens, no DAPR sidecar, no eager resolution in degraded/test boot.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs` (UPDATE)
  - Current state: forbids future data workflows and direct host/gateway references; allows `IConsumerProfileDataClient`.
  - What this story changes: allow only the approved edit port/surface.
  - Preserve: source scans exclude `bin/` and `obj/`.
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` and composition tests (UPDATE)
  - Current state: pins no list/search/no party-id surface.
  - What this story changes: add assertions for the new profile write method and command-client delegation.
  - Preserve: tripwire intent.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It must not reference `Hexalith.Parties.UI` to consume `ISelfScopedPartiesClient`, `OptimisticReconcile`, shared UI components, or host-owned status types. Use a ConsumerPortal-owned port with a UI-host adapter. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- Consumer data access is own-data-only. The UI BFF resolves the Consumer's `party_id` and injects it; ConsumerPortal must never accept route ids, query ids, hidden form party ids, or arbitrary ids. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- The browser talks only to the UI host. Do not add public ConsumerPortal APIs, direct browser calls to EventStore, direct actor-host calls, DAPR ACLs, or browser bearer-token flows. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- Use the single status mapping and live-region split: accepted/processing and freshness are polite; validation/failures are assertive alerts. Do not create a second mapping for `/me/edit`. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- `OptimisticReconcile` already implements optimistic apply, polite saving announce, command issue, SignalR/polling reconcile, rejection revert, and one-shot duplicate-confirm guard. Reuse it through a valid dependency direction or keep the same behavior via the host adapter. [Source: `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs`]
- Projection freshness metadata is the primary degraded signal. Treat `DegradedResponseHeaderHandler` as secondary because it captures headers in an HTTP handler scope, not necessarily the active Blazor circuit. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Use Central Package Management. Do not add package versions to csproj files and do not bump packages for this story. [Source: `Directory.Packages.props`] [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Command and Validation Guidance

- Available typed client methods include `UpdatePersonDetailsWithResultAsync`, `UpdateOrganizationDetailsWithResultAsync`, `UpdateContactChannelWithResultAsync`, and `UpdatePartyCompositeWithResultAsync`. The self-scoped accessor should inject the resolved id into one of these methods; ConsumerPortal must not call `IPartiesCommandClient` directly. [Source: `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`]
- `UpdatePartyComposite` can update `PersonDetails` or `OrganizationDetails`, add contact channels/identifiers, update contact channels, and remove contact/identifier ids. It cannot update an existing identifier value except remove/add. Avoid accidental duplication. [Source: `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`]
- `UpdatePartyCompositeValidator` validates `PartyId`, sub-operation count, add/update contact channel fields, add identifier fields, and remove identifier ids. Server validation rejection appears as `PartyCommandValidationRejected` and is mapped by client surfaces to 400/422 validation outcomes. [Source: `src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs`] [Source: `src/Hexalith.Parties.Contracts/Events/PartyCommandValidationRejected.cs`]
- AdminPortal's `CreateEditPartyPage` has useful patterns for `EditContext`, `ValidationMessageStore`, server-property mapping, safe generic validation messages, and focusable alert behavior. Reuse the pattern, but do not reuse its admin route-id command path for Consumer. [Source: `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`]
- AdminPortal maps `PartiesClientException` status 400/422 to `AdminPortalCommandOutcome.ValidationRejected`. The Consumer edit adapter/result should expose an equivalent validation outcome without leaking raw exception details or PII. [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs#MapCommandOutcome`]

### Data and Rendering Guidance

- `PartyDetail` carries personal values in `DisplayName`, `SortName`, `PersonDetails`, `ContactChannels.Value`, `PartyIdentifier.Value`, and `NameHistory`. Render/edit only intended profile fields and never log these values. [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`]
- `PersonDetails` fields are `FirstName`, `LastName`, `DateOfBirth`, `Prefix`, and `Suffix`. Consumer edit should preserve nullable prefix/suffix/date semantics and not invent derived display-name fields. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/PersonDetails.cs`]
- `OrganizationDetails` fields are `LegalName`, `TradingName`, `LegalForm`, `RegistrationNumber`, and `IsNaturalPerson`. Render/edit only when the loaded party is Organization. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/OrganizationDetails.cs`]
- Freshness statuses are `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`. Only `Current` means "Up to date"; all non-current states should keep content visible with last-known/degraded copy where possible. [Source: `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs`]
- The regulated-language review explicitly calls out view/edit drift in the profile email mock as a defect. Make parity a test: the prefilled value must be identical to the stored/rendered value until the user edits it. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]

### UX and Accessibility Guardrails

- Consumer pages are phone-first, single-column, roomy, 16px body text, and one Fluent family with the Admin area. Do not make a marketing/landing page or a second visual brand. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Key-Flows`] [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Typography`]
- Cold load uses a skeleton or calm loading state, never spinner-only. Stale reads render last-known content and announce freshness politely. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Validation rejected states must be inline, assertive via `role="alert"`, tied to affected fields, and must preserve typed input. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Accepted-but-processing states should optimistically echo the changed value and reconcile silently on projection confirm. Do not move focus to a toast on routine save. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Interaction-Primitives`]
- The regulated-language review forbids conflicting status sources on the edit profile screen. This story must show exactly one save status source at a time. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Use FluentUI/FrontComposer tokens; no raw hex/rgb/hsl and no raw `#0097A7` for text or button fill. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Colors`]

### Previous Story Intelligence

- Story 4.4 added `IConsumerProfileDataClient`, `ConsumerProfileDataClient`, `FreshnessStatus`, `ProfileField`, real `/me` rendering, focused bUnit tests, packaging guards, and UI-host composition tests. Build on these instead of creating a parallel read path. [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md#Completion-Notes-List`]
- Story 4.4 corrected the epic wording from `GetPartyAsync(myPartyId)` to the actual no-id `GetMyPartyAsync()` code contract. Keep that no-id contract for writes. [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md#Current-Implementation-State`]
- Story 4.4 found `dotnet test`/MTP wrapper failures despite compiled xUnit executables passing. Continue to run focused builds and compiled test executables when `dotnet test` is unreliable, and record exact failures. [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md#Debug-Log-References`]
- Recent commits are sequential and scoped: Story 4.1 ADR, Story 4.2 binding provisioning, Story 4.3 ConsumerPortal stand-up, Story 4.4 read-only profile. Keep Story 4.5 focused on edit profile. [Source: `git log -5`]

### Testing and Validation Guidance

- Required ConsumerPortal component tests:
  - `/me/edit` loading state renders valid skeleton/status before data resolves.
  - Person profile pre-fills first name, last name, date of birth, prefix, and suffix from the loaded `PartyDetail` with no view/edit drift.
  - Organization profile pre-fills legal name, trading name, legal form, registration number, and natural-person flag from the loaded `PartyDetail`.
  - Client validation blocks submit, preserves input, sets field messages, and renders exactly one `role="alert"`.
  - Server validation rejection maps property names to fields, preserves typed values, and clears accepted-processing status.
  - Accepted save optimistically shows the changed value and one polite status source.
  - Stale/degraded/null freshness keeps last-known content visible.
  - Erased profile renders tombstone and no fixture PII values.
  - Load/save failure renders generic PII-free alert and retry where applicable.
- Required boundary/static tests:
  - ConsumerPortal has no reference to `Hexalith.Parties.UI`.
  - ConsumerPortal does not contain `ListPartiesAsync`, `SearchPartiesAsync`, `IPartiesQueryClient`, `IAdminPortalGdprClient`, direct `GetPartyAsync(`, route/caller-supplied party id parameters in profile ports, or raw gateway command clients.
  - `ISelfScopedPartiesClient` still exposes no list/search members, no `PagedResult<>`, and no party id parameters after adding profile write.
  - UI host registers the edit adapter as Scoped and after `AddSelfScopedPartiesClient()`.
  - Adapter delegates to the self-scoped write method and never accepts a caller-supplied id.
  - Edit page source does not use logging/telemetry APIs for profile values.
- Required E2E updates:
  - Replace `/me/edit` placeholder status expectations.
  - Keep unbound Consumer fail-closed tests proving `NoPartyBinding` wins and no edit page data appears.
- Validation commands should mirror Story 4.4's focused build/test approach. Do not weaken warnings-as-errors or use `-p:TreatWarningsAsErrors=false`. [Source: `_bmad-output/project-context.md#Development-Workflow-Rules`]

### Latest Technical Information

- Microsoft Learn's current Blazor forms guidance says `EditForm` binds to a model/EditContext, should use a unique `FormName`, and exposes `OnSubmit`, `OnValidSubmit`, and `OnInvalidSubmit`; `OnSubmit` can call `EditContext.Validate()` when manual validation/message-store mapping is needed. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor lifecycle guidance says an async lifecycle method must leave the component in a valid render state before awaiting incomplete work. Apply this to the edit page load before awaiting `GetMyPartyAsync(...)`. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor DI guidance supports normal service injection from the app service collection. Keep ConsumerPortal ports and UI-host adapters Scoped to remain compatible with the self-scoped accessor and `ValidateScopes=true`. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET 10 SDK `10.0.302`, FluentUI Blazor `5.0.0-rc.3-26138.1`, xUnit v3, Shouldly, NSubstitute, and bUnit. [Source: `global.json`] [Source: `Directory.Packages.props`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileEditClient.cs` or extension of `IConsumerProfileDataClient`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileUpdateRequest.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileUpdateResult.cs`
  - `src/Hexalith.Parties.UI/Services/ConsumerProfileEditClient.cs` if a sibling edit adapter is chosen
  - `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor.css`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerProfileEditClientTests.cs`
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
  - `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
  - `tests/e2e/specs/consumer-party-binding.spec.ts`
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
- Keep generated/build artifacts out of the repo. Do not edit or commit `bin/`, `obj/`, or `obj/**/generated/**`.

### Out of Scope

- No consent grant/withdraw UI or commands; Epic 5 owns FR-Consumer-3.
- No data export, erasure request/cancel, or processing-records UI; Epic 5 owns FR-Consumer-4.
- No identity binding redesign, self-registration, IdP federation, binding-store changes, or Keycloak mapper changes.
- No Parties aggregate/event/projection/actor changes.
- No public UI API endpoint, EventStore route, DAPR ACL, direct actor-host call, browser token flow, or deployment/KMS change.
- No package upgrades and no new third-party libraries.
- No broad shared UI package refactor unless it is strictly required to keep dependency direction valid.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.5-Edit-my-profile-FR-Consumer-2`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileDataClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs`]
- [Source: `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`]
- [Source: `src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`]
- [Source: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]

## Validation Summary

- Source discovery loaded the BMAD create-story skill, discovery protocol, template, checklist, BMad config, sprint status, project context, planning epics, architecture, UX experience/design/review files, Story 4.4, current ConsumerPortal source/tests, UI self-scope source/tests, command client/DTO/validator surfaces, AdminPortal edit patterns, optimistic reconcile primitive/tests, recent git history, and current Microsoft Blazor docs.
- Checklist fixes applied before finalizing: corrected the epic's generic `Update*` wording into a no-party-id self-scoped write path, made ConsumerPortal-to-UI dependency boundaries explicit, required view/edit prefill parity tests, required one status source, prevented add-only duplication for existing contacts/identifiers, pinned validation rejection accessibility, and preserved self-scope/static guard tripwires.
- Latest-technology review found no dependency upgrade requirement; implementation should use normal Blazor `EditForm`/`EditContext`, lifecycle, and DI patterns on the pinned local stack.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: `git diff --check` passed.
- 2026-06-10: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-10: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-10: `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: 44 total, 0 failed, 0 skipped.
- 2026-06-10: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: 292 total, 0 failed, 0 skipped.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` was attempted; wrapper output repeatedly reported `Build failed with exit code: 1.` after `Determining projects to restore...`, while the wrapper process returned exit code 0. Focused builds and compiled xUnit executables above were used as the reliable validation evidence.
- 2026-06-10: Senior Developer Review auto-fix pass ran `git diff --check`; passed.
- 2026-06-10: Senior Developer Review auto-fix pass ran `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`; passed with 0 warnings and 0 errors.
- 2026-06-10: Senior Developer Review auto-fix pass ran `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests`; passed: 44 total, 0 failed, 0 skipped.
- 2026-06-10: Senior Developer Review auto-fix pass ran `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`; passed with 0 warnings and 0 errors.
- 2026-06-10: Senior Developer Review auto-fix pass ran `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests`; passed: 292 total, 0 failed, 0 skipped.

### Completion Notes List

- Replaced `/me/edit` placeholder with a real ConsumerPortal edit profile page that loads only through `IConsumerProfileDataClient.GetMyPartyAsync`, keeps loading/failure/erased states safe, and never accepts a caller-supplied profile id.
- Added a sibling ConsumerPortal edit port (`IConsumerProfileEditClient`) plus request/result DTOs. The UI-host adapter delegates to a new `ISelfScopedPartiesClient.UpdateMyProfileAsync` method using a no-party-id request shape.
- Extended `SelfScopedPartiesClient` to resolve the bound party id fail-closed and inject it into `IPartiesCommandClient.UpdatePartyCompositeWithResultAsync`.
- Built a type-specific `EditForm`/`EditContext` form for Person and Organization details with resource-backed labels, inline validation, assertive alerts, and a single polite save status source.
- Left contact channels and identifiers read-only on `/me/edit`; updating them safely would require existing-id update semantics beyond this focused profile-details story, so the implementation avoids add-only duplication behavior.
- Added focused bUnit tests, ConsumerPortal packaging/source guards, UI host adapter tests, self-scoped surface/composition tests, and E2E route expectation updates for the real `/me/edit` page.

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-06-10

Outcome: approved after auto-fix. No critical issues remain.

Findings fixed:

- [HIGH] Thrown save failures from the edit port could escape `SubmitAsync`, bypassing the generic PII-free save alert and typed-input preservation path. Fixed by catching non-cancellation save exceptions and rendering the same safe failure state as returned failed outcomes.
- [MEDIUM] Field validation messages and the summary message both used `role="alert"`, creating multiple simultaneous assertive alert sources. Fixed by keeping field messages tied via `aria-describedby` and reserving `role="alert"` for the summary.
- [MEDIUM] The Organization natural-person checkbox did not use the same accessible description/error id pattern as the other editable controls, and the Cancel button inside the form did not explicitly declare non-submit behavior. Fixed by adding described-field attributes for the checkbox and `Type="ButtonType.Button"` for Cancel.
- [MEDIUM] `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` was changed for the consumer edit E2E save/reconcile path but omitted from the story File List. Fixed by documenting it in the File List.
- [LOW] The edit-page freshness tests covered stale/degraded only, while AC6 explicitly lists rebuilding, unavailable, and local-only. Fixed by extending the test matrix to all non-current freshness states.

### File List

- `_bmad-output/implementation-artifacts/4-5-edit-my-profile-fr-consumer-2.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileUpdateOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileUpdateRequest.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileUpdateResult.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerProfileValidationFailure.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileEditClient.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerProfileEditClient.cs`
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedProfileUpdateRequest.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/EditMyProfilePageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerProfileEditClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Change Log

- 2026-06-10: Implemented Story 4.5 edit profile flow, self-scoped write adapter, resource-backed UI, focused tests, static guards, validation, and sprint/story status updates.
- 2026-06-10: Senior Developer Review auto-fixed save exception handling, one-alert validation semantics, checkbox description coverage, cancel button type, non-current freshness tests, File List completeness, and marked the story done.
