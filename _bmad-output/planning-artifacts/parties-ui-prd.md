---
project_name: parties
document_type: prd
status: canonical-requirements-source
date: 2026-06-27
last_updated: 2026-08-18
version: 1.2.0
requirements_basis: "Brownfield docs + final UX design set + architecture requirements inventory + epics FR map"
---

# Parties UI PRD

## Purpose

This PRD is the canonical, PRD-shaped requirements source for implementation
readiness checks for the `parties-ui` initiative.

The project is brownfield. The original requirements were captured in the
architecture document, the final UX design set, the existing docs baseline, and
the epics/story breakdown. This file consolidates that requirements basis so
readiness tooling can extract FR/NFR coverage without treating the absence of a
traditional PRD as a blocker.

## Source Artifacts

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/validation-report.md`
- `docs/index.md` and linked brownfield project documentation

This file is canonical for requirement identity and scope: which requirements
exist, their IDs, and what is in or out of scope. When this PRD and source
artifacts conflict on a topic's detailed semantics, the source artifact owning
the topic wins: architecture for system decisions, UX spines for product
experience, and implementation story records for completed work evidence.
UX-DR identifiers are defined in `epics.md`; the UX design set records the
resolved experience those IDs implement.

## Product Scope

Realize `parties-ui`: a single responsive Blazor Server application on
FrontComposer and FluentUI Blazor V5, with two role-gated areas:

- Admin records management and GDPR/DPO operations under `/admin/parties*`.
- Consumer own-data GDPR self-service under `/me*`.

The app extends the existing Hexalith.Parties event-sourced/CQRS service through
the EventStore gateway. The browser talks only to the UI host/BFF. The UI host
owns OIDC sign-in and keeps tokens server-side.

## Functional Requirements

### FR-Shell

Authenticate users through host-owned OIDC, preserve return URLs, and route users
to the correct area by role. Admin or TenantOwner users land in Admin; Consumer
users land in Consumer. A principal holding both Admin and Consumer roles lands
in Admin (the shell checks Admin-policy roles first); an authenticated principal
with neither role sees a fail-closed no-area state. There is no separate DPO
role: DPO duties are performed under the Admin policy. Navigation is
policy-gated so Admin and Consumer entries do not cross-render. Consumers
without exactly one verified `party_id` claim land in the fail-closed
`NoPartyBinding` state, never on a data screen.

### FR-Admin-1: Parties List

Admins can search and filter parties server-side by display name, party type, and
active state. The list supports paging, row-to-detail navigation, stale/degraded
read handling per the NFR2 freshness contract, last-known rendering, and
accessible keyboard navigation.

### FR-Admin-2: Party Detail

Admins can view the full `PartyDetail`, including lifecycle state and freshness.
The detail view provides entry points to edit and GDPR operations. Missing or
erased parties render PII-free tombstone states.

### FR-Admin-3: Create and Edit Party

Admins can create and edit Person and Organization parties through validated
forms; the validation rules are owned by the architecture's domain validation
contract (rejections surface as `PartyCommandValidationRejected`).
Person/Organization selection uses a real radiogroup, route ids are
authoritative on edit, validation errors are announced accessibly, and successful
commands use optimistic UI plus projection reconciliation.

### FR-Admin-4: GDPR Operations

DPO/Admin users can erase a party with typed-name confirmation, restrict and lift
processing restriction, record and revoke consent, export data under Art.20, view
processing records under Art.30, and prove erasure with a verification report
bounded per the D7 erasure-certificate decision in `epics.md` (delivered by
Stories 3.5 and 3.6). GDPR operations must avoid PII leakage and route through
the existing typed client/gateway seams (`IAdminPortalGdprClient`,
`IErasureVerificationService`).

### FR-Consumer-1: My Profile

Bound Consumers can view their own personal data and projection freshness. They
never see list/search surfaces. Stale/degraded reads show last-known data, and an
erased self renders a PII-free tombstone.

### FR-Consumer-2: Edit My Profile

Bound Consumers can correct their own data through validated, self-scoped update
commands (same validation ownership as FR-Admin-3). Prefilled values match
stored values, validation preserves input, and accepted commands reconcile
through the shared optimistic/freshness pattern.

### FR-Consumer-3: My Consent

Bound Consumers can grant and withdraw consent honestly. Consent toggles default
Off, are real switch controls, and distinguish consent-based items from contract,
legal, and legitimate-interest bases. Legitimate-interest items provide Object
under Art.21 rather than a withdraw toggle.

### FR-Consumer-4: My Data and Privacy

Bound Consumers can export their own data as machine-readable JSON, request
erasure, and cancel a requested erasure while the erasure obligation is still
pending — cancellation is accepted until erasure processing begins and is
rejected afterwards ("deletion has already begun"). They can view what is
processed about them through the bounded Art.30 `ProcessingActivityRecord`
reads defined by the architecture's projection/query contract. Copy must be
plain, honest, and free of hard timing promises that the system cannot
guarantee.

## Non-Functional Requirements

### NFR1: Accessibility

Consumer-facing surfaces target WCAG 2.2 AA. Required patterns include real ARIA
semantics, correct live-region politeness split, visible focus, forced-colors and
reduced-motion support, non-color cues, keyboard operation, and target sizes of
at least 24×24 CSS px (WCAG 2.2 SC 2.5.8).

### NFR2: Eventual Consistency UX

Projection freshness is first-class. The UI renders last-known data on stale or
degraded reads, uses optimistic echo for accepted commands, reconciles on
projection confirmation, and never treats accepted commands as read-your-write.
A read counts as stale or degraded per the architecture's projection-freshness
contract (`ProjectionFreshnessMetadata` and the degraded-response middleware).

### NFR3: Security and Own-Data Privacy

Consumer operations are own-data only. Consumer pages use the self-scoped accessor
and must not accept caller-supplied party ids. Parties-side defense-in-depth
asserts `aggregateId == party_id`; this Parties-side assertion is the implemented
enforcement today, and the deferred gateway-level data-subject/self principal
support (see Out of MVP Scope) would add an earlier enforcement seam without
replacing it. Logs, telemetry, tombstones, and error copy do not expose PII.

### NFR4: GDPR Honesty

Consent is opt-in and default Off. Erasure copy commits to starting the obligation
and states completed erasure is permanent. Export copy promises machine-readable
delivery but no fixed completion time. Legal bases are surfaced as recorded
(`LawfulBasis`), never coerced into consent toggles.

### NFR5: Responsive Design

Admin is desktop-first but reflows to sheet/full-screen detail on small screens.
Consumer is phone-first and single-column. Both areas share one responsive codebase
with different density postures.

### NFR6: Multi-Tenancy

Admin operates within tenant scope. Tenant access fails closed and may be
eventually consistent after restart. Tenant warm-up is surfaced as a distinct
temporary warm-up state, not as an access-denied error.

### NFR7: Brand Discipline

The UI inherits FrontComposer and FluentUI V5/Fluent 2. New styling is limited to
the agreed domain deltas (UX-DR1 through UX-DR7, recorded in `epics.md`). Do not
hard-code raw accent colors for text-bearing controls or redeclare Fluent tokens
in product CSS.

### NFR8: Observability

The UI host uses `Hexalith.Commons.ServiceDefaults` (the local ServiceDefaults
wrapper was retired by Story 8.4), OpenTelemetry, health checks, degraded
headers, and freshness metadata without logging personal data or event payloads.

### NFR9: Build and Quality Gates

The work stays on .NET 10, central package management, `.slnx`, warnings as
errors, xUnit v3/Shouldly/NSubstitute/bUnit, Playwright accessibility checks, and
root-level submodules under `references/` only.

## UX Requirements

The final UX design set is authoritative for the product experience; the UX-DR
identifiers below are defined individually in `epics.md` (Design Requirements):

- UX-DR1 — AA-safe brand fill: filled primary buttons bind to an AA-safe token.
- UX-DR2 — Status token pairs: party/GDPR/freshness state colors map to Fluent 2
  token pairs.
- UX-DR3 — Inheritance discipline: no redeclaring Fluent 2 custom properties.
- UX-DR4 — Party-state badge: badge pill with color plus text label.
- UX-DR5 — Data-freshness indicator: dot plus word for fresh/stale ("as of …").
- UX-DR6 — GDPR destructive button: danger fill for destructive GDPR actions.
- UX-DR7 — Party picker re-skin with a full WAI-ARIA combobox contract.
- UX-DR8 — Live-region politeness split across status, freshness, and
  accepted-processing announcements.
- UX-DR9 — Real semantics, no interactive `<div>`s (consent controls are real
  switches).
- UX-DR10 — Per-surface focus contract: skip links; sheet open moves focus in,
  close returns it to the originating row.
- UX-DR11 — Non-color cues and target sizing for active/selected affordances.
- UX-DR12 — Forced-colors and reduced-motion support product-wide.
- UX-DR13 — Honest erasure copy: commit to the start; completed erasure is
  permanent.
- UX-DR14 — Lawful-basis honesty: consent-controlled items split from other
  lawful bases.
- UX-DR15 — Export copy with no time promise.
- UX-DR16 — Plain verbs and a single status source per surface.

## Traceability Matrix

| Requirement | Primary Epic | Primary Surfaces |
|---|---|---|
| FR-Shell | Epic 1 | Sign-in, role landing, navigation, NoPartyBinding |
| FR-Admin-1 | Epic 2 | `/admin/parties` |
| FR-Admin-2 | Epic 2 | `/admin/parties/{id}` |
| FR-Admin-3 | Epic 2 | `/admin/parties/new`, `/admin/parties/{id}/edit` |
| FR-Admin-4 | Epic 3 | `/admin/parties/{id}/gdpr` |
| FR-Consumer-1 | Epic 4 | `/me` |
| FR-Consumer-2 | Epic 4 | `/me/edit` |
| FR-Consumer-3 | Epic 5 | `/me/consent` |
| FR-Consumer-4 | Epic 5 | `/me/privacy` |

NFR coverage (cross-cutting; primary owners and verification gates):

| Requirement | Primary Epics | Verified By |
|---|---|---|
| NFR1 | Epics 1–5 (a11y gate scaffolded in Epic 1) | `ui-a11y` CI job (bUnit) + Playwright `tests/e2e` |
| NFR2 | Epics 1–2, 4 (shared freshness/optimistic pattern) | All data surfaces; freshness contract tests |
| NFR3 | Epic 1 (Story 1.4 binding), Epics 4–5 (self-scoped clients) | `/me*` surfaces; binding fitness tests |
| NFR4 | Epics 3, 5 | GDPR and consent surface copy |
| NFR5 | Epics 2, 4 | Admin sheet reflow, Consumer single-column |
| NFR6 | Epics 1–2 | Admin tenant scope; warm-up state |
| NFR7 | Epics 1–5 (UX-DR1–UX-DR3) | Accessibility style guard tests |
| NFR8 | Epic 1 (host wiring) | UI host telemetry/health endpoints |
| NFR9 | Epic 6 and CI | All five CI jobs (`lint`, `test`, `ui-a11y`, `contract-test`, `report`) |

## Current Implementation Evidence

As of 2026-08-18, `_bmad-output/implementation-artifacts/sprint-status.yaml`
marks the MVP scope — Epics 1-5 and their stories — as `done`, Epic 6 as
`done`, Epic 7 as `done`, and Epic 8 as `in-progress` (stories 8.1-8.6 and
8.11-8.13 `done`; 8.7 and 8.8 `blocked`; 8.9 `backlog`; 8.10 in `review`).
Readiness validation after this date must reconcile this PRD and planning
documents with implementation story records.

Post-MVP maintenance status:

**Scope invariant:** Epics 6, 7, and 8 are maintenance scope only. None of them
introduces or covers a new PRD functional requirement, and none may be counted
as MVP or product-feature functional coverage.

- Epic 6 (`done`) is in-repository consolidation scope. It supports NFR9 and
  carries no new PRD functional requirement coverage.
- Epic 7 (`done`) is completed platform-alignment maintenance scope. Its final
  readiness record preserved rollback paths and deferred deletion-safe cleanup;
  the projection rollback-only paths were subsequently removed on 2026-08-01
  under the governed retention closure (Story 8.6,
  `sprint-change-proposal-2026-08-01-projection-rollback-retention-revalidation.md`),
  while crypto/key-management rollback paths remain preserved pending the
  Story 8.7 gate.
- Epic 8 (`in-progress`), approved by `sprint-change-proposal-2026-07-06.md`,
  is domain-focus refactoring and platform extraction; stories 8.11-8.13 were
  added by the 2026-07-07 and 2026-07-08 correct-course proposals. It is
  post-MVP maintenance only, carries no new PRD functional requirement
  coverage, and must not be reported as product-feature delivery.

Known completed dependency evidence:

- Story 1.4 completed fail-closed `party_id` claim resolution with synthetic-claim
  and DI coverage.
- Story 3.5 completed the D7 erasure certificate (the D7 decision in `epics.md`)
  and retry backend behavior through existing projection-query and command seams.
- Story 3.6 completed the bounded Admin erasure-verification report UI.
- Story 4.1 completed the accepted Consumer identity binding ADR.
- Story 4.2 completed admin-link identity binding provisioning.

## Deployment Gates

Not UI feature scope, but go-live requirements that readiness reporting must
surface rather than ignore:

- **Production KMS provisioning** is a deployment prerequisite before processing
  real regulated EU personal data; the default key store
  (`LocalDevKeyStorageBackend`) is dev-only. Owner: deployment/platform owner.
  Verification: the GDPR notice in `docs/index.md` and the production KMS gate
  in `docs/getting-started.md` (the former `docs/deployment-security-checklist.md`
  was retired by Story 8.13).

## Out of MVP Scope

- Production KMS provisioning is tracked as a deployment gate above, not a UI
  feature story.
- Gateway-level data-subject/self principal support remains a future enhancement.
- Consumer self-registration and IdP federation are future provisioning options.
- Temporal name-as-of queries and semantic/graph/hybrid search remain deferred.

## Document Control

Frontmatter semantics: `date` is the original issue date; `last_updated` and
`version` advance with every edit; `status: canonical-requirements-source` is a
stable machine anchor for readiness tooling and does not change.

Requirement ID conventions: functional requirements use `FR-<Area>` or
`FR-<Area>-<n>` (FR-Shell, FR-Admin-1..4, FR-Consumer-1..4); non-functional
requirements use `NFR<n>` (NFR1..NFR9); UX design requirements use `UX-DR<n>`
(UX-DR1..UX-DR16, defined in `epics.md`). Only `FR-*` entries count as PRD
functional requirements for scope invariants such as the Epics 6-8 zero-new-FR
rule; NFRs and UX-DRs are non-functional and design coverage respectively.

Readiness contract: readiness tooling extracts requirement identity and
epic/surface mapping from this file (Traceability Matrix); acceptance evidence
for each requirement lives in the implementation story records, which win on
completed-work evidence per the Source Artifacts precedence rule.

Change log:

| Version | Date | Change |
|---|---|---|
| 1.0.0 | 2026-06-27 | Initial brownfield consolidation. |
| 1.1.0 | 2026-07-06 | Post-MVP maintenance status: Epic 7 completion, Epic 8 approval. |
| 1.1.1 | 2026-07-16 | Epics 7-8 maintenance-scope invariant (`sprint-change-proposal-2026-07-16-epics-7-8-maintenance-scope.md`). |
| 1.2.0 | 2026-08-18 | Governed correction from `prds/prd-parties-2026-08-18/validation-report.md`: currency refresh, NFR traceability, UX-DR enumeration, deployment-gate section, clarified wording. No functional requirement added or removed. |
