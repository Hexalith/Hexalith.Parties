---
title: Post-Update Adversarial Reviewer Gate — Epic 8 Architecture Spine
target: _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
lens: adversarial (two-units-one-level-down incompatibility construction, re-run after 2026-08-18 amendments)
reviewer: BMad Reviewer Gate — adversarial (post-update pass)
prior-pass: reviews/review-adversarial.md (11 findings; ADV-1..4 + parts of 5/6 addressed by amendment)
date: 2026-08-18
mode: VALIDATE-only (no spine or project file modified)
verdict: CONDITIONAL PASS — the amendments genuinely close the four front doors
  (the original ADV-1..4 unit pairs are no longer constructible as stated); the
  remaining holes are second-order — arbitration, scoping, and decidability
  gaps inside the new I16–I18 text itself — none critical, three worth fixing
  before the first deferral-activation spec is authored.
findings: 8 new (0 critical, 3 high, 3 medium, 2 low) + 5 deferred re-verifications (none worsened)
---

# Post-Update Adversarial Review — Epic 8 Architecture Spine

Attack method unchanged from the prior pass: for each candidate hole, construct
two future units of work one level down — spec-driven sessions under §4/I17 or
ledger-deferral executors of `8.6-residual-review-debt`,
`8.7-data-protection-extraction`, `8.8-runtime-boundary-cleanup`,
`8.9-frontcomposer-ui-consolidation`, `external-runtime-deployment` — that each
obey every invariant **including the new I16–I18 and the amended I1/I1a/§2/§4/§5
to the letter**, yet build incompatibly. Findings ranked by realistic
likelihood. Deferred prior findings (ADV-5 residual, ADV-7, ADV-8, ADV-9,
ADV-11) are re-verified in §Re-verification, not re-reported.

Evidence base for this pass: the amended spine (all sections + §8),
`deferred-work.md` (accepted-deferral block, lines 368–413, and the open
ledger), `story-8-3-platform-api-prerequisite-matrix.md` (Story 8.10
retained-identity reconciliation ledger), the spine folder `.memlog.md`
(deferral dispositions V8/V9/V10/V12/V25), and
`tests/Hexalith.Parties.Tests/FitnessTests/{EpicEightClosureFitnessTests,PlatformApiPrerequisitesTests}.cs`.

## What the amendments actually closed (credited before attacking)

- **ADV-1 front door closed, with mechanical teeth I did not expect:**
  `PlatformApiPrerequisitesTests` (line 600) asserts the **literal**
  `<HexalithEventStoreVersion …>3.95.0</…>` inside the checked-out Builds
  catalog. The "transitive bump via the Builds catalog" escape I probed —
  bump the Builds gitlink, claim only the *Builds* identity changed, never
  mention EventStore — is therefore mechanically visible: the bump goes red
  until the test constant and the 8.3 reconciliation rows are touched. The
  letter of I16 ("Builds catalog value" is itself a named identity form) plus
  this pin means the pure transitive escape fails. What survives at that
  forced touchpoint is Finding P1.
- **ADV-2 closed** by I17's spec-file requirement for accepted deferrals; what
  survives is its scope boundary (P3) and same-deferral concurrency (P6).
- **ADV-3 closed** by I1's single-authoritative-owner-file + tuple assertion +
  baseline-not-ceiling clause, and I1a's deferral-precedence rule; what
  survives is I1a's own enumeration error (P4).
- **ADV-4 closed** for the single-changeset case by I18; what survives is the
  two-changeset split and the approver-less successor clause (P2).
- **ADV-5 arbitration closed**: §2 names owners per shape and adds "where this
  table and the Epic 7 spine disagree, this table wins."
- **ADV-6 closed**: §5 binds the 8.6→8.9 order onto the deferrals; what
  survives is that its concurrency carve-out is vacuous (P7).

---

## P1 (HIGH) — I16's escape hatch is a permanent parking lot: "records the claim as unvalidated" satisfies the letter forever, and "the affected named test surfaces" has no arbiter

**The amended letter (I16):** "…the changing story re-runs the affected named
test surfaces at the new identity **or records the claim as unvalidated in the
8.3 matrix** before merging. No deletion authorized by parity evidence may
merge into a tree whose retained identity differs from the evidence's stamp."

Three gaps compose:

1. The second disjunct is a complete discharge. Recording "unvalidated" and
   merging is letter-compliant, and **nothing anywhere obliges anyone to ever
   re-validate**: no invariant forbids the tree from sitting indefinitely with
   deletions whose authorizing evidence is marked void; no sprint-status or
   fitness pin models "unvalidated" (`EpicEightClosureFitnessTests` checks
   deferral fields and story statuses — 8.6 stays `done` regardless).
2. "The affected named test surfaces" is self-arbitrated by the changing
   story. No rule links "affected" to the I18 baseline or to the identity
   stamps; the bump story may declare "affected: none (build-only change)".
3. Sentence 3 gates only **future deletion merges**. The deletions I16 exists
   to protect merged *in the past*; a later identity bump does not
   retroactively violate sentence 3.

The harm concentrates precisely where CI cannot save you. For claims backed by
always-run suites (`PartySdkProjectionHandlerTests` etc.) the bump's own CI run
*is* an implicit re-validation. But the parity receipts that authorized the
irreversible deletions are largely **one-shot, non-repeatable receipts**: the
I10 rebuild-executed-and-verified-against-replay receipt, 8.5's exercised
switch-back, topology-gated `EventStoreGatewayE2ETests` ("runs fully only with
Docker/DAPR available"), publish parity. A bump story parks exactly those as
"unvalidated" because they are expensive, and CI never implicitly re-runs them.

**Unit A** — a routine `fix: bump submodules` session (the repo's recent
history shows these are frequent) adopts Builds v4.20 for an unrelated
commitlint/props fix. The fitness pin forces it to touch the catalog constant
and the 8.3 reconciliation rows; it dutifully writes "8.5 switch-back receipt
and 8.6 rebuild-vs-replay receipt recorded at EventStore 3.95.0: unvalidated at
3.98.0" and declares no affected test surfaces beyond the build lanes. Every
sentence of I16 satisfied. Merged.

**Unit B** — the `8.6-residual-review-debt` executor months later, through a
fully compliant I17 six-clause spec. Its §4 clause-1 identity check passes
(the *current* matrix identity matches the tree). It removes an ACL/host
compatibility seam with fresh parity evidence at the new identity for the
surfaces it names — while the rebuild-vs-replay and switch-back receipts its
deferral's rollback clause presumes valid have been void for months. The seam
it deletes was the switch-back path those parked receipts were supposed to
guarantee. Both units letter-compliant; the composition deletes a rollback
surface whose validity evidence was parked, not re-proven.

**Minimal wording fix (I16, append):** "An `unvalidated` claim is a blocking
state, not a disposition: while any claim affecting an invariant is
unvalidated, no changeset may delete or weaken a rollback seam, receipt, or
test surface that the unvalidated claim's original evidence covered, and the
deferral owning that surface may not progress past its current status.
'Affected' is decided by the I16 stamps, not by the changing story: every
claim stamped with any identity of a dependency whose catalog value, gitlink,
or pin the change alters is affected."

---

## P2 (HIGH) — I18's prohibition is scoped to "the same changeset", and its successor clause names no approver: weaken-in-slice-N, delete-in-slice-N+1, self-approved successor

**The amended letter (I18):** "…(or name an approved successor test in the
same spec). Deleting or weakening a baseline surface **in the same changeset**
that deletes the implementation it guards is invalid as parity evidence…"

Two gaps, one pair:

1. The adversarial-review proposal read "successor test **accepted by the
   test-architect owner**"; the adopted text says only "an **approved**
   successor test in the same spec" — approved *by whom* is unstated. The spec
   author approves their own successor. (The memlog defers "decidable approval
   mechanisms" as V12 for I1a/I5/I2 — but I18's approver is new text from this
   amendment, not covered by that deferral.)
2. "In the same changeset" invites the split that sliced execution — the
   *mandated* style for 8.8/8.9 ("revert each future adoption slice
   independently") — makes natural.

**Unit A** — slice 1 of a compliant `8.9-frontcomposer-ui-consolidation` spec:
introduces a FrontComposer-backed successor test, marks it "approved" in the
spec (no approver exists to gainsay it), and deletes/retargets the Parties
bUnit baseline surface. **No implementation is deleted in this changeset**, so
I18's invalidity clause does not fire; I16 stamps fine; all remaining suites
green.

**Unit B** — slice 2 of the *same* spec: deletes the Parties-local picker
implementation, citing the successor test named-and-approved in the same spec.
I18 satisfied verbatim ("name an approved successor test in the same spec" —
it did). The baseline surface died one changeset before the implementation it
guarded, exactly the `TenantSafeProjectionReadGuardrailsTests` shape that
motivated I18 — now with a compliance receipt.

**Minimal wording fix (I18):** replace "in the same changeset that deletes the
implementation it guards" with "at any point between the activation of the
spec that deletes the implementation and that deletion's merge"; and replace
"an approved successor test" with "a successor test approved by the
test-architect owner named in the deferral entry, with the approval recorded
in the spec, and shown green at the baseline identity **before** the baseline
surface is removed."

---

## P3 (HIGH) — I17's scope is label-based, the baseline's guards are surface-based: a non-Epic-8 ledger item can weaken an I18 baseline surface with no gate at all

**The letter (I17):** "an accepted Epic 8 closure deferral **(or any Epic 8
ledger item)** may be worked only through a spec file declaring all six §4
clauses." §4 adds "any later-added **deletion-heavy** Epic 8 spec."

The ledger the sweep executors traverse is not partitioned by epic. Open items
whose `source_spec` is *not* an Epic 8 spec already touch Epic 8's guarded
surfaces today: the item from `spec-align-assistant-commit-message-generation.md`
("Bind the operational-index metadata ACL route to the EventStore policy, POST
verb, and allow action in one focused assertion") edits the exact fitness
surface I1 leans on; the `spec-gh-30708560778` scoring items edit the party
search hot path under I10. And "deletion-heavy" for later SCP additions is an
unarbitrated adjective — a future correct-course story that self-describes as
"hardening" escapes §4 by classification.

**Unit A** — a `bmad-loop-sweep` bundle works the ACL-assertion item. It is
not an Epic 8 ledger item by label, so I17's letter does not apply; it authors
no §4 spec. While "binding the route in one focused assertion" it restructures
`DocumentationFitnessTests`' ACL checks — weakening tuple coverage for the
other twelve routes. **I18 does not fire either**: no implementation is
deleted in this changeset, so weakening the baseline surface is legal.

**Unit B** — the `8.6-residual-review-debt` executor later deletes retained
ACL compatibility seams, enumerating `DocumentationFitnessTests` as the green
I1 baseline surface its parity evidence discharges (I18 satisfied — the
surface exists and is green; nothing says it must still assert what it
asserted at the closure commit *content-wise*, only that the named surface is
enumerated). The deny-default guarantee has been laundered across the scope
boundary: one unit weakened the guard outside the gate, the other deleted the
implementation inside it.

**Minimal wording fix (I17, append):** "The gate attaches to surfaces, not
labels: any change — regardless of the ledger item's epic or a spec's
self-classification — that edits a §7 baseline test surface, the ACL owner
file, a retained rollback seam, or an identity named in the 8.3 reconciliation
ledger is Epic 8 work for the purposes of this invariant and §4.
'Deletion-heavy' is decided by whether the change deletes any implementation
or baseline surface, not by the spec's description."

---

## P4 (MEDIUM) — I1a's "(currently `8.6-residual-review-debt`)" is factually incomplete on the day it was published: two more accepted deferrals name the Parties AppHost in their rollback clauses

**The amended letter (I1a):** "Retirement additionally requires that every
accepted deferral whose exit proof or rollback names the Parties AppHost
(**currently `8.6-residual-review-debt`**) has passed that proof or been
re-approved against the successor topology…"

The dynamic rule is right; the parenthetical is wrong **today**. In
`deferred-work.md`: `8.8-runtime-boundary-cleanup`'s rollback names
"…build selectors, **and Parties AppHost**" (line 396), and
`external-runtime-deployment`'s rollback promises "This repository keeps …
**the local Parties AppHost migration rollback topology**" (line 412). Three
deferrals qualify; the spine enumerates one. No hard deadlock exists — the
"re-approved against the successor topology" valve resolves the 8.6-residual
coupling the prior review raised — but the valve's approver is undefined
(deferred as V12), and the stale enumeration creates the pair:

**Unit A** — the `8.8` executor reads "currently" as the spine's authoritative
determination of which deferrals qualify (that is what "currently" asserts),
satisfies 8.6-residual's runtime-ACL proof on the successor topology, and
retires the AppHost. Letter-compliant on that reading.

**Unit B** — the external platform-operations owner executes runtime rollback
per their *accepted* rollback clause, which states this repository keeps the
local Parties AppHost rollback topology — and it no longer exists. Two
ratified texts, one artifact, opposite lifecycle promises; I1a was supposed to
be the arbiter and its illustration pointed Unit A away from Unit B's clause.

**Minimal wording fix:** delete the parenthetical or make it honest and
self-expiring: "(as of 2026-08-18: `8.6-residual-review-debt`,
`8.8-runtime-boundary-cleanup` itself, and `external-runtime-deployment`;
the ledger text, not this list, is authoritative)". Re-approval requires a
recorded decision by the named owner of the affected deferral entry.

---

## P5 (MEDIUM) — I18's baseline is pinned to "the Epic 8 closure commit", a commit that does not exist and that no document nominates; at every commit that exists today the §7 map is absent

Verified directly: `git show HEAD:…/ARCHITECTURE-SPINE.md` at
`37f4ec8` contains **no** `epic-8-invariant-map` marker and no `| I` rows —
I16–I18 and the whole §7 map live only in the uncommitted working tree. So
"the set of named test surfaces in the §7 map as of the Epic 8 closure commit"
currently denotes either nothing or the empty set, and even once the pending
commit lands, nothing names *which* commit holds the title — the closure will
plausibly land as several commits (gitlink bump, fitness tests, spine
amendment), and the only commit constant pinned anywhere in the closure gate
is `EpicEightClosureFitnessTests.BaselineCommit = 37f4ec8`, which is the PRD
baseline, **predates the amendment, and contains no map at all**.

**Unit A** — a deferral executor resolves "the Epic 8 closure commit" to the
only pinned commit in the gate (`37f4ec8`): empty map, empty baseline, every
parity obligation vacuously dischargeable. **Unit B** — an auditor resolves it
to the future amendment-landing commit and its corrected rows (I11's
`IdentifierValidatorTests` + `PartyAggregateCompositeTests`, I13's shell-slice
wording). Both readings are defensible; their parity obligations differ per
invariant. §8 flags the uncommitted state as the open closure condition, which
mitigates honesty but not decidability.

**Minimal wording fix (I18):** "…as of the Epic 8 closure commit, whose SHA is
recorded under `closure_baseline:` in the Story 8.10 retained-identity
reconciliation ledger of the 8.3 matrix once that commit lands; until it is
recorded, the working-tree §7 map as amended 2026-08-18 governs and no
baseline may be treated as empty." Then record the SHA in the same commit that
lands this amendment.

---

## P6 (MEDIUM) — I17's activation annotation makes concurrent activation of the *same* deferral visible, not prohibited — and the annotation is unenforced, while the one machine-checked field cannot carry it

**The letter:** "on activation, the deferral entry is annotated with that
spec's path **so concurrent activation is visible**." Visibility is the whole
guarantee. §5's disjointness rule governs "two deferrals", not two activations
of one deferral. And enforcement is absent twice over:
`EpicEightClosureFitnessTests.ParseDeferrals` reads only
deferral_id/status/owner/exit_proof/rollback/evidence — an `activated_by:`
line is invisible to it — while `deferral.Status.ShouldBe("accepted")` means
the *only* machine-checked field must **not** change on activation, so the
signal cannot travel through the field a test would catch.

**Unit A / Unit B** — two sessions (this project has same-day overlapping-run
precedent on record) each author a compliant six-clause spec for
`8.9-frontcomposer-ui-consolidation`, each annotates the entry (or neither
does — nothing goes red), and each works a different slice with contradictory
non-goals ("skip links stay local this pass" vs. deleting them after producer
parity). Both are I17-compliant: they annotated; concurrency was visible;
nothing said visible concurrency is forbidden.

**Minimal wording fix (I17):** "…is annotated with that spec's path; **at most
one activation annotation may exist per deferral at a time**, a second
activation requires the first spec to be closed or explicitly superseded in
the annotation, and the closure fitness suite must fail when an accepted
deferral is edited by a changeset that carries no matching activation
annotation."

---

## P7 (LOW) — §5's concurrency carve-out is vacuous by construction: §4 clause 2 puts Parties in *every* touched-repo set, so clause-2 sets can never be disjoint

§4 clause 2 defines the touched set as "**Parties +** each of EventStore /
Commons / FrontComposer / Builds / `deploy` that the change edits" — Parties is
unconditional. §5 permits concurrency "only if their §4 clause-2 touched-repo
and clause-6 parity-checklist sets are disjoint." Two clause-2 sets always
intersect at Parties; the permission can never legally fire. The failure
direction is *safe* (all concurrency forbidden — hence LOW), but it is a
dead-letter rule with two costs: genuinely independent pairs
(`external-runtime-deployment` × a `8.9` UI slice) are forbidden by a rule
that visibly intends to permit them, which pressures executors into the
unwritten "obviously they meant repo-level-excluding-Parties / path-level"
reinterpretation — and the text is silent on repo-vs-path granularity, so
Unit A (repo-level: never concurrent) and Unit B (path-level within Parties:
concurrent when edited paths disjoint) both claim the letter.

**Minimal wording fix (§5):** "…only if, excluding the always-present Parties
root, their clause-2 repo/submodule sets are disjoint **and** their edited
Parties path sets (as declared in clause 2) are disjoint **and** their
clause-6 checklists share no invariant."

---

## P8 (LOW) — I1's new "recorded owner approval" for route-list changes has no record home (same class as deferred V12; noted, not inflated)

The I1 amendment introduces "route-list changes require a recorded owner
approval referencing this invariant" without saying where the record lives
(8.3 matrix? deferral entry? spec? commit message?) or which owner (ACL owner
file's owner? EventStore platform owner?). Two units record approvals in two
different homes; an auditor finds neither; a forged-by-sloppiness approval
("approved per I1" in a commit body) satisfies the letter. The memlog already
defers decidable approval mechanisms (V12) for I1a/I5/I2; this instance is new
I1 text, so it is recorded here as LOW with the same revisit condition:
resolve at first deferral-spec authoring by naming one home — suggested: an
`acl-approvals` block in the 8.3 matrix reconciliation ledger, approver = the
owner listed in the deferral entry whose rollback covers the ACL.

---

## Re-verification of deferred prior findings (not re-reported)

| Prior | Status vs. new text | Note |
|---|---|---|
| ADV-5 residual (paging/freshness owner) | **Improved** | §2 names owners per shape + precedence rule; dependency direction (EventStore.Contracts referencing Commons paging) now stated. Nothing new to attack until G6 lands. |
| ADV-7 (freshness grammar, deferred V8) | **Unchanged** | I10 still pins presence, not grammar. Not worsened; revisit at G6 delivery as logged. |
| ADV-8 (Memories ledger classification, deferred V9) | **Unchanged** | I18's baseline mechanics do not touch persistence classes. I19 candidate stands. Not worsened. |
| ADV-9 (described-not-exercised rollback, deferred V10) | **Unchanged, exposure widened** | The hole is identical, but I17 now routes *all* deferral executors through §4 — more executors rely on the weak clause 3. Honor the V10 revisit trigger ("first deferral-spec authoring") strictly; do not let the first activation spec ship a described-only rollback. |
| ADV-11 (key-ring/cursor continuity, deferred V25) | **Unchanged** | "State continuity" still absent from I1a's parity list; the new deferral-precedence sentence adds a checkpoint where it *can* be raised but no obligation. Not worsened. |
| ADV-10 (§7 amendment protocol) | **Partially mitigated by I18** | Freezing the baseline at the closure commit removes the incentive to game later map edits (they cannot move the baseline). The residual is P5's decidability gap, reported above. |

## Summary table

| # | Severity | Hole (one line) | Fix locus |
|---|----------|-----------------|-----------|
| P1 | HIGH | I16's "record as unvalidated" is a permanent, unmonitored parking lot and "affected surfaces" is self-arbitrated; one-shot receipts (rebuild-vs-replay, exercised switch-back) rot silently | I16: unvalidated = blocking state; affected = stamp-derived |
| P2 | HIGH | I18 prohibits weaken+delete only "in the same changeset" and its successor test is "approved" by no one — slice-split laundering with a compliance receipt | I18: spec-lifetime scope; named approver; successor green at baseline identity |
| P3 | HIGH | I17/§4 scope by label ("Epic 8 ledger item", "deletion-heavy"); non-Epic-8 items already touch I1/I10 baseline surfaces and I18 doesn't fire when no implementation is deleted | I17: gate attaches to surfaces, not labels |
| P4 | MEDIUM | I1a's "currently 8.6-residual" enumeration omits 8.8 and external-runtime-deployment, whose rollback clauses name the Parties AppHost — retirement vs. external rollback promise | I1a: fix/expire the parenthetical; ledger text authoritative |
| P5 | MEDIUM | I18's baseline commit doesn't exist and is nominated nowhere; at every existing commit the §7 map is absent (empty-baseline reading available); the only pinned commit in the gate is the wrong one | I18 + 8.3 ledger `closure_baseline:` SHA |
| P6 | MEDIUM | I17 activation annotation = visibility only; unenforced (fitness parser blind to it, status field must stay "accepted"); concurrent same-deferral activation legal | I17: single-activation rule + fail-closed fitness |
| P7 | LOW | §5 concurrency carve-out vacuous (Parties in every clause-2 set) — safe but dead-letter, and silent on repo-vs-path granularity | §5: granularity-explicit disjointness |
| P8 | LOW | I1 "recorded owner approval" has no record home/approver (same class as deferred V12) | one named home in the 8.3 ledger |

## Overall assessment

The amendment did what it claimed: each of the four prior critical/high unit
pairs now hits a written rule before it composes, and one of them (the
transitive Builds bump) additionally hits a literal fitness pin. The residual
attack surface has moved from *missing rules* to *rules whose scoping words —
"unvalidated", "affected", "same changeset", "approved", "Epic 8 ledger item",
"deletion-heavy", "currently", "the closure commit" — are each one adjective
away from decidable*. P1–P3 should be fixed before the first I17 activation
spec is authored, because that spec is exactly where all three scoping words
get their first adversarial reader; P5 costs one line in the 8.3 ledger at the
moment the closure commit lands and is free to fix now.
