---
baseline_commit: 9a59b48
---

# Story 4.1: Decide the Consumer identity -> `party_id` binding mechanism (design spike -> ADR)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a product owner / architect,
I want a decided, recorded mechanism for binding a consumer's identity to their party,
so that the Consumer area can be estimated and built against a known design.

## Acceptance Criteria

1. Given the undesigned AR-Gap-Binding gap blocks Epics 4-5, when the options `admin-link`, `self-registration`, and `IdP federation` are evaluated against tenancy, fail-closed resolution, provisioning effort, production Keycloak/tache fit, auditability, and where verified `party_id` is held, then exactly one mechanism is selected and recorded in an ADR.
2. Given the selected mechanism, when the ADR is written, then it records the decision, status, context, selected option, rejected alternatives, trade-offs, provisioning/onboarding flow, trust boundary, failure behavior, and binding-store shape if any. The binding must live in the IdP claim and/or a small binding store, never in the Parties event stream.
3. Given the existing UI host already consumes a verified `party_id` claim fail-closed, when the ADR describes implementation impact, then it preserves `PartyIdClaimResolver`, `NoPartyBinding`, and `ISelfScopedPartiesClient` as consumers of the binding rather than redesigning those seams.
4. Given the ADR is accepted, when Story 4.2 acceptance criteria are updated, then they are derived directly from the chosen option with no open design questions, and Story 4.2 references the ADR as its source.
5. Given this is a decision spike, when the story is complete, then no production binding implementation, no ConsumerPortal feature work, no EventStore command/event changes, and no new Parties event-stream mapping have been added.

## Tasks / Subtasks

- [x] Create the Consumer binding ADR (AC: 1, 2)
  - [x] Add `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`.
  - [x] Use status `Accepted` and include: Context, Decision, Selected Mechanism, Alternatives Considered, Trade-offs, Provisioning / Onboarding Flow, Binding Data Shape, Security / Privacy Guardrails, Implementation Impact, Test Strategy, and Consequences.
  - [x] Evaluate all required options: `admin-link`, `self-registration`, and `IdP federation`.
  - [x] State whether the binding is IdP-only or IdP plus a small binding store. If a store is selected, define fields, ownership, lookup key, update path, and deletion/rotation behavior. Do not use the Parties event stream for the mapping.

- [x] Derive implementation requirements for Story 4.2 (AC: 2, 4)
  - [x] Update `_bmad-output/planning-artifacts/epics.md` Story 4.2 acceptance criteria so the provisioning flow, files/areas likely to change, tests, and fail-closed behavior come directly from the ADR.
  - [x] Remove vague wording such as "mechanism selected in Story 4.1" from Story 4.2 where it can be replaced by concrete ADR-derived requirements.
  - [x] Keep Story 4.2 as the implementation story; do not implement the binding in Story 4.1.

- [x] Update architecture planning notes to close AR-Gap-Binding (AC: 1, 2, 4)
  - [x] Update `_bmad-output/planning-artifacts/architecture.md` D2 / Gap Analysis sections to reference the ADR and the selected mechanism.
  - [x] Preserve the existing decisions: host-owned OIDC, server-side tokens only, Consumer policy, fail-closed `party_id` resolution, BFF self-scope, and Parties-side defense-in-depth.
  - [x] Keep the residual gateway self-principal risk deferred unless the selected mechanism explicitly requires changing it in a future story.

- [x] Validate consistency and scope (AC: 3, 5)
  - [x] Confirm the ADR references the existing consumers: `PartyIdClaimResolver`, `RoleLandingRedirect`, `NoPartyBinding`, `ISelfScopedPartiesClient`, and the Keycloak realm mapper shape.
  - [x] Confirm no production code files were changed unless they are purely documentation comments required to link the ADR.
  - [x] Confirm no new command, event, projection, actor, DAPR ACL, public endpoint, or browser token flow was introduced.
  - [x] Run `git diff --check`.

## Dev Notes

### Current Implementation State

- Story 1.4 already delivered `PartyIdClaimResolver` as a Scoped, dependency-free, fail-closed resolver for exactly one non-empty `party_id` claim. Zero, empty, or multiple claims resolve unbound; a single claim resolves `{tenant, party_id}`. Preserve this consumer seam. [Source: src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs]
- Story 1.3/1.4 already route Consumers through `RoleLandingRedirect`: bound Consumers go to `/me`, unbound or ambiguous Consumers go to `/no-party-binding`, never to a data screen. Preserve this behavior and make the ADR's failure flow match it. [Source: src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor] [Source: src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor]
- Story 1.5 already delivered `ISelfScopedPartiesClient` / `SelfScopedPartiesClient`, which resolves the current principal and injects the resolved party id into query/GDPR client calls. It never accepts a caller-supplied party id and throws before gateway calls when unbound. Preserve this as the only Consumer data-access path. [Source: src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs]
- The committed dev Keycloak realm already includes a `party-id-mapper` on `hexalith-parties-ui`, mapping user attribute `party_id` to claim `party_id` on id token, access token, and userinfo, with `multivalued=false`. It also seeds `readonly-user` with `Consumer` and `party_id=["party-readonly-001"]`. This is evidence for the IdP-claim/admin-link option, not a final production decision by itself. [Source: src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json]
- Topology tests already pin the confidential `hexalith-parties-ui` authorization-code client, EventStore audience mapper, flat `roles` claim, and `Consumer` role. If the ADR selects an IdP-based mechanism, Story 4.2 should extend this topology-test style rather than start a running-Keycloak-only test requirement. [Source: tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiKeycloakRealmTests.cs] [Source: tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiRealmRolesTests.cs]

### Decision Inputs the ADR Must Evaluate

- `admin-link`: an authorized Admin/TenantOwner or support operator binds an existing Party to an existing IdP user. Strong fit with the current realm mapper and fail-closed resolver; lower MVP complexity; requires clear operator flow and audit trail outside the Parties event stream.
- `self-registration`: a Consumer claims or creates a binding themselves. Higher UX value but must prevent account takeover, duplicate party creation, unverified identifier matching, and privacy leakage. It likely requires verification and a binding store/workflow beyond the current seed-user mapper.
- `IdP federation`: an upstream identity provider asserts a stable external identity that maps to a Party. Strong enterprise fit when upstream identity proof exists; higher integration dependency and may need tenant-specific federation/mapping contracts.
- Recommended default unless the ADR finds contrary evidence: choose an admin-link flow that writes `party_id` as a verified IdP user attribute/claim for MVP, because the current realm already supports that claim shape and existing UI code consumes it fail-closed. If the decision needs auditability that Keycloak attributes cannot provide alone, specify a small binding store owned outside the Parties event stream and make Story 4.2 implement it.

### Current Files Being Modified - Required Reading

- `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md` (NEW)
  - Current state: absent.
  - What this story changes: create the ADR that closes AR-Gap-Binding.
  - Preserve: planning artifact location; do not use `docs/` as scratch. `docs/` is published DocFX content and should only be edited when the decision is ready to become product documentation.
- `_bmad-output/planning-artifacts/epics.md`
  - Current state: Story 4.2 depends on "the mechanism selected in Story 4.1" and still carries open design wording.
  - What this story changes: update Story 4.2 ACs with concrete ADR-derived provisioning, claim/store, fail-closed, and test requirements.
  - Preserve: all other epic/story statuses, sequencing, and already completed Epic 1-3 history.
- `_bmad-output/planning-artifacts/architecture.md`
  - Current state: D2 says Consumer identity is a verified `party_id` claim, but Gap Analysis still says the provisioning mechanism is undesigned.
  - What this story changes: reference the accepted ADR and replace the gap with the selected mechanism summary.
  - Preserve: existing D1/D3/D5 decisions, no-browser-token rule, BFF/self-scope, EventStore gateway boundary, and no event-stream mapping.

### Architecture Guardrails

- The Parties host has no public API; public traffic enters the Hexalith.EventStore gateway. Story 4.1 must not add actor-host endpoints, public controllers, or direct browser-to-gateway flows. [Source: _bmad-output/project-context.md#Framework-Specific-Rules-Event-Sourcing--CQRS--DAPR-behind-EventStore]
- The UI host owns OIDC sign-in and stores tokens server-side. OIDC tokens never leave the server; the browser must not receive bearer tokens or call EventStore directly. [Source: _bmad-output/planning-artifacts/architecture.md#Authentication--Security]
- The binding mapping may be held in the IdP claim and/or a small binding store, but never in the Parties event stream. Do not add Parties commands/events/projections for identity binding in this spike. [Source: _bmad-output/planning-artifacts/architecture.md#Data-Architecture]
- Consumer data access remains self-scoped: Consumers never call list/search, and every Consumer operation must resolve the bound party id through the single accessor before using gateway clients. [Source: _bmad-output/planning-artifacts/architecture.md#Process-Patterns]
- Tenant claim remains `eventstore:tenant`, normalized by `PartiesClaimsTransformation`; the effective Consumer scope is `{tenant, party_id}`. [Source: src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs]
- No real PII should appear in the ADR, sample payloads, tests, logs, or seed docs. Use synthetic ids such as `party-readonly-001`; do not include decoded JWT payloads, secrets, or real user data. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]

### UX and Product Guardrails

- Unbound Consumers see the existing neutral onboarding/error state, not a data screen. The ADR's failure path should use that concept: "account not linked yet" instead of exposing authorization internals. [Source: src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor]
- Consumer copy is plain and reassuring; say what will happen in human words, then name the right where relevant. Story 4.1 is not a UI copy story, but the provisioning/onboarding flow in the ADR should not imply misleading timing or legal promises. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Foundation]
- Consumer surfaces are phone-first, roomy, and localized in later ConsumerPortal stories. The ADR should not prescribe hardcoded future page copy or build ConsumerPortal in this spike. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Density--Radii]

### Previous Story Intelligence

- Epic 3 finished with the pattern that planning stories should keep scope tight, identify exact files, preserve existing seams, and validate with `git diff --check`. Reuse that discipline here. [Source: _bmad-output/implementation-artifacts/3-6-admin-erasure-verification-report-ui-consumes-the-d7-contract.md]
- Recent commits are sequential GDPR/UI work ending at `feat(story-3.6): Admin erasure verification report UI consumes the D7 contract`; Story 4.1 starts the next epic and should not refactor the completed Admin/GDPR path. [Source: git log -5]

### Testing and Validation Guidance

- This is a decision-spike story. Expected validation is document consistency, not a full product test lane, unless the implementation adds test-only checks around planning artifacts.
- Run `git diff --check`.
- Recommended consistency checks:
  - `rg -n "AR-Gap-Binding|Story 4\\.2|party_id|admin-link|self-registration|IdP federation" _bmad-output/planning-artifacts`
  - `rg -n "party-id-mapper|party_id|readonly-user|Consumer" src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json tests/Hexalith.Parties.IntegrationTests/Topology`
- If any production code is touched, run the narrow relevant build/test lane and explain why code changes were necessary despite the spike scope.

### Out of Scope

- Do not implement the selected binding mechanism; that is Story 4.2.
- Do not create `Hexalith.Parties.ConsumerPortal`; that is Story 4.3.
- Do not change `PartyIdClaimResolver`, `RoleLandingRedirect`, `NoPartyBinding`, or `ISelfScopedPartiesClient` behavior unless the ADR uncovers a contradiction and explicitly schedules it for a later story.
- Do not add a Parties command/event/projection for identity binding.
- Do not modify Hexalith.EventStore, Hexalith.Tenants, DAPR ACLs, or the EventStore gateway self-principal model.
- Do not add package dependencies or change Central Package Management.

### Latest Technical Information

- No external dependency or package upgrade is required for this story. Use the pinned local stack: .NET 10, OpenIdConnect/JwtBearer 10.0.8, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, Shouldly, NSubstitute, and bUnit where tests are needed. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- The relevant technical specifics are local: existing Keycloak realm mapper shape, existing fail-closed resolver/accessor, and architecture planning docs. No web research or library upgrade is needed to decide the binding mechanism.

### Project Structure Notes

- Planning output belongs in `_bmad-output/planning-artifacts/`.
- Keep `docs/` out of this story unless the ADR decision is intentionally promoted into published product docs; it is not a scratch area.
- Keep all changes additive and narrow. Do not reformat the whole epics or architecture file.
- Use ASCII in new planning text unless a file already requires Unicode.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.1-Decide-the-Consumer-identity-party_id-binding-mechanism-design-spike-ADR]
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication--Security]
- [Source: _bmad-output/planning-artifacts/architecture.md#Gap-Analysis-Results]
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Foundation]
- [Source: src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs]
- [Source: src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs]
- [Source: src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor]
- [Source: src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor]
- [Source: src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs]
- [Source: src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json]
- [Source: tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiKeycloakRealmTests.cs]
- [Source: tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiRealmRolesTests.cs]

## Validation Summary

- Source discovery loaded project context facts, sprint status, planning epics, architecture, UX design/experience/reviews, previous Story 3.6, current UI authentication/self-scope files, Keycloak realm topology tests, Keycloak realm mapper shape, and recent git history.
- Checklist fixes applied before finalizing: kept Story 4.1 as a decision spike only, pinned reuse of existing fail-closed consumers, required all three binding options to be evaluated, specified ADR output location, made Story 4.2 derivation explicit, and forbade event-stream identity binding or production implementation in this story.
- Latest-technology review found no new dependency requirement; implementation should use the pinned local stack and existing OIDC/Keycloak planning artifacts.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: Loaded BMAD dev-story workflow, DoD checklist, project contexts, sprint status, and full Story 4.1.
- 2026-06-10: Preserved existing `baseline_commit: 9a59b48`; updated sprint status to `in-progress` before implementation.
- 2026-06-10: Red check for ADR file/headings failed before creation as expected.
- 2026-06-10: Created accepted ADR selecting admin-link with IdP `party_id` claim plus small binding audit store; updated Story 4.2 and architecture planning sections.
- 2026-06-10: Consistency scans passed for Story 4.2 wording; senior review later fixed stale architecture handoff wording that still described D2 as a design prerequisite.
- 2026-06-10: `git diff --check` passed.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` attempted; wrapper printed repeated `Build failed with exit code: 1` messages while returning process exit 0. Focused `dotnet test` / `dotnet restore` on `tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj` also failed during restore with exit code 1 and 0 MSBuild errors/warnings, matching the local test harness issue recorded in prior story notes.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane all` attempted for full regression; wrapper printed `Build failed with exit code: 1` while returning process exit 0, same local harness/restore failure.

### Completion Notes List

- Accepted ADR added for Consumer identity binding. Decision: admin-link provisioning for MVP, runtime `party_id` from the IdP claim, and small external binding audit/reconciliation store; no Parties event-stream mapping.
- Story 4.2 acceptance criteria now name the concrete provisioning flow, store shape, likely implementation areas, tests, and fail-closed behavior derived from the ADR.
- Architecture D2 and Gap Analysis now reference the accepted ADR and selected admin-link mechanism while preserving host-owned OIDC, server-side tokens, `PartyIdClaimResolver`, `NoPartyBinding`, `ISelfScopedPartiesClient`, BFF self-scope, Parties-side defense-in-depth, and the deferred gateway self-principal risk.
- Scope stayed decision-spike only: no production code, commands, events, projections, actors, DAPR ACLs, public endpoints, browser token flows, package dependencies, or ConsumerPortal implementation were added. A static topology test was added to pin the existing Keycloak realm mapper and bound Consumer seed shape against the accepted ADR.

### File List

- `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/4-1-decide-the-consumer-identity-party-id-binding-mechanism-design-spike-adr.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs`

### Change Log

- 2026-06-10: Completed Story 4.1 decision spike; accepted admin-link ADR, derived Story 4.2 requirements, closed AR-Gap-Binding planning notes, and validated no-production-implementation scope.
- 2026-06-10: Senior review auto-fixes applied; recorded topology test coverage in the story File List, corrected stale architecture handoff wording, and synced sprint status to done.

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after auto-fixes.

Findings fixed:

- [Medium] Story File List omitted `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs` even though the review surface added this static topology test for the accepted ADR mapper and seed-user contract. Fixed by adding the test and updated test summary to the File List.
- [Medium] Completion notes said the story stayed "documentation/planning only" while a test-only topology contract was added. Fixed the note to distinguish no production implementation from added validation coverage.
- [Medium] `_bmad-output/planning-artifacts/architecture.md` still told implementers to resolve the D2 binding-provisioning design before Consumer work, despite the accepted ADR. Fixed the readiness and handoff language to point to Story 4.2 implementation instead.
- [Low] Sprint tracking still held Story 4.1 at `review` and used the old Story 4.2 slug after the title was made concrete. Fixed Story 4.1 to `done` and aligned the Story 4.2 key with the updated title.

Review notes:

- Acceptance Criteria 1-4 are implemented by the accepted ADR plus concrete Story 4.2 and architecture planning updates.
- Acceptance Criterion 5 holds: no production binding implementation, ConsumerPortal feature work, EventStore command/event change, or Parties event-stream mapping was added.
- The modified published `docs/` files describe prior D7/GDPR documentation updates and are not part of the Story 4.1 File List or Consumer binding review surface.
