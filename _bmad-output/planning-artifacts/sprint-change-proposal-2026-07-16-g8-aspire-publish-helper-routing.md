---
title: Sprint Change Proposal — Route G8 Aspire Helpers and AppHost Ownership
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approval_required: false
approved_at: 2026-07-16T00:42:32+02:00
applied_at: 2026-07-16T00:44:50+02:00
handoff_status: routed
trigger: >
  Route G8 Aspire publish helpers to Hexalith.EventStore.Aspire and the
  AppHost owners, provide audience-aware JWT and granular typed-client
  registration, and confirm the Parties topology/deployment owner before
  Story 8.8 starts.
related:
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - docs/architecture.md
  - docs/deployment-guide.md
  - docs/source-tree-analysis.md
  - references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs
  - references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs
  - references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.AppHost/Program.cs
---

# Sprint Change Proposal — Route G8 Aspire Helpers and AppHost Ownership

## 1. Issue Summary

Story 8.8 cannot safely remove Parties-local AppHost wiring because G8 has two
unclosed platform API requirements and an imprecise topology-owner boundary:

1. An audience-aware EventStore Aspire JWT helper, either
   `WithEventStoreJwtAuthentication(audience)` or an owner-documented
   `WithJwtBearerSecurity(..., audience)` replacement with equivalent run and
   publish behavior.
2. Granular, composable typed-client registration that preserves the
   Parties-specific command, query, and GDPR clients while sharing EventStore
   transport behavior.
3. A named owner for the canonical integrated topology containing Parties, and
   a clear distinction between local topology, container publication, and
   runtime deployment orchestration.

The G8 matrix row correctly remains `needs-additive-api`. The current source
provides useful building blocks, but not enough evidence to authorize deletion
of `src/Hexalith.Parties.AppHost/` or the Parties client registrations.

### Trigger and classification

- **Triggering story:** Epic 8 Story 8.8, Client, MCP, AppHost, build, and
  runtime-boundary cleanup.
- **Issue type:** technical/API limitation plus architecture ownership
  clarification during sprint execution.
- **Scope:** moderate. This is coordinated platform-owner work and planning
  correction across EventStore.Aspire, EventStore.Client, FrontComposer.AppHost,
  Parties, and platform operations; it adds no product feature or epic.
- **Immediate effect:** Story 8.8 remains in `backlog` and must not start its G8
  deletion/migration slice until the owner evidence gate is satisfied.

### Evidence verified 2026-07-16

- `HexalithEventStoreSecurityExtensions` exposes
  `WithJwtBearerSecurity(resource, security, audience, requireHttpsMetadata)`.
  It writes authority, issuer, audience, HTTPS-metadata, and signing-key
  settings from a concrete `HexalithEventStoreSecurityResources` object.
- Parties currently has a private `WithJwtAuthentication` helper that covers
  both local realm settings and publish-mode external authority/issuer settings.
  It also supplies multi-resource valid audiences: the workload's own audience
  plus `eventstore` where required.
- The current `WithJwtBearerSecurity` overload is therefore not yet proven as a
  complete replacement for the Parties run/publish path. Owner documentation or
  an additive helper must settle external-orchestrator settings, issuer/
  authority selection, audience semantics, HTTPS metadata, and signing-key
  clearing without emitting secrets.
- `AddEventStoreGatewayClient` registers only
  `IEventStoreGatewayClient`/`EventStoreGatewayClient`. It does not provide
  granular module-typed registration for `IPartiesCommandClient`,
  `IPartiesQueryClient`, or `IAdminPortalGdprClient`.
- Parties currently registers those three typed clients independently.
  FrontComposer also owns EventStore-specific named command/query clients and
  service replacements, so an all-or-nothing registration helper would risk
  removing or overriding module registrations.
- `AddEventStoreDomainModule` already owns reusable Dapr sidecar, state-store,
  pub/sub, app-id, and health composition. Its documentation says domain modules
  should not ship their own AppHost/Aspire wiring.
- `Hexalith.FrontComposer.AppHost` already composes EventStore, Tenants, and
  Parties and uses `AddEventStoreDomainModule` and
  `WithJwtBearerSecurity`. The Parties G8 fitness-test owner mapping also points
  “platform AppHost owners” to the FrontComposer repository.
- That FrontComposer composition is an owner candidate and target foundation,
  not current parity evidence: it currently includes `eventstore`, `tenants`,
  `parties`, and `frontcomposer-ui`, but not `parties-mcp` or the standalone
  `parties-ui`. Its AppHost is publishable but currently declares no
  `PUBLISH_TARGET` Docker/Kubernetes/ACA environment selection. By contrast,
  `aspire ls` in the Parties workspace finds only the current Parties AppHost,
  whose own `aspire.config.json` remains the active local entry point.
- Parties deployment documentation already says this repository owns its three
  workload images and GitHub Actions publication, while an external deployment
  orchestrator owns environment-specific Dapr, ingress, secrets, image pull,
  scanning/signing, and promotion.
- G12 package publication is now resolved. The Story 8.8 draft still describes
  G12 as `blocked`; that statement is stale and must be corrected separately
  from the still-open G6/G8/G11/G7-G9 gates.

## 2. Ownership Decision

The approved owner split is:

| Concern | Accountable owner | Parties responsibility |
|---|---|---|
| Reusable Aspire domain-module and JWT/publish helpers | `Hexalith.EventStore.Aspire` owners | Consume only after owner proof and parity |
| Shared EventStore transport and granular registration primitives | `Hexalith.EventStore.Client` owners, coordinated with FrontComposer consumers | Retain domain-typed interfaces and validate coexistence |
| Target canonical integrated local topology containing EventStore, Tenants, and Parties | `Hexalith.FrontComposer.AppHost` / approved platform AppHost owners | Supply Parties resource requirements and consumer parity tests; retain the current AppHost until handoff acceptance |
| Parties workload source and immutable image publication | Parties repository owners | Continue owning `parties`, `parties-mcp`, and `parties-ui` CI publication |
| Runtime deployment manifests, apply, ingress, Dapr production resources, secrets, pull credentials, signing/scanning, promotion | External platform deployment orchestrator / platform operations | Document the workload contract; do not reclaim deployment orchestration |

`src/Hexalith.Parties.AppHost/` remains a temporary compatibility and rollback
surface while G8 is open. After the FrontComposer/platform AppHost proves
topology and publish parity, Story 8.8 retires the domain-owned AppHost rather
than preserving a permanent “thin AppHost.” This resolves the conflict between
the repository-wide domain-module rule and the current Epic 8 wording.

## 3. Impact Analysis

### Epic and story impact

- **Epic 8 remains viable.** No new epic, product scope, or MVP requirement is
  introduced.
- The existing sequence remains
  `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`.
- **Story 8.8 remains `backlog`.** Its AppHost/client slice is gated; routing the
  work does not make G8 `available`.
- Story 8.10 may close the AppHost retirement item only after the owner decision,
  exact consumption identity, parity evidence, and rollback disposition are
  recorded.
- Other epics and the delivered MVP are unaffected.

### Artifact impact

- **PRD:** no change. No functional or non-functional product requirement changes.
- **UX:** no change. No screen, interaction, copy, accessibility, or design-token
  behavior changes.
- **Epic 8 architecture spine:** change the target boundary from Parties keeping
  a “thin AppHost” to Parties keeping domain samples while canonical integrated
  topology moves to the platform AppHost owner.
- **Epics:** align the Epic 8 summary and Story 8.8 acceptance criteria with that
  owner boundary.
- **Story 8.8 spec:** remove stale G12-blocked language, make G8 exit conditions
  explicit, and identify the temporary Parties AppHost rollback surface.
- **Story 8.3 prerequisite matrix:** expand the G8 row into named owner work
  packages and proof criteria; retain `needs-additive-api`.
- **Sprint status:** route the G8 action and mark that action `in-progress` after
  approval; do not change Story 8.8 status.
- **Deployment/project docs:** record current-versus-target local topology
  ownership while retaining the already-correct external runtime-deployment
  boundary.
- **Owner repositories:** no submodule content is changed by this workflow.
  EventStore and FrontComposer owners implement and approve their work in their
  own repositories.

### Risk and rollback

- **Effort:** low for the approved Parties planning edits; medium for the owner
  API and topology proof; medium for later Story 8.8 consumer migration.
- **Primary risk:** treating the existing single-audience overload or generic
  gateway client as sufficient and deleting Parties wiring before publish-mode,
  multi-audience, and typed-client coexistence are proven.
- **Rollback:** retain the current Parties AppHost and typed-client registrations
  until the platform topology and client registration path pass parity. There is
  no reason to roll back completed product functionality.
- **Timeline:** Story 8.8 remains paused at the G8 slice; no MVP impact.

## 4. Recommended Approach

**Selected: direct adjustment with three owner work packages and a consumer
deletion gate.**

### Package A — audience-aware Aspire security

Owner: `Hexalith.EventStore.Aspire`.

Deliver one of:

1. An additive `WithEventStoreJwtAuthentication(audience)`-style helper; or
2. Owner-approved documentation and tests establishing
   `WithJwtBearerSecurity(..., audience)` as the canonical replacement.

The proof must cover:

- local run mode and publish/external-orchestrator mode;
- authority, issuer, audience, valid-audience, HTTPS-metadata, and signing-key
  behavior;
- Parties, Parties MCP, Tenants, and EventStore audience relationships;
- no secret or token material in a publish manifest;
- the exact package version or root-declared submodule pin that Parties may
  consume.

### Package B — granular typed-client registration

Owner: `Hexalith.EventStore.Client`, reviewed against FrontComposer and Parties
consumer registrations.

Deliver additive, independently selectable registration primitives that:

- share EventStore endpoint, validation, correlation, bounded ProblemDetails,
  and handler behavior where appropriate;
- allow command, query, and GDPR clients to be registered independently;
- preserve module-typed interfaces and do not remove, replace, or duplicate
  FrontComposer/Parties registrations unexpectedly;
- define deterministic handler ordering and repeat-registration behavior;
- include a coexistence test with both EventStore generic transport and
  module-typed clients;
- publish an exact approved package version or root-declared submodule pin.

The owner may choose a generic extension, typed builders, or documented adapter
pattern; the behavioral contract matters more than the method name.

### Package C — topology and deployment ownership proof

Owners: `Hexalith.FrontComposer.AppHost` for canonical integrated local
topology; external platform operations for runtime deployment.

The owner must first close the known composition gap, then prove that the
canonical AppHost preserves or intentionally versions:

- resource names and dependencies for EventStore, Parties, Parties MCP,
  Parties UI, and Tenants, or an explicit replacement map where
  `frontcomposer-ui` supersedes the standalone Parties UI without losing its
  routes, policies, OIDC/BFF behavior, health contract, or published workload
  obligations;
- `AddEventStoreDomainModule` use for domain hosts;
- Dapr sidecars/components and deny-by-default ACL with only the required
  EventStore-to-Parties `/process` path;
- run-mode and publish-mode security environment parity;
- Docker, Kubernetes, and ACA publish-environment selection or an explicit
  owner-approved reduction of that surface;
- source-mode and package-mode consumption expectations for reusable libraries;
- health/resource references and the approved container identities;
- the external deployment handoff without restoring production manifests to
  the Parties repository.

Because `Hexalith.FrontComposer.AppHost` is non-packable, its consumption proof
must name an exact FrontComposer source commit/root gitlink or a separately
approved host artifact identity. It must also name the exact
`Hexalith.EventStore.Aspire` package/submodule identity it consumes; a generic
“latest platform AppHost” reference is not acceptable.

The Parties AppHost is deleted only after this proof is accepted and the
Parties topology lane passes against the platform-owned host. Current
source-reading fitness tests that are hard-coded to
`src/Hexalith.Parties.AppHost/` must be re-homed to the owner repository or
adapted into consumer parity tests before that directory is removed.

### Alternatives considered

- **Keep a permanent thin Parties AppHost:** rejected because it conflicts with
  the repository's domain-module rule and duplicates a cross-domain topology
  already composed by FrontComposer.AppHost.
- **Make EventStore.AppHost the integrated Parties topology owner:** not selected.
  EventStore owns its canonical service/sample host and reusable Aspire helpers,
  while FrontComposer.AppHost is the existing cross-domain composition that
  explicitly includes Parties and Tenants. FrontComposer must still add or
  explicitly disposition Parties MCP, standalone Parties UI, and publish-target
  parity before handoff.
- **Move runtime deployment back into Parties:** rejected. The approved boundary
  is immutable image publication here and environment orchestration in platform
  operations.
- **Proceed with Story 8.8 using local adapters only:** rejected because it would
  hide the platform API/ownership gap and weaken the deletion gate.

## 5. Approved Planning Edits

Administrator approved this proposal on 2026-07-16. The planning, tracking,
and project-documentation edits in this section have been applied; no production
code or submodule content was changed.

### 5.1 Epic 8 architecture spine

Artifact:
`_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`

**OLD:**

```text
| Domain samples, **thin** AppHost | Build-root probing, AppHost
security/module helpers, platform deploy assets -> Builds / platform-ops |
```

**NEW:**

```text
| Domain samples; no domain-owned AppHost in the target state | Build-root
probing -> Builds; reusable security/module helpers -> EventStore.Aspire;
canonical integrated local topology -> FrontComposer.AppHost / approved
platform AppHost owner; runtime deploy orchestration -> platform-ops |
```

Add an invariant that a domain-owned AppHost may remain only as a migration
rollback surface and is retired after platform topology parity.

### 5.2 Epic 8 summary and Story 8.8

Artifact: `_bmad-output/planning-artifacts/epics.md`

- Replace “sample, and a thin AppHost” in the Epic 8 summary with “domain
  samples,” followed by the explicit platform AppHost/deploy ownership boundary.
- Replace Story 8.8's statement that Parties permanently retains local AppHost
  topology with current-versus-target wording: current Parties AppHost remains
  until parity; canonical integrated topology moves to the platform AppHost;
  Parties retains workload source, CI, and owned container publication.
- Add acceptance that G8 remains closed until Packages A-C have named owner
  approval, exact consumption identity, green producer/consumer evidence, and a
  rollback record.

### 5.3 Story 8.3 G8 prerequisite row

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

- Keep status `needs-additive-api`.
- Name the helper owner as `Hexalith.EventStore.Aspire`, the granular transport
  registration owner as `Hexalith.EventStore.Client`, the integrated topology
  owner as `Hexalith.FrontComposer.AppHost`, and runtime deploy owner as external
  platform operations.
- Record that FrontComposer ownership is the target decision, while current
  execution remains rooted at `src/Hexalith.Parties.AppHost/` until the missing
  MCP/UI and publish-target parity is accepted.
- Record Packages A-C and their proof criteria from Section 4.
- Keep the current Parties AppHost and typed-client registration as rollback.
- Change to `available` only after named owner approval, exact release/submodule
  identity, producer tests, Parties consumer topology/coexistence tests, and
  tested rollback are recorded.

Routing is not delivery and does not change the row status.

### 5.4 Story 8.8 spec

Artifact:
`_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`

- Replace the stale “G12 is blocked” statement and AC with the factual result:
  G12 was resolved by package publication on 2026-07-11.
- Retain G6/G8/G11/G7-G9 as independent gated slices.
- Expand G8 to the Packages A-C contract above.
- Split or hard-gate the broad story as already allowed: 8.8a client, 8.8b MCP,
  8.8c AppHost/build, and 8.8d runtime-boundary documentation. The G8 work lands
  in 8.8a/8.8c; external deployment remains a boundary, not an in-repo manifest
  implementation.
- Replace stale `deploy/k8s`/deploy-lane assumptions with the already-approved
  image-publication and external-orchestrator boundary.

### 5.5 Sprint action routing

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:** G8 action owner is
`Winston (Architect) + Hexalith.EventStore.Aspire & AppHost owners`, status
`open`.

**NEW:** name the Package A-C owners, link this proposal, and set the action to
`in-progress`. Story 8.8 remains `backlog`. The action becomes `done` only after
the G8 matrix row records approved delivery and consumption proof.

### 5.6 Current and target project documentation

Artifacts: `docs/architecture.md`, `docs/deployment-guide.md`, and
`docs/source-tree-analysis.md`.

Record both states without pretending the migration has already happened:

- **Current:** `src/Hexalith.Parties.AppHost/` remains the local development and
  rollback entry point.
- **Target after G8 parity:** the approved FrontComposer/platform AppHost is the
  canonical integrated local entry point and the Parties AppHost is retired.
- **Unchanged:** Parties owns workload source and image publication; external
  platform operations owns runtime deployment orchestration.

## 6. Validation and Exit Criteria

G8 is `available` only when all of the following are recorded:

1. EventStore.Aspire owner approval for the audience-aware JWT surface or the
   documented `WithJwtBearerSecurity(..., audience)` replacement.
2. Tests prove local run and publish/external settings, including audience
   relationships and no-secret manifest output.
3. EventStore.Client owner approval for granular, composable typed-client
   registration.
4. Producer and consumer tests prove module-typed clients coexist without
   replacement, duplication, or handler-order regression.
5. FrontComposer/platform AppHost owner approval for the integrated Parties
   topology and an exact FrontComposer source/host-artifact identity plus the
   exact EventStore.Aspire package/submodule identity.
6. The Parties topology lane proves resource, health, security, Dapr ACL, and
   publish behavior parity against that identity, including an explicit
   disposition for `parties-mcp`, standalone `parties-ui` versus
   `frontcomposer-ui`, and Docker/Kubernetes/ACA publish targets.
7. Runtime deployment ownership remains external and no production manifests
   are reintroduced into Parties.
8. The current Parties AppHost and client registrations remain available as
   rollback until the applicable parity evidence is accepted.

Expected consumer verification includes:

```text
dotnet build Hexalith.Parties.slnx -c Release -m:1
pwsh scripts/test.ps1 -Lane topology
```

Focused fitness and owner-repository tests must additionally cover the G8
security tokens, typed-client coexistence, source/package identity, and
publish-manifest safety. Exact commands belong in the approved owner proof and
Story 8.8 implementation record.

## 7. Correct-Course Checklist Result

| Checklist area | Result |
|---|---|
| Triggering story and issue | Complete — Story 8.8, technical/API and owner gap |
| Epic/story impact | Complete — Epic 8 viable; no resequencing or new epic |
| PRD/UX impact | No change |
| Architecture impact | Applied — permanent thin-AppHost target removed from planning |
| Technical path | Viable — direct adjustment with three owner packages |
| Rollback | Current Parties AppHost and client registrations retained until parity |
| Handoff | Architect routes; EventStore/FrontComposer/platform owners deliver; Parties validates |
| Approval | Approved by Administrator on 2026-07-16; planning edits applied |

## 8. Handoff

- **Winston / architecture owner:** track the three routed packages and keep the
  G8 matrix row fail-closed.
- **EventStore.Aspire owner:** deliver/approve Package A.
- **EventStore.Client owner:** deliver/approve Package B with FrontComposer and
  Parties coexistence evidence.
- **FrontComposer.AppHost owner:** accept Package C topology accountability and
  publish the exact consumable identity.
- **Platform operations:** confirm runtime deployment ownership and workload
  contract; no Parties-repository manifest work is requested.
- **Parties owner/developer:** planning edits are applied; implement Story 8.8
  only after G8 is `available` and retain rollback until consumer parity is
  green.

Following Administrator approval, the planning, prerequisite, sprint-routing,
and current-versus-target documentation edits in Section 5 were applied. No
production code or submodule content was changed; delivery of Packages A-C
remains owner-repository work and G8 remains `needs-additive-api`.
