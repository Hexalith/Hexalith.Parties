---
stepsCompleted: ['step-01-document-discovery', 'step-02-requirements-analysis', 'step-03-epic-coverage-validation', 'step-04-ux-alignment', 'step-05-epic-quality-review', 'step-06-final-assessment']
status: complete
readinessVerdict: 'READY WITH MINOR GAPS (conditional) — 0 critical, 1 major (MAJOR-1 reuse/boilerplate story-spec), 4 minor, 3 tracked dependencies'
documentsIncluded:
  architecture: 'planning-artifacts/architecture.md'
  epics: 'planning-artifacts/epics.md'
  ux: 'planning-artifacts/ux-designs/ux-parties-2026-06-09/'
  prd: 'NOT FOUND — brownfield; requirements baseline = docs/ + _bmad-output/project-context.md'
  requirementsSubstitute:
    - 'docs/ (api-contracts, data-models, component-inventory, event-handler-patterns, architecture)'
    - '_bmad-output/project-context.md (51 rules)'
assessmentLens: 'Reuse existing submodule classes; minimal technical layer; no unneeded boilerplate; just enough to spin up required servers.'
date: 2026-06-09
project: parties
---

# Implementation Readiness Assessment Report

**Date:** 2026-06-09
**Project:** parties
**Assessment Lens (user-directed):** Verify reuse of existing classes in submodules and absence of unneeded boilerplate. This is a **domain module** — it should have a **minimal technical layer**, just what's needed to spin up the required servers.

---

## Step 1 — Document Inventory

| Type | Status | Location | Notes |
|---|---|---|---|
| Architecture | ✅ Found (whole) | `planning-artifacts/architecture.md` (49 KB) | Single doc, no shard conflict |
| Epics & Stories | ✅ Found (whole) | `planning-artifacts/epics.md` (58 KB) | Single doc, no shard conflict |
| UX Design | ✅ Found (sharded) | `planning-artifacts/ux-designs/ux-parties-2026-06-09/` | DESIGN.md, EXPERIENCE.md, reviews, mockups |
| PRD | ⚠️ Not found | — | Brownfield domain module; requirements baseline = `docs/` + `project-context.md` |

**Requirements substitute for traceability:** `docs/` knowledge base (`api-contracts.md`, `data-models.md`, `component-inventory.md`, `event-handler-patterns.md`, `architecture.md`, GDPR docs) and `_bmad-output/project-context.md` (51 project rules).

**Duplicates:** None. **Output collision:** Resolved — writing to `-v2` file to preserve the prior 22:09 run.

---

## Step 2 — Requirements Analysis (PRD substitute: architecture.md + epics.md)

> No formal PRD exists (brownfield). The requirements baseline is `architecture.md` §"Requirements Overview" + `epics.md` §"Requirements Inventory", both derived from the UX `EXPERIENCE.md` and the `docs/` brownfield set. FR/NFR labels are preserved verbatim from those documents.

### Functional Requirements (9)

| ID | Requirement (extracted) |
|---|---|
| **FR-Shell** | One host-owned OIDC sign-in; route to landing area **by role** (Admin/TenantOwner→Admin, Consumer→Consumer); preserve return URL on `SignInRequired`; nav auto-populates from domain manifests, gated by `<AuthorizeView Policy>`, areas never cross-render; a Consumer with no `party_id` claim is routed fail-closed to onboarding/error, never a data screen. |
| **FR-Admin-1** | Parties list — server-driven debounced display-name search + Person/Org type filter + active filter (`FluentSelect`); row→detail; render last-known on staleness (never block). |
| **FR-Admin-2** | Party detail — view full `PartyDetail`; entry to Edit + GDPR; party-state badge reflects lifecycle. |
| **FR-Admin-3** | Create/Edit party — validated form → `CreateParty(Composite)`/`Update*`; in-form `<hexalith-party-picker>`; Person/Org chooser as radiogroup. |
| **FR-Admin-4** | GDPR/DPO ops — erase (typed-name confirm) · restrict/lift · record/revoke consent · Art.20 export · Art.30 processing records · erasure-verification report (the report depends on D7). |
| **FR-Consumer-1** | My profile — view own personal data + freshness; no list/search. |
| **FR-Consumer-2** | Edit my profile — correct/update own data (validated → command). |
| **FR-Consumer-3** | My consent — grant/withdraw; opt-in **default Off**, never pre-checked; **Object (Art.21)** for legitimate-interest bases; optimistic flip → reconcile. |
| **FR-Consumer-4** | My data & privacy — export own data (async, machine-readable JSON) · request erasure (cancellable-until-start, permanent-once-complete) · see what's processed, split "control" vs "kept". |

**Total FRs: 9 — all mapped to epics (see FR Coverage Map in epics.md).**

### Non-Functional Requirements (9)

| ID | Requirement (extracted) |
|---|---|
| **NFR1** | Accessibility WCAG 2.2 AA (consumer-facing): real ARIA semantics; aria-live politeness split (status/freshness=polite, validation/failure=assertive `role=alert`); per-surface focus contract; forced-colors + reduced-motion product-wide; color-never-alone; ≥24px (≥44px touch); AA contrast gate (`--colorBrandBackground`, never raw teal `#0097A7` @3.51:1). |
| **NFR2** | Eventual consistency first-class UX: surface `ProjectionFreshnessMetadata` + `StatusKind`/`PartyPickerSearchState` machines; optimistic echo + silent reconcile; render last-known cache, never blank/throw; fail-closed tenant warm-up = "still warming up." Not read-your-write. |
| **NFR3** | Security/own-data privacy: Consumer scoped to own party only (single choke point); no PII in logs/telemetry/copy/tombstones; admin typed-name erase compared in-memory only. |
| **NFR4** | GDPR honesty (copy): consent opt-in default-Off; erasure commits to the *start* (Art.12(3)), states completed erasure permanent; Art.21 Object; Art.20 export machine-readable + async. |
| **NFR5** | Responsive: Admin desktop-first master-detail (→ sheet/full-screen w/ focus contract); Consumer phone-first single column. One codebase, two density postures. |
| **NFR6** | Multi-tenancy: Admin within tenant scope; isolation preserved; tenant-access fails closed + eventually consistent. |
| **NFR7** | Brand discipline: inherit FluentUI V5 + FrontComposer shell wholesale; specify brand-delta only; theme via design-token API, never hard-coded hex. |
| **NFR8** | Observability: OpenTelemetry + health on UI host; surface `X-Service-Degraded`/`X-Stale-Data-Age` into UI state. |
| **NFR9** | Build/quality gates: .NET 10, CPM (no `Version=`), solution-wide `TreatWarningsAsErrors`, `.slnx` only, root-repository submodules under `references/`, Conventional Commits — all apply to the new tier. |

**Total NFRs: 9.**

### Additional / Technical Requirements (constraints that shape stories)

- **AR-Starter** — new standalone Blazor Server host `Hexalith.Parties.UI` on the FrontComposer shell-host pattern (`FrontComposer/samples/Counter/Counter.Web`).
- **AR-D1..D11** — architecture decisions inherited as constraints (render model, identity binding, own-data authz, composition, transport/auth, live freshness, GDPR-stub completion, export delivery, a11y enforcement, AppHost/deploy, picker re-skin).
- **AR-Client** — existing client surface **reused, not rebuilt**: `IPartiesCommandClient`, `IPartiesQueryClient`, `IAdminPortalGdprClient` are the building blocks.
- **AR-Gdpr-Export/Erased/Records/Keys** — GDPR backend behaviors already present in the domain; UI consumes them.
- **AR-Gap-Binding / AR-Gap-D7 / AR-Gap-KMS** — three known gaps tracked as scoped stories/prerequisites.
- **UX-DR1..16** — design-token, four-domain-component, accessibility, and regulated-copy implementation requirements.

### Requirements Completeness Assessment (initial)

- **FR coverage:** 9/9 FRs explicitly mapped to epics; no orphan FRs. ✅
- **NFR threading:** NFRs are not parked — they are wired into Epic 1 shared enablers (StatusKind map, a11y gate, domain components, SignalR freshness) and re-asserted per story. ✅
- **Requirements basis is explicit and honest** about the missing PRD; traceability to UX `EXPERIENCE.md` + `docs/` is documented in both artifacts' frontmatter. ✅
- **Reuse posture (user's lens) is stated as a first-class requirement** (AR-Client, AR-Starter, D4 composition, "zero EventStore submodule change for the core path") — to be verified against the actual codebase in Steps 4–5. 🔎

---

## Step 3 — Epic Coverage Validation

### FR → Epic/Story Coverage Matrix

| FR | Requirement (short) | Epic | Implementing story(ies) | Status |
|---|---|---|---|---|
| **FR-Shell** | Sign-in + role landing + fail-closed binding | Epic 1 | 1.2 (OIDC), 1.3 (role landing/nav), 1.4 (`party_id` resolution) | ✅ Covered |
| **FR-Admin-1** | Parties list (search/filter/paging) | Epic 2 | 2.2 | ✅ Covered |
| **FR-Admin-2** | Party detail | Epic 2 | 2.3 | ✅ Covered |
| **FR-Admin-3** | Create/Edit + in-form picker | Epic 2 | 2.4, 2.5 (picker) | ✅ Covered |
| **FR-Admin-4** | GDPR/DPO ops + verification report | Epic 3 | 3.1, 3.2, 3.3, 3.4, **3.5 (D7 backend), 3.6 (report UI)** | ✅ Covered (report gated on D7) |
| **FR-Consumer-1** | My profile | Epic 4 | 4.4 | ✅ Covered |
| **FR-Consumer-2** | Edit my profile | Epic 4 | 4.5 | ✅ Covered |
| **FR-Consumer-3** | My consent (default-Off, Art.21) | Epic 5 | 5.1 | ✅ Covered |
| **FR-Consumer-4** | My data & privacy (export/erasure/records) | Epic 5 | 5.2 (export), 5.3 (erasure), 5.4 (processing records) | ✅ Covered |

### Enabling stories (support FRs, not 1:1 FR-mapped)

| Story | Role | Supports |
|---|---|---|
| 1.1 | Stand up `Hexalith.Parties.UI` host + AppHost | All FRs (foundation / "spin up the server") |
| 1.5 | Consumer own-data self-authorization (`IDataSubjectAccessService`) | NFR3, FR-Consumer-* |
| 1.6 | Canonical StatusKind→UI mapping + aria-live split | NFR1, NFR2 (all screens) |
| 1.7 | SignalR live freshness + optimistic-reconcile effect | NFR2, NFR8 |
| 1.8 | Three shared domain components | NFR1, NFR7, UX-DR4/5/6 |
| 1.9 | A11y foundation + CI a11y gate | NFR1 |
| 1.10 | Deploy parties-ui (container + K8s) + KMS gate | NFR8, NFR9, AR-Gap-KMS |
| 2.1 | Embed AdminPortal behind Admin policy | FR-Admin-* |
| 4.1 | **Binding mechanism decision (ADR spike)** | AR-Gap-Binding (gates all of Epic 4–5) |
| 4.2 | Implement chosen binding mechanism | AR-Gap-Binding |
| 4.3 | Stand up ConsumerPortal RCL | FR-Consumer-* |

### Coverage Statistics

- **Total FRs:** 9
- **FRs covered in epics:** 9
- **Coverage:** **100%** ✅
- **FRs in epics but not in requirements baseline (scope creep):** 0 ✅
- **Conditional coverage:** FR-Admin-4's *erasure-verification report* (Story 3.6) is correctly gated behind the cross-submodule D7 backend contract (Story 3.5, approval-required). The rest of FR-Admin-4 ships without it. This is a **sequenced dependency, not a coverage gap.**

### Coverage Notes (with the reuse lens)

- The epics explicitly route Admin FRs into the **existing `AdminPortal` RCL** (Epic 2/3 "wraps existing panels", uses `IAdminPortalGdprClient`) rather than rebuilding them — consistent with the user's reuse directive. To be verified against real code in Step 4.
- Only **two net-new projects** carry FR implementation (`Hexalith.Parties.UI` host, `Hexalith.Parties.ConsumerPortal` RCL); everything else is `◆ extend existing`. This is the central claim to validate for "minimal technical layer / no boilerplate."

---

## Step 4 — UX Alignment + Reuse / Boilerplate Verification (your directive)

### UX Document Status

**Found** — sharded set `ux-parties-2026-06-09/` (DESIGN.md spine, EXPERIENCE.md spine, 3 review lenses, validation-report). The set is **self-validated**: 3 critical + 7 high findings, **all resolved in-run** (teal-contrast critical, hard-30-day-SLA critical, irreversibility-not-stated critical; live-region/combobox/semantics highs). Residuals are medium/low and mostly mock-fidelity, which the epics neutralize with the normative "spine wins over mockups" rule + the Story 1.9 a11y gate.

### UX ↔ Requirements ↔ Architecture Alignment

| Check | Verdict |
|---|---|
| UX flows → FRs | ✅ EXPERIENCE.md flows map 1:1 to FR-Shell/Admin/Consumer; no orphan UX flow. |
| UX → Architecture support | ✅ D1 (Interactive Server), D6 (SignalR freshness), D9 (a11y gate), D4 (RCL split), D11 (picker) each back a concrete UX requirement. |
| UX domain components → arch | ✅ 4 specified deltas (badge/freshness/destructive/picker) placed in `UI/Components/Shared` + `Picker`. |
| Mockup-fidelity risk | ✅ Mitigated — spine authoritative, a11y gate is the backstop. |
| **Architecture vs existing code reality** | ⚠️ **Mismatch — see R1 below.** The arch project tree specifies net-new `*Page.razor` components for Admin routes that an existing component already serves. |

### Reuse Verification — claims vs. actual code

Verified the plan's reuse claims against the live source tree + submodules (FrontComposer, EventStore, Tenants). **The reuse philosophy is sound and almost all claims hold.** Evidence:

| Claimed reuse | Reality | Verdict |
|---|---|---|
| `AdminPortal` RCL + GDPR panels | EXISTS — 8 panels incl. `ErasureVerificationReportPanel.razor`; routable `PartiesAdminPortal.razor` on `/admin/parties[/{id}[/gdpr]]` | ✅ Solid (but see R1) |
| `Hexalith.Parties.Client` (`IPartiesCommandClient`/`IPartiesQueryClient`/`IAdminPortalGdprClient` + `AddPartiesClient` + `requestCustomizer`) | EXISTS, all three interfaces + DI + hook | ✅ Solid |
| GDPR 501 stubs (`GetErasureCertificateAsync`/`RetryErasureVerificationAsync`) | EXISTS — `ContractUnavailable()` 501 / `ContractUnavailable` outcome | ✅ Confirms D7/Story 3.5 is real |
| `Contracts` (commands/models incl. `PartyDetail`, `PartyDataPortabilityPackage`, `ProjectionFreshnessMetadata`) | EXISTS | ✅ Solid |
| Host authz: `Admin` policy, `ITenantAccessService`, `PartiesClaimsTransformation` | EXISTS; **no** `Consumer`/`IDataSubjectAccessService`/`party_id` scoping | ✅ Plan correctly scopes these as NEW |
| `ServiceDefaults` (OTel/health) | EXISTS | ✅ Solid |
| FrontComposer `Counter.Web` host pattern + Quickstart/DevMode/`AddHexalithDomain`/Shell | EXISTS (187-line precedent; shell gives nav/theme/density/palette/skip-links) | ✅ Solid |
| EventStore `Hexalith.EventStore.SignalR` projection hub (`ProjectionChanged`) | EXISTS | ✅ Solid |
| Thin no-sidecar host precedent (`parties-mcp` 72 LOC) + AppHost ~5-line add | EXISTS | ✅ "Minimal technical layer" is realistic |
| `SourceTools` `[Projection]`/`[Command]` generator (Fluxor boilerplate generated, not hand-written) | EXISTS, wired as analyzer | ✅ Boilerplate is generated |

**Genuinely-new work (verified absent — no duplication):** the `Hexalith.Parties.UI` host, `ConsumerPortal` RCL, Consumer auth (`Consumer` policy + `IDataSubjectAccessService` + `party_id` resolver + `ISelfScopedPartiesClient`), **Create/Edit party form (FR-Admin-3 — confirmed absent)**, the 3 shared domain components, SignalR freshness wiring, the a11y CI gate, and the D7 backend. The **"2 new projects" claim holds.** ✅

### Findings (reuse / boilerplate lens)

**R1 — ⚠️ MEDIUM: Epic 2 & 3 acceptance criteria / arch project-tree describe rebuilding Admin functionality that already exists.**
`PartiesAdminPortal.razor` is a single, mature master-detail component that **already implements FR-Admin-1 (list + debounced display-name search + Person/Org + active + created/modified-date filters + paging + erased-exclusion), FR-Admin-2 (full `PartyDetail` render: summary, person/org, contacts, identifiers, consents, restrictions, name-history, EventStore links), and FR-Admin-4 wiring (embeds `PartyGdprOperationsPanel` → the 8 GDPR panels)** — with degraded/stale handling, tenant-switch cross-leak guards, cancellation/versioning, and ARIA semantics already in place. Yet:
- `architecture.md` project tree (§ "Complete Project Directory Structure") lists **net-new** `PartiesListPage.razor`, `PartyDetailPage.razor`, `PartyGdprPage.razor` to *add* to AdminPortal.
- Story 2.2 / 2.3 / 3.1 acceptance criteria read as **build-from-scratch** (FluentDataGrid, `SearchPartiesAsync`, filters, detail sections, GDPR page) and **never reference the existing `PartiesAdminPortal.razor`**.
- The plan's page-per-route split would **regress the existing single master-detail** into parallel pages — rebuilding working code.
- **Impact (your directive):** taken literally this is exactly the "unneeded boilerplate" risk — duplicate/rewrite of ~1,400 lines of working, hardened Admin UI.
- **Recommendation:** Reframe Epic 2/3 stories as **"reuse & enhance `PartiesAdminPortal.razor` + existing panels,"** scoped to the genuine delta: (a) **mount** the existing component in the new host behind the `Admin` policy (Story 2.1 — already correct); (b) add the **phone master-detail→sheet reflow + focus contract** (Story 2.3's real new AC); (c) **swap the inline `<span>` badge + inline freshness text for the new shared `PartyStateBadge`/`DataFreshnessIndicator`** (retrofit, see R2); (d) **wire SignalR optimistic-reconcile** into it; (e) build the **only truly-missing Admin surface — Create/Edit (FR-Admin-3, Story 2.4)** + the **picker re-skin (Story 2.5)**; (f) reduce **Story 3.6** to **"wire the existing `ErasureVerificationReportPanel` to the D7 contract,"** not "build the report UI."

**R2 — 🟡 LOW: New shared primitives must retrofit the existing component, or you get two parallel systems.**
The 3 shared domain components and the canonical `StatusKind→UI` map (Stories 1.6/1.8) are correctly net-new (verified absent). But `PartiesAdminPortal.razor` already carries inline equivalents — an inline badge `<span>`, inline freshness text, and its **own private `StatusKind` enum** (the only `StatusKind` in the tree). Stories 1.6/1.8 ACs should **explicitly require extracting/replacing those inline implementations**, else the codebase ends up with a private and a shared `StatusKind` that drift, plus dead inline UI.

**R3 — ℹ️ CONFIRMATION: Picker re-skin (D11/Story 2.5) is genuinely needed and correctly scoped.**
The picker CSS still uses **legacy FAST tokens** (`--accent-fill-rest`, `--error-fill-rest`, `--neutral-layer-1/2`, `--neutral-stroke-rest`); **no** Fluent 2 `--colorNeutral*`/`--colorBrand*` tokens present. This matches the plan's premise. (Note: the codebase sweep first mislabeled these as "modern Fluent 2" — the raw token names prove they are the legacy set the re-skin targets.)

### UX Warnings

- No missing-UX warning — UX is present, validated, and architecturally supported.
- The only alignment warning is **R1** (structure/AC vs. existing-code reality), carried into Step 5 epic-quality review and the final recommendation.

---

## Step 5 — Epic Quality Review (create-epics-and-stories standards)

### Best-Practices Compliance Matrix

| Epic | User value (not technical milestone) | Independent (no fwd-epic need) | Story sizing | AC quality (G/W/T, testable, errors) | FR traceability |
|---|---|---|---|---|---|
| Epic 1 — Foundation & Sign-In | ⚠️ Mixed (user sign-in value + heavy enabler bundle) | ✅ Stands alone | ✅ | ✅ Strong | FR-Shell |
| Epic 2 — Admin Records Mgmt | ✅ Clear | ✅ Needs only Epic 1 | ✅ | ✅ Strong | FR-Admin-1/2/3 |
| Epic 3 — Admin GDPR/DPO | ✅ Clear | ✅ Needs only 1+2 (3.5 cross-submodule isolated) | ✅ | ✅ Strong | FR-Admin-4 |
| Epic 4 — Consumer Binding & Profile | ✅ Clear | ✅ Needs only Epic 1 | ✅ | ✅ Strong | FR-Consumer-1/2 |
| Epic 5 — Consumer Consent/Export/Erasure | ✅ Clear | ✅ Needs only Epic 4 | ✅ | ✅ Strong | FR-Consumer-3/4 |

**Epic dependency graph** `1→{2,4}` · `2→3` · `4→5` — **all backward; no epic requires a later epic.** ✅
**Starter-template rule:** architecture mandates the FrontComposer shell-host starter → Epic 1 Story 1.1 *is* "stand up the host." ✅
**No upfront-DB anti-pattern:** event-sourced (no DB/ORM); the only new persistence (identity→`party_id` binding store) is created in Epic 4 Story 4.2 *when first needed*, not upfront. ✅
**Brownfield integration:** stories integrate with existing AdminPortal/Client/gateway/EventStore.SignalR. ✅
**AC quality (sampled across all 30 stories):** consistent Given/When/Then; happy + error + edge (stale/degraded, erased/Gone, validation-rejected, phone reflow, transient) covered; a11y + PII-hygiene assertions embedded; testable via bUnit/Playwright/integration. **Exceptionally high.** ✅

### Findings by severity

#### 🔴 Critical violations — **None**
No technical-milestone-only epics; no broken epic independence; no epic-sized unimplementable stories.

#### 🟠 Major issues

**MAJOR-1 — Stories 2.2 / 2.3 / 3.1 + the architecture project-tree specify net-new pages that duplicate existing, working code (carries R1 from Step 4).**
The ACs for the Admin list (2.2), detail (2.3), and GDPR page (3.1) read as **build-from-scratch** and never reference `PartiesAdminPortal.razor`, which already implements list+search+filters+paging, full detail, and the GDPR-panel wiring for the very same routes. The arch tree compounds this by listing net-new `PartiesListPage/PartyDetailPage/PartyGdprPage.razor`. An implementing agent following the ACs literally would **rebuild ~1,400 lines of hardened UI** — the exact boilerplate/duplication you asked me to catch.
- **Genuine delta these stories *should* carry:** debounce on search (2.2), phone master-detail→sheet + focus contract (2.3), swap inline badge/freshness → shared components, SignalR reconcile wiring, and (3.6) wiring the **existing** `ErasureVerificationReportPanel` to the D7 contract rather than "building the report."
- **Remediation:** Re-anchor every Epic 2/3 Admin story AC to **"reuse & enhance `PartiesAdminPortal.razor` + existing panels"**; explicitly list the delta; demote Story 3.6 to a wiring story; keep **Create/Edit (2.4)** and **picker re-skin (2.5)** as the only true net-new Admin builds. This both fixes the quality defect and realizes your "minimal layer / no boilerplate" intent.

#### 🟡 Minor concerns

**MINOR-1 — Cross-epic forward reference: Story 1.4 (Epic 1) ↔ Story 4.2 (Epic 4).** Story 1.4's happy path (resolve a real `party_id` claim end-to-end) can't be fully verified until the binding mechanism is built in Epic 4. The epics are **self-aware** and mitigate it: 1.4 is scoped to "consume an existing claim," unit-tested for claim-present/absent; true e2e defers to 4.2. Acceptable, but it is a forward reference — keep the mitigation explicit so 1.4 isn't marked "done-verified" prematurely.

**MINOR-2 — Story 4.1 is a decision-spike-as-story** (produces an ADR, not user value). The epics label it explicitly as a gating spike resolving AR-Gap-Binding (a prior readiness course-correction, M1). Acceptable — deciding the binding before building beats building blind — but note it's the one "story" that yields a decision artifact rather than shippable user value.

**MINOR-3 — New shared primitives must retrofit existing inline equivalents (carries R2).** Stories 1.6 (canonical `StatusKind→UI`) and 1.8 (shared domain components) are correctly net-new, but `PartiesAdminPortal.razor` already holds a **private `StatusKind` enum**, an inline badge `<span>`, and inline freshness text. Add ACs requiring those inline forms to be **replaced** by the shared primitives, or the codebase ends up with two divergent `StatusKind` concepts + dead inline UI.

**MINOR-4 — Epic 1 concentrates the technical-enabler load.** Six of its ten stories (1.1 host, 1.6 StatusKind, 1.7 SignalR, 1.8 components, 1.9 a11y gate, 1.10 deploy) have no direct standalone user value. This is the **correct place** for shared infrastructure (better bundled here than scattered), but Epic 1 is the heaviest increment — sequence it so the user-facing thread (1.2/1.3 sign-in+landing) can demo before all enablers land.

### Remediation summary

| ID | Severity | Action | Owner artifact |
|---|---|---|---|
| MAJOR-1 | 🟠 | Re-anchor Epic 2/3 Admin story ACs to enhance `PartiesAdminPortal.razor`; demote 3.6 to a wiring story | `epics.md` (2.2/2.3/3.1/3.6), `architecture.md` project tree |
| MINOR-1 | 🟡 | Keep 1.4's "consume existing claim" scoping + defer e2e to 4.2 explicit | `epics.md` Story 1.4 |
| MINOR-2 | 🟡 | Accept 4.1 as a labelled spike (no change needed) | `epics.md` Story 4.1 |
| MINOR-3 | 🟡 | Add "replace inline badge/freshness/private StatusKind" ACs to 1.6/1.8 | `epics.md` Stories 1.6, 1.8 |
| MINOR-4 | 🟡 | Sequence Epic 1 so sign-in demos before all enablers | sprint plan |

---

## Summary and Recommendations

### Overall Readiness Status

**READY WITH MINOR GAPS (conditional) — proceed on the Foundation + Admin path now; fix MAJOR-1 first to honor the "no boilerplate" directive.**

The planning set is unusually strong: **100% FR coverage** (9/9 traced to stories), a **self-validated UX** (all critical/high resolved, spine-wins-over-mockups rule + a11y gate as backstop), **exemplary acceptance criteria**, **clean backward-only epic dependencies**, and a **reuse posture that is real, not aspirational** — verified against the live code and submodules. There are **no critical violations** and **no blockers to starting**. The gaps are a single major story-spec correction and three already-tracked dependencies.

### On your specific directive (reuse existing classes / minimal technical layer / no boilerplate)

**Verdict: the architecture's reuse intent is sound and the "minimal technical layer / 2 new projects" claim holds — but the *story/structure detail contradicts it in one place* and would generate avoidable boilerplate if implemented literally.**

- ✅ **Reuse is genuine:** AdminPortal RCL (+8 GDPR panels incl. `ErasureVerificationReportPanel`), `Hexalith.Parties.Client` (all 3 client interfaces + DI + `requestCustomizer`), Contracts, ServiceDefaults, FrontComposer shell-host pattern + Quickstart/Shell/`SourceTools` generator, EventStore `.SignalR` hub, and the `parties-mcp` 72-LOC thin-host precedent — **all exist and are correctly leveraged.** Boilerplate (Fluxor Feature/Actions/Reducers/command forms) is **source-generated**, not hand-written.
- ✅ **Minimal layer confirmed:** only **2 net-new projects** (`Hexalith.Parties.UI` host, `ConsumerPortal` RCL); everything else extends existing projects. Host bootstrapping ≈ `parties-mcp` precedent; AppHost add ≈ 5 lines, no DAPR sidecar.
- ⚠️ **The one boilerplate risk (MAJOR-1):** Epic 2/3 Admin story ACs and the arch project-tree describe **net-new `*Page.razor` components** for routes that `PartiesAdminPortal.razor` **already serves** (list+search+filters+paging, full detail, GDPR-panel wiring). Implemented literally → rebuild of ~1,400 lines of hardened UI. **This is the single thing to fix before Admin work starts.**

### Critical Issues Requiring Immediate Action

1. **None are blockers.** The highest-priority item is **MAJOR-1** (re-anchor Admin stories to the existing component) — fix it before Epic 2/3 implementation to avoid wasted/duplicated work, directly per your directive.

### Issue Inventory

| ID | Severity | Summary |
|---|---|---|
| MAJOR-1 | 🟠 | Epic 2/3 Admin stories + arch tree spec net-new pages duplicating existing `PartiesAdminPortal.razor` + panels → rebuild/boilerplate risk |
| MINOR-1 | 🟡 | Story 1.4 cross-epic forward ref to Story 4.2 (mitigated via unit tests) |
| MINOR-2 | 🟡 | Story 4.1 is a decision-spike-as-story (accepted, gating) |
| MINOR-3 | 🟡 | New shared `StatusKind`/badge/freshness must replace existing inline equivalents, not run parallel |
| MINOR-4 | 🟡 | Epic 1 concentrates 6 technical-enabler stories — sequence sign-in to demo early |
| WARN-1 | ⚪ | No formal PRD (brownfield) — requirements basis is docs/ + UX + architecture (accepted, documented) |

### Already-tracked dependencies (not new findings — correctly gated)

- **AR-Gap-Binding (D2):** decide the `party_id` provisioning mechanism (Story 4.1 ADR) **before** building the Consumer area (Epics 4–5). Admin path is unaffected.
- **AR-Gap-D7:** EventStore erasure-verification contract (Story 3.5, cross-submodule, **approval-required**) gates only the FR-Admin-4 verification report (Story 3.6). Rest of Epic 3 ships without it.
- **AR-Gap-KMS:** production KMS must replace `LocalDevKeyStorageBackend` before any real EU PII (deploy-gate, Story 1.10).

### Recommended Next Steps

1. **Fix MAJOR-1 (small edit, high leverage):** rewrite Epic 2/3 Admin story ACs (2.2, 2.3, 3.1, 3.6) and the `architecture.md` project tree to **"reuse & enhance `PartiesAdminPortal.razor` + existing panels,"** scoping each to its real delta (search debounce; phone master-detail→sheet + focus contract; swap inline badge/freshness → shared components; SignalR reconcile wiring; demote 3.6 to "wire existing `ErasureVerificationReportPanel` to D7"). Keep **2.4 Create/Edit** and **2.5 picker re-skin** as the only net-new Admin builds.
2. **Add retrofit ACs (MINOR-3)** to Stories 1.6/1.8 so the new shared `StatusKind`/`PartyStateBadge`/`DataFreshnessIndicator` *replace* the existing inline forms in `PartiesAdminPortal.razor`.
3. **Resolve the binding decision (Story 4.1 ADR)** before estimating/starting Epic 4; it gates the whole Consumer area.
4. **Start implementation now on the unblocked spine:** Story 1.1 (host stand-up) → 1.2/1.3 (OIDC + role landing) → mount existing AdminPortal (2.1) — none of these are blocked, and they validate the reuse posture end-to-end early.
5. **Treat Story 3.5 (D7) as a separately-approved cross-submodule backend story**; don't let it block the rest of Epic 3.

### Final Note

This assessment identified **1 major + 4 minor issues + 1 accepted warning** across requirements, UX, and epic-quality categories, plus **3 already-tracked dependencies**. There are **no critical blockers**. The dominant, directive-relevant finding is **MAJOR-1**: the reuse *strategy* is correct, but the Admin story specs would, if followed literally, rebuild working code — correct the story ACs and the plan fully realizes your "minimal layer, no boilerplate" intent. You may proceed to implementation on the Foundation + Admin spine immediately while these edits are made.

---

*Assessment by: Implementation Readiness workflow (acting PM) · Assessor lens: reuse existing submodule classes, minimal technical layer, no boilerplate · Date: 2026-06-09 · Source artifacts: architecture.md, epics.md, ux-parties-2026-06-09/, verified against live source tree + submodules (FrontComposer, EventStore, Tenants).*
