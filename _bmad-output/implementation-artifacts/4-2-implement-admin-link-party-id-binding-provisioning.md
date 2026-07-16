---
baseline_commit: 64c42e1
---

# Story 4.2: Implement admin-link `party_id` binding provisioning

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a product owner,
I want the admin-link binding mechanism from the accepted Consumer identity binding ADR implemented end-to-end,
so that consumers can be provisioned and the Consumer area becomes reachable.

## Acceptance Criteria

1. Given the accepted ADR `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`, when Story 4.2 implements provisioning, then an authorized Admin, TenantOwner, or support operator can link an existing IdP user to an existing Party through the admin-link flow, the IdP user attribute `party_id` is set for the verified Party, and a small identity-binding audit store records `{tenant, idp_issuer, idp_subject, party_id, status, operator, timestamps, verification_reference, reason_code, version}` outside the Parties event stream.
2. Given a newly provisioned Consumer, when they sign in or refresh their session, then the Keycloak/tache mapper emits exactly one `party_id` claim (`multivalued=false`) consumed by `PartyIdClaimResolver`, `RoleLandingRedirect` sends the bound Consumer to `/me`, and unbound, empty, duplicated, suspended, or removed bindings send the Consumer to `NoPartyBinding` rather than any data screen.
3. Given an active binding already exists for `{tenant, idp_issuer, idp_subject}`, when an operator attempts to create another active binding, then the request is rejected fail-closed unless it is an explicit rotation that supersedes the previous active party id, updates the IdP attribute, increments/validates the version or etag, and records the operator/audit metadata.
4. Given a binding is suspended or removed, when the operation completes, then the IdP `party_id` attribute is cleared, the binding store status becomes `Suspended` or `Removed`, historical audit metadata is retained without real PII, and the next Consumer sign-in/session refresh fails closed to `NoPartyBinding`.
5. Given the audit store and IdP user attribute drift apart, when reconciliation runs or the provisioning service checks a binding, then drift is detected and surfaced to operators without leaking PII, and the system never treats a store-only record as enough for Consumer runtime access; runtime access still depends on exactly one verified IdP `party_id` claim.
6. Given implementation scope, when files are changed, then changes are limited to Keycloak/tache realm configuration and topology tests, the identity-binding provisioning surface/service/store, operator authorization, and host/flow tests for bound and unbound Consumers; no Parties command, event, projection, actor, DAPR ACL expansion, public actor-host endpoint, browser token flow, or Parties event-stream mapping is introduced.
7. Given security and privacy guardrails, when provisioning, audit, telemetry, logs, tests, fixtures, or docs are produced, then they use synthetic ids only, never include decoded JWT payloads, secrets, bearer tokens, real PII, verification evidence details, or Party event data, and never use party list/search disclosure as identity proof.
8. Given the story is complete, when validation runs, then tests cover mapper shape, bound Consumer to `/me`, unbound/removed/suspended/ambiguous Consumer to `NoPartyBinding`, duplicate active binding rejection, rotation, suspend/remove, unauthorized operator denial, audit-store/IdP-attribute drift handling, and the static no-event-stream/no-public-actor-endpoint boundary.

## Tasks / Subtasks

- [x] Implement the identity-binding provisioning model and store outside the Parties event stream (AC: 1, 3, 4, 5, 6, 7)
  - [x] Add a host-owned provisioning component, preferably under `src/Hexalith.Parties.UI/IdentityBinding/` or a similarly UI/BFF-owned namespace, not under `Hexalith.Parties.Contracts` or the Parties domain host.
  - [x] Define immutable records/enums for binding state: lookup key `{tenant, idp_issuer, idp_subject}`, active `party_id`, status `Active|Suspended|Removed`, bound/updated operator subjects, UTC timestamps, opaque `verification_reference`, `reason_code`, and `version` or `etag`.
  - [x] Provide a store abstraction plus implementation appropriate for this MVP. It must be outside the Parties event stream and must not require a new Parties command/event/projection.
  - [x] Enforce version/etag checks so concurrent operator updates cannot silently overwrite an active binding.
  - [x] Keep verification evidence outside the store; record only an opaque reference and bounded reason code.

- [x] Implement the IdP attribute update seam (AC: 1, 2, 3, 4, 5, 7)
  - [x] Add a small adapter abstraction for the Keycloak/tache user attribute operation: set `party_id` to the verified Party id, clear `party_id` on suspend/remove, and read current user attributes for reconciliation.
  - [x] Do not expose bearer tokens, admin credentials, client secrets, or decoded JWT payloads to the browser, logs, fixtures, snapshots, or story docs.
  - [x] Use the local realm contract already pinned by `ConsumerPartyIdBindingRealmTests`: user attribute `party_id` maps to claim `party_id`, `multivalued=false`, emitted to id token, access token, and userinfo.
  - [x] Keep the production tache realm contract logically identical to the local Keycloak mapper; if a deployment script/config change is required, cover it with static validation.

- [x] Add the operator-facing provisioning surface or service entry point (AC: 1, 3, 4, 5, 6, 7)
  - [x] Gate all create, rotate, suspend, remove, and reconcile actions behind existing Admin/TenantOwner support policy checks. Prefer reusing `IAdminPortalAuthorizationService`/`AdminPortalAuthorizationState` patterns where the surface is in `AdminPortal`; do not hard-code a second role matrix.
  - [x] If a UI page is added, keep it under Admin routes/navigation and never render for Consumer users. Admin copy should be terse and precise; no Consumer self-service onboarding is introduced in this story.
  - [x] If no UI page is added, provide a tested host/service entry point that still proves the end-to-end provisioning behavior and audit/IdP update contract.
  - [x] Require the operator to provide the target Party id, IdP subject, tenant/issuer context, verification reference, and reason code; do not use party search results as proof of identity.

- [x] Preserve existing Consumer runtime seams (AC: 2, 6)
  - [x] Do not redesign `PartyIdClaimResolver`; it remains the fail-closed single-claim resolver.
  - [x] Do not redesign `RoleLandingRedirect`; bound Consumers go to `/me`, unbound or ambiguous Consumers go to `/no-party-binding`.
  - [x] Do not turn `NoPartyBinding` into a data-fetching screen; it remains static neutral copy with no PII.
  - [x] Do not bypass `ISelfScopedPartiesClient` for future Consumer pages; Consumer data access remains own-data-only and never list/search.

- [x] Extend realm/topology and routing tests (AC: 2, 4, 5, 8)
  - [x] Extend `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs` or add a sibling test for unbound and removed/suspended seed-user cases if those are represented in committed realm config.
  - [x] Keep existing mapper tests for `party-id-mapper`, `user.attribute=party_id`, `claim.name=party_id`, `multivalued=false`, and token-surface emission.
  - [x] Add host/routing tests proving provisioned/bound principals reach `/me`, while absent, empty, duplicated, suspended, or removed bindings reach `NoPartyBinding`.
  - [x] Add store/adapter tests for duplicate active binding rejection, rotation, suspend/remove clearing, unauthorized operator denial, and drift handling.

- [x] Add boundary and privacy guardrail tests (AC: 6, 7, 8)
  - [x] Add static tests that fail if identity-binding types are added to `Hexalith.Parties.Contracts/Commands`, `Contracts/Events`, projections, aggregate handlers, actor endpoints, DAPR ACLs, or public actor-host endpoints.
  - [x] Add source/fixture scans or focused assertions that binding logs/messages do not contain secrets, decoded JWT payloads, real PII, verification evidence text, or Party event payloads.
  - [x] Confirm no browser token flow is introduced; the browser still holds only the server-side cookie.

- [x] Validate the focused implementation (AC: 8)
  - [x] Run `git diff --check`.
  - [x] Run focused Release builds for touched projects, normally with `--no-restore -m:1` if restore/audit access is blocked locally.
  - [x] Run the relevant xUnit v3 tests via the lane runner or compiled test executable filters. Avoid relying solely on `dotnet test --filter`, which prior stories recorded can report zero tests under the MTP setup.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` and `pwsh scripts/test.ps1 -Lane topology` where the environment allows it; record any Docker/DAPR/network/socket limitations exactly.

## Dev Notes

### Current Implementation State

- Story 4.1 accepted the admin-link ADR. The selected mechanism is: authorized operator links an existing IdP user to an existing Party; runtime access comes from an IdP `party_id` claim; operator audit/reconciliation lives in a small identity-binding store outside the Parties event stream. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Decision`]
- `PartyIdClaimResolver` is already implemented and registered as Scoped. It resolves bound only when the principal has exactly one non-empty `party_id` claim; zero, empty, or multiple claims resolve unbound. Preserve this behavior. [Source: `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`]
- `RoleLandingRedirect` already gives Admin/TenantOwner precedence, sends bound Consumers to `/me`, sends unbound/ambiguous Consumers to `/no-party-binding`, and does not route role-less authenticated users to data areas. Preserve this behavior. [Source: `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor`]
- `NoPartyBinding` is a static, Consumer-policy-gated onboarding/error state. It renders no party data, no form, and no PII. Preserve that shape. [Source: `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor`]
- `ISelfScopedPartiesClient` / `SelfScopedPartiesClient` are already the single Consumer own-data accessor. They inject the resolved party id and expose no list/search or caller-supplied party id members. Story 4.2 provisions the claim; it does not replace this accessor. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- The dev Keycloak realm already has `party-id-mapper` on `hexalith-parties-ui`: user attribute `party_id` to claim `party_id`, `multivalued=false`, emitted to id token, access token, and userinfo. It also seeds `readonly-user` as a Consumer with `party_id=["party-readonly-001"]`. [Source: `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`]
- Static topology tests already pin the existing mapper and bound seed shape. Extend that style for Story 4.2; do not require running Keycloak for every mapper-contract assertion. [Source: `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs`]
- Existing bUnit/DI tests already cover claim resolution, role landing, `NoPartyBinding`, and Admin/Consumer policy mapping. Add focused tests for the new provisioning behavior without weakening these tests. [Source: `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs`] [Source: `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs`] [Source: `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` (UPDATE if local seed users or mapper proof changes)
  - Current state: declares the `hexalith-parties-ui` confidential client, flat `roles` mapper, `party-id-mapper`, and `readonly-user` with a synthetic single `party_id`.
  - What this story may change: add unbound/removed/suspended synthetic users only if needed for local topology tests; keep mapper contract unchanged unless the ADR is superseded.
  - Preserve: no secrets beyond existing dev-only fixtures; no real PII; `party_id` remains single-valued.
- `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs` (UPDATE)
  - Current state: static JSON tests for mapper shape and bound Consumer seed.
  - What this story changes: add coverage for any new synthetic seed cases and mapper invariants required by provisioning.
  - Preserve: no running Keycloak dependency for static realm-contract assertions.
- `src/Hexalith.Parties.AdminPortal/Extensions/PartiesAdminPortalServiceCollectionExtensions.cs` (UPDATE if provisioning is surfaced in AdminPortal)
  - Current state: registers AdminPortal services, coordinators, and `IAdminPortalAuthorizationService`.
  - What this story may change: register identity-binding provisioning UI/service dependencies.
  - Preserve: existing AdminPortal composition and FrontComposer quickstart pattern.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs` and `IAdminPortalAuthorizationService.cs` (READ; UPDATE only if the current abstraction cannot express support-operator authorization)
  - Current state: resolves tenant context and tenant-owner Admin status through Tenants projection.
  - What this story may change: extend authorization only if needed and tested.
  - Preserve: fail-closed tenant behavior, no ad hoc role matrix, no PII in context signatures.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE if provisioning services are host-owned)
  - Current state: registers FrontComposer, AdminPortal, Admin/Consumer policies, claim resolution, self-scoped client, freshness, typed Parties clients, and host-owned OIDC. Browser tokens never leave the server.
  - What this story may change: register identity-binding store/adapter/provisioning services with proper scoped lifetimes and options.
  - Preserve: `ValidateScopes=true`, unconditional auth policy/claim-resolution registration, server-side token rule, no DAPR sidecar for `parties-ui`.
- `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`, `RoleLandingRedirect.razor`, `NoPartyBinding.razor`, `ISelfScopedPartiesClient.cs`, `SelfScopedPartiesClient.cs` (READ; avoid UPDATE)
  - Current state: accepted runtime consumer seams from Stories 1.4 and 1.5.
  - What this story changes: ideally nothing. If a test proves a contradiction, document it and keep behavior fail-closed.
  - Preserve: exactly-one-claim binding, neutral no-data unbound state, and single own-data accessor.

### Architecture Guardrails

- The identity-to-party mapping must never be a Parties command, event, projection, aggregate state, read model, actor, or event-stream payload. It belongs to the IdP attribute plus the small binding audit/reconciliation store. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Binding-Data-Shape`]
- The `parties` actor host has no public command/query API; all public domain traffic enters the EventStore gateway. Do not add public controllers, minimal APIs, DAPR ACL expansions, or browser-to-EventStore token flows for identity binding. [Source: `docs/api-contracts.md#The-boundary-in-one-picture`]
- `parties-ui` owns OIDC sign-in and server-side cookies. Tokens are stored server-side and never reach the browser. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- The effective Consumer scope remains `{tenant, party_id}`. `eventstore:tenant` is normalized by `PartiesClaimsTransformation`; `party_id` comes from the IdP claim. [Source: `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`]
- Operator audit metadata must be bounded and non-PII. Do not store verification evidence details, decoded JWTs, secrets, Party event payloads, or real personal data in the binding store. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Security--Privacy-Guardrails`]
- The binding store is not a runtime authorization source for Consumer pages. Consumer pages access data only after the IdP emits exactly one verified `party_id` claim and the existing resolver accepts it. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Trade-offs`]
- Do not modify submodule files for this story without explicit approval. The provisioning implementation should live in this repo's UI/BFF/AdminPortal scope unless a later architecture decision explicitly moves it. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### UX and Product Guardrails

- This is an Admin/operator provisioning story, not Consumer self-registration. Do not introduce Consumer self-claiming, duplicate-party lookup UX, identity proofing, or federation flows. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Alternatives-Considered`]
- If an operator UI is added, use existing Admin area posture: terse, precise, policy-gated, keyboard accessible, and localized with surrounding AdminPortal conventions where localization already exists.
- `NoPartyBinding` copy remains neutral and reassuring. It must not disclose internal authorization state, IdP attributes, party ids, tenant ids, or support evidence. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- Use synthetic ids in examples/tests such as `party-readonly-001`, `party-bound-001`, `idp-subject-001`, and `tenant-a`. Do not use real names/emails beyond existing dev-only fixtures.

### Previous Story Intelligence

- Story 4.1 added the accepted ADR, updated Story 4.2 acceptance criteria, and added `ConsumerPartyIdBindingRealmTests.cs` as static topology coverage. Do not duplicate that test intent; extend it only for provisioning-specific cases. [Source: `_bmad-output/implementation-artifacts/4-1-decide-the-consumer-identity-party-id-binding-mechanism-design-spike-adr.md`]
- Story 4.1 review fixed stale planning language and clarified that the story was not production implementation. Story 4.2 is the production implementation story and must update File List/Completion Notes honestly if it adds test-only or production code. [Source: `_bmad-output/implementation-artifacts/4-1-decide-the-consumer-identity-party-id-binding-mechanism-design-spike-adr.md#Senior-Developer-Review-AI`]
- Recent commits are sequential Admin/GDPR and binding-ADR work ending at `feat(story-4.1): Consumer identity party id binding decision ADR`. Keep Story 4.2 focused; do not refactor completed Admin/GDPR paths. [Source: `git log -5`]
- Prior stories record that `dotnet test --filter` can be unreliable under xUnit v3/MTP and that restore-enabled commands may fail on local NuGet/audit access. Prefer focused Release builds, lane runner where possible, and direct test executable filters when needed; record limitations instead of claiming tests passed. [Source: `_bmad-output/implementation-artifacts/tests/test-summary.md`]

### Testing and Validation Guidance

- Required test families:
  - Store/service unit tests: create, duplicate active rejection, rotation, suspend, remove, optimistic version/etag conflict, drift detection, unauthorized operator denial, no-PII audit fields.
  - IdP adapter tests: set attribute, clear attribute, read/reconcile attribute, no secret/token logging, failure mapping.
  - Host/routing tests: bound -> `/me`; absent/empty/multiple/removed/suspended -> `/no-party-binding`.
  - Static topology tests: mapper shape remains `party_id` single-valued and emitted to required token surfaces.
  - Boundary tests: no binding commands/events/projections/actors, no public actor-host endpoint, no DAPR ACL expansion, no browser bearer-token flow.
- Use xUnit v3, Shouldly, NSubstitute, and bUnit to match the repo. Do not introduce Moq, FluentAssertions, or package versions in csproj files.
- Run at least:
  - `git diff --check`
  - `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` if UI host services are touched
  - `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release --no-restore -m:1 -v:minimal` if AdminPortal is touched
  - `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` for host/routing tests
  - `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore -m:1 -v:minimal` for topology tests
  - `pwsh scripts/test.ps1 -Lane unit` and `pwsh scripts/test.ps1 -Lane topology` when the local environment supports them.

### Out of Scope

- No ConsumerPortal RCL or `/me` page implementation; Story 4.3 owns ConsumerPortal stand-up.
- No Consumer self-registration, identity proofing UX, duplicate-party lookup UX, or IdP federation.
- No Parties command/event/projection/aggregate/actor changes for identity binding.
- No EventStore, Tenants, or Memories submodule changes unless separately approved.
- No DAPR ACL expansion, public `parties` actor-host endpoint, browser bearer-token handling, or direct browser calls to EventStore.
- No production KMS/key-management work.

### Latest Technical Information

- No package upgrade or external dependency research is required for this story. Use the pinned local stack: .NET 10 SDK `10.0.302`, FluentUI Blazor `5.0.0-rc.3`, Microsoft OIDC packages `10.0.8`, xUnit v3, Shouldly, NSubstitute, and bUnit. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]
- Keycloak/tache requirements are local contract requirements from the accepted ADR and committed realm import: user attribute `party_id` -> claim `party_id`, single-valued, emitted to token surfaces used by the host. Do not add a generic Keycloak library or version bump unless implementation proves it is necessary and central package management is updated deliberately. [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md#Selected-Mechanism`]

### Project Structure Notes

- Prefer new identity-binding implementation files in the UI/BFF or AdminPortal ownership area:
  - `src/Hexalith.Parties.UI/IdentityBinding/*` for host-owned service/store/IdP adapter/options.
  - `src/Hexalith.Parties.AdminPortal/Components/*` and `Services/*` only if a visible Admin provisioning surface is added.
  - `tests/Hexalith.Parties.UI.Tests/*` for service/routing/component tests.
  - `tests/Hexalith.Parties.IntegrationTests/Topology/*` for static realm/topology tests.
- Do not put identity-binding contracts under `src/Hexalith.Parties.Contracts`; that package is the Parties domain/event contract and must remain infrastructure-free.
- Do not use `docs/` as scratch space. Planning/story artifacts belong in `_bmad-output/`; production docs updates are separate, intentional changes.

### References

- [Source: `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.2-Implement-admin-link-party_id-binding-provisioning`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `docs/api-contracts.md#The-boundary-in-one-picture`]
- [Source: `docs/development-guide.md#4-Test`]
- [Source: `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`]
- [Source: `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor`]
- [Source: `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`]
- [Source: `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs`]

## Validation Summary

- Source discovery loaded BMAD workflow files, config, sprint status, persistent project-context files, planning epics, architecture, accepted Consumer binding ADR, UX design/review files, brownfield architecture/API/development docs, previous Story 4.1, existing UI auth/self-scope files, AdminPortal authorization patterns, Keycloak realm config, topology/routing tests, recent git history, and prior test-summary notes.
- Checklist fixes applied before finalizing: made the implementation/store boundary explicit, separated IdP runtime claim from audit store, identified current files to read before modification, pinned preservation of fail-closed consumer seams, added drift/concurrency/security/privacy tests, and forbade event-stream/public-endpoint/DAPR/browser-token regressions.
- Latest-technology review found no package upgrade requirement; the story relies on the local ADR and committed realm contract instead of external API drift.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: `dotnet test tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-build -v:minimal` failed before executing tests with `System.Net.Sockets.SocketException (13): Permission denied` while the .NET test runner created its named-pipe/socket server.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` and `pwsh scripts/test.ps1 -Lane topology` were attempted; the script attempted restore/build and printed `Build failed with exit code: 1` for restore-enabled project builds in this sandbox. Focused no-restore builds and compiled xUnit runners were used for validation.
- 2026-06-10: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10: `dotnet build src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor` passed: 273 tests, 0 failed.
- 2026-06-10: `tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests -noLogo -noColor -namespace "Hexalith.Parties.IntegrationTests.Topology"` passed: 10 tests, 0 failed.
- 2026-06-10: `git diff --check` passed.
- 2026-06-10 review: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10 review: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -noLogo -noColor` passed: 278 tests, 0 failed.
- 2026-06-10 review: `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- 2026-06-10 review: `tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests -noLogo -noColor -namespace Hexalith.Parties.IntegrationTests.Topology` passed: 10 tests, 0 failed.
- 2026-06-10 review: `npm run typecheck` in `tests/e2e` passed.
- 2026-06-10 review: `git diff --check` passed.

### Completion Notes List

- Added a UI/BFF-owned identity binding provisioning subsystem under `src/Hexalith.Parties.UI/IdentityBinding/`, including immutable binding/audit records, MVP in-memory audit store, IdP party attribute adapter seam, optimistic version checks, create/rotate/suspend/remove/reconcile service operations, and DI registration.
- Reused `IAdminPortalAuthorizationService`/`AdminPortalAuthorizationState` to gate provisioning operations without adding a second role matrix. No operator UI was added; the tested service entry point proves the end-to-end provisioning behavior.
- Preserved the existing Consumer runtime seams: `PartyIdClaimResolver`, `RoleLandingRedirect`, `NoPartyBinding`, and `ISelfScopedPartiesClient` were not changed. Runtime access still depends on exactly one IdP-emitted `party_id` claim.
- Added service, routing, composition, boundary, and privacy guardrail tests covering duplicate active rejection, rotation, version conflict, suspend/remove attribute clearing, unauthorized operator denial, drift detection, empty/ambiguous fail-closed routing, no event-stream placement, no public identity-binding endpoint, no DAPR ACL expansion, and no browser token-flow expansion.
- Senior review auto-fixes gated reconciliation behind the existing AdminPortal authorization service, added rollback for failed IdP attribute set/clear operations, preserved audit history when relinking after suspended/removed records, and added regression coverage for those cases.

### File List

- `_bmad-output/implementation-artifacts/4-2-implement-admin-link-party-id-binding-provisioning.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/ChangeIdentityBindingStatusRequest.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/CreateIdentityBindingRequest.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IIdentityBindingProvisioningService.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IIdentityBindingStore.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IIdentityProviderPartyAttributeClient.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingAuditEntry.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingDriftReport.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingKey.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingOperationResult.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingProvisioningService.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingRecord.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingStatus.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/IdentityBindingStoreConflictException.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/InMemoryIdentityBindingStore.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/InMemoryIdentityProviderPartyAttributeClient.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/ReconcileIdentityBindingRequest.cs`
- `src/Hexalith.Parties.UI/IdentityBinding/RotateIdentityBindingRequest.cs`
- `tests/Hexalith.Parties.UI.Tests/IdentityBindingBoundaryTests.cs`
- `tests/Hexalith.Parties.UI.Tests/IdentityBindingProvisioningServiceTests.cs`
- `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-party-binding.spec.ts`

## Senior Developer Review (AI)

### Findings Fixed

- [HIGH] `ReconcileAsync` was not authorization-gated even though the story required reconcile actions to use the existing AdminPortal operator authorization path. Fixed by checking `IAdminPortalAuthorizationService` before reading binding or IdP state.
- [HIGH] Link/rotate/suspend/remove committed the audit store before IdP attribute updates succeeded, which could leave an active store record without the runtime `party_id` claim. Fixed with version-checked rollback on failed IdP set/clear operations.
- [MEDIUM] Relinking after a suspended or removed record replaced the store record with a new audit trail, losing historical audit metadata. Fixed by appending the new link audit entry to the existing trail.
- [MEDIUM] The story File List omitted the new Consumer Playwright binding spec and test summary update. Fixed in the File List above.

### Review Validation

- ACs and completed tasks were cross-checked against implementation files, tests, and the accepted ADR boundary.
- Focused UI build and compiled xUnit run passed: 278 tests, 0 failed.
- Focused topology build and compiled topology namespace run passed: 10 tests, 0 failed.
- `npm run typecheck` in `tests/e2e` passed.
- `git diff --check` passed.

### Change Log

- 2026-06-10: Implemented Story 4.2 admin-link identity binding provisioning service/store/adapter, tests, and validation updates.
- 2026-06-10: Senior review auto-fixed reconciliation authorization, IdP failure rollback, retained relink audit history, and story File List completeness; marked story done.
