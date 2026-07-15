---
project_name: parties
document_type: prd
status: canonical-requirements-source
date: 2026-06-27
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

When this PRD and source artifacts conflict, the source artifact owning the topic
wins: architecture for system decisions, UX spines for product experience, and
implementation story records for completed work evidence.

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
users land in Consumer. Navigation is policy-gated so Admin and Consumer entries
do not cross-render. Consumers without exactly one verified `party_id` claim land
in the fail-closed `NoPartyBinding` state, never on a data screen.

### FR-Admin-1: Parties List

Admins can search and filter parties server-side by display name, party type, and
active state. The list supports paging, row-to-detail navigation, stale/degraded
read handling, last-known rendering, and accessible keyboard navigation.

### FR-Admin-2: Party Detail

Admins can view the full `PartyDetail`, including lifecycle state and freshness.
The detail view provides entry points to edit and GDPR operations. Missing or
erased parties render PII-free tombstone states.

### FR-Admin-3: Create and Edit Party

Admins can create and edit Person and Organization parties through validated
forms. Person/Organization selection uses a real radiogroup, route ids are
authoritative on edit, validation errors are announced accessibly, and successful
commands use optimistic UI plus projection reconciliation.

### FR-Admin-4: GDPR Operations

DPO/Admin users can erase a party with typed-name confirmation, restrict and lift
processing restriction, record and revoke consent, export data under Art.20, view
processing records under Art.30, and prove erasure with a bounded verification
report. GDPR operations must avoid PII leakage and route through existing typed
client/gateway seams.

### FR-Consumer-1: My Profile

Bound Consumers can view their own personal data and projection freshness. They
never see list/search surfaces. Stale/degraded reads show last-known data, and an
erased self renders a PII-free tombstone.

### FR-Consumer-2: Edit My Profile

Bound Consumers can correct their own data through validated, self-scoped update
commands. Prefilled values match stored values, validation preserves input, and
accepted commands reconcile through the shared optimistic/freshness pattern.

### FR-Consumer-3: My Consent

Bound Consumers can grant and withdraw consent honestly. Consent toggles default
Off, are real switch controls, and distinguish consent-based items from contract,
legal, and legitimate-interest bases. Legitimate-interest items provide Object
under Art.21 rather than a withdraw toggle.

### FR-Consumer-4: My Data and Privacy

Bound Consumers can export their own data as machine-readable JSON, request or
cancel erasure while cancellation is still allowed, and view what is processed
about them through bounded audit metadata. Copy must be plain, honest, and free of
hard timing promises that the system cannot guarantee.

## Non-Functional Requirements

### NFR1: Accessibility

Consumer-facing surfaces target WCAG 2.2 AA. Required patterns include real ARIA
semantics, correct live-region politeness split, visible focus, forced-colors and
reduced-motion support, non-color cues, keyboard operation, and usable target
sizes.

### NFR2: Eventual Consistency UX

Projection freshness is first-class. The UI renders last-known data on stale or
degraded reads, uses optimistic echo for accepted commands, reconciles on
projection confirmation, and never treats accepted commands as read-your-write.

### NFR3: Security and Own-Data Privacy

Consumer operations are own-data only. Consumer pages use the self-scoped accessor
and must not accept caller-supplied party ids. Parties-side defense-in-depth
asserts `aggregateId == party_id`. Logs, telemetry, tombstones, and error copy do
not expose PII.

### NFR4: GDPR Honesty

Consent is opt-in and default Off. Erasure copy commits to starting the obligation
and states completed erasure is permanent. Export copy promises machine-readable
delivery but no fixed completion time. Legal bases are represented honestly.

### NFR5: Responsive Design

Admin is desktop-first but reflows to sheet/full-screen detail on small screens.
Consumer is phone-first and single-column. Both areas share one responsive codebase
with different density postures.

### NFR6: Multi-Tenancy

Admin operates within tenant scope. Tenant access fails closed and may be
eventually consistent after restart. Tenant warm-up is communicated as a temporary
state, not as misleading access denial.

### NFR7: Brand Discipline

The UI inherits FrontComposer and FluentUI V5/Fluent 2. New styling is limited to
the agreed domain deltas. Do not hard-code raw accent colors for text-bearing
controls or redeclare Fluent tokens in product CSS.

### NFR8: Observability

The UI host uses ServiceDefaults, OpenTelemetry, health checks, degraded headers,
and freshness metadata without logging personal data or event payloads.

### NFR9: Build and Quality Gates

The work stays on .NET 10, central package management, `.slnx`, warnings as
errors, xUnit v3/Shouldly/NSubstitute/bUnit, Playwright accessibility checks, and
root-level submodules under `references/` only.

## UX Requirements

The final UX design set is authoritative for the product experience. Key
requirements include:

- UX-DR1 through UX-DR3: AA-safe brand fill, status token pairs, and Fluent
  inheritance discipline.
- UX-DR4 through UX-DR7: party-state badge, data-freshness indicator, GDPR
  destructive button, and Fluent 2/WAI-ARIA party picker.
- UX-DR8 through UX-DR12: live-region split, real semantics, focus contracts,
  non-color cues, target sizing, forced-colors, and reduced-motion support.
- UX-DR13 through UX-DR16: honest erasure, lawful-basis, export, plain-verb, and
  single-status-source copy.

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

## Current Implementation Evidence

As of 2026-06-27, `_bmad-output/implementation-artifacts/sprint-status.yaml`
marks Epics 1-5 and their stories as `done`. Readiness validation after this date
must reconcile this PRD and planning documents with implementation story records.

Post-MVP maintenance status:

**Scope invariant:** Epics 7 and 8 are maintenance scope only. Neither epic
introduces or covers a new PRD functional requirement, and neither may be
counted as MVP or product-feature functional coverage.

- Epic 6 is in-repository consolidation scope. It supports NFR9 and carries no
  new PRD functional requirement coverage.
- Epic 7 is completed partial platform-alignment scope. Its final readiness
  record preserves rollback paths and deferred deletion-safe cleanup. It carries
  no new PRD functional requirement coverage.
- Epic 8, approved by `sprint-change-proposal-2026-07-06.md`, is domain-focus
  refactoring and platform extraction. It is post-MVP maintenance only, carries
  no new PRD functional requirement coverage, and must not be reported as
  product-feature delivery.

Known completed dependency evidence:

- Story 1.4 completed fail-closed `party_id` claim resolution with synthetic-claim
  and DI coverage.
- Story 3.5 completed D7 erasure certificate and retry backend behavior through
  existing projection-query and command seams.
- Story 3.6 completed the bounded Admin erasure-verification report UI.
- Story 4.1 completed the accepted Consumer identity binding ADR.
- Story 4.2 completed admin-link identity binding provisioning.

## Out of MVP Scope

- Production KMS provisioning is a deployment prerequisite before processing real
  regulated EU personal data, not a UI feature story.
- Gateway-level data-subject/self principal support remains a future enhancement.
- Consumer self-registration and IdP federation are future provisioning options.
- Temporal name-as-of queries and semantic/graph/hybrid search remain deferred.
