# Validation Report — Parties UI PRD

- **PRD:** `_bmad-output/planning-artifacts/parties-ui-prd.md`
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-08-18T19:56:56+02:00
- **Grade:** Fair

## Overall verdict

This PRD does its declared job well: a compact, theater-free consolidation with a real precedence rule ("the source artifact owning the topic wins"), clean FR identification, a complete FR traceability matrix, and unusually honest scope ring-fencing — and its brownfield references check out on disk (all six §Source Artifacts paths exist; the Epics 1–5 `done` claim matches `sprint-status.yaml`). The risk sits on the non-FR half of its own promise: NFR coverage is not extractable from the traceability matrix, and the UX-DR ID scheme does not resolve in the artifact the PRD names as its authority — so readiness tooling gets first-class FRs but second-class NFR/UX-DR coverage.

The adversarial and source-fidelity reviewers materially shift the picture on currency and change control. The document was edited on 2026-07-06 and 2026-07-16 (git-verified) with the frontmatter date never bumped and no version, changelog, or owner; §Current Implementation Evidence is now stale (Epics 6–7 `done`, Epic 8 `in-progress` with stories 8.11–8.13 the PRD does not know exist); and — decisive for planning any fix — the file is **frozen byte-for-byte by an active fitness gate** (`EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement`, baseline 37f4ec8 = current HEAD), so every corrective edit must be batched as a governed non-Epic-8 change that advances the baseline. The adversarial reviewer rated three findings critical (canonical-in-name-only, false date, unrecoverable UX-DRs); this synthesis consolidates them to high — the evidence-based reviewers corroborate their substance at lower severity, and the readiness process's explicit reconciliation step blunts the worst-case reading — but their substance stands unrefuted, and together with the extraction gaps they set the grade at Fair rather than Good.

## Dimension verdicts

- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate (weighted lower for this shape)
- Done-ness clarity — adequate
- Scope honesty — strong
- Downstream usability — adequate
- Shape fit — strong

## Findings by severity

Severities below are the consolidated (synthesis) ratings. Where a reviewer's original rating differed, it is noted. Duplicated findings across reviewers are merged, with corroborating reviewers named.

### Critical (0)

None after consolidation. The adversarial reviewer's three critical-rated findings appear under High with their original rating noted; their full adversarial statements are preserved in `review-adversarial-general.md`.

### High (11)

**[Source-fidelity]** — PRD frozen byte-for-byte by an active fitness gate; any corrective edit fails CI (§ whole document)
`EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement` (lines 153–178) asserts `git diff --name-only 37f4ec8` over the PRD and `epics.md` is empty; `BaselineCommit` 37f4ec8 is the current HEAD. It depends on the PRD's exact bytes — every fix in this report breaks the Epic 8 zero-PRD gate unless the baseline constant is advanced or the change is governed as an explicit non-Epic-8 correction with the test updated in the same change.
Fix: Batch all PRD corrections into one governed docs change that also advances `BaselineCommit`, ideally after Story 8.10 closes; do not hand-edit the PRD in isolation.

**[Adversarial + Source-fidelity]** — Frontmatter date is false and the document mutates without change control (§ frontmatter vs §Current Implementation Evidence) *(adversarial rated critical)*
`git log --follow` confirms material edits on 2026-07-06 (f93534e, added the Post-MVP maintenance block) and 2026-07-16 (df2cc6e, added the Scope invariant) with `date: 2026-06-27` never updated; no version, changelog, owner, or approver. The body cites a 2026-07-06 SCP and Epic 7 completion — facts later than the document's only date. Readiness tooling records this file's currency, so the false marker propagates into coverage snapshots.
Fix: Add `version`, `last_updated` (true last edit: 2026-07-16), and an owner to frontmatter; add a changelog; scope "as of" claims per subsection; bump on every edit — coordinated with the freeze fix.

**[Rubric + Adversarial]** — NFR (and UX-DR) coverage not extractable from the traceability matrix (§ Traceability Matrix, §Purpose)
The Purpose promises extraction of "FR/NFR coverage", but the matrix maps only the nine FRs; the sole NFR→epic mapping is one prose fragment ("Epic 6... supports NFR9"). NFR1–NFR8 and UX-DR1–16 have no epic, surface, or gate mapping anywhere. A tool extracting NFR coverage will crash, report 0%, or silently skip — all three defeat the document's purpose.
Fix: Add matrix rows for NFR1–9 (and UX-DR1–16, or a "verified by" column naming the test lane/gate per NFR — e.g., NFR1→`ui-a11y` gate, NFR9→CI).

**[Rubric + Adversarial]** — UX-DR IDs unrecoverable from this file, and the declared authority does not define them (§ UX Requirements) *(adversarial rated critical; rubric rated medium)*
Sixteen requirements are compressed into four range bullets with two ID/label count mismatches (DR8–12: 5 IDs glossed by 7 features; DR13–16: 4 IDs by 5). No injective ID-to-requirement mapping exists. The section names "the final UX design set" as authoritative, but `DESIGN.md`, `EXPERIENCE.md`, and `validation-report.md` contain zero occurrences of "UX-DR" — the IDs are defined in `epics.md` (e.g., line 267, "UX-DR1 — AA-safe brand fill"). The PRD points readers and tools at a source that does not contain the IDs it cites.
Fix: Enumerate all 16 UX-DRs as individual one-line requirements, and cite `epics.md` as their defining source (or move the definitions into the design set and re-point).

**[Source-fidelity + Rubric]** — §Current Implementation Evidence is stale against today's sprint status (§ Current Implementation Evidence)
`sprint-status.yaml` (2026-08-18) shows `epic-6: done`, `epic-7: done`, `epic-8: in-progress` with 8.1–8.6 and 8.11–8.13 `done`, 8.7/8.8 `blocked`, 8.9 `backlog`, 8.10 `review`. The PRD describes Epic 8 only as "approved" backlog scope and knows nothing of stories 8.11–8.13 (added by the 2026-07-07/07-08 correct-course SCPs). A reader treating the section as evidence would conclude Epic 8 is un-started.
Fix: Refresh with a current as-of date, Epic 6/7 done status, and Epic 8 story-level state — coordinated with the baseline-freeze fix.

**[Adversarial]** — Canonical in name, subordinate in fact (§ Purpose vs §Source Artifacts) *(adversarial rated critical)*
The PRD declares itself "the canonical ... requirements source", then rules that on any conflict "the source artifact owning the topic wins" — three domains that partition essentially everything the PRD contains. None of the six sources is pinned to a revision or hash, so a reader of this file alone cannot detect silent divergence; §Current Implementation Evidence itself instructs readiness validation to reconcile elsewhere. Consolidated to high because today's sources verify accurate and the reconciliation step is real — but the authority model is genuinely unfalsifiable as written.
Fix: Either make it genuinely canonical (changes flow in via change control; this file wins), or rename it a "consolidated requirements index" and pin source revisions (commit SHA or content hash per source) in this file.

**[Adversarial]** — Untestable normative language throughout FRs/NFRs (§ FR-Consumer-3/4, NFR1, NFR4, NFR6, NFR7, FR-Admin-1/4)
"Honestly", "plain", "usable target sizes" (WCAG 2.2 supplies the number — 24×24 CSS px, SC 2.5.8 — which the PRD declines to state), "communicated as a temporary state", "stale/degraded read handling" (handling defined nowhere), "bounded" (twice, no bound — also flagged by the rubric as its Done-ness medium), "the agreed domain deltas" (agreed where, recorded in what artifact). Each reduces coverage checking to keyword presence.
Fix: Replace every adverbial quality with an observable criterion; name the artifact that records the agreed deltas.

**[Adversarial]** — No acceptance criteria, no NFR thresholds, whole NFR categories missing (§ Functional Requirements, §Non-Functional Requirements)
Every FR is capability prose without acceptance criteria; every NFR is unquantified; no performance, latency, availability, capacity, concurrency, or browser-support NFR exists at all; NFR2 sets no bound on how stale data may be before the UI must say so. Moderated only by the fact that Epics 1–5 story records carry the acceptance evidence (per the precedence rule).
Fix: Attach 2–5 verifiable acceptance criteria per FR, quantify each NFR, and explicitly declare waived NFR categories so absence is a decision, not a hole.

**[Adversarial]** — Three incompatible requirement ID grammars break mechanical extraction (§ FR-Shell, §NFR1, §UX Requirements)
`FR-Shell` (no number), `FR-Admin-N`/`FR-Consumer-N`, `NFR1` (no hyphen), `UX-DRn`. A regex like `FR-[A-Za-z]+-\d+` misses FR-Shell; `NFR-\d+` misses every NFR. The document never declares which ID classes count as "functional requirements" for the Epic 7/8 scope invariant — a tool must guess whether UX-DRs participate.
Fix: Normalize IDs, state the ID scheme in a conventions note, and declare which classes participate in functional-coverage counting.

**[Adversarial]** — Role model incoherent: DPO exists in FR-Admin-4 but not in the shell (§ FR-Shell vs §FR-Admin-4)
FR-Shell routes exactly three roles; FR-Admin-4 then grants erasure and Art.30 powers to "DPO/Admin users" — a role the shell never routes, gates, or lands. Multi-role (Admin+Consumer) and no-role principals are also unspecified beyond the Consumer-specific NoPartyBinding state.
Fix: Add a complete role/landing/navigation table covering Admin, TenantOwner, DPO, Consumer, multi-role, and no-role principals.

**[Adversarial + Source-fidelity]** — Compliance-blocking KMS prerequisite buried in "Out of MVP Scope", and its documented home was retired (§ Out of MVP Scope)
The production-KMS gate is a legal go-live prerequisite for the product's core GDPR purpose, yet it is phrased as a non-requirement in the section coverage tooling ignores; no FR, NFR, or gate owns it. Compounding it: `docs/deployment-security-checklist.md` — the checklist that owned this gate — was deleted by Story 8.13 (commit 30c5fd9), and `architecture.md:41` (a source artifact that "wins" per the PRD's own conflict rule) plus `project-context.md:257` still point at the dead path. The claim itself remains documented in `docs/index.md:86` and `docs/getting-started.md:446`.
Fix: Promote the KMS gate to an NFR or explicit deployment-gate requirement with an owner and verification method; repoint `architecture.md:41` and `project-context.md` to the surviving KMS documentation.

### Medium (6)

**[Rubric]** — "Bounded" without a bound (§ FR-Admin-4, §FR-Consumer-4)
"A bounded verification report" and "bounded audit metadata" state a bound exists but name neither the bound nor its owning record; Stories 3.5/3.6 own the evidence but the FRs don't cite them. (Overlaps the adversarial untestable-language finding; kept separately because the fix is narrower.)
Fix: State the bound in one clause per FR, or cite the owning story/architecture record by name.

**[Adversarial]** — NFR3 asserts an enforcement the out-of-scope section defers (§ NFR3 vs §Out of MVP Scope)
NFR3's "Parties-side defense-in-depth asserts `aggregateId == party_id`" sits against "Gateway-level data-subject/self principal support remains a future enhancement" with no statement of which layer enforces own-data-only today versus later.
Fix: State which assertion exists now, at which seam, and what the deferred gateway enhancement adds.

**[Adversarial]** — Scope invariant excludes Epic 6 and strains against its own bullets (§ Current Implementation Evidence)
The bolded invariant binds only Epics 7–8; Epic 6's identical claim is an ordinary bullet. Epic 7 is "completed partial platform-alignment scope" inside an invariant that says "maintenance scope only"; "partial platform-alignment" is defined nowhere.
Fix: One invariant covering Epics 6–8 uniformly; drop or define "partial platform-alignment".

**[Adversarial]** — Unresolvable references inside the evidence and NFRs (§ Current Implementation Evidence, §NFR7, §FR-Admin-4)
`D7` (an epics.md decision ID) is undefined in this file; "the agreed domain deltas" names no recording artifact; "existing typed client/gateway seams" names no seams.
Fix: Define or link every imported identifier at first use.

**[Adversarial]** — FR-Consumer-4's cancellation window is undefined — by the PRD's own honesty standard (§ FR-Consumer-4)
"Cancel erasure while cancellation is still allowed" defines no boundary event, while the same section forbids copy making timing promises the system cannot guarantee.
Fix: Define the cancellation boundary as an event (e.g., "until the erasure obligation transitions to started").

**[Adversarial + Source-fidelity]** — No readiness contract, and "Epic 7 preserves rollback paths" is superseded (§ Purpose; §Current Implementation Evidence, Epic 7 bullet)
The document never states what constitutes coverage or which requirement classes gate readiness — different tools compute different verdicts. And the Epic 7 bullet's "preserves rollback paths" now misleads: the projection-rollback retention item was closed 2026-08-01 under Story 8.6 (rollback-only actor/rebuild/adapter paths removed after governed parity evidence); only the crypto/key-management retention (Story 8.7 gate) remains open.
Fix: Add a short "readiness contract" section; qualify the Epic 7 bullet with the 2026-08-01 projection-path closure and the surviving crypto retention.

### Low (5)

**[Rubric]** — Evidence snapshot ages without a refresh contract (§ Current Implementation Evidence) — subsumed by the High stale-evidence finding; the residual point is the missing refresh *contract*. Fix: re-stamp the snapshot date on each readiness run.

**[Rubric]** — Validation rules unlocated (§ FR-Admin-3, §FR-Consumer-2) — "validated forms" doesn't say which artifact owns the validation contract. Fix: name the owning artifact.

**[Rubric]** — Staleness trigger undefined (§ NFR2, §FR-Admin-1) — behavior once stale is testable, but the trigger lives elsewhere unnamed. Fix: cite the architecture's freshness/degraded-read contract.

**[Rubric]** — No Glossary (whole document) — pattern vocabulary (tombstone, optimistic echo, bound Consumer, tenant warm-up) is consistent but undefined in-doc. Fix: optional ten-line glossary.

**[Adversarial + Source-fidelity]** — NFR9 is a process gate, not a product NFR; frontmatter `status` is a role claim, not a lifecycle state; NFR8's "ServiceDefaults" wording predates the Story 8.4 Commons cutover (still literally true via `Hexalith.Commons.ServiceDefaults` in `src/Hexalith.Parties.UI/Program.cs:1,33,226`). Fix: label NFR9's class explicitly; split `status` into lifecycle + `role`; reword NFR8 to "Hexalith.Commons.ServiceDefaults" when the PRD is next unfrozen.

## Verified accurate (source-fidelity)

Eleven checked claims held up, including: all six §Source Artifacts paths exist; the full FR→Epic traceability matrix matches `epics.md` exactly; all nine matrix routes exist as real `@page` directives in AdminPortal/ConsumerPortal; "Epics 1–5 done" and "Epic 7 completed" match `sprint-status.yaml`; all five dependency-evidence stories (1.4, 3.5, 3.6, 4.1, 4.2) are `done`; all NFR9 build-gate claims hold (net10.0, warnings-as-errors, CPM, `.slnx`, xUnit v3, Playwright a11y, root-level submodules); the temporal-query deferral is real (no `get_party_name_at` MCP tool); readiness reports genuinely select this file as the PRD; no consumer parses section names or FR-ID strings programmatically — the only code-level dependency is the byte-for-byte freeze.

## Mechanical notes

- All six §Source Artifacts paths verified present on disk; `sprint-change-proposal-2026-07-06.md` resolves.
- Sprint-status roundtrip: "Epics 1-5... done" matches; post-PRD-date drift: `epic-6: done`, `epic-7: done`, `epic-8: in-progress`.
- ID continuity: FR-Shell + FR-Admin-1..4 + FR-Consumer-1..4 unique, no gaps; NFR1–NFR9 contiguous; matrix covers all nine FRs and only FRs.
- UX-DR IDs: ranges only; absent from all three named UX design set files; defined individually in `epics.md`; two range/label count mismatches (DR8–12: 5 IDs / 7 labels; DR13–16: 4 IDs / 5 labels).
- Assumptions Index roundtrip: vacuously consistent (no tags, no index). UJ protagonists: no UJs — shape-appropriate.
- Glossary drift: none to drift against; `party_id`, "Bound Consumers", "last-known", "tombstone" used consistently.
- Frontmatter: `date: 2026-06-27` predates the review date; `status: canonical-requirements-source` is a useful machine anchor and should stay stable.

## Reviewer files

- `review-rubric.md`
- `review-adversarial-general.md`
- `review-source-fidelity.md`
