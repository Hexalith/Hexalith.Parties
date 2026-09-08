# PRD Quality Review — parties-ui-prd

Reviewed: `_bmad-output/planning-artifacts/parties-ui-prd.md` (no addendum.md).
Date: 2026-09-08.

Calibration: brownfield consolidation whose declared job is to be the
machine-extractable FR/NFR identity and scope source for implementation-readiness
checks. Vision, personas, and innovation theater are not expected. Extractability,
scope honesty, done-ness, and currency of the readiness contract are load-bearing.
Strategic coherence is weighted lower. Prior 2026-08-18 findings were re-checked
against this v1.2.0 text and are not restated unless they still bite.

## Overall verdict

The v1.2.0 correction closed the Fair-grade identity holes: NFR rows exist, UX-DR1–16
are enumerated against `epics.md`, change control is real, the role model and the
worst untestable phrases were tightened, and KMS is a named deployment gate. What is
at risk is the new readiness surface itself. The NFR "Verified By" column cites a
five-job CI topology this repository does not run, so a tool that extracts coverage
gates from the Traceability Matrix will chase jobs that are not there. Residual
scope cracks — the party picker missing from FR-Admin-3, NFR1 scoped to
"Consumer-facing" while UX-DRs claim product-wide AA, and UX-DRs plus KMS sitting
off the matrix the contract names — matter because this file is the chain-top index.

## Decision-readiness — adequate

For this shape, acting on the PRD means a readiness operator can trust its answers
about which requirements exist, what is in or out, and which evidence to reconcile.
§Source Artifacts now states the authority split as a decision, not a hedge: this
file is "canonical for requirement identity and scope"; owning artifacts win on
"detailed semantics." §Current Implementation Evidence states a scope invariant with
teeth covering Epics 6, 7, and 8. §Document Control names the readiness contract:
tooling extracts identity and epic/surface mapping here; "acceptance evidence for
each requirement lives in the implementation story records." Those are usable
decisions. Zero Open Questions and zero `[NOTE FOR PM]` callouts remain
shape-correct — the MVP is shipped.

The soft spot is no longer an undated evidence block. The snapshot is pinned
("As of 2026-08-18") and tells later validation to reconcile with story records.
One listed story status has already moved. That is residual aging of an honest
snapshot, not the old "Epic 8 is only approved" false picture.

### Findings
- **low** Story 8.9 status has moved since the snapshot (§Current Implementation Evidence) — The roster says "8.9 `backlog`"; `_bmad-output/implementation-artifacts/sprint-status.yaml` (`last_updated: 2026-09-07`) marks `8-9-ui-frontcomposer-and-fluent-consolidation: blocked`. FR identity is unaffected (Epic 8 remains zero-new-FR maintenance). A reader who treats the roster as live status will mis-file 8.9. *Fix:* restamp the as-of date and 8.9 status on the next governed edit, or drop the story-level roster and point at `sprint-status.yaml` as the only status source.

## Substance over theater — strong

There is still no furniture in the product body. No personas, no vision statement,
no differentiation section — and for a retroactive consolidation that absence is
honesty. NFR2 still commits to "never treats accepted commands as read-your-write."
NFR4 still pins copy ("no fixed completion time"; legal bases "never coerced into
consent toggles"). NFR7 still bans a concrete failure mode ("Do not hard-code raw
accent colors"). The UX-DR list is now sixteen identity lines, not range theater.
The one false-precision flourish is the new NFR "Verified By" column, which looks
like a gate map and names jobs that do not exist — logged under Downstream
usability, where it harms the readiness contract.

### Findings
None.

## Strategic coherence — adequate

Weighted lower for this shape: the PRD is not betting a thesis for a green-light;
it is indexing a shipped brownfield product. §Product Scope still carries a compact
thesis (one responsive Blazor Server app, two role-gated areas, browser talks only
to the UI host/BFF, tokens server-side). The FR arc — shell, admin records, admin
GDPR, consumer profile, consumer consent/privacy — still maps one-to-one onto
Epics 1–5 in the Traceability Matrix. No Success Metrics and no counter-metrics;
for a consolidation whose success is extractable coverage, that omission stays
appropriate.

### Findings
None.

## Done-ness clarity — adequate

The 2026-08-18 done-ness residues are closed in this text. FR-Admin-4 now bounds
the verification report "per the D7 erasure-certificate decision in `epics.md`
(delivered by Stories 3.5 and 3.6)" and names `IAdminPortalGdprClient` /
`IErasureVerificationService`. FR-Consumer-4 now defines the cancellation event
("accepted until erasure processing begins and is rejected afterwards") and quotes
the rejection copy. FR-Admin-3 names the architecture's domain validation contract
and `PartyCommandValidationRejected`. NFR2 names the staleness trigger
(`ProjectionFreshnessMetadata` and the degraded-response middleware). NFR1 states
"at least 24×24 CSS px (WCAG 2.2 SC 2.5.8)." Those are testable.

What remains is not missing AC theater — stories own acceptance, per the readiness
contract — but two scope/criterion mismatches a coverage check will get wrong if it
trusts this file's wording over `epics.md`.

### Findings
- **medium** NFR1 scopes AA to Consumer-facing while UX-DRs and Admin GDPR are product-wide (§NFR1, §UX-DR9, §UX-DR12) — NFR1 opens "Consumer-facing surfaces target WCAG 2.2 AA." UX-DR12 is "Forced-colors and reduced-motion support product-wide." Typed-name erasure confirmation, the party-picker combobox, and admin sheets are Admin surfaces. The NFR1 matrix row then maps Epics 1–5 and a missing `ui-a11y` job. A tool that honors the "Consumer-facing" sentence can skip Admin a11y and still pass NFR1. *Fix:* state the AA target as product-wide, or list excluded Admin surfaces, and point verification at tests that actually cover Admin GDPR and the picker.
- **low** Residual adverbial copy requirements (§FR-Consumer-3, §FR-Consumer-4) — "grant and withdraw consent honestly" and "Copy must be plain, honest" remain adjectives. UX-DR13–16 own the observable copy rules, so this is residue, not a hole. *Fix:* drop the adverbs or replace them with the already-stated observables (default Off, no wall-clock export promise, Art.21 Object rather than a withdraw toggle).

## Scope honesty — adequate

Omissions still do real work. §Out of MVP Scope names gateway self-principal,
consumer self-registration, and temporal/semantic search. The scope invariant now
covers Epics 6, 7, and 8 uniformly ("None of them introduces or covers a new PRD
functional requirement"). Production KMS was promoted into §Deployment Gates with
an owner and surviving doc pointers — the old "buried in Out of MVP Scope" finding
is closed as a burial. Two cracks remain: a delivered MVP surface is missing from
its FR, and the KMS gate is still dual-classed.

### Findings
- **medium** Party picker is a delivered MVP surface with no FR row (§FR-Admin-3, §Traceability Matrix vs `epics.md`) — Epics FR-Admin-3 includes the in-form `<hexalith-party-picker>`; Story 2.5 is `done`. This PRD's FR-Admin-3 names "a real radiogroup" and "route ids are authoritative on edit" and omits the picker. UX-DR7 exists only in the §UX Requirements list, which the readiness contract does not treat as the extraction surface. A coverage walk of FR rows can report Admin create/edit complete without the combobox contract. *Fix:* restore the picker to FR-Admin-3 (or add an FR-Admin-5) and map it on the matrix to the create/edit routes plus the picker host.
- **medium** KMS is a go-live gate without a requirement ID and is still listed as out of MVP (§Deployment Gates, §Out of MVP Scope) — The section is real ("Production KMS provisioning is a deployment prerequisite before processing real regulated EU personal data"). It has no `GATE-*` / NFR ID, no matrix row, and the first §Out of MVP Scope bullet still lists it. Verification is "the GDPR notice in `docs/index.md`" — a read, not a fail-closed startup or CI check. Brownfield `docs/architecture.md` still records that `LocalDevKeyStorageBackend` is the only registered store and that there is no production-environment rejection guard. *Fix:* give the gate a stable ID, put it on the matrix the contract names, keep it out of Out of MVP Scope, and name the fail-closed check if one exists — or state that the gate is documentary-only.

## Downstream usability — thin

This dimension is load-bearing: readiness tooling and later stories consume this
file. The FR identity side is now clean — nine stable FR IDs, epic and route
mapping, documented ID conventions, `status: canonical-requirements-source` as a
machine anchor. The NFR *identity* table closes the old "NFR coverage not
extractable" finding: NFR1–NFR9 have primary epics and a Verified By column.

The new column is the failure. NFR1 is "Verified By" "`ui-a11y` CI job (bUnit) +
Playwright `tests/e2e`." NFR9 is "Verified By" "All five CI jobs (`lint`, `test`,
`ui-a11y`, `contract-test`, `report`)." Those five job names do not exist.
`.github/workflows/ci.yml` is a single reusable caller of
`Hexalith.Builds` `domain-ci.yml@main` whose jobs are `build-and-test`,
`aspire-tests`, and `performance-tests`. `.github/workflows` contains no `ui-a11y`
or Playwright job. Playwright specs exist under `tests/e2e` (including
`parties-accessibility.spec.ts`) but are not a CI job. `docs/ci.md` states Pact
scripts "are not currently exposed at the repository root"; brownfield
`docs/architecture.md` still records `contract-test` as unenforced/skipped. v1.2.0
invented an extractable verification map that a coverage tool will treat as gates.

The readiness contract then over-narrows extraction: "readiness tooling extracts
requirement identity and epic/surface mapping from this file (Traceability
Matrix)." That matrix still has nine FR rows and nine NFR rows. UX-DR1–16 and the
KMS gate have no matrix row. Enumerating UX-DRs in a prose list closes the old
"unrecoverable IDs" finding for a human reader; it does not put them on the surface
the new contract blesses.

### Findings
- **high** NFR "Verified By" cites a CI topology this repository does not run (§Traceability Matrix NFR1 / NFR9 vs `.github/workflows/ci.yml`) — The extractable gate names (`lint`, `test`, `ui-a11y`, `contract-test`, `report`) are not workflow jobs. Current CI is `domain-ci.yml` tiers; Pact is unscaffolded at the root; Playwright a11y is a local `tests/e2e` lane, not a CI job. A tool that trusts this column will look for gates that do not exist and can still report green. *Fix:* name the jobs and test projects that actually run (`ci.yml` / `domain-ci.yml` tiers, plus the local Playwright lane), and mark NFR1 Playwright as out-of-CI until a workflow exists.
- **medium** Readiness contract extracts from a matrix that omits UX-DRs and the KMS gate (§Document Control, §Traceability Matrix, §UX Requirements, §Deployment Gates) — Document Control tells tooling to extract identity and epic/surface mapping from the Traceability Matrix. UX-DR1–16 and the production-KMS gate have no matrix row and no FR/NFR ID. *Fix:* add UX-DR and GATE-KMS (or NFR) rows to the same matrix, or change the contract to name every extractable section.
- **medium** NFR7's owned UX-DR range disagrees with its matrix cell (§NFR7 vs §Traceability Matrix NFR7) — NFR7 body: "the agreed domain deltas (UX-DR1 through UX-DR7, recorded in `epics.md`)." NFR7 matrix: "Epics 1–5 (UX-DR1–UX-DR3)." An extractor cannot tell whether brand discipline owns three token DRs or seven domain deltas. *Fix:* make the body and the matrix cell name the same UX-DR set.

## Shape fit — strong

The PRD still names its shape in §Purpose and holds to it. It is a brownfield
capability consolidation: no personas, no user journeys, no success-metric
apparatus bolted on to look like a consumer-product PRD. §Document Control now
makes the ID grammar and the FR-only scope invariant explicit, which is the right
formality for a chain-top index. Brownfield path checks still hold (all six
§Source Artifacts paths exist; `docs/deployment-security-checklist.md` is correctly
described as retired). Done MVP versus Epics 6–8 maintenance is distinguished
crisply. The verification-map and picker-identity wrinkles are logged above rather
than double-counted as a shape failure.

### Findings
None.

## Mechanical notes

- **Closed since 2026-08-18 (not re-filed):** frontmatter `last_updated` / `version` /
  changelog; NFR identity table; UX-DR1–16 enumerated with `epics.md` as ID
  authority; FR-Shell role/landing rules (dual-role → Admin, no-role fail-closed,
  DPO duties under Admin policy); NFR3 now-vs-deferred seam; Epic 6 included in
  the maintenance invariant; D7 / named GDPR seams; cancellation event; validation
  and staleness owning records; NFR8 `Hexalith.Commons.ServiceDefaults`; KMS
  promoted out of a lone Out-of-MVP bullet into §Deployment Gates.
- **Cross-refs:** all six §Source Artifacts paths present on disk;
  `sprint-change-proposal-2026-07-06.md` resolves; KMS verification paths
  `docs/index.md` and `docs/getting-started.md` exist;
  `IAdminPortalGdprClient` and `IErasureVerificationService` exist in source.
- **Sprint-status roundtrip:** Epics 1–7 `done` and Epic 8 `in-progress` still
  match. Story-level drift since the 2026-08-18 snapshot: 8.9 `backlog` →
  `blocked`; 8.7 / 8.8 remain `blocked`; 8.10 remains `review`.
- **ID continuity:** FR-Shell + FR-Admin-1..4 + FR-Consumer-1..4 unique, no gaps;
  NFR1–NFR9 contiguous; UX-DR1–UX-DR16 contiguous in §UX Requirements. Document
  Control now declares which classes count as functional requirements.
- **Assumptions Index roundtrip:** vacuously consistent — no inline
  `[ASSUMPTION]` or `[NOTE FOR PM]` tags and no index.
- **UJ protagonists:** no UJs — shape-appropriate.
- **Glossary:** still absent; `party_id`, "Bound Consumers", "last-known",
  "tombstone" remain consistent in case and form.
- **Fitness freeze (context, not a PRD prose defect):**
  `EpicEightClosureFitnessTests.ExtractRequirementIds` matches heading-shaped
  `FR-*` / `NFR*` IDs only, so UX-DR bullets and Deployment Gates are invisible
  to the inventory freeze that protects this file.
