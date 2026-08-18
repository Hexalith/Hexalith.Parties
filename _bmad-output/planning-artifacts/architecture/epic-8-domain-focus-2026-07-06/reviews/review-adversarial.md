---
title: Adversarial Reviewer Gate — Epic 8 Architecture Spine
target: _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
lens: adversarial (two-units-one-level-down incompatibility construction)
reviewer: BMad Reviewer Gate — adversarial
date: 2026-08-18
mode: VALIDATE-only (no spine or project file modified)
verdict: CONDITIONAL — the spine is sound for the work already landed, but its
  invariants were written for a world where 8.6–8.10 execute as sequenced spec
  files; the 8.10 deferral-based closure moved the remaining work into ledger
  entries that the letter of the spine no longer binds, and identity/parity
  coupling across those executors is the largest unclosed hole.
findings: 11 (1 critical, 3 high, 5 medium, 2 low)
---

# Adversarial Review — Epic 8 Architecture Spine

Attack method: for each hole, construct two future units of work "one level
down" (executors of the accepted Epic 8 closure deferrals `8.6-residual-review-debt`,
`8.7-data-protection-extraction`, `8.8-runtime-boundary-cleanup`,
`8.9-frontcomposer-ui-consolidation`, `external-runtime-deployment`, or ledger
items swept by `bmad-loop-sweep`) that each obey every invariant I1–I15 and
every §4 gate clause **to the letter**, yet produce incompatible results.
Findings are ranked by realistic likelihood; each closes with tightened or new
invariant wording.

Evidence base: the spine itself (§2–§7), `deferred-work.md` (Story 8.10
accepted-deferral block + open ledger), `story-8-3-platform-api-prerequisite-matrix.md`
(2026-08-18 retained-identity reconciliation + G-rows),
`spec-8-10-final-readiness-documentation-and-retirement-gate.md`,
`epic-8-context.md`, the Epic 7 parent spine, and
`tests/Hexalith.Parties.Tests/FitnessTests/{EpicEightClosureFitnessTests,DocumentationFitnessTests,PlatformApiPrerequisitesTests}.cs`.

---

## ADV-1 (CRITICAL) — One retained dependency identity, N per-story identity receipts: a later identity bump silently invalidates the parity evidence that authorized an earlier deletion (I3 × I4)

**The shared entity with two owners:** the root `references/Hexalith.EventStore`
gitlink and the single `HexalithEventStoreVersion` in the imported Builds
catalog (`Directory.Packages.props` → `references/Hexalith.Builds/Props/Directory.Packages.props`).
The tree can hold exactly **one** of each at a time. I4 and §4 clause 1,
however, are written per-prerequisite/per-story: each story records "the exact
released package version or root-declared submodule gitlink **selected by the
consumer**."

**Unit A** — the `8.7-data-protection-extraction` executor. It delivers G5 at
(say) EventStore `3.97.0`, records that exact identity in its matrix row
(satisfying I4 verbatim), passes the full compatibility packet with exercised
switch-back (satisfying its deferral exit proof and I3's "parity evidence and
proven rollback"), and deletes the 18 MOVE files in `Hexalith.Parties.Security`.
Every invariant satisfied to the letter.

**Unit B** — the `8.8-runtime-boundary-cleanup` executor, working
concurrently or immediately after. Its G1/G2/G6 adoption needs EventStore
`3.98.0`. The matrix explicitly instructs it: "If Story 8.8 selects another
package or gitlink identity, it must refresh this row before consumption."
Unit B bumps the Builds catalog and gitlink, refreshes **its own** rows,
records its exact identity — I4 satisfied verbatim.

**The incompatibility:** Unit A's parity evidence — the evidence that
authorized an irreversible deletion — was produced at `3.97.0`. After Unit B's
bump the tree consumes `3.98.0`, at which Unit A's parity was never run. The
rollback path is already gone. Nothing in I3, I4, or §4 requires Unit B to
re-run Unit A's parity suites, or blocks Unit A's deletion from surviving an
identity move. This is not hypothetical: the 8.3 matrix already documents the
exact failure shape twice — "Story 8.6's regression evidence is not yet
re-confirmed at that later pin; re-run the focused projection/query suite
before treating `4bcf2484` as validated," and again at the `1d6e9321` refresh.
The spine ratified those notes as history but extracted no invariant from them.

**Severity:** critical. Highest-likelihood hole in the set: five deferral
executors share one gitlink/catalog, the matrix already shows three identity
moves in five weeks, and the consequence (deletion authorized by invalidated
evidence) is precisely what I3 exists to prevent.

**Close with (tighten I3 + I4, new I16):**

> **I16 (identity-stamped parity).** Parity evidence is valid only at the exact
> package version or gitlink SHA it was produced against, and must record that
> identity. Any change to a retained dependency identity (Builds catalog value,
> root gitlink, or package pin) **re-opens every parity claim recorded at a
> different identity of the same dependency**: the changing story must either
> re-run each affected named test surface at the new identity and append the
> receipt, or record the affected claim as unvalidated in the 8.3 matrix before
> merging. The Story 8.10 retained-identity table in the 8.3 matrix is the
> single writable ledger of current identities; per-story rows are historical
> evidence only. No deletion authorized by parity evidence may merge into a
> tree whose retained identity differs from the evidence's stamp.

---

## ADV-2 (HIGH) — The §4 readiness gate binds "spec files 8.6–8.10"; the closure converted the remaining work into ledger deferrals that no clause of §4 textually binds

**The letter:** §4 opens "Each `spec-8-x` (8.6–8.10) is **not** ready for a dev
session until its spec file declares all six, in the spec itself." §7 then
dispositions 8.7/8.8/8.9 as **deferred** into `deferred-work.md` entries whose
schema (enforced by `EpicEightClosureFitnessTests.DescribeDeferralGaps`) has
exactly four fields: owner, exit_proof, rollback, evidence. Missing from that
schema: §4 clauses 1 (prerequisite identity match), 2 (touched repos),
4 (validation lanes), 5 (non-goals), 6 (parity checklist).

**Unit A** — a future dev session invoked as `spec-8-9`: authors a full
six-clause spec, declares non-goals ("skip links stay local; typed-name
confirmation slice is out of scope this pass"), and works the G4 picker slice.

**Unit B** — a `bmad-loop-sweep` bundle executor picking up
`8.9-frontcomposer-ui-consolidation` (or one of the ~40 open un-ID'd 8.6
ledger items) directly from `deferred-work.md`. The entry's exit_proof names
the full G4 set including skip links and typed-name confirmation. Unit B, to
the letter of the accepted deferral, deletes the Parties-local skip links after
producer bUnit parity — never authoring a §4 spec, because no text makes §4
apply to a ledger-driven session. It has obeyed the deferral entry (the only
contract §7 gives it) completely.

**The incompatibility:** Unit A's spec's non-goals ("must not be deleted yet")
and Unit B's deletion are direct contradictions over the same UI surfaces, and
neither violated any written rule — §4 governs only "spec files," and the
deferral entries carry no non-goal or touched-repo clause. This project
demonstrably runs sweep executors over this exact ledger (the ledger's own
format and `bmad-loop-sweep` skill exist for it), so the pair is realistic.

**Severity:** high.

**Close with (new I17):**

> **I17 (deferral executors inherit the gate).** An accepted Epic 8 closure
> deferral may be worked only through a spec file that declares all six §4
> clauses; picking a deferral (or any Epic 8 ledger item) into a dev or sweep
> session without such a spec is prohibited. On activation, the deferral entry
> must be annotated with the spec's path, making concurrent activation of the
> same deferral visible. The four deferral fields are the deferral's contract
> for *waiting*; the six §4 clauses are the contract for *working*.

---

## ADV-3 (HIGH) — Two owners of one ACL, and an AppHost whose retirement one deferral requires and another forbids (I1 / I1a transition window)

**The shared entities:** the deny-default ACL
(`src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`,
whose route list I1 freezes verbatim) and the Parties AppHost itself.

**Unit A** — the `8.8-runtime-boundary-cleanup` executor. I1a: the domain
AppHost "is retired only after topology, security, publish, and rollback
parity are proven." Unit A proves all four against FrontComposer.AppHost (G8
package C), then retires the Parties AppHost — I1a satisfied to the letter.
The canonical ACL now lives in the FrontComposer repository.

**Unit B** — the `8.6-residual-review-debt` executor. Its accepted exit proof:
"enforce the deny-default EventStore-only DAPR ACL **in a runnable topology**
before removing retained host or ACL rollback seams"; its rollback clause:
"retain … the Parties AppHost and gateway topology as switch-back and
diagnostic surfaces, and do not delete further host … or ACL compatibility
seams until the corresponding deferred proof passes." Unit B therefore blocks
the AppHost's deletion until *its* runtime-ACL proof lands — which today can
only run on the Parties AppHost topology.

**The incompatibility (three-way):**
1. Two accepted deferrals give the same artifact opposite lifecycles with no
   stated priority: 8.8's parity proof authorizes retirement the moment it
   exists; 8.6-residual's rollback clause forbids it until a different proof
   exists. Both executors are letter-compliant.
2. After retirement, I1's frozen route list binds a file in a repository the
   spine does not govern, while `DocumentationFitnessTests` (line 119) still
   reads the Parties-local copy — the fitness gate verifies a non-authoritative
   artifact, and the authoritative copy can gain an allowed app-id or verb
   without any Parties invariant firing. Two copies of one ACL: the classic
   two-owners hole.
3. I1 hard-codes thirteen routes with no change protocol. When the EventStore
   SDK adds `/project/rebuild/verify/v2`, an executor must either violate I1's
   enumerated letter (adding a route) or break function (omitting it) — and
   the open ledger item "independent string assertions can pass when
   `/admin/operational-index-metadata` is placed under the wrong app policy,
   verb, or action" shows the current enforcement is spoofable route-by-route.

**Severity:** high (the I1a window is exactly where 8.8's work happens, and the
fitness-vs-authority split is mechanical, not speculative).

**Close with (tighten I1/I1a):**

> **I1 (amended, last sentence).** The ACL's authoritative copy has exactly one
> named owner file at any time; the Parties fitness gate must assert against
> the authoritative copy (directly or via a recorded identity of the owning
> repo), and each allowed route must be asserted as the (app-id, verb, policy,
> action) tuple, not by independent string presence. Route-list changes require
> a recorded owner approval referencing this invariant; the list here is the
> baseline, not a hand-editable ceiling.
>
> **I1a (amended).** Retirement of the domain AppHost additionally requires
> that every accepted deferral whose exit proof or rollback names the Parties
> AppHost (currently `8.6-residual-review-debt`) has either passed that proof
> or been re-approved against the successor topology. Deferral rollback
> clauses take precedence over parity-based retirement until then.

---

## ADV-4 (HIGH) — "Parity evidence" has no defined baseline, and the letter permits deleting the baseline tests together with the code they guard (§4 clauses 4/6, I3)

**The gameable clause:** §4 clause 4 demands "the **parity evidence** required
before any deletion" and clause 6 an "I5–I10/I8 checklist" — but neither
defines what parity is measured *against*. Not the baseline suite, not who may
shrink it, not whether the evidence survives later slices.

**Unit A** — an executor that deletes a local implementation **and its
local-only test surface in the same change**, then presents the remaining
(green, smaller) suite as parity evidence. Letter-compliant: every named lane
passes; nothing says the deleted tests were part of the baseline. This is not
contrived — it already happened once inside Epic 8: Story 8.6 deleted
`TenantSafeProjectionReadGuardrailsTests`, and the ledger now records "party-id
length/allowlist validation … weakly gated" as open debt. The spine ratified
8.6 without extracting a rule from that incident.

**Unit B** — a later `8.9` slice executor reading the current suite as the
behavioral contract. Since the guardrail tests no longer exist, Unit B builds
the shared FrontComposer primitive without those behaviors — letter-compliant
under I5/I6/I13 (all *remaining* named tests pass) — and the two units have now
jointly laundered a behavior out of the system with parity evidence at every
step.

**Severity:** high (one occurrence already on the record; every remaining
deferral is deletion-shaped).

**Close with (new I18):**

> **I18 (parity baseline).** The parity baseline for each invariant is the set
> of named test surfaces in the §7 invariant map as of the Epic 8 closure
> commit. Parity evidence must enumerate the baseline surfaces it discharges
> and show each green against the replacement — or name an approved successor
> test accepted by the test-architect owner in the same spec. A change that
> deletes or weakens a baseline test surface in the same changeset that
> deletes the implementation it guards is invalid as parity evidence.
> Evidence in hand at slice N does not certify slices > N; each deletion
> re-states which baseline surfaces it relies on and their last-run identity
> (see I16).

---

## ADV-5 (MEDIUM) — Two authoritative documents assign paging/freshness shapes to two different owners (§2 "owning modules" left unnamed)

**The letter:** §2 MOVES row: "Command envelopes, paging/freshness models, MCP
plumbing → **owning modules**" — owner unnamed. §1 declares the Epic 7 spine
part of the "authoritative spine artifact set (read together)." Epic 7 AD-4/B10
say: "Commons for **pure paging** and string helpers … B10 paging → Commons
generic paging result plus Parties compatibility adapter." The 8.3 matrix G6
row says: "EventStore client envelopes/**freshness**/error codes … Parties
`Current, Stale, Rebuilding, Degraded, Unavailable, LocalOnly` freshness
semantics" are EventStore-owned surfaces (`QueryResponseMetadata` in
EventStore.Contracts).

**Unit A** — an `8.8` executor moves the Parties paging result to
`Hexalith.Commons` citing Epic 7 AD-4/B10 verbatim (an authoritative spine
artifact).

**Unit B** — a different `8.8`/`8.6-residual` slice adopts the future G6
EventStore envelope, in which paging metadata and freshness ride the same
`QueryResponseMetadata` shape, citing the G6 matrix row verbatim.

**The incompatibility:** one wire shape (paged, freshness-stamped query
response — the thing `ProjectionFreshnessMetadata` and the cursor ride on)
split across two packages with an unresolved dependency direction: does
EventStore.Contracts reference Commons paging, or does Parties adapt between
two half-shapes forever? Both units obeyed an authoritative document; the two
documents disagree and the Epic 8 spine, which incorporates both, never
arbitrates.

**Severity:** medium (needs the G6 API to actually land, but both citations
already exist in ratified text today).

**Close with (tighten §2):** name the owner per shape in the MOVES table —
e.g. "paging primitives → Commons (per Epic 7 AD-4); query envelope +
freshness metadata → EventStore.Contracts, referencing Commons paging; command
envelopes → EventStore.Contracts; MCP plumbing → FrontComposer.Mcp with
Commons.Http mechanics (per G11 routing)" — and add: "where this table and the
Epic 7 spine disagree, this table wins."

---

## ADV-6 (MEDIUM) — §5's strict sequence no longer binds anyone: deferral executors can interleave 8.9-before-8.7 and mutually invalidate parity

**The letter:** §5 pins `8.6 → 8.7 → 8.8 → 8.9 → 8.10` for **stories**. 8.10 is
done; 8.7/8.8/8.9 are now *deferrals* whose entries say "revert each future
adoption slice independently" and impose no cross-deferral order. No text says
the §5 order binds ledger executors.

**Unit A** — the `8.9-frontcomposer-ui-consolidation` executor starts first
(its owner set is ready), lands the G4 shared safe-download and GDPR-copy
primitives, and proves parity for Art.20 export downloads against the
**current local** payload-protection provider — exactly what its exit proof
demands ("pass producer bUnit plus Parties bUnit/Playwright parity … GDPR
copy").

**Unit B** — the `8.7-data-protection-extraction` executor then swaps the
protection engine (G5, `pdenc-v2`, new export/certificate producer per its
exit proof: "protected, redacted, legacy, typed-unreadable, no-leak, Art.20,
Art.30, erasure certificate/report"). Unit A's export-download parity was
proven against the provider Unit B just replaced; the shared primitive's
contract (payload framing, filename/content-type, failure surfaces for
typed-unreadable) may no longer match. Run the two in the §5 order and the
problem cannot occur; run them in the order the deferral letter permits and
each unit is individually compliant while their composition is unverified.

**Severity:** medium (both deferrals are real queued work; the coupling
surface — Art.20 exports — is named in both exit proofs, so collision is
plausible rather than contrived).

**Close with (tighten §5):**

> §5 (amended): the `8.6 → 8.7 → 8.8 → 8.9` order binds the accepted closure
> deferrals exactly as it bound the stories, unless a sprint-change proposal
> explicitly re-orders them and records which downstream parity claims the
> re-ordering re-opens (per I16). Two deferrals may execute concurrently only
> if their specs' touched-repo and parity-checklist clauses (§4.2, §4.6) are
> disjoint.

---

## ADV-7 (MEDIUM) — I10 pins the *presence* of freshness metadata, not its grammar: two already-documented `ProjectionVersion` vocabularies can each satisfy I10 while a shared consumer can parse only one

**The letter:** I10 requires "`ProjectionFreshnessMetadata` on every read" and
names target abstractions — nothing about the semantic content. The ledger
already records the divergence seeds: "Index `ProjectionVersion` scheme
(`global:N` / `{id}:{seq}` / keep-current) lacks Fold/class remarks for
freshness/query consumers," and "Erasure copies through pre-erasure
`ProjectedAt`/`ProjectionVersion` … entangled with the open AC7
freshness-mapping gap."

**Unit A** — the `8.6-residual` executor closes AC7 locally: stamps
erasure-time freshness, documents `global:N` as the index grammar. I10
satisfied verbatim.

**Unit B** — the `8.9` executor adopts G4 package B, the "UI-normalized
per-record freshness indicator," built by FrontComposer against the G6 wire
semantics ("without redefining EventStore/G6 wire semantics" — i.e., the
EventStore grammar, not Parties' local one), expecting `{id}:{seq}` per-record
versions and last-fold-time `ProjectedAt`. I10 satisfied verbatim — metadata
present on every read.

**The incompatibility:** the shared indicator renders wrong or blank staleness
for index-backed rows (a `global:N` token where it expects `{id}:{seq}`), and
erased rows show pre-erasure timestamps in one producer and erasure-time in
the other — same read model, two freshness dialects, both invariant-compliant.

**Severity:** medium.

**Close with (tighten I10):** add — "The freshness contract is semantic, not
just structural: the state vocabulary (`Current/Stale/Rebuilding/Degraded/
Unavailable/LocalOnly`), the `ProjectionVersion` grammar per read model, and
the erasure-time stamping rule are one owned, versioned shape (owner:
EventStore G6 envelope once delivered; until then, a single documented
Parties definition that all local emitters must match). A UI consumer may
bind only to that named shape version."

---

## ADV-8 (MEDIUM) — I9's replay-from-zero vs the Memories cleanup ledger: one persisted entity, two legal classifications with incompatible rebuild semantics

**The letter:** I9: "Replay-from-zero on every delivery; per-read-model
sequence checkpoints." I10: "A full rebuild is executed and verified against
aggregate replay before local code deletion." Meanwhile an open ledger item
demands: "Move the Memories cleanup mapping ledger behind an approved
EventStore persistence abstraction — `PartyMemoryUnitMappingStore` persists …
outside the EventStore read-model and write-policy abstractions **required for
domain-module persistence**."

**Unit A** — a `8.6-residual` executor resolves that item literally: wraps the
mapping ledger in `IReadModelStore`/`ReadModelWritePolicy`. It is now, by
construction, a read model — and read models are subject to I9 replay-from-zero
and I10 rebuild-vs-replay verification. But the ledger records **external side
effects** (Memories units created/deleted in another system); a rebuild from
the event stream cannot reconstruct it and a rebuild-triggered overwrite
destroys cleanup receipts that erasure certification depends on. Unit A is
letter-compliant with the abstraction requirement and has broken erasure
verification.

**Unit B** — a `8.7` executor certifying erasure treats the same store as a
durable operational ledger that must *survive* rebuilds (its exit proof:
"erasure certificate/report … parity"), and asserts rebuild-vs-replay
verification per I10 — which now either wipes Unit A's read-model-ified
ledger or fails verification because the ledger is not derivable from replay.

**The incompatibility:** one persisted entity, two classifications (read model
vs side-effect ledger), each mandated by ratified text, with opposite rebuild
behavior. The spine has no vocabulary for non-replay-derivable operational
state, so both units are "right."

**Severity:** medium.

**Close with (new I19):** "Persisted state is classified as either (a) an
event-derived read model — rebuildable from zero, subject to I9/I10
rebuild-vs-replay verification — or (b) an operational side-effect ledger —
never rebuilt from replay, excluded from rebuild-vs-replay verification,
retained across rebuilds, and covered by its own consistency tests. Every
store names its class; the Memories mapping ledger is class (b). Moving a
store between classes is a spec-level decision with its own parity evidence."

---

## ADV-9 (MEDIUM) — §4 clause 3 accepts a *described* rollback; slice interleaving silently expires an untested revert path

**The letter:** clause 3 requires the spec to declare "which local code stays
until parity, and **how to revert**" — a description, not a rehearsal. The 8.7
deferral demands "**exercised** switch-back parity," but the 8.9 deferral's
exit proof requires only bUnit/Playwright parity, with rollback = "revert a
failed slice independently."

**Unit A** — a `8.9` executor lands slice 1 (shared picker), declaring rollback
"git-revert the slice" — clause 3 satisfied verbatim, never executed.

**Unit B** — the same deferral's slice 2 executor (typed-name confirmation)
lands changes that overlap slice 1's files and retarget shared bUnit fixtures
to the FrontComposer picker markup. When slice 1's shared picker later fails
in production, the declared revert no longer applies cleanly and the retained
local picker's tests no longer compile against the moved fixtures. Both units
obeyed clause 3 and the deferral's "revert each slice independently" — which
was true when written and false after composition.

**Severity:** medium.

**Close with (tighten §4.3):** "Rollback path — which local code stays until
parity, how to revert, **and evidence the revert was exercised at least once
(build + named baseline tests green on the reverted tree)**. A later slice
touching a prior slice's files must re-validate (or explicitly re-declare)
every still-live rollback path it disturbs."

---

## ADV-10 (LOW) — The closure gate's own pins are editable only by the actor they gate: no amendment protocol for §7 or the fitness pins

`EpicEightClosureFitnessTests` hard-pins `8-7 = blocked`, `8-8 = blocked`,
`8-9 = backlog` and the five expected deferral IDs; §7's invariant map pins the
evidence classes. When the `8.7` executor legitimately finishes, the gate goes
red and the only path forward is for that same executor to rewrite the gate
(test + map) in the same change that satisfies it — self-authorized gate
modification with no protocol, indistinguishable in mechanism from gaming it.
A second unit (an auditor or 8.10-style closure reviewer) reading §7 as
immutable closure evidence now disagrees with the tree. Also on record: the
open review finding that `EpicEightAddsNoPrdFunctionalRequirement` runs a
fragile `git diff` against a baseline commit unavailable on shallow CI
checkouts — I15's enforcement can pass vacuously exactly where it matters.

**Severity:** low (visible in review; but cheap to close).

**Close with:** add to §7 — "The invariant map and the closure fitness pins
may change only in a changeset that cites the sprint-change proposal or
deferral exit-proof receipt authorizing the transition; the fitness tests must
fail closed (not skip) when their baseline inputs (e.g., the baseline commit)
are unavailable."

---

## ADV-11 (LOW) — Cursor/key-ring continuity across the I1a topology cutover: both hosts use `IQueryCursorCodec` to the letter, neither can read the other's cursors

I10 names `IQueryCursorCodec`; 8.6 adopted the EventStore DataProtection-backed
codec with key-ring persistence in `DaprXmlRepository` against the Parties
AppHost's statestore. **Unit A**: the retained Parties host issues cursors
protected under that key ring. **Unit B**: the `8.8` integrated-topology
executor runs the same service under FrontComposer.AppHost with its own
statestore/key-ring. Cursors (and any DataProtection payloads outside 8.7's
explicitly-covered `pdenc` formats) issued on one side are `InvalidCursor` on
the other — including during rollback, when switching back to the Parties
AppHost invalidates every cursor issued after cutover. Both units satisfied
I10 and I1a's parity list ("topology, security, publish, rollback"), because
key-ring continuity is named in none of them. Impact is bounded (cursors are
short-lived; rejection is typed and logged per the resolved
`LogCursorRejected` item), hence low.

**Close with (tighten I1a):** add "state continuity" to the parity list:
"…retired only after topology, security, publish, rollback, **and persisted
key-ring/state continuity** parity are proven — the successor topology must
read DataProtection key rings (cursor purposes included) persisted by the
predecessor, or the cutover plan must declare the invalidation window and its
user-visible behavior."

---

## Summary table

| # | Severity | Hole (one line) | Close via |
|---|----------|-----------------|-----------|
| ADV-1 | critical | Single Builds-catalog/gitlink vs per-story identity receipts; identity bump silently invalidates parity that authorized deletion | new I16 (identity-stamped parity, single retained-identity ledger) |
| ADV-2 | high | §4 gate binds spec files only; ledger/sweep executors of accepted deferrals inherit no clause of it | new I17 (deferral executors must work through a six-clause spec) |
| ADV-3 | high | AppHost retirement required by 8.8 parity and forbidden by 8.6-residual rollback; ACL forks into two copies with the fitness gate on the wrong one; frozen route list has no change protocol | amend I1 + I1a |
| ADV-4 | high | Parity baseline undefined; deleting baseline tests with the code passed the letter once already (TenantSafeProjectionReadGuardrailsTests) | new I18 (parity baseline = §7 map at closure commit) |
| ADV-5 | medium | Epic 7 spine (Commons owns paging) vs G6 matrix row (EventStore owns envelope/freshness) — both authoritative, §2 leaves "owning modules" unnamed | name owners in §2; precedence rule |
| ADV-6 | medium | §5 order no longer binds deferral executors; 8.9-before-8.7 mutually invalidates Art.20 export parity | amend §5 (order binds deferrals; concurrency needs disjoint §4.2/§4.6) |
| ADV-7 | medium | I10 pins freshness *presence*, not grammar; `global:N` vs `{id}:{seq}` vs G6 wire dialects all comply | amend I10 (semantic freshness contract with named owner) |
| ADV-8 | medium | Memories mapping ledger: read-model classification (per open ledger item) vs rebuild-surviving operational ledger (per erasure certification) — I9 replay-from-zero breaks one of them | new I19 (persistence-class taxonomy) |
| ADV-9 | medium | §4.3 accepts a described, never-exercised rollback; later slices silently expire earlier slices' revert paths | tighten §4.3 (exercised revert; re-validate disturbed paths) |
| ADV-10 | low | Closure fitness pins/§7 map editable only by the actor they gate; I15 diff test can pass vacuously on shallow checkout | §7 amendment protocol; fail-closed fitness |
| ADV-11 | low | Cursor/DataProtection key-ring continuity absent from I1a's parity list; cutover/rollback invalidates live cursors | amend I1a (state continuity in parity list) |

## Overall assessment

The spine is unusually honest about what is deferred and its §7 map is a real
strength — every invariant is tied to a named executable surface or a named
deferral. The structural weakness is that the whole gate system (§4, §5, I3/I4)
was written in the vocabulary of *sequenced spec files*, and the 8.10 closure
legitimately transformed the remaining work into *independent ledger deferrals*
without porting the gate semantics onto them. ADV-1, ADV-2, and ADV-6 are three
faces of that one gap; ADV-3 and ADV-4 are the two places where a single shared
artifact (the ACL/AppHost, the parity baseline suite) already has, or has
already had, two effective owners. Closing I16–I18 plus the I1a amendment would
neutralize every critical/high pair in this review.
