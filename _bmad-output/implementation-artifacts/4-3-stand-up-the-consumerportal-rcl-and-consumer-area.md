---
baseline_commit: a03c5ac
---

# Story 4.3: Stand up the ConsumerPortal RCL and Consumer area

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want the Consumer area mounted under `/me`,
so that my self-service pages are reachable and protected.

## Acceptance Criteria

1. Given the new `Hexalith.Parties.ConsumerPortal` RCL (`Microsoft.NET.Sdk.Razor`, mirroring AdminPortal), when it is referenced and mounted by the host, then every `/me/*` page carries `@attribute [Authorize(Policy = "Consumer")]`, the area renders at roomy density with 16px body type, and a `Resources/` set holds the regulated GDPR microcopy as localized/auditable copy, never inline literals in feature components.
2. Given the solution build, when it runs, then the RCL builds green, references `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, and `Hexalith.FrontComposer.Shell`, and its Consumer nav entries render for Consumers only.
3. Given Story 4.2 is complete, when a bound Consumer follows the existing role landing flow, then the host discovers the ConsumerPortal routable assembly, `/me` resolves to the ConsumerPortal route shell, and the existing host-local `/me` stub is removed so no duplicate route exists.
4. Given Story 4.3 is a Consumer area stand-up story, when protected route shells are added for future Consumer features, then they do not fetch profile data, issue GDPR commands, implement consent/privacy behavior, or call `ListPartiesAsync`/`SearchPartiesAsync`; future data access remains only through `ISelfScopedPartiesClient` in Stories 4.4, 4.5, and Epic 5.

## Tasks / Subtasks

- [x] Create the ConsumerPortal Razor class library and add it to solution structure (AC: 1, 2)
  - [x] Add `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` using `Microsoft.NET.Sdk.Razor`.
  - [x] Set `PackageId` and `Description` consistently with `Hexalith.Parties.AdminPortal`; do not redeclare inherited `TargetFramework`, `Nullable`, `ImplicitUsings`, or `TreatWarningsAsErrors`.
  - [x] Reference `Microsoft.AspNetCore.App`, `Microsoft.FluentUI.AspNetCore.Components`, `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, and `Hexalith.FrontComposer.Shell`. Add `Hexalith.FrontComposer.Contracts` only if the ConsumerPortal owns manifest/navigation metadata.
  - [x] Add the project to `Hexalith.Parties.slnx` under `/src/`. Do not create a classic `.sln`.

- [x] Add protected Consumer route shells without implementing future data workflows (AC: 1, 3, 4)
  - [x] Add `_Imports.razor` with the minimal namespaces required for components, authorization, FluentUI, and project services.
  - [x] Add `Components/MyProfilePage.razor` at `@page "/me"` with `@attribute [Authorize(Policy = "Consumer")]`.
  - [x] Add only lightweight route shells/placeholders for future Consumer surfaces if needed by navigation: `/me/edit`, `/me/consent`, `/me/privacy`. Every such route must carry the same Consumer policy attribute.
  - [x] Keep these shells non-data-fetching in this story. No `ISelfScopedPartiesClient.GetPartyAsync`, no consent/export/erasure command calls, no EventStore gateway calls, no list/search.
  - [x] Remove `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor` or otherwise eliminate its `@page "/me"` route so the host does not have duplicate `/me` matches.

- [x] Establish ConsumerPortal copy/localization and visual posture (AC: 1)
  - [x] Add `Resources/` under `Hexalith.Parties.ConsumerPortal` and place Consumer GDPR microcopy defaults there or behind a `ConsumerPortalLabels`/resource wrapper that can later be localized/audited.
  - [x] Keep copy plain and PII-free. Do not hard-code regulated text directly in feature components beyond resource keys or label accessors.
  - [x] Apply Consumer area posture: roomy density, 16px body, single-column centered/narrow measure. Use FluentUI/FrontComposer tokens only; no raw hex colors, no separate Consumer brand, no gradients.
  - [x] Do not use the raw teal accent `#0097A7` as a white-text button fill; primary buttons must inherit Fluent's AA-safe brand background token.

- [x] Wire ConsumerPortal into the host and router (AC: 2, 3)
  - [x] Add a `ProjectReference` from `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` to `..\Hexalith.Parties.ConsumerPortal\Hexalith.Parties.ConsumerPortal.csproj`.
  - [x] Update `src/Hexalith.Parties.UI/Components/Routes.razor` so the `Router` `AdditionalAssemblies` includes both AdminPortal and ConsumerPortal routable assemblies.
  - [x] Update `src/Hexalith.Parties.UI/Program.cs` so `app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(...)` includes the ConsumerPortal assembly as well as AdminPortal.
  - [x] If a ConsumerPortal service registration extension is added, call it from `Program.cs` after FrontComposer and before route execution; keep lifetimes scoped/transient as appropriate and compatible with `ValidateScopes=true`.

- [x] Register Consumer navigation deliberately (AC: 2)
  - [x] Preserve the existing policy gate: Consumer nav entries use `RequiredPolicy: PartiesUiAuthorization.ConsumerPolicy`; Admin nav entries remain Admin-only.
  - [x] Decide whether Story 4.3 keeps the single existing "My space" nav entry to `/me` or moves Consumer nav metadata into a `ConsumerPortalManifest`. Either choice must keep Admin and Consumer nav from cross-rendering.
  - [x] If additional Consumer nav entries are introduced now, point only to protected route shells and order them predictably. Do not expose Admin routes to Consumers or Consumer routes to Admin-only principals.

- [x] Add focused tests for packaging, routing, authorization, and no-regression boundaries (AC: 1, 2, 3, 4)
  - [x] Add `tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj` using xUnit v3, Shouldly, NSubstitute, bUnit, and no package `Version=` attributes. Add it to `Hexalith.Parties.slnx`.
  - [x] Test every ConsumerPortal routable page has `AuthorizeAttribute` with `Policy == "Consumer"`.
  - [x] Test the ConsumerPortal project references Client, Contracts, and FrontComposer.Shell, and does not reference internal host/server/projection/security/testing projects.
  - [x] Test placeholder pages render Consumer-facing plain copy and have no list/search/self-scoped data calls in this story.
  - [x] Extend `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs` to assert the UI host references ConsumerPortal and includes its assembly in `Routes.razor` and `Program.cs`.
  - [x] Extend existing nav gating tests or add ConsumerPortal equivalents to prove Consumer entries render for Consumer policy only and never for Admin-only or unauthenticated principals.
  - [x] Add a source/static guard that the host-local `/me` stub is gone or no longer declares `@page "/me"`.

- [x] Validate the focused implementation (AC: 2)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run the compiled xUnit v3 test executables for the new ConsumerPortal tests and focused UI host/routing tests where the local runner supports it.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record socket/restore/DAPR limitations exactly instead of claiming success.

## Dev Notes

### Current Implementation State

- Story 4.2 is done and provides the prerequisite admin-link binding implementation. Runtime Consumer access still depends on exactly one IdP-emitted `party_id` claim, resolved by `PartyIdClaimResolver`; the binding store is not a runtime authorization source for Consumer pages. [Source: `_bmad-output/implementation-artifacts/4-2-implement-admin-link-party-id-binding-provisioning.md#Completion-Notes-List`] [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Consequences`]
- The UI host already owns OIDC sign-in, role policies, claim resolution, the self-scoped client, identity-binding provisioning, freshness services, and typed Parties clients. Do not redesign these seams in Story 4.3. [Source: `src/Hexalith.Parties.UI/Program.cs`]
- `RoleLandingRedirect` already sends bound Consumers to `/me` and unbound/ambiguous Consumers to `/no-party-binding`. Story 4.3 must make `/me` resolve to ConsumerPortal, not change landing semantics. [Source: `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor`] [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Implementation-Impact`]
- `NoPartyBinding` remains a neutral, Consumer-policy-gated no-data state. Story 4.3 must not turn it into a data-fetching or diagnostic screen. [Source: `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor`]
- `ISelfScopedPartiesClient` / `SelfScopedPartiesClient` are already the single Consumer own-data accessor and expose no list/search or caller-supplied party id members. Story 4.3 route shells should not consume it yet; Story 4.4 starts data access. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- The current host has a transitional `/me` page at `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor`. Its comment explicitly says Story 4.3/4.4 must remove it when ConsumerPortal's `/me` route is embedded to avoid duplicate routes. [Source: `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor`]
- The current router and Razor component mapping discover only the AdminPortal assembly. ConsumerPortal will not serve routable RCL pages until both places include the ConsumerPortal assembly. [Source: `src/Hexalith.Parties.UI/Components/Routes.razor`] [Source: `src/Hexalith.Parties.UI/Program.cs`]
- The existing nav registration already has one Consumer-gated entry: title "My space", href `/me`, policy `Consumer`. Reuse or evolve it deliberately; do not duplicate an un-gated Consumer entry elsewhere. [Source: `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`]

### Current Files Being Modified - Required Reading

- `Hexalith.Parties.slnx` (UPDATE)
  - Current state: XML solution format with `src/Hexalith.Parties.UI` and test projects registered; no ConsumerPortal project exists.
  - What this story changes: add `src/Hexalith.Parties.ConsumerPortal` and `tests/Hexalith.Parties.ConsumerPortal.Tests` under the existing folders.
  - Preserve: XML `.slnx` format; do not create or use `.sln`.
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` (UPDATE)
  - Current state: references AdminPortal, Client, Contracts, Picker, ServiceDefaults, EventStore.SignalR, FrontComposer Shell/Mcp, and SourceTools analyzer; no ConsumerPortal reference.
  - What this story changes: add a project reference to ConsumerPortal only.
  - Preserve: no package versions in csproj; no references from UI to internal Parties host/server/projection/security/testing projects.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers AdminPortal services and maps AdminPortal as the only additional Razor assembly.
  - What this story changes: include ConsumerPortal in component discovery and call ConsumerPortal DI registration if introduced.
  - Preserve: `ValidateScopes=true`, auth/claims/self-scope registrations, server-side token rule, no DAPR sidecar, no browser bearer tokens.
- `src/Hexalith.Parties.UI/Components/Routes.razor` (UPDATE)
  - Current state: `Router` `AdditionalAssemblies` includes only AdminPortal and uses `AuthorizeRouteView`.
  - What this story changes: include ConsumerPortal assembly in `AdditionalAssemblies`.
  - Preserve: `AuthorizeRouteView`; do not regress `[Authorize]` enforcement.
- `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor` (DELETE or UPDATE)
  - Current state: transitional host-local `@page "/me"` stub protected by Consumer policy.
  - What this story changes: remove the `/me` route from the host so the ConsumerPortal route owns `/me`.
  - Preserve: no unauthenticated or unbound Consumer data route.
- `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs` (UPDATE only if nav changes)
  - Current state: registers Admin "Parties" and Consumer "My space" entries, both policy-gated.
  - What this story may change: point to a ConsumerPortal route constant or add Consumer entries.
  - Preserve: Admin and Consumer nav never cross-render.
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`, `PartiesUiNavigationRegistrationTests.cs`, `PartiesUiNavEntryGatingTests.cs` (UPDATE)
  - Current state: pins AdminPortal reference/additional assembly and role-gated nav behavior.
  - What this story changes: add ConsumerPortal reference/discovery assertions and keep Consumer policy gating pinned.
  - Preserve: bUnit/xUnit v3/Shouldly/NSubstitute style.

### Architecture Guardrails

- ConsumerPortal is an independent RCL, not a host. It must not reference `Hexalith.Parties.UI` to consume host-owned shared components or status primitives. If future pages need `StatusKind`, `DataFreshnessIndicator`, or shared domain components currently in UI, promote/share them through an explicit later story or keep the mapping at the host boundary. [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- The browser talks only to the UI host. ConsumerPortal must not add public command/query APIs, direct browser calls to EventStore, DAPR ACL changes, or actor-host endpoints. [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`]
- `/me*` pages require the `Consumer` policy. Do not rely only on navigation gating; route-level `[Authorize]` is required and enforced by `AuthorizeRouteView`. [Source: `_bmad-output/planning-artifacts/architecture.md#Naming-Patterns`] [Source: `src/Hexalith.Parties.UI/Components/Routes.razor`]
- Consumer data access is own-data-only and future pages must use `ISelfScopedPartiesClient`. Story 4.3 should not introduce list/search, caller-supplied party ids, or gateway calls. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Use Central Package Management. Do not add `Version=` attributes to any project file, and do not bump packages for this story. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]
- Do not modify submodule files for this story. ConsumerPortal is owned by this repo. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### UX and Product Guardrails

- ConsumerPortal should feel like the actual application area, not a landing page or marketing page. Use compact, useful route shells that establish navigation/protection/copy posture while avoiding fake functionality.
- Consumer area defaults: phone-first, single column, centered/narrow measure, roomy density, 16px body type, Fluent/FrontComposer design tokens. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Typography`] [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Responsive--Platform`]
- Copy posture: Consumer copy is plain and reassuring. For GDPR copy, say what will happen in human words, then name the right. Keep copy in resources/labels so regulated language can be audited. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- Avoid regulated-language mistakes already identified by UX review: no hard "within 30 days" finish promise, no hiding completed-erasure permanence, no pre-checked consent, no "withdraw" toggle for contract/legitimate-interest bases, no sub-minute export promise. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Accessibility: status/freshness/accepted-processing are polite live regions; validation/failure paths are `role="alert"`. Do not use interactive styled `div`s. Preserve skip links/focus behavior inherited from the shell. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]

### Previous Story Intelligence

- Story 4.2 added `src/Hexalith.Parties.UI/IdentityBinding/*`, service registration in `Program.cs`, and tests around provisioning/routing/boundary behavior. Do not refactor that work; ConsumerPortal consumes the resulting runtime contract through existing host seams. [Source: `_bmad-output/implementation-artifacts/4-2-implement-admin-link-party-id-binding-provisioning.md#File-List`]
- Story 4.2 validation noted direct compiled xUnit executable runs worked while `dotnet test` could fail in this sandbox with socket permission issues. Prefer focused builds and compiled test executables when needed; record limitations honestly. [Source: `_bmad-output/implementation-artifacts/4-2-implement-admin-link-party-id-binding-provisioning.md#Debug-Log-References`]
- Recent commits are sequential and scoped: `feat(story-4.2): Admin link party id binding provisioning`, `feat(story-4.1): Consumer identity party id binding decision ADR`, and completed Epic 3 GDPR work. Keep Story 4.3 focused on ConsumerPortal stand-up. [Source: `git log -5`]

### Testing and Validation Guidance

- Required test families:
  - Packaging/static tests: ConsumerPortal in `.slnx`, UI references ConsumerPortal, ConsumerPortal references Client/Contracts/FrontComposer.Shell and not internal projects.
  - Route tests: all `/me*` ConsumerPortal pages carry `AuthorizeAttribute` with Consumer policy; no host-local duplicate `/me` route remains.
  - Host discovery tests: `Routes.razor` and `Program.cs` include the ConsumerPortal assembly in additional assemblies.
  - Nav tests: Consumer entries render only under Consumer policy; Admin entries still render only under Admin policy; unauthenticated sees neither.
  - UX/copy tests: route shells use label/resource accessors for regulated copy, render at Consumer posture, and do not contain banned timing/consent/erasure phrases.
  - Boundary tests: no ConsumerPortal call to `ListPartiesAsync`, `SearchPartiesAsync`, gateway clients, or GDPR command methods in this story.
- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, raw `Assert.*`, or csproj package versions.
- If Verify snapshots are added, set `DiffEngine_Disabled=true` when running tests; otherwise prefer direct markup assertions for this narrow story.

### Out of Scope

- No My Profile data fetch, freshness indicator integration, or PII rendering. Story 4.4 owns `ISelfScopedPartiesClient.GetPartyAsync(myPartyId)` and profile UI.
- No Edit My Profile command flow. Story 4.5 owns validated correction/update behavior.
- No consent grant/withdraw, data export, erasure request/cancel, or processing-records implementation. Epic 5 owns those flows.
- No Consumer self-registration, duplicate-party lookup, identity proofing UX, or IdP federation.
- No Parties command/event/projection/aggregate/actor changes.
- No EventStore, Tenants, FrontComposer, Memories, DAPR ACL, or deployment changes unless a build file already needs a local project reference.
- No package upgrades.

### Latest Technical Information

- Current ASP.NET Core Blazor docs state that routable components from an RCL must have the RCL assembly disclosed to the app router through `Router.AdditionalAssemblies`; this matches the existing AdminPortal pattern and is required for ConsumerPortal. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0#make-routable-components-available-from-the-rcl`]
- The same Microsoft Learn RCL guidance confirms components and static assets from an RCL are consumed through project references/assemblies; CSS isolation is bundled automatically. Use normal RCL patterns rather than custom static-asset plumbing unless ConsumerPortal adds global non-isolated CSS. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET 10 SDK `10.0.300`, FluentUI Blazor `5.0.0-rc.3`, FrontComposer Shell, xUnit v3, Shouldly, NSubstitute, and bUnit. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- New source project:
  - `src/Hexalith.Parties.ConsumerPortal/`
  - `src/Hexalith.Parties.ConsumerPortal/Components/`
  - `src/Hexalith.Parties.ConsumerPortal/Resources/`
  - `src/Hexalith.Parties.ConsumerPortal/Services/` only if labels/service registration are needed.
- New test project:
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/`
- Keep ConsumerPortal adopter-facing. It may reference `Client`, `Contracts`, and FrontComposer Shell. It must not reference the UI host or internal Parties projects.
- Keep generated/build artifacts out of the repo. Do not edit `obj/**/generated/**`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.3-Stand-up-the-ConsumerPortal-RCL-and-Consumer-area`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `src/Hexalith.Parties.UI/Program.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Routes.razor`]
- [Source: `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor`]
- [Source: `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj`]
- [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiNavigationRegistrationTests.cs`]
- [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`]

## Validation Summary

- Source discovery loaded BMAD workflow files, config, sprint status, all persistent project-context files, planning epics, architecture, accepted Consumer binding ADR, UX experience/design/review files, previous Stories 4.1 and 4.2, current UI host routing/composition/nav files, AdminPortal RCL patterns, UI host tests, recent git history, and current Blazor RCL routing documentation.
- Checklist fixes applied before finalizing: made the duplicate `/me` host stub removal explicit, required ConsumerPortal assembly discovery in both router and component mapping, pinned route-level Consumer authorization, separated route-shell stand-up from future data workflows, added RCL boundary tests, and prohibited host-to-RCL dependency inversion.
- Latest-technology review found no dependency upgrade requirement; the story relies on the pinned local stack and current Microsoft Blazor RCL routing guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release -m:1 -v:minimal` failed before implementation because `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` did not exist; restore also reported network-blocked NuGet vulnerability data (`NU1900`) until local restore used `--ignore-failed-sources -p:NuGetAudit=false`.
- Green/refactor validation: `git diff --check` passed.
- Green/refactor validation: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Green/refactor validation: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Green/refactor validation: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Green/refactor validation: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Focused xUnit executable before senior review auto-fix: `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -noLogo -noColor` passed 16/16.
- Focused xUnit executable: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor -class Hexalith.Parties.UI.Tests.PartiesUiHostCompositionTests -class Hexalith.Parties.UI.Tests.PartiesUiAreaAuthorizationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavigationRegistrationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavEntryGatingTests` passed 24/24.
- Unit lane attempt: `pwsh scripts/test.ps1 -Lane unit` returned process exit 0 but printed repeated `Build failed with exit code: 1.` messages after `Determining projects to restore...`; direct `dotnet test --project tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --verbosity normal` reproduced the same restore/build failure with 0 warnings and 0 errors emitted, so the lane was not claimable as passed in this sandbox.
- QA E2E test generation: `npm run typecheck` in `tests/e2e` passed.
- QA E2E test generation: `git diff --check` passed.
- QA E2E test generation: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- QA E2E test generation: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- QA E2E test generation before senior review auto-fix: `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -noLogo -noColor` passed 16/16.
- QA E2E test generation: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor -class Hexalith.Parties.UI.Tests.PartiesUiHostCompositionTests -class Hexalith.Parties.UI.Tests.PartiesUiAreaAuthorizationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavigationRegistrationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavEntryGatingTests` passed 24/24.
- QA E2E test generation: `npm run test -- specs/consumer-party-binding.spec.ts specs/consumer-portal-routes.spec.ts --project=chromium` could not start the Playwright web server in this sandbox because Kestrel failed with `System.Net.Sockets.SocketException (13): Permission denied`.
- Senior review auto-fix validation: `git diff --check` passed.
- Senior review auto-fix validation: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Senior review auto-fix validation: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Senior review auto-fix validation: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Senior review auto-fix validation: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings, 0 errors.
- Senior review focused xUnit executable: `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -noLogo -noColor` passed 17/17.
- Senior review focused xUnit executable: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor -class Hexalith.Parties.UI.Tests.PartiesUiHostCompositionTests -class Hexalith.Parties.UI.Tests.PartiesUiAreaAuthorizationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavigationRegistrationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavEntryGatingTests` passed 24/24.

### Completion Notes List

- Added the `Hexalith.Parties.ConsumerPortal` Razor class library with the required adopter-facing references and no inherited build-property redeclarations.
- Added protected `/me`, `/me/edit`, `/me/consent`, and `/me/privacy` route shells with route-level `Authorize(Policy = "Consumer")`; shells do not fetch data or call future GDPR/profile workflows.
- Added `Resources/ConsumerPortalResources.resx` plus `ConsumerPortalLabels` so Consumer/GDPR copy is centralized behind resource accessors.
- Added single-column, roomy Consumer shell styling with 16px body text and Fluent/FrontComposer CSS tokens, with no raw brand colors or gradients.
- Wired ConsumerPortal into the host project reference, `Routes.razor` router additional assemblies, and static Razor component discovery in `Program.cs`.
- Kept the existing single Consumer-gated "My space" nav entry to `/me`; no additional Consumer nav entries were introduced.
- Removed the host-local duplicate `/me` route by eliminating `@page "/me"` from `ConsumerLanding.razor`.
- Added ConsumerPortal packaging, authorization, rendering, copy, and no-future-data boundary tests; extended UI host composition/area tests for ConsumerPortal discovery and duplicate-route guard.
- QA workflow added Playwright coverage for direct bound Consumer access and unauthenticated challenge behavior across `/me`, `/me/edit`, `/me/consent`, and `/me/privacy`; it also updated the existing Consumer landing E2E assertion from the removed host stub heading to the ConsumerPortal `My profile` heading.
- Senior review auto-fixed ConsumerPortal static guards so they scan only app-authored files, assert the required framework/package references, and cover isolated CSS token/no-raw-color constraints.

### File List

- `Hexalith.Parties.slnx`
- `_bmad-output/implementation-artifacts/4-3-stand-up-the-consumerportal-rcl-and-consumer-area.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj`
- `src/Hexalith.Parties.ConsumerPortal/_Imports.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor`
- `src/Hexalith.Parties.UI/Components/Routes.razor`
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`
- `src/Hexalith.Parties.UI/Program.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalAuthorizationTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Usings.cs`
- `tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-party-binding.spec.ts`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`

### Change Log

- 2026-06-10: Implemented Story 4.3 ConsumerPortal RCL stand-up, host routing discovery, route-shell authorization, resource-backed copy, focused tests, and validation.
- 2026-06-10: Senior developer review auto-fixed ConsumerPortal packaging/static guard coverage and approved the story.

## Senior Developer Review (AI)

### Review Summary

- Outcome: Approve.
- Story status: done.
- Critical issues remaining: 0.
- High issues remaining: 0.
- Medium issues fixed: 1.

### Findings and Fixes

- MEDIUM: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs` scanned all descendant `.cs`/`.razor` files under `src/Hexalith.Parties.ConsumerPortal`, including `bin/` and `obj/` when previous builds existed. That made the no-future-data-workflow guard dependent on generated/build output instead of source. Fixed by adding a source-file reader that excludes `bin/` and `obj/`.
- MEDIUM: The same packaging guard did not pin `Microsoft.AspNetCore.App`, `Microsoft.FluentUI.AspNetCore.Components`, or isolated CSS no-raw-color constraints even though these are explicit Story 4.3 requirements. Fixed by adding framework/package assertions and a ConsumerPortal CSS token guard.

### Acceptance Criteria Validation

- AC1: Implemented. ConsumerPortal is an RCL, `/me`, `/me/edit`, `/me/consent`, and `/me/privacy` are route-level Consumer-policy gated, copy is resource-backed through `ConsumerPortalLabels`, and the route shell uses roomy 16px single-column styling.
- AC2: Implemented. ConsumerPortal references Client, Contracts, and FrontComposer Shell; focused builds pass; existing Consumer nav remains gated on `PartiesUiAuthorization.ConsumerPolicy`.
- AC3: Implemented. UI host references ConsumerPortal, router/component mapping include the ConsumerPortal assembly, and the host-local `ConsumerLanding.razor` no longer declares `@page "/me"`.
- AC4: Implemented. ConsumerPortal route shells do not call profile, list/search, EventStore, self-scoped, or GDPR command workflows.

### Validation

- `git diff --check` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests -noLogo -noColor` passed 17/17.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor -class Hexalith.Parties.UI.Tests.PartiesUiHostCompositionTests -class Hexalith.Parties.UI.Tests.PartiesUiAreaAuthorizationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavigationRegistrationTests -class Hexalith.Parties.UI.Tests.PartiesUiNavEntryGatingTests` passed 24/24.
