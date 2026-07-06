---
project_name: parties
document_type: sprint-change-proposal
date: 2026-07-06
trigger_file: fable_changes.md
status: approved
mode: batch
scope_classification: major
recommended_route: PM/Architect -> PO/Developer
approved_by: Administrator
approved_at: 2026-07-06T21:11:33+02:00
---

# Sprint Change Proposal - Domain-Focus Refactoring

## 1. Issue Summary

`fable_changes.md` records a 2026-07-06 domain-focus analysis for
Hexalith.Parties. The trigger is not a single implementation bug. It is a
structural finding that Hexalith.Parties still carries broad reusable platform
infrastructure that should live in Hexalith.Commons, Hexalith.EventStore,
Hexalith.FrontComposer, Hexalith.Builds, or platform/ops repositories.

The core problem:

- The EventStore domain-service SDK is still bypassed by a Parties-specific
  `/process` invoker, Dapr projection actors, query actors, and rebuild services.
- Forbidden or now-obsolete platform projects remain in the domain module,
  including `Hexalith.Parties.ServiceDefaults`, `Hexalith.Parties.Authentication`,
  and `Hexalith.Parties.Server` as a separate shell project.
- Generic crypto/key-management mechanics, correlation, ProblemDetails,
  deployment scaffolding, client envelopes, paging, UI orchestration, MCP
  plumbing, build probing, and test helpers are still partly local to Parties.
- Identifier handling violates the Hexalith ULID rule: production validators and
  aggregate logic still use `Guid.TryParse` for aggregate IDs, and several command
  or correlation ID paths still mint GUIDs.
- UI implementation still contains legacy FAST/v4 token usage and local
  FrontComposer-like primitives outside the intended domain delta.

Evidence comes from `fable_changes.md`, the current planning artifacts, completed
Epic 7 story evidence, and a source scan performed during this workflow. The scan
confirmed active production hits for `Guid.TryParse`, `Guid.NewGuid` identifier
minting, legacy token names, `MediatR` package references, `catch
(NotImplementedException)` Dapr-remoting control flow, and the still-present
`Hexalith.Parties.ServiceDefaults`, `Hexalith.Parties.Authentication`, and
`Hexalith.Parties.Server` projects.

Important context: Epic 7 is marked `done` in `sprint-status.yaml`, but its final
readiness artifact explicitly states that no production source cleanup was
performed in Story 7.8 and that projection and crypto rollback paths remain
intact because deletion-safe parity was not proven. Therefore this proposal should
not rewrite completed Epic 7 history. It should create a new post-MVP maintenance
epic that uses Epic 7 as prior evidence and advances the stronger domain-focus
end state.

## 2. Impact Analysis

### Epic Impact

Epics 1-5 remain complete PRD feature scope. Their FR coverage should not change.

Epic 6 remains completed or substantially completed in-repository consolidation.
It should not be reopened unless one of its shared anchors becomes obsolete during
the new cleanup.

Epic 7 remains completed post-MVP platform maintenance, but the planning text in
`epics.md` is stale because it still says developer execution starts only when
`7-*` story files are created. `sprint-status.yaml` and implementation artifacts
show stories 7.1 through 7.8 are already `done`. Epic 7 should be reclassified as
"completed partial platform alignment, with deferred deletion-safe cleanup."

New Epic 8 is required: **Domain-Focus Refactoring and Platform Extraction**. It
should carry the remaining domain-boundary work identified by `fable_changes.md`.
This is post-MVP platform maintenance and covers no new PRD functional
requirements.

### Story Impact

No completed Epics 1-7 stories should be edited retroactively except for status
and traceability notes in planning artifacts.

New Epic 8 stories should be created after approval:

1. **8.1 Baseline and release-blocker stabilization**
   Establish clean build/test baseline, fix `scripts/test.ps1` coverage gaps, and
   resolve or explicitly pin current submodule drift blockers before structural
   deletion work starts.
2. **8.2 Identifier correctness and zero-risk hygiene**
   Replace aggregate-ID `Guid.TryParse` with `AggregateIdentity`/ULID-compatible
   rules that still accept existing GUID-shaped IDs, replace identifier GUID
   minting with Commons unique ID helpers, remove dead package references, delete
   committed `*.csproj.lscache`, and update validator tests.
3. **8.3 Platform API prerequisites**
   Land or validate additive platform APIs for EventStore SDK projection/query
   support, DataProtection, EventStore client envelopes/freshness/error codes,
   tenant claims transformation, Aspire publish helpers, FrontComposer UI
   primitives, Commons HTTP helpers, and Builds shared props/targets.
4. **8.4 Leaf-project retirement**
   Delete `Hexalith.Parties.ServiceDefaults`, consume platform tenant-claim
   transformation and delete `Hexalith.Parties.Authentication`, merge
   `PartyAggregate` into the domain library, and delete `Hexalith.Parties.Server`
   as a separate project shell.
5. **8.5 EventStore domain-service SDK host cutover**
   Replace the Parties-specific domain-service invoker and host mappings with the
   SDK shape, keeping only domain registrations, payload-protection hooks, and
   Parties-specific policy.
6. **8.6 Projection and query SDK migration**
   Re-home projection folds as `IDomainProjectionHandler`, query paths as
   `IDomainQueryHandler`, persistence through `IReadModelStore` and
   `ReadModelWritePolicy`, and pagination through `IQueryCursorCodec`. Execute and
   verify a full projection rebuild.
7. **8.7 Data-protection extraction**
   Move generic key management, payload protection, audit, retry, rotation, and
   circuit-breaker mechanics to EventStore DataProtection while keeping Parties
   GDPR policy, erasure orchestration semantics, legal copy, and domain contracts
   where they belong.
8. **8.8 Client, MCP, AppHost, build, and deploy cleanup**
   Adopt Commons/EventStore/FrontComposer helpers for command envelopes, paging,
   ProblemDetails scrubbing, MCP context/result plumbing, AppHost security/module
   helpers, build-root probing, and platform-owned deployment assets.
9. **8.9 UI FrontComposer and Fluent consolidation**
   Replace local projection stream/freshness/reconcile/status/result/grid/picker
   primitives with FrontComposer/Fluent equivalents where parity exists, purge
   legacy FAST/v4 tokens, move E2E fixtures/specimens out of production
   assemblies, and preserve UX/ARIA contracts.
10. **8.10 Final readiness, documentation, and retirement gate**
    Regenerate docs, update fitness tests to pin new invariants, run the agreed
    focused and broad validation lanes, record rollback paths, and close residual
    deferred work explicitly.

### Artifact Conflicts

- `parties-ui-prd.md` is accurate for product FR scope, but it does not identify
  completed Epic 6/Epic 7 maintenance evidence or the new Epic 8 maintenance
  scope.
- `epics.md` is stale for Epic 7 execution status and has no Epic 8 domain-focus
  backlog.
- `architecture.md` still describes the original UI architecture and the old
  Class B platform-consumption boundary. It needs an addendum or new architecture
  spine for the stronger domain-focus end state.
- UX design requirements remain valid, but the implementation must be brought
  back into conformance: legacy FAST/v4 token usage and duplicated
  FrontComposer-like UI primitives conflict with NFR7 and UX-DR1 through UX-DR12.
- Document Project docs under `docs/` still describe current or old project
  inventory, including `ServiceDefaults`, `Server`, and Dapr actor internals. They
  should be regenerated after the structural refactor, not before.

### Technical Impact

This is multi-repository work. It affects Parties plus likely Commons,
EventStore, FrontComposer, Builds, and platform/ops repositories. It is not safe
as a single developer cleanup story.

Primary technical risks:

- SDK projection/query parity must be proven before deleting Dapr actor paths,
  companion sequence keys, rebuild services, or rollback adapters.
- Crypto extraction must preserve existing `json+pdenc-v1` and `json-redacted`
  payload compatibility, typed unreadable outcomes, redaction, erasure
  certificates, export/processing-record behavior, and no-leak diagnostics.
- Public package contracts may need a major-version strategy if public key
  management or security contracts move out of `Hexalith.Parties.Contracts`.
- Existing GUID-shaped aggregate IDs must keep replay compatibility.
- Fitness tests that grep project structure and source references must be updated
  in the same story as each move.
- Full solution validation is currently sensitive to submodule drift and package
  test network access.

## 3. Checklist Results

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [N/A] | Trigger is analysis file `fable_changes.md`, not a single story. Closest active context is completed Epic 7 final readiness plus deferred cleanup. |
| 1.2 Core problem | [x] | Domain module still owns broad platform infrastructure and violates Hexalith domain-centric rules. |
| 1.3 Evidence | [x] | Evidence in `fable_changes.md`, Epic 7 final readiness, sprint status, and source scan. |
| 2.1 Current epic viability | [!] | Epic 7 is complete but insufficient for the new domain-focus target. Do not reopen; add Epic 8. |
| 2.2 Epic-level changes | [x] | Add Epic 8 and update Epic 7 traceability text. |
| 2.3 Future epic review | [x] | Epics 1-5 unchanged; Epic 6 may be affected only by later cleanup; Epic 8 becomes active maintenance backlog. |
| 2.4 Obsolete/new epics | [x] | No product epics obsolete. New post-MVP maintenance Epic 8 required. |
| 2.5 Priority/order | [x] | Baseline -> identifier/hygiene -> platform prerequisites -> leaf deletes -> SDK cutovers -> UI/deploy/docs -> final gate. |
| 3.1 PRD conflicts | [x] | No FR change. Add maintenance-scope evidence note only. |
| 3.2 Architecture conflicts | [x] | Architecture needs domain-focus addendum/spine and updated platform boundary. |
| 3.3 UX conflicts | [x] | UX spec stays valid; implementation must remove legacy token and local UI primitive drift. |
| 3.4 Secondary artifacts | [x] | sprint-status, docs, test plans, deploy docs, package/API snapshots, fitness tests, and CI evidence affected. |
| 4.1 Direct adjustment | Viable | Add Epic 8 and story files. Effort high, risk medium-high. |
| 4.2 Rollback | Not viable | Reverting completed Epics 6-7 would discard useful adapter and harness work. Use it as foundation. |
| 4.3 MVP review | Not viable | Product MVP FR scope is complete and unaffected. This is post-MVP maintenance. |
| 4.4 Recommended path | [x] | Hybrid major course correction: new Epic 8 plus artifact updates, then developer story creation. |
| 5.1 Issue summary | [x] | Included above. |
| 5.2 Epic/artifact needs | [x] | Included above and below. |
| 5.3 Recommended path | [x] | New Epic 8, no PRD FR expansion. |
| 5.4 MVP impact | [x] | No MVP FR change; NFR9/NFR7/NFR8 maintainability and quality impact. |
| 5.5 Handoff plan | [x] | PM/Architect first, then PO/Developer. |
| 6.1 Checklist completion | [x] | Complete with approval pending. |
| 6.2 Proposal accuracy | [x] | Draft grounded in loaded artifacts and source scan. |
| 6.3 User approval | [!] | Pending explicit approval. |
| 6.4 sprint-status.yaml update | [!] | Do after approval only. |
| 6.5 Handoff confirmation | [!] | Pending approval and routing. |

## 4. Recommended Approach

Recommended path: **Major replanning through a new post-MVP maintenance Epic 8**.

Do not roll back Epic 7. It produced useful target-destination decisions,
adapters, compatibility harnesses, and readiness evidence. But do not pretend Epic
7 reached the stricter domain-focus target. Treat Epic 7 as a completed partial
alignment and create Epic 8 for the remaining source-level domain-boundary
correction.

Effort estimate: high.

Risk level: medium-high.

Timeline impact: multi-sprint, platform-first. Stories touching only hygiene or
identifier correctness can start earlier after baseline stabilization. SDK,
projection, crypto, and UI replacement work must wait for platform APIs and
deletion-safe parity evidence.

## 5. Detailed Change Proposals

### PRD Change

File: `_bmad-output/planning-artifacts/parties-ui-prd.md`

Section: `Current Implementation Evidence`

OLD:

```markdown
As of 2026-06-27, `_bmad-output/implementation-artifacts/sprint-status.yaml`
marks Epics 1-5 and their stories as `done`. Readiness validation after this date
must reconcile this PRD and planning documents with implementation story records.
```

NEW:

```markdown
As of 2026-06-27, `_bmad-output/implementation-artifacts/sprint-status.yaml`
marks Epics 1-5 and their stories as `done`. Readiness validation after this date
must reconcile this PRD and planning documents with implementation story records.

Post-MVP maintenance status:

- Epic 6 is in-repository consolidation scope. It supports NFR9 and carries no
  new PRD functional requirement coverage.
- Epic 7 is completed partial platform-alignment scope. Its final readiness
  record preserves rollback paths and deferred deletion-safe cleanup. It carries
  no new PRD functional requirement coverage.
- Epic 8, approved by `sprint-change-proposal-2026-07-06.md`, is domain-focus
  refactoring and platform extraction. It is post-MVP maintenance only and must
  not be reported as product-feature coverage.
```

Rationale: The PRD remains the product requirements source. It only needs
maintenance traceability so future readiness checks do not confuse Epic 8 with
new product scope.

### Epics Change

File: `_bmad-output/planning-artifacts/epics.md`

Section: `Implementation Scope Classification`

OLD:

```markdown
- **Approved post-MVP platform maintenance scope:** Epic 7. This is a Class B
  platform-alignment effort with PM/Architect implementation plan approved on
  2026-06-29 in `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md`.
  It covers no new PRD FRs, is not a dependency for Epic 6, and remains backlog
  until `7-*` implementation story files are created by the story workflow.
```

NEW:

```markdown
- **Completed post-MVP platform maintenance scope:** Epic 7. This is a Class B
  platform-alignment effort with PM/Architect implementation plan approved on
  2026-06-29 in `_bmad-output/planning-artifacts/epic-7-implementation-plan-2026-06-29.md`.
  Stories 7.1 through 7.8 are complete in sprint status. Epic 7 produced adapter,
  compatibility, and readiness evidence, but its final readiness record preserved
  several rollback paths and deferred deletion-safe cleanup.
- **Approved post-MVP domain-focus maintenance scope:** Epic 8. This is a
  Class C domain-boundary correction based on `fable_changes.md`. It covers no
  new PRD FRs, depends on Epic 7 evidence, and starts only after an Epic 8
  architecture spine and detailed `8-*` implementation story files are created.
```

Section: after Epic 7

ADD:

```markdown
## Epic 8: Domain-Focus Refactoring and Platform Extraction (Class C)

Epic 8 removes remaining generic platform infrastructure from Hexalith.Parties so
the module converges on the Hexalith domain-module contract: domain aggregate,
contracts, validators, domain projection/query handlers, Parties-specific GDPR
policy, typed domain clients, domain UI, MCP tool definitions, sample, and a thin
AppHost.

**Implementation classification:** post-MVP maintenance. Epic 8 covers no new PRD
functional requirements and must not be reported as MVP feature delivery.

**Trigger:** `fable_changes.md`, 2026-07-06.

### Epic 8 Sequencing

`8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`

Stories 8.2 and parts of 8.8 may run early only after 8.1 establishes a reliable
baseline. Stories 8.5 through 8.7 require platform API readiness from 8.3. Story
8.10 runs last.

[Add the 8.1 through 8.10 story summaries from this proposal.]
```

Rationale: The current Epic 7 text is stale and cannot absorb the stronger
domain-focus plan without rewriting completed story history.

### Sprint Status Change

File: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
  epic-7: in-progress
  7-1-platform-target-destination-adr-and-release-rollback-plan: done
  ...
  7-8-release-rollback-cleanup-and-readiness-gate: done
  epic-7-retrospective: optional
```

NEW after approval:

```yaml
  epic-7: done
  7-1-platform-target-destination-adr-and-release-rollback-plan: done
  ...
  7-8-release-rollback-cleanup-and-readiness-gate: done
  epic-7-retrospective: optional

  # -- Epic 8: Domain-Focus Refactoring and Platform Extraction (Class C) ----
  # Added by Correct Course 2026-07-06.
  # Post-MVP maintenance only; no new PRD functional requirements.
  epic-8: backlog
  8-1-baseline-and-release-blocker-stabilization: backlog
  8-2-identifier-correctness-and-zero-risk-hygiene: backlog
  8-3-platform-api-prerequisites: backlog
  8-4-leaf-project-retirement: backlog
  8-5-eventstore-domain-service-sdk-host-cutover: backlog
  8-6-projection-and-query-sdk-migration: backlog
  8-7-data-protection-extraction: backlog
  8-8-client-mcp-apphost-build-and-deploy-cleanup: backlog
  8-9-ui-frontcomposer-and-fluent-consolidation: backlog
  8-10-final-readiness-documentation-and-retirement-gate: backlog
  epic-8-retrospective: optional
```

Rationale: `sprint-status.yaml` currently has all 7.x stories done while leaving
`epic-7` in-progress. The new work needs its own tracking keys.

### Architecture Change

File: `_bmad-output/planning-artifacts/architecture.md` or new
`_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`

OLD:

```markdown
- **Platform-consumption boundary (Class B, deferred Epic 7):** generic technical
  infrastructure belongs in the shared submodules, not the application.
```

NEW:

```markdown
- **Platform-consumption boundary (Class B, completed partial Epic 7):** Epic 7
  introduced adapters, compatibility harnesses, and readiness evidence for shared
  Commons/EventStore/FrontComposer adoption. It intentionally preserved rollback
  paths where deletion-safe parity was not proven.
- **Domain-focus boundary (Class C, approved Epic 8):** Hexalith.Parties must not
  ship generic platform implementations. The target state removes local
  ServiceDefaults, authentication transformation, domain-service invoker,
  projection/query actors, projection rebuild service, generic crypto engine,
  generic client envelopes/paging/freshness, FrontComposer-like UI primitives,
  generic MCP plumbing, platform deployment assets, and build-root probing once
  replacement APIs and rollback evidence exist.
```

Rationale: Architecture needs to capture the new invariant before developers
start deletion-heavy stories.

### UX Change

File: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
and `EXPERIENCE.md`

OLD:

No product-experience change required.

NEW:

```markdown
Implementation conformance note: Epic 8 does not change UX requirements. It
removes implementation drift from the existing UX contract: legacy FAST/v4 tokens,
duplicated FrontComposer-like status/freshness/reconcile/grid primitives, and
production-only E2E/specimen fixtures must be retired only when the replacement
preserves the documented Fluent 2, accessibility, focus, state, and GDPR copy
contracts.
```

Rationale: The UX design is already strict enough. The change is conformance, not
new UX scope.

## 6. Implementation Handoff

Scope classification: **Major**.

Reason: The change spans project architecture, multiple submodules, public
package surfaces, read-model persistence, crypto/data-protection behavior, UI
framework adoption, deployment assets, build gates, and broad test migration.

Route:

- Product Manager / Architect: approve Epic 8 scope, create/update architecture
  spine, confirm platform owners and sequencing.
- Product Owner / Developer: create `8-*` implementation story files in order
  after the Epic 8 architecture spine is approved.
- Developer agents: implement only from approved story files, preserving
  rollback paths until each deletion has parity evidence.
- Test Architect / Reviewer: define required focused and broad validation lanes
  per story, especially for projection/query, crypto, UI accessibility, package
  compatibility, and deployment validation.

Success criteria:

- Epics 1-5 FR coverage remains unchanged.
- Epic 7 is closed as completed partial platform alignment.
- Epic 8 exists with approved backlog and status tracking.
- `Guid.TryParse` no longer rejects valid ULID-compatible aggregate IDs while
  existing GUID-shaped IDs remain accepted.
- `AddEventStoreDomainService` / `UseEventStoreDomainService` are the host path
  for the domain service.
- `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`,
  `ReadModelWritePolicy`, and `IQueryCursorCodec` replace local projection/query
  actors and ad hoc pagination.
- Forbidden local platform projects are deleted or replaced by approved shared
  modules.
- Generic crypto mechanics move behind EventStore/shared security contracts
  without weakening GDPR erasure guarantees.
- FrontComposer and Fluent UI own shared UI lifecycle/status/grid/picker
  primitives where parity exists.
- Final validation records the build/test state and every remaining deferred
  item with owner, reason, and required proof.

## 7. Approval Record

Approved by Administrator on 2026-07-06 after batch review.

Approved immediate actions:

- Update planning traceability in the PRD and epics.
- Close Epic 7 status as done.
- Add Epic 8 backlog tracking.

Handoff:

- Route to PM/Architect for the Epic 8 architecture spine and platform-owner
  sequencing.
- Route to PO/Developer for `8-*` implementation story creation after the
  architecture spine is approved.
- Keep source implementation blocked until each `8-*` story exists with explicit
  acceptance criteria and validation gates.
