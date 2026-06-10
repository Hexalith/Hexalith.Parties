---
baseline_commit: 06b4884f8c8d341cceaa1ada4158d066d8c33b7c
---

# Story 4.4: My Profile (FR-Consumer-1)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to see my own personal data and how fresh it is,
so that I know what is held about me.

## Acceptance Criteria

1. Given a signed-in, bound Consumer, when they open `/me`, then `MyProfilePage` loads the current user's party only through the self-scoped data path, renders a calm roomy profile layout, and shows a freshness dot with the text "Up to date" when `ProjectionFreshnessStatus.Current`.
2. Given the current implementation has `ISelfScopedPartiesClient.GetMyPartyAsync()` in the UI host, when ConsumerPortal needs profile data, then the RCL must not reference `Hexalith.Parties.UI`; it must use a ConsumerPortal-owned narrow profile data port that the UI host registers as an adapter to `ISelfScopedPartiesClient.GetMyPartyAsync()`, or explicitly promote the interface into an adopter-facing shared package with tests.
3. Given the profile read is stale, degraded, unavailable, rebuilding, local-only, or has null freshness metadata, when `/me` renders, then it keeps the last-known profile content visible where available, shows a polite freshness cue, and never blanks or throws during render.
4. Given the loaded self party is erased, when `/me` renders, then it shows a PII-free tombstone and status/freshness context only; it must not render display name, person details, organization details, contact channels, identifiers, name history, or raw party id.
5. Given a profile load fails or the self binding unexpectedly fails closed, when `/me` renders the error state, then the message is generic, uses `role="alert"` for blocking failure, keeps PII out of copy/logs/telemetry, and never falls back to list/search or caller-supplied party ids.
6. Given Story 4.4 is a read-only profile story, when files are changed, then no profile edit commands, consent changes, export, erasure request/cancel, processing-records workflow, public API endpoint, DAPR ACL, EventStore route, or browser token flow is introduced.

## Tasks / Subtasks

- [x] Replace the `/me` placeholder with a real read-only profile experience (AC: 1, 3, 4)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor` so it no longer renders the generic setup-shell "not loaded" state.
  - [x] Render a phone-first, single-column, roomy Consumer profile layout using Fluent/FrontComposer tokens only.
  - [x] Show profile sections from `PartyDetail`: display name; person details when `Type == Person`; organization details when `Type == Organization`; contact channels; identifiers; lifecycle/restriction state; created/modified dates where useful.
  - [x] Do not render scoped party id, tenant id, sort name, raw event identifiers, diagnostics, or internal aggregate identity as user-facing copy.
  - [x] Use localized/resource-backed labels through `ConsumerPortalLabels` / `.resx`; do not inline regulated or user-facing copy in the component body except resource keys and structural text.

- [x] Add the ConsumerPortal profile data port without reversing project dependencies (AC: 1, 2, 5)
  - [x] Add a ConsumerPortal-owned interface such as `IConsumerProfileDataClient` in `src/Hexalith.Parties.ConsumerPortal/Services/` with a single method shaped like `Task<PartyDetail> GetMyPartyAsync(CancellationToken cancellationToken = default)`.
  - [x] Add a UI-host adapter such as `ConsumerProfileDataClient` in `src/Hexalith.Parties.UI/Services/` that implements the ConsumerPortal interface and delegates only to `ISelfScopedPartiesClient.GetMyPartyAsync(...)`.
  - [x] Register the adapter in `src/Hexalith.Parties.UI/Program.cs` as Scoped, after the existing self-scoped client registration.
  - [x] Keep `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` free of any reference to `Hexalith.Parties.UI`.
  - [x] Do not call `IPartiesQueryClient`, `IAdminPortalGdprClient`, `ListPartiesAsync`, `SearchPartiesAsync`, or any method accepting caller-supplied `partyId` from ConsumerPortal.

- [x] Implement load, stale/degraded, and failure states safely (AC: 1, 3, 5)
  - [x] Use `OnInitializedAsync` or an equivalent component lifecycle hook to request the profile once for `/me`; leave the component in a valid loading state before awaiting.
  - [x] Use a component-level cancellation token and dispose/cancel it so in-flight loads do not paint after the component is gone.
  - [x] Render a skeleton/calm loading state, not a spinner-only page.
  - [x] Map `PartyDetail.Freshness` to visible freshness text: `Current` -> "Up to date"; `Stale` -> "Showing what we last knew - refreshing"; other non-current statuses or null -> "Showing last known".
  - [x] Freshness text must be a polite live region (`role="status" aria-live="polite"`) with a decorative dot (`aria-hidden="true"`), never color-only.
  - [x] For blocking load failure, render a generic `role="alert"` message and a retry control; do not include exception text, party id, tenant, claims, contact values, or display name in the error.

- [x] Handle erased self-party without leaking PII (AC: 3, 4, 5)
  - [x] If `PartyDetail.IsErased` is true, short-circuit normal profile sections.
  - [x] Show tombstone copy such as "This profile was deleted" from resources, plus the freshness indicator where available.
  - [x] Do not render `DisplayName`, `SortName`, `PersonDetails`, `OrganizationDetails`, `ContactChannels`, `Identifiers`, `ConsentRecords`, or `NameHistory` for erased self.
  - [x] Ensure tombstone state uses neutral/info posture, not celebratory success-green copy.

- [x] Update copy and styles through existing ConsumerPortal conventions (AC: 1, 3, 4, 5)
  - [x] Add profile labels to `Resources/ConsumerPortalResources.resx` and expose them via `Services/ConsumerPortalLabels.cs`.
  - [x] Add or update CSS isolation under `Components/*.razor.css` using tokens and system colors only; no raw hex/rgb/hsl and no raw `#0097A7`.
  - [x] Preserve the existing Consumer area 16px body text, single-column measure, and accessible focus inheritance from the shell.

- [x] Add focused bUnit and static tests (AC: 1-6)
  - [x] Update `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs` or add `MyProfilePageTests.cs` to cover loading, successful current profile, stale/degraded/null freshness, erased tombstone, failure alert, and retry.
  - [x] Use xUnit v3, bUnit, Shouldly, and NSubstitute; do not introduce Moq, FluentAssertions, raw `Assert.*`, or csproj package versions.
  - [x] Add a fake/stub `IConsumerProfileDataClient` in tests; assert `MyProfilePage` calls the ConsumerPortal data port exactly as needed and never needs a caller-supplied party id.
  - [x] Update packaging/static guards so ConsumerPortal is allowed to use the new profile data port but still forbidden from `ListPartiesAsync`, `SearchPartiesAsync`, `IPartiesQueryClient`, `IAdminPortalGdprClient`, `GetPartyAsync(`, and `Hexalith.Parties.UI` references.
  - [x] Add UI-host composition tests proving the adapter registration delegates to the host-owned `ISelfScopedPartiesClient.GetMyPartyAsync()` and remains Scoped.
  - [x] Add source guards that profile page code does not inject/use `ILogger`, `Console`, `Debug`, or telemetry APIs for profile read values unless the log assertion proves no PII values are written.
  - [x] Update E2E expectations in `tests/e2e/specs/consumer-portal-routes.spec.ts` and `consumer-party-binding.spec.ts` from the setup status copy to the real profile state where local fixtures can support it.

- [x] Validate the focused implementation (AC: 1-6)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run compiled focused xUnit v3 executables for ConsumerPortal tests and relevant UI host/self-scope tests when available.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record socket/restore/DAPR limitations exactly instead of claiming success.

## Dev Notes

### Current Implementation State

- `4-4-my-profile-fr-consumer-1` is the first backlog story after Story 4.3. Sprint status currently has Epic 4 in progress and Stories 4.1-4.3 done. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 4.3 created `Hexalith.Parties.ConsumerPortal` as an adopter-facing RCL with protected `/me`, `/me/edit`, `/me/consent`, and `/me/privacy` route shells. `/me` currently renders placeholder copy and no data. [Source: `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md#Completion-Notes-List`]
- The current `/me` component is `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`. It uses `ConsumerRouteShell` with setup labels and does not fetch data. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`]
- The current resource wrapper is `ConsumerPortalLabels`; add keys there rather than inlining UI copy. [Source: `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`] [Source: `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`]
- The self-scoped data accessor already exists in the UI host and is intentionally caller-id-free: `ISelfScopedPartiesClient.GetMyPartyAsync()` delegates to `IPartiesQueryClient.GetPartyAsync(resolvedPartyId, ct)` after fail-closed claim resolution. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- The Epic text says `ISelfScopedPartiesClient.GetPartyAsync(myPartyId)`, but the actual implemented contract is `GetMyPartyAsync()` with no party id parameter. Follow the code contract; that is how the AC's own-data-only intent is enforced. [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.4-My-profile-FR-Consumer-1`] [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor` (UPDATE)
  - Current state: route-level `[Authorize(Policy = "Consumer")]`; placeholder route shell; no data fetch.
  - What this story changes: real profile load/render/failure/tombstone states.
  - Preserve: `/me` route and Consumer policy; no edit, consent, privacy commands.
- `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor` and `.razor.css` (UPDATE only if reused)
  - Current state: generic heading/status/next-steps shell used by all Consumer placeholder pages.
  - What this story may change: either leave it for future placeholder pages or extend carefully without breaking `/me/edit`, `/me/consent`, `/me/privacy`.
  - Preserve: 16px Consumer posture, token-only styling, polite status semantics.
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs` and `Resources/ConsumerPortalResources.resx` (UPDATE)
  - Current state: resource-backed setup copy.
  - What this story changes: add labels for profile sections, loading, retry, failure, freshness, erased tombstone, empty contact/identifier states.
  - Preserve: regulated copy stays centralized and auditable.
- `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` (UPDATE only if needed)
  - Current state: references Client, Contracts, FrontComposer Shell, FluentUI, and ASP.NET Core framework; no UI host reference.
  - Preserve: no `Version=` attributes; no `Hexalith.Parties.UI`, Server, Projections, Security, or Testing references.
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` and `SelfScopedPartiesClient.cs` (READ; UPDATE only if intentionally promoting/renaming)
  - Current state: host-owned self-scope interface/implementation; Scoped; no list/search; no party id parameters; generic fail-closed exception.
  - What this story likely changes: no direct change; add an adapter beside it that implements the ConsumerPortal-owned data port.
  - Preserve: fail-closed resolution, no PII logging, no caller-supplied party id.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers ConsumerPortal assembly discovery and self-scoped client services.
  - What this story changes: register the ConsumerPortal profile adapter as Scoped.
  - Preserve: `ValidateScopes=true`, server-side token rule, no DAPR sidecar, no browser bearer tokens.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs` and `Packaging/ConsumerPortalPackagingTests.cs` (UPDATE)
  - Current state: route shell copy tests and static guard forbidding Story 4.3 future data workflows including `ISelfScopedPartiesClient` and `GetPartyAsync`.
  - What this story changes: replace placeholder assertions and adjust guard to allow only the new ConsumerPortal profile data port while still forbidding list/search/direct gateway clients.
  - Preserve: xUnit v3, Shouldly, bUnit style; source scans exclude `bin/` and `obj/`.
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs` and `SelfScopedPartiesClientSurfaceTests.cs` (READ; UPDATE if adapter tests are placed nearby)
  - Current state: pins the self-scope accessor's resolved-id injection and no list/search/no party-id surface.
  - Preserve: every Consumer read must still flow through this accessor at the host boundary.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It must not reference `Hexalith.Parties.UI` to consume `ISelfScopedPartiesClient`, `DataFreshnessIndicator`, `PartyStateBadge`, `StatusKind`, or any host-owned type. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- The browser talks only to the UI host. Do not add public ConsumerPortal endpoints, direct browser calls to EventStore, actor-host endpoints, or DAPR ACL changes. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- Consumer data access is own-data-only. For this story, ConsumerPortal must call only the new profile data port, and the UI adapter must delegate only to `ISelfScopedPartiesClient.GetMyPartyAsync()`. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- RCL status/freshness sharing is a known architectural gap from Epic 1: host-owned primitives cannot simply be consumed from RCLs. For Story 4.4, keep the dependency direction valid. If you create ConsumerPortal-local freshness markup, pin it to the same behavior/tests as the host component and do not broaden the story into a shared component package refactor. [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- `PartyDetail.Freshness` is the primary degraded signal. Treat `DegradedResponseHeaderHandler` as secondary because it records data in an HTTP handler scope, not necessarily the active Blazor circuit scope. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Use Central Package Management. Do not add package versions to csproj files and do not bump packages for this story. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Data and Rendering Guidance

- `PartyDetail` includes `[PersonalData]` values: `DisplayName`, `SortName`, `PersonDetails`, `ContactChannels.Value`, `PartyIdentifier.Value`, and `NameHistory`. Render only normal, visible profile fields for non-erased profiles; never log these values. [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`]
- `PersonDetails` fields are `FirstName`, `LastName`, `DateOfBirth`, `Prefix`, and `Suffix`. `DateOfBirth` is personal data; display only as profile content and never in logs/errors. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/PersonDetails.cs`]
- `OrganizationDetails` fields are `LegalName`, `TradingName`, `LegalForm`, `RegistrationNumber`, and `IsNaturalPerson`. Render only when the party type is Organization and data is not erased. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/OrganizationDetails.cs`]
- `ContactChannel` and `PartyIdentifier` expose personal values. Render as profile content with labels and empty states; do not include values in telemetry, thrown messages, test failure messages beyond fixture-only test data, or URL/query strings. [Source: `src/Hexalith.Parties.Contracts/ValueObjects/ContactChannel.cs`] [Source: `src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs`]
- Freshness statuses are `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`. `Current` is the only "Up to date" case; all others keep content visible with last-known/degraded copy. [Source: `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs`]

### UX and Accessibility Guardrails

- Consumer `/me` is phone-first, single-column, roomy, and 16px body text. It is the actual application surface, not a landing page or marketing page. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Flow-1--Marc-checks-whats-held-about-him`] [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Typography`]
- Freshness is dot plus word, never color alone, and the text node is `role="status" aria-live="polite"`. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns`]
- Stale reads render last-known content and a quiet "refreshing" cue. Do not blank, throw, or show alarming stale/projection jargon. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Erased/Gone renders a tombstone with no personal fields. Links resolve gracefully and copy must be PII-free. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Validation/failure states use `role="alert"`; status/freshness/loading use polite status. Never blanket-polite all messages. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]
- Use FluentUI/FrontComposer tokens. Do not use raw teal `#0097A7` for text/button fill and do not introduce a separate Consumer visual brand. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Colors`]

### Previous Story Intelligence

- Story 4.3 intentionally did not fetch profile data, call the self-scoped accessor, or issue GDPR commands; its static test currently forbids those future workflows. Story 4.4 must update that guard narrowly instead of deleting it. [Source: `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md#Tasks--Subtasks`] [Source: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`]
- Story 4.3's senior review fixed source scans to exclude `bin/` and `obj/`. Preserve that pattern for any new source/static guards. [Source: `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md#Senior-Developer-Review-AI`]
- Story 4.2 delivered identity-binding provisioning and bound/unbound Consumer routing. Do not refactor that work for Story 4.4; `/me` should assume bound users have reached it through the existing role landing flow. [Source: `_bmad-output/implementation-artifacts/4-2-implement-admin-link-party-id-binding-provisioning.md#Completion-Notes-List`]
- Recent commits are sequential and scoped: Story 4.1 ADR, Story 4.2 binding provisioning, Story 4.3 ConsumerPortal stand-up. Keep Story 4.4 focused on My Profile read-only behavior. [Source: `git log -5`]

### Testing and Validation Guidance

- Required ConsumerPortal tests:
  - Successful current profile renders heading, profile sections, data values, and "Up to date".
  - Stale/degraded/unavailable/rebuilding/local-only/null freshness renders last-known content plus polite freshness cue.
  - Erased profile renders tombstone and does not render fixture PII values.
  - Load failure renders generic `role="alert"` and retry; exception text/PII absent.
  - Loading state renders valid markup before the awaited call completes.
  - Retry calls the profile data port again without changing route or requiring party id.
- Required boundary/static tests:
  - ConsumerPortal has no reference to `Hexalith.Parties.UI`.
  - ConsumerPortal does not contain `ListPartiesAsync`, `SearchPartiesAsync`, `IPartiesQueryClient`, `IAdminPortalGdprClient`, direct `GetPartyAsync(` calls, or methods with `partyId` parameters in the profile data port.
  - UI host registers the ConsumerPortal profile adapter as Scoped and delegates to `ISelfScopedPartiesClient.GetMyPartyAsync()`.
  - No profile page logging/telemetry APIs include profile values.
- Required E2E updates:
  - Replace Story 4.3 placeholder status expectations for `/me`.
  - Keep unbound Consumer test proving `NoPartyBinding` still wins and no profile data page appears.
- Validation commands should mirror Story 4.3's successful focused builds and compiled xUnit executable pattern. The unit lane may be unreliable in this sandbox; record exact failure output if it cannot complete. [Source: `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md#Debug-Log-References`]

### Latest Technical Information

- Microsoft Learn's current Blazor lifecycle guidance says a component awaiting incomplete async work in `OnInitializedAsync` must first leave itself in a valid render state. Apply that to `/me`: set loading state before awaiting profile data. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- Blazor DI supports `@inject` property injection and constructor injection for services registered in the app service collection. Use normal DI for the ConsumerPortal profile data port; keep service lifetimes compatible with the host's Scoped self-scope accessor. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]
- Microsoft Learn's RCL guidance confirms CSS isolation is bundled automatically for library components and routable RCL components must be disclosed through `Router.AdditionalAssemblies`. Story 4.3 already wired the assembly; Story 4.4 should use ordinary RCL component/CSS patterns, not custom static asset plumbing. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET 10 SDK `10.0.300`, FluentUI Blazor `5.0.0-rc.3`, FrontComposer Shell, xUnit v3, Shouldly, NSubstitute, and bUnit. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileDataClient.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Components/ProfileFreshnessIndicator.razor` and `.razor.css` if the page needs a local RCL-safe freshness component.
  - `src/Hexalith.Parties.UI/Services/ConsumerProfileDataClient.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyProfilePageTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerProfileDataClientTests.cs` or equivalent composition tests.
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/e2e/specs/consumer-party-binding.spec.ts`
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
- Keep generated/build artifacts out of the repo. Do not edit or commit `bin/`, `obj/`, or `obj/**/generated/**`.

### Out of Scope

- No profile edit form or update commands; Story 4.5 owns FR-Consumer-2.
- No consent grant/withdraw UI or commands; Epic 5 owns FR-Consumer-3.
- No data export, erasure request/cancel, or processing-records UI; Epic 5 owns FR-Consumer-4.
- No identity binding redesign, self-registration, IdP federation, or binding-store changes.
- No Parties aggregate/event/projection/actor changes.
- No EventStore, Tenants, FrontComposer, Memories, DAPR ACL, deployment, or KMS changes.
- No package upgrades and no new third-party libraries.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.4-My-profile-FR-Consumer-1`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor`]
- [Source: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]

## Validation Summary

- Source discovery loaded the BMAD create-story skill, discovery protocol, template, checklist, BMad config, sprint status, root and sibling project-context facts, planning epics, architecture, UX experience/design documents, Story 4.3, current ConsumerPortal source/tests, UI self-scope source/tests, PartyDetail/freshness contracts, AdminPortal rendering patterns, recent git history, and current Microsoft Blazor docs.
- Checklist fixes applied before finalizing: made the RCL-to-UI dependency conflict explicit, corrected the epic wording to the actual `GetMyPartyAsync()` self-scope API, required a ConsumerPortal-owned data port with a UI-host adapter, pinned erased-profile PII suppression, required stale/degraded/null freshness behavior, and preserved static guards rather than deleting them.
- Latest-technology review found no dependency upgrade requirement; implementation should use normal Blazor lifecycle/DI/RCL CSS isolation patterns on the pinned local stack.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: `git diff --check` passed.
- 2026-06-10: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors. First attempt exited 139 without C# diagnostics; rerun passed.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `./tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: Total 26, Failed 0.
- 2026-06-10: `./tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: Total 284, Failed 0.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` attempted. The wrapper invokes `dotnet test --project ...`; each project printed `Determining projects to restore...` followed by `Build failed with exit code: 1.` while the wrapper process returned exit code 0. Direct `dotnet test tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj --configuration Release --verbosity diagnostic --no-restore` reproduced `_MTPBuild` failure with `Build FAILED. 0 Warning(s) 0 Error(s)` and `Build failed with exit code: 1.`. Compiled xUnit v3 binaries were used for focused test execution.
- 2026-06-10 Review: `git diff --check` passed after review fixes.
- 2026-06-10 Review: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10 Review: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors after rerunning sequentially to avoid a transient parallel file-copy retry warning.
- 2026-06-10 Review: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10 Review: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10 Review: `./tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: Total 28, Failed 0.
- 2026-06-10 Review: `./tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: Total 284, Failed 0.
- 2026-06-10 Review: `pwsh scripts/test.ps1 -Lane unit` attempted. It returned process exit code 0, but each project printed `Determining projects to restore...` followed by `Build failed with exit code: 1.`.

### Completion Notes List

- Replaced `/me` placeholder with a read-only profile page that loads through a ConsumerPortal-owned profile data port, renders loading/current/stale/degraded/null freshness/failure/erased states, and suppresses PII for erased profiles.
- Added `IConsumerProfileDataClient` in ConsumerPortal and a scoped UI-host `ConsumerProfileDataClient` adapter delegating only to `ISelfScopedPartiesClient.GetMyPartyAsync`.
- Added resource-backed profile copy, CSS-isolated token/system-color styling, bUnit profile-state coverage, static boundary guards, UI adapter tests, and e2e expectation updates for local bound-consumer fixtures.

### File List

- `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Components/ProfileField.razor`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerProfileDataClient.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerProfileDataClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyProfilePageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerProfileDataClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-party-binding.spec.ts`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-06-10

Outcome: Approved after automatic fixes. No critical issues remain.

Findings fixed:

- [MEDIUM] `/me` still used future-tense placeholder summary copy ("You will be able...") after the page had become a real profile surface. Fixed `MyProfileSummary` to present-tense profile copy and added a regression assertion in `MyProfilePageTests`.
- [MEDIUM] Identifier jurisdiction used `ProfileField` inside a `<ul>`, which emitted `<dt>/<dd>` outside a `<dl>` and produced invalid semantics in the identifier list. Replaced it with normal inline list markup and expanded the profile render assertion.

Validation notes:

- Acceptance Criteria 1-6 were cross-checked against the source and tests. The profile loads only through `IConsumerProfileDataClient`, the UI adapter delegates to `ISelfScopedPartiesClient.GetMyPartyAsync`, erased profiles short-circuit PII fields, error copy is generic, and no edit/GDPR/export/API/token/DAPR/EventStore route surface was introduced by this story.
- Story File List matches the reviewed source/test surface. `_bmad-output` runtime artifacts were excluded from code review except for status/review-note updates, per workflow.
- Microsoft Learn references for Blazor lifecycle, DI, and RCL component consumption/CSS isolation were checked during review:
  - `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`
  - `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`
  - `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`

### Change Log

- 2026-06-10: Implemented Story 4.4 read-only Consumer My Profile page and self-scoped profile adapter; added focused tests, static guards, and e2e expectation updates.
- 2026-06-10: Senior review fixed profile summary copy and identifier-list semantics; status moved to done.
