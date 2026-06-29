# Sprint Change Proposal — Code Redundancy & Technical-Module Placement

- **Date:** 2026-06-28
- **Author:** Administrator (via Correct Course workflow)
- **Trigger type:** Architecture / maintainability hygiene (post-MVP)
- **Change scope classification:** **Moderate** (backlog reorganization — PO/DEV) for Class A; **Major** (PM/Architect) deferred for Class B
- **Status:** Approved — readiness remediation applied 2026-06-28

---

## Section 1 — Issue Summary

**Problem statement.** A review was requested to verify that the Parties codebase carries no redundant code, and that domain-agnostic *technical* code is not sitting in the application when it belongs in the shared technical submodules (`references/Hexalith.*`). The review confirmed the concern is real: there is both **in-repo duplication** across the 14 `src/Hexalith.Parties.*` projects and **misplaced generic infrastructure** that duplicates or belongs in the technical submodules.

**How it was discovered.** A three-part parallel audit on 2026-06-28:
1. Cataloged the reusable surface already shipped by `Hexalith.Commons` / `Hexalith.EventStore` / `Hexalith.Tenants` / `Hexalith.PolymorphicSerializations` / `Hexalith.Memories` / `Hexalith.FrontComposer`.
2. Scanned the 14 application projects for duplicated constants/helpers/registration.
3. Scanned for generic technical code that belongs in a submodule (or re-implements one).

**Evidence (not hypothetical — duplication has already drifted into divergent behaviour):**
- **Wire JSON options** — the canonical `{CamelCase + WhenWritingNull + JsonStringEnumConverter}` is hand-copied in ≥5 places; two copies have **already drifted** (`PartyPayloadProtectionService.cs:33` omits the enum converter; `ProjectionRebuildService.cs:27` uses `PropertyNameCaseInsensitive` with no naming policy).
- **GDPR export filename** — two builders produce **different** filenames for the same export: `GdprExportFileNameBuilder.Build` → `party-{t}-export-{yyyyMMddHHmmss}Z.json` vs `HttpAdminPortalGdprClient.BuildSafeExportFileName` → `party-{t}-{yyyyMMddTHHmmssZ}.json`.
- **Security-sensitive constants** — the `"eventstore:tenant"` claim type is redeclared 4× (+ a raw literal); a typo in any copy silently breaks tenant scoping.
- **Root cause (Class B)** — `Hexalith.Parties.*` does **not reference `Hexalith.Commons` at all**, and has built a *parallel* projection checkpoint / rebuild / freshness stack alongside the one `Hexalith.EventStore` already ships.

---

## Section 2 — Impact Analysis

### Epic impact
- **MVP epics 1–5: all `done`.** This change does **not** modify, invalidate, or re-sequence any delivered epic. It is purely additive technical-debt remediation.
- **New epic required:** Epic 6 — *Internal Code Consolidation (Class A)* — backlog, now defined in canonical `epics.md` with detailed implementation stories 6.1 through 6.7.
- **New epic deferred/documented:** Epic 7 — *Platform Alignment: adopt Commons/EventStore (Class B)* — canonical deferred placeholder only; no implementation story files until PM/Architect planning expands and approves the cross-repo work.

### Story impact
- No existing story changes. New detailed story files are created under Epic 6 (see Section 4). Class B stories are explicitly deferred to Epic 7 planning and removed from developer-executable scope until architecture approval.

### Artifact conflicts
- **PRD:** none. No functional/behavioral requirement changes; A8 changes one export *filename* format only, pinned below.
- **Architecture (`architecture.md`):** already updated with the shared anchors in `Hexalith.Parties.Contracts` (claim types, JSON options, projection names/actor-ids, role arrays, text/format helpers) and the Class B platform-consumption boundary.
- **`project-context.md`:** already updated with the shared-anchor convention in "Put code where it belongs"; new constants/options/format helpers go to `Contracts`, not re-hardcoded.
- **Fitness tests:** A1/A5 put `System.Security.Claims`-typed helpers in Contracts (BCL, allowed) — the boundary fitness test must stay green; constants are unconditionally safe.

### Technical impact
- Class A is **in-repo only**, no cross-repo coordination, low risk; `TreatWarningsAsErrors` + the existing test suites are the safety net.
- Class B is **cross-repo** (separate git submodules → version bump + package/release coordination + careful generic-vs-domain splitting); intentionally deferred.

---

## Section 3 — Recommended Approach

**Selected path: Hybrid — Direct Adjustment now (Class A), new deferred epic later (Class B).**

| Option | Verdict | Rationale |
|---|---|---|
| Direct Adjustment (Class A: in-repo dedup → new Epic 6) | **Chosen — do now** | Low effort/risk, immediate value, fixes 2 live drift bugs. All MVP epics done, so no in-flight work is destabilized. |
| Cross-repo moves (Class B) now | **Deferred → Epic 7** | High effort/risk, cross-repo release coordination, needs Architect sign-off (esp. crypto-shredding seam + projection platform). |
| Rollback | **Not viable** | Nothing to revert — this is additive consolidation, not a failed approach. |
| MVP review / scope cut | **N/A** | MVP is delivered and unaffected. |

**Effort / risk:** Class A — **Low–Medium effort / Low risk**. Class B — **High effort / Medium–High risk** (deferred).

---

## Section 4 — Detailed Change Proposals

### Class A — Internal consolidation (approved; → Epic 6)

Spine: introduce shared anchors in **`Hexalith.Parties.Contracts`** (the only project on every other project's reference path), plus two exceptions that need a different home.

| ID | Change | Destination | Notes / decision |
|---|---|---|---|
| **A1** | Single `eventstore:tenant` (+ `sub`/`oid`/`party_id`) claim constants | `Contracts/Authorization/PartiesClaimTypes.cs` | Replaces 4 const copies + 1 literal. Pure `const string` — fitness-safe. |
| **A2** | Single canonical wire `JsonSerializerOptions` (`PartiesJsonOptions.Default`) | `Contracts` | **Fixes drift.** 5 serializing sites adopt it; `PartyPayloadProtectionService` regains the enum converter; `ProjectionRebuildService` reader options stay separate but renamed for intent. |
| **A3** | De-duplicate `PartiesClaimsTransformation` JWT parsing (core + UI) | **New `Hexalith.Parties.Authentication` lib** — *not* Contracts (needs `Microsoft.AspNetCore.Authentication`) | **Decision pinned 2026-06-28:** new thin shared lib referenced by core + UI; do not move this into Commons in Epic 6. |
| **A4** | Single projection-name consts + actor-id builders (`PartyProjectionNames`, `PartyActorIds.Build*`) | `Contracts` | Replaces 6+ name consts and 3 hand-built actor-id format strings. |
| **A5** | Single `ExtractUserId`/`ExtractTenant` helpers (+ `sub`/`oid`) | `Contracts` (rides on A1) | AdminPortal calls shared helpers. |
| **A6** | Single GDPR HTTP-status → `AdminPortalGdprOutcome` mapping | `Hexalith.Parties.Client` (`HttpAdminPortalGdprClient`) | AdminPortal (already references Client) calls it; unify the `int`-keyed and `HttpStatusCode`-keyed copies. |
| **A7** | Single `ContainsTenant` text classifier | `Contracts` (`PartiesTextHeuristics`) | Replaces 3 copies. |
| **A8** | Single GDPR export filename builder | `Contracts` (`PartyExportFileName.Build`) | **Decision pinned 2026-06-28:** canonical format is `party-{tenant}-{yyyyMMddTHHmmssZ}.json` (current Client shape). This intentionally changes the current AdminPortal `party-{tenant}-export-{yyyyMMddHHmmss}Z.json` output. |
| **A9** | Shared role-name base arrays + policy names (`PartiesRoles.Admin/Consumer`) | `Contracts` | Host uses base; UI uses `[..PartiesRoles.Admin, "TenantOwner","tenantowner"]`. **Preserves the UI's intentional, documented `TenantOwner` addition — not forced identical.** |
| **A10** | Shared `FormatDate`/`FormatBoolean` (`PartyDisplayFormat`) | ⚠️ **`Contracts`** — *not* the UI host (portals don't reference it → would be circular) | Each portal passes its own localized `Labels` + culture; preserve Admin `"g"` vs Consumer `"d"` unless you want them unified. |

**Verified non-finding (left as-is):** the 13 small `IRejectionEvent` records share shape but are legitimately distinct event-sourcing types (routed by concrete type; each has a dedicated `PartyState.Apply` overload). Collapsing them would be a domain remodel, not a dedup.

**Readiness decisions now closed:** A3 uses a new `Hexalith.Parties.Authentication` library. A8 uses `party-{tenant}-{yyyyMMddTHHmmssZ}.json` as the canonical filename format.

### Class B — Misplaced generic technical code (deferred; → Epic 7)

Root cause: Parties does not consume the shared platform libraries and re-built parts of them.

| ID | Finding | Destination | Re-implements existing? |
|---|---|---|---|
| B1 | Projection sequence-checkpoint + idempotent replay-dedupe | `Hexalith.EventStore` | **Yes** — `IProjectionCheckpointTracker` |
| B2 | Resumable projection rebuild / replay-from-zero | `Hexalith.EventStore` | **Yes** — `IProjectionRebuildOrchestrator` |
| B3 | Crypto-shredding payload-protection engine (AES-GCM) | `Hexalith.EventStore` security impl | Implements an EventStore **seam** but parked in the app |
| B4 | Party key-management subsystem (versioned key store + wrapping + audit) | `Hexalith.EventStore` / shared security | No — generic infra parked in app |
| B5 | `ServiceDefaults/Extensions.cs` | `Hexalith.Commons.ServiceDefaults` | **Likely** — Commons ships `AddHexalithServiceDefaults` |
| B6 | Correlation-ID accessor + middleware | `Hexalith.Commons` | Partial (Commons has the data field, not the middleware) |
| B7 | `PartiesGlobalExceptionHandler` (RFC9457) | `Hexalith.Commons` / shared ServiceDefaults | Unknown |
| B8 | Jaro-Winkler similarity + diacritic normalization (**also duplicated internally** across 2 providers) | `Hexalith.Commons` / `Hexalith.Memories` | No externally; yes internally |
| B9 | `ProjectionFreshnessMetadata`/`Status` vocabulary | `Hexalith.EventStore.Contracts` | Overlaps EventStore ETag freshness |
| B10 | `PagedResult<T>` | `Hexalith.Commons` | No — genuinely missing shared type |
| B11 | `DecryptionCircuitBreaker`, `PartyEventTypeResolver` (**dup internally**), typed-client ProblemDetails, UI orchestration primitives, `IIndexPartitionStrategy` | Commons / EventStore / FrontComposer | Mixed |

---

## Section 5 — Implementation Handoff

- **Class A → Moderate scope → Product Owner / Developer.**
  - PO: accept Epic 6 + its detailed stories into the backlog (canonical `epics.md` and `6-*` story files are the spec).
  - DEV: implement per story; A3 and A8 are already pinned above; keep the boundary fitness test green; rely on `TreatWarningsAsErrors` + existing suites.
  - **Success criteria:** zero behavior change except the single A8 filename format; each duplicated definition reduced to one shared source; `dotnet build Hexalith.Parties.slnx -c Release` green; all test lanes green; A2/A8 drift eliminated.
- **Class B → Major scope → Product Manager / Solution Architect (deferred).**
  - Epic 7 planning: cross-repo sequencing, version/release coordination, generic-vs-domain split for crypto-shredding (B3/B4), and the decision to adopt EventStore's projection platform (B1/B2). Approval-gated; no `7-*` implementation story files should be created before approval.

---

## Section 6 — Approval & Routing

- **Approved by:** Administrator
- **Approval date:** 2026-06-28
- **Approved proposals:** A1-A10 for Class A in-repo consolidation; B1-B11 as deferred Class B platform-alignment planning items.
- **Class A route:** Product Owner / Developer; Epic 6 is defined in `_bmad-output/planning-artifacts/epics.md`, detailed `6-*` story files exist under `_bmad-output/implementation-artifacts/`, and sprint status marks them ready for development.
- **Class B route:** Product Manager / Solution Architect; Epic 7 is defined as a deferred canonical placeholder in `_bmad-output/planning-artifacts/epics.md` and remains architecture-gated before any implementation story files are created.
- **Implementation success criteria:** preserve current behavior except the approved A8 filename-format normalization; remove duplicate definitions in favor of shared anchors; keep `Contracts` infrastructure-free; pass `dotnet build Hexalith.Parties.slnx -c Release` and the relevant test lanes.

---

## Appendix — Change Navigation Checklist results

- **§1 Trigger & context** — Done. Architecture/maintainability hygiene; precise problem statement + concrete drift evidence captured.
- **§2 Epic impact** — Done. MVP epics 1–5 unaffected (all done); Epic 6 added (Class A); Epic 7 deferred (Class B).
- **§3 Artifact conflicts** — Done. PRD none; Architecture + project-context minor updates; UI/UX none; fitness tests noted.
- **§4 Path forward** — Done. Hybrid (Direct Adjustment now + deferred new epic). Rollback N/A; MVP review N/A.
- **§5 Proposal components** — Done. Sections 1–5 above.
- **§6 Final review & handoff** — Done. Administrator approved all proposals on 2026-06-28; readiness remediation added canonical Epic 6/Epic 7 material, created detailed Epic 6 story files, pinned A3/A8, routed Class A to PO/DEV, and kept Class B with PM/Architect.
