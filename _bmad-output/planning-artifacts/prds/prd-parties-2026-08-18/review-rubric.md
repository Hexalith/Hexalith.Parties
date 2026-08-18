# PRD Quality Review — parties-ui-prd

Reviewed: `_bmad-output/planning-artifacts/parties-ui-prd.md` (no addendum.md).
Calibration: brownfield, retroactive consolidation PRD. Per §Purpose it exists to be the
"canonical, PRD-shaped requirements source for implementation readiness checks" — Epics 1–5
(the MVP) are shipped and `done`; Epics 6–8 are maintenance scope. Judged accordingly:
Done-ness clarity and Downstream usability (extraction by readiness tooling) weigh most;
Strategic coherence, personas/UJs, and Success Metrics weigh less and that is said explicitly
where it applies.

## Overall verdict

This PRD does its declared job well: a compact, theater-free consolidation with a real
precedence rule ("the source artifact owning the topic wins"), clean FR identification, a
complete FR traceability matrix, and unusually honest scope ring-fencing — and its brownfield
references check out on disk (all six §Source Artifacts paths exist; the Epics 1–5 `done`
claim matches `sprint-status.yaml`). The risk sits on the non-FR half of its own promise:
NFR coverage is not extractable from the traceability matrix, and the UX-DR ID scheme does
not resolve in the artifact the PRD names as its authority — so readiness tooling gets
first-class FRs but second-class NFR/UX-DR coverage. Fit for purpose with targeted fixes;
no critical findings.

## Decision-readiness — adequate

For this shape, "acting on the PRD" means a readiness operator or maintainer can trust its
answers. The PRD makes its two load-bearing decisions as decisions, not considerations:
§Source Artifacts states a conflict-precedence rule ("the source artifact owning the topic
wins: architecture for system decisions, UX spines for product experience, and implementation
story records for completed work evidence"), and §Current Implementation Evidence states a
scope invariant with teeth ("Epics 7 and 8 are maintenance scope only... must not be reported
as product-feature delivery"). The absence of Open Questions and `[NOTE FOR PM]` callouts is
shape-consistent — the underlying decisions were made and shipped; there is nothing left
open to dodge.

The one soft spot is temporal: the evidence snapshot is pinned to 2026-06-27 and the document
correctly instructs that later validation "must reconcile this PRD and planning documents with
implementation story records," but the maintenance bullets partially read as current state
while the tree has moved (as of this review, `sprint-status.yaml` shows epics 6–7 `done` and
epic 8 `in-progress`).

### Findings
- **[low]** Evidence snapshot ages without a refresh contract (§Current Implementation Evidence) — "As of 2026-06-27" is now ~7 weeks old; epic 6 and 7 are `done` and epic 8 `in-progress` in `sprint-status.yaml`, while the bullets still describe Epic 8 as "approved". The reconciliation sentence mitigates, but a reader can mistake the bullets for live status. *Fix:* re-stamp the snapshot date on each readiness run, or mark each maintenance bullet explicitly as-of its date.

## Substance over theater — strong

There is no furniture in this document. No personas, no vision statement, no differentiation
section — and for a retroactive consolidation that absence is honesty, not a gap. What is
here is earned and product-specific throughout: NFR2 commits to "never treats accepted
commands as read-your-write"; NFR4 pins copy behavior ("Erasure copy commits to starting the
obligation and states completed erasure is permanent... no fixed completion time"); NFR7 bans
a concrete failure mode ("Do not hard-code raw accent colors for text-bearing controls or
redeclare Fluent tokens in product CSS"). None of the nine NFRs is copyable boilerplate —
each would be falsified by a specific implementation mistake. The §UX Requirements section is
deliberately a pointer rather than a restatement, which is consistent with the precedence
rule (its extraction weakness is logged under Downstream usability, not here).

### Findings
No findings.

## Strategic coherence — adequate

This dimension matters less for this shape: the PRD is not betting a thesis to win a
green-light; the product is shipped. Judged lightly, it still coheres: §Product Scope carries
a compact thesis (one responsive Blazor Server app, two role-gated areas, browser talks only
to the UI host/BFF, tokens server-side), and the FR arc — shell, admin records, admin GDPR,
consumer self-service, consumer consent/privacy — mirrors Epics 1–5 one-to-one in the
§Traceability Matrix rather than reading as a backlog. There are no Success Metrics and no
counter-metrics; for a retroactive consolidation whose "success" is that readiness tooling
can extract coverage, that omission is appropriate and is noted here rather than penalized.

### Findings
No findings.

## Done-ness clarity — adequate

This dimension matters most here, so it was judged unforgivingly. Most FRs carry at least one
directly testable consequence: FR-Shell's "Consumers without exactly one verified `party_id`
claim land in the fail-closed `NoPartyBinding` state, never on a data screen" is a crisp
pass/fail; FR-Consumer-3's "Consent toggles default Off, are real switch controls" likewise;
FR-Admin-3 pins "route ids are authoritative on edit" and "a real radiogroup"; FR-Admin-4
enumerates its operations (typed-name erasure confirmation, restrict/lift, record/revoke
consent, Art.20 export, Art.30 records). The pattern vocabulary — "last-known rendering",
"optimistic echo", "PII-free tombstone" — is defined once in NFR2/NFR3 and reused across FRs,
which keeps the terse FR paragraphs testable rather than adjectival.

The residue: "bounded" appears twice with no bound in-document and no pointer to the record
that owns the bound; "validated forms" leaves the validation contract unlocated; and nothing
in the PRD defines when a read counts as "stale" or "degraded" (the behavior once stale is
testable, but the trigger lives elsewhere unnamed). FR-Consumer-4's "Copy must be plain,
honest" is adjectives, but UX-DR13–16 explicitly own copy requirements, so it resolves.
Because Epics 1–5 story records carry the acceptance evidence and the precedence rule
delegates authority, these are moderated in severity — but a reader working from this
document alone hits them.

### Findings
- **[medium]** "Bounded" without a bound (§FR-Admin-4, §FR-Consumer-4) — "a bounded verification report" and "bounded audit metadata" state a bound exists but neither the bound nor its owning record is named; §Current Implementation Evidence mentions Story 3.5/3.6 (D7 erasure certificate, bounded Admin erasure-verification report UI) but the FRs don't cite them. *Fix:* state the bound in one clause per FR, or cite the owning story/architecture record by name.
- **[low]** Validation rules unlocated (§FR-Admin-3, §FR-Consumer-2) — "validated forms" and "validated, self-scoped update commands" don't say which artifact owns the validation contract; the precedence rule implies architecture or UX but doesn't disambiguate for this topic. *Fix:* name the owning artifact for form-validation rules.
- **[low]** Staleness trigger undefined (§NFR2, §FR-Admin-1) — the response to a stale/degraded read is well-specified, but no in-document definition or named pointer says what makes a read stale/degraded. *Fix:* cite the architecture's freshness/degraded-read contract.

## Scope honesty — strong

Omissions are explicit and the boundaries do real work. §Out of MVP Scope names four
deferrals with their correct dispositions — notably "Production KMS provisioning is a
deployment prerequisite before processing real regulated EU personal data, not a UI feature
story", which is exactly the kind of reclassification that silent de-scoping would hide. The
§Current Implementation Evidence scope invariant is an unusually honest anti-inflation guard:
it doesn't just exclude Epics 6–8 from FR coverage, it forbids counting them ("must not be
reported as product-feature delivery"). Zero `[ASSUMPTION]` tags and zero open items is
shape-consistent, not evasion — the sources are shipped artifacts, so nothing was inferred
without confirmation. Open-items density of zero on a retroactive PRD is exactly right.

### Findings
No findings.

## Downstream usability — adequate

This dimension matters most for this PRD — extraction by readiness tooling is its stated
purpose. The FR side is clean: nine unique, stable FR IDs; a §Traceability Matrix covering
every FR with epic and concrete surfaces (routes); frontmatter `status:
canonical-requirements-source` as a tooling anchor; and every §Source Artifacts path verified
present on disk. The NFR and UX-DR sides are weaker, and both weaknesses cut directly against
the §Purpose promise that "readiness tooling can extract FR/NFR coverage."

The UX-DR problem is specific: §UX Requirements opens "The final UX design set is
authoritative for the product experience" — but `DESIGN.md`, `EXPERIENCE.md`, and
`validation-report.md` contain zero occurrences of "UX-DR". The individually numbered
definitions live in `epics.md` (e.g., "UX-DR9 — Real semantics, no interactive `<div>`s").
On top of the misattribution, the grouped labels don't align with the ranges: "UX-DR8 through
UX-DR12" lists seven items (live-region split, real semantics, focus contracts, non-color
cues, target sizing, forced-colors, reduced-motion) across five IDs, and "UX-DR13 through
UX-DR16" lists five items across four IDs — so neither a tool nor a human can map a given
UX-DR ID to its requirement from this document or from the artifact it names as authority.

### Findings
- **[high]** NFR coverage not extractable (§Traceability Matrix, §Purpose) — the Purpose promises extraction of "FR/NFR coverage", but the matrix maps only the nine FRs; the sole NFR→epic mapping in the document is one prose fragment ("Epic 6... supports NFR9"). NFR1–NFR8 coverage must be inferred from outside the canonical source. *Fix:* add NFR rows to the traceability matrix (or a second matrix) mapping each NFR to its primary epics/surfaces or gate (e.g., NFR1→a11y gate, NFR9→CI).
- **[medium]** UX-DR IDs don't resolve in their named authority (§UX Requirements) — the named-authoritative UX design set carries no UX-DR identifiers (they are defined individually in `epics.md`), and two range/label groupings mismatch (DR8–12: 5 IDs vs 7 labels; DR13–16: 4 IDs vs 5 labels), leaving individual UX-DR requirements unmappable from this PRD. *Fix:* enumerate UX-DR1–16 individually with one line each, or explicitly name `epics.md` as the ID authority and align each label to its ID.
- **[low]** No Glossary (whole document) — domain nouns (tombstone, optimistic echo, projection freshness, bound Consumer, self-scoped accessor, tenant warm-up) are used consistently but never defined in-doc; tolerable under the precedence rule, but sections don't fully stand alone when pulled out. *Fix:* optional ten-line glossary anchoring the pattern vocabulary.

## Shape fit — strong

The PRD names its own shape in §Purpose and holds to it. It is a brownfield capability
consolidation and reads like one: no personas, no user journeys, no success-metric apparatus
bolted on to look like a chain-top PRD — the rubric's over-formalization trap is exactly what
this document avoids. Brownfield accuracy checks pass: all six §Source Artifacts paths exist;
`sprint-change-proposal-2026-07-06.md` resolves; the "Epics 1-5 and their stories as `done`"
claim matches `sprint-status.yaml`; the Story 1.4/3.5/3.6/4.1/4.2 dependency-evidence entries
match existing implementation-artifact records. Done MVP versus maintenance scope is
distinguished crisply rather than left to inference. The single shape wrinkle — the UX-DR
authority misattribution — is logged under Downstream usability rather than double-counted
here.

### Findings
No findings.

## Mechanical notes

- **Cross-refs:** all six §Source Artifacts paths verified present on disk;
  `sprint-change-proposal-2026-07-06.md` resolves in `planning-artifacts/`.
- **Sprint-status roundtrip:** PRD's "Epics 1-5... `done`" matches `sprint-status.yaml`
  (`epic-1`..`epic-5: done`). Post-PRD-date drift: `epic-6: done`, `epic-7: done`,
  `epic-8: in-progress` (see Decision-readiness low finding).
- **ID continuity:** FR-Shell + FR-Admin-1..4 + FR-Consumer-1..4 — unique, no gaps or
  duplicates; the mixed scheme (one unnumbered FR-Shell alongside numbered per-area FRs) is
  internally consistent and matches `epics.md` usage. NFR1–NFR9 contiguous. §Traceability
  Matrix covers all nine FRs and only FRs.
- **UX-DR IDs:** referenced only as ranges in the PRD; absent from all three named UX design
  set files; defined individually in `epics.md`; two range/label count mismatches (DR8–12:
  5 IDs / 7 labels; DR13–16: 4 IDs / 5 labels). Cross-listed as the Downstream usability
  medium finding.
- **Assumptions Index roundtrip:** vacuously consistent — no inline `[ASSUMPTION]` or
  `[NOTE FOR PM]` tags and no index.
- **UJ protagonists:** no UJs exist, so no protagonist check applies — shape-appropriate.
- **Glossary drift:** no glossary to drift against; usage of `party_id`, "Bound Consumers",
  "last-known", and "tombstone" is consistent in case and form across FRs and NFRs.
- **Frontmatter:** `date: 2026-06-27` predates the review date (2026-08-18);
  `status: canonical-requirements-source` is a useful machine anchor and should stay stable.
