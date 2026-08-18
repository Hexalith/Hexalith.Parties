# Epic 8 Architecture Spine — Validation Report (Reviewer Gate)

- **Target:** `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- **Intent:** Validate (critique only — the spine was not modified)
- **Date:** 2026-08-18
- **Gate composition:** deterministic lint pass + 4 parallel reviewer lenses
  - Lint (`lint_spine.py`): **0 findings**
  - Rubric walker → `reviews/review-rubric-walker.md` — PASS WITH CONDITIONS (0C/1H/4M/3L)
  - Reality check → `reviews/review-reality-check.md` — PASS with findings (0C/1H/2M/3L)
  - Adversarial → `reviews/review-adversarial.md` — CONDITIONAL (1C/3H/5M/2L)
  - Closure-evidence integrity (ad-hoc) → `reviews/review-closure-evidence.md` — honest & load-bearing (0C/0H/3M/5L)
- **Raw findings:** 33 → **26 after cross-lens dedup** (1 critical, 4 high, 13 medium, 8 low)

## Gate verdict

**CONDITIONAL PASS.** The spine is a sound, unusually well machine-enforced reconciliation
contract — every named route, type, version, test class, and deferral ID is real and
reality-checked, §4 demonstrably propagated into specs 8.6–8.10, and no invariant
contradicts the Epic 7 parent — but two structural conditions block treating it as
closure-grade: (1) §7's I4 row claims "Executable" on a Builds identity that exists only in
the uncommitted working tree, violating I4's own consumption-evidence rule; and (2) the
§4/§5 gate system binds *spec files*, while the remaining work now lives in ledger
deferrals whose executors inherit none of its clauses — with identity/parity coupling
across those executors (the one critical finding) left unclosed.

## Outcome — update applied 2026-08-18 (same day)

The findings were rolled into a spine update: **V1, V3, V4, V5** closed by new invariants
I16–I18 and the I1/I1a amendments; **V2** annotated in the §7 I4 row (the commit itself
remains the open closure condition); **V6, V11, V16, V17, V18, V20, V21, V23** fixed in
the §2/§5/§7 sweep; the rest deferred with revisit conditions in the workspace
`.memlog.md`. A post-update gate pass (rubric, reality-check, adversarial) confirmed the
closures, surfaced second-order fixes on the new text (including correcting the §2
supersession to *gated, not executed*), which were applied the same day; final state:
lint 0 findings, `EpicEightClosureFitnessTests` 13/13 green, spine `status: final`.

## What holds (strengths)

- I1's 13-route deny-default ACL list matches `accesscontrol.parties.yaml` verbatim; two fitness suites pin it.
- All 29 named §7 test classes and all 5 deferral IDs exist; the map itself is machine-verified fail-closed by `EpicEightClosureFitnessTests`.
- I2 SDK entry points, all five I10 abstractions, and every I12 build-discipline claim verified against the repo; no decision asserted from training data.
- §4's six clauses are present in all five specs 8.6–8.10; broad stories were split/hard-gated as required.
- No Epic 8 invariant weakens an inherited Epic 7 decision (I3≈AD-1/AD-6, I9/I10≈AD-2, I7/I8≈AD-3, §2≈AD-4, I4≥AD-5).
- The headline "no deferred item is represented as delivered" holds in the forward direction for all five accepted deferrals.

## Critical

### V1 — Identity-stamped parity gap: a later identity bump silently invalidates the parity evidence that authorized an earlier irreversible deletion (I3 × I4) `[ADV-1]`
Five deferral executors share one `references/Hexalith.EventStore` gitlink and one Builds
catalog version, but I4/§4.1 record identity *per story*. Executor A (e.g. 8.7) proves
parity at EventStore 3.97.0, deletes its rollback surface; executor B (8.8) bumps to
3.98.0 and refreshes only its own rows — both letter-compliant, and A's deletion now
stands on evidence never run at the consumed identity. The 8.3 matrix already documents
this failure shape twice ("re-run the focused projection/query suite before treating
`4bcf2484` as validated"). **Close:** new **I16** — parity evidence is valid only at the
identity it was produced against; any retained-identity change re-opens every parity claim
stamped at a different identity; no deletion merges into a tree whose identity differs
from its evidence's stamp.

## High

### V2 — §7 I4 "Executable" rests on an uncommitted Builds gitlink `[RC-F1 · RW-F1 · CE-F5 · RC-F4]` — found independently by three lenses
Spine, 8.3 matrix, and `PlatformApiPrerequisitesTests.cs:18` pin Builds to `17b1c7aa…`,
but `git ls-tree HEAD` records `6b78075…` — only the working-tree checkout matches. A
fresh clone of HEAD fails the fitness test; the matrix's "root gitlink and checkout"
wording is false at the committed state; `sprint-status.yaml:197–201` already admits
closure stays open on exactly this, yet §7 carries no caveat. The §7 evidence also rests
on untracked files (`EpicEightClosureFitnessTests.cs`, `DocumentationFitnessTests.cs`,
`ClosureDeferral.cs`). **Remedy:** commit the submodule bump + pending closure files (or
correct the pins to the committed SHA), and annotate the I4 row until then.

### V3 — The §4 gate is scoped by enumeration: it binds "spec files 8.6–8.10" and nothing else `[ADV-2 · RW-F2]`
Backward: stories 8.11–8.13 (8.13 deletion-heavy) executed entirely outside §4 under SCP
authority, while §5 still claims the sequence "unchanged" and §7 silently consumes their
outputs. Forward: accepted deferral entries carry only 4 fields (owner/exit-proof/
rollback/evidence) — no prerequisites, touched-repos, lanes, non-goals, or parity
checklist — so a `bmad-loop-sweep` ledger executor can delete surfaces a concurrent
spec-driven session declared non-goals, both letter-compliant. **Close:** new **I17**
(deferrals may be worked only through a six-clause §4 spec; activation annotates the
entry) + reword §4 to gate "any deletion-heavy spec in this epic, present or later
added" + a §5 note acknowledging 8.11–8.13.

### V4 — AppHost retirement is required by one deferral and forbidden by another; the ACL forks into two copies with the fitness gate on the wrong one `[ADV-3]`
8.8's parity clause (I1a) authorizes retiring the Parties AppHost the moment parity is
proven; `8.6-residual-review-debt`'s rollback clause forbids deleting host/ACL seams until
its runtime-ACL proof lands — which today can only run on that AppHost. Post-retirement,
I1's frozen route list binds a file in a repo the spine doesn't govern while
`DocumentationFitnessTests` still asserts the Parties-local copy; the 13-route enumeration
has no change protocol; and the ledger already records the route assertions as spoofable
tuple-by-tuple. **Close:** amend I1 (single named authoritative ACL owner; assert
(app-id, verb, policy, action) tuples; owner-approved changes) and I1a (deferral rollback
clauses take precedence over parity-based retirement).

### V5 — "Parity evidence" has no defined baseline; deleting baseline tests with the code they guard has already passed once `[ADV-4]`
§4 clauses 4/6 demand parity evidence but never define what parity is measured against.
Story 8.6 deleted `TenantSafeProjectionReadGuardrailsTests` alongside its code, and the
ledger now carries the lost behavior as open debt — the spine ratified the incident
without extracting a rule. A later executor reading the shrunken suite as the contract
launders the behavior out permanently, parity-green at every step. **Close:** new
**I18** — the parity baseline is the §7 map's named surfaces at the closure commit;
deleting/weakening a baseline surface in the changeset that deletes its implementation is
invalid as parity evidence.

## Medium

- **V6 — Two authoritative documents assign paging/freshness to two owners** `[ADV-5]` — Epic 7 AD-4/B10 say Commons owns paging; the G6 matrix row puts envelope+freshness in EventStore.Contracts; §2 leaves "owning modules" unnamed. Name owners per shape in §2 and add a precedence rule.
- **V7 — §5's order no longer binds anyone** `[ADV-6]` — deferral entries impose no cross-deferral order; running 8.9 before 8.7 proves Art.20 export-download parity against a protection engine 8.7 then replaces. Amend §5: the order binds deferrals; concurrency requires disjoint §4.2/§4.6 clauses.
- **V8 — I10 pins freshness *presence*, not grammar** `[ADV-7]` — `global:N` vs `{id}:{seq}` vs G6 wire dialects all comply; a shared UI indicator renders wrong staleness. Amend I10 with a semantic, owned, versioned freshness contract.
- **V9 — Memories mapping ledger has two mandated classifications** `[ADV-8]` — read model (per open ledger item) vs rebuild-surviving operational ledger (per erasure certification); I9 replay-from-zero breaks one of them. Close with new **I19** persistence-class taxonomy.
- **V10 — §4.3 accepts a described, never-exercised rollback** `[ADV-9]` — later slices silently expire earlier slices' revert paths. Require an exercised revert and re-validation of disturbed rollback paths.
- **V11 — §2 omits two SCP-named extractions; Class A supersession unstated** `[RW-F3]` — tenant-claims transformation (owner decided: EventStore.Authentication) and FrontComposer UI primitives (G4) are missing from the MOVES table; the deliberate supersession of Epic 7's Class A anchor boundary (deleting `Hexalith.Parties.Authentication`) is never stated.
- **V12 — Three invariants lack a decidable approval mechanism** `[RW-F4]` — I1a "explicitly approved" owner, I5 "intentionally versioned", I2 "hooks the SDK cannot own": bind each to a recorded artifact (matrix row / ADR / release plan).
- **V13 — Production key-backend/KMS is silent** `[RW-F5]` — 8.7 moves key management, G5 tracks the backend as undelivered, the readiness report flags KMS before regulated data — yet no spine line owns it; Epic 7 telemetry conventions bind only by artifact-set reference.
- **V14 — OR-shaped gitlink assertion masks HEAD/checkout divergence** `[RC-F2]` — `AssertGitlinkAndCheckout` passes on any of HEAD ∨ index ∨ checkout; the EventStore-specific path already does the strict `ls-tree HEAD` check. I4's "test-pinned identity" claim inherits the weakness.
- **V15 — I13 "purge FAST/v4 tokens" is partially unrealized** `[RC-F3]` — three retained CSS files still consume undefined FAST-convention tokens (`PartiesAdminPortal.razor.css:75` with no fallback); no token-purity guard test exists.
- **V16 — I2's E2E citation is environment-gated and vacuous** `[CE-F1]` — `EventStoreGatewayE2ETests` returns green without Docker/DAPR and seeded no tenants in the recorded closure lane; qualify the citation ("topology-gated") or drop it for the deferral the row already names.
- **V17 — I13/I14 and the 8.9 ledger entry are one remediation stale** `[CE-F2]` — the same-day authorized FrontComposer shell adoption already delivered the skip-links/landmarks slice the ledger still describes as wholly retained/deferred; carve out the adopted slice or the map's honesty claim degrades at the next review.
- **V18 — "Executable"-only rows omit accepted residual debt touching their substance** `[CE-F3]` — I7 foremost (erasure-certificate identity validation, Memories cleanup races), plus I9/I10 quality debts; tag the rows `+ deferred (8.6-residual-review-debt)` or widen that umbrella's stated scope.

## Low

- **V19** `[RC-F5]` — `ReadModelWritePolicy` is a static class listed among "target abstractions"; recategorize.
- **V20** `[RC-F6]` — §7 I11 credits ULID preservation to tests that contain no ULID coverage; the real witnesses are `IdentifierValidatorTests` / `PartyAggregateCompositeTests`.
- **V21** `[CE-F4]` — I12 row omits the open ledger item that CI no longer runs the Playwright a11y lane (closure-time receipt gate is fail-closed, so not false — just unmentioned).
- **V22** `[CE-F6]` — open ledger items outside the five-ID closure set (DW-1/DW-2, pre-DW entries) have no §7 or deferral pointer.
- **V23** `[RW-F6 · CE-F7]` — the §7 I3 row's deferral list is incomplete: missing `8.6-residual-review-debt` (also retains rollback seams) and `external-runtime-deployment` (owns the "release recovery" path).
- **V24** `[RW-F7 · CE-F8 · ADV-10]` — §7 and the closure fitness pins are amendable only by the actor they gate (no amendment protocol), and the I15 guard is narrow by construction: diff vs closure-HEAD only, skipped on shallow checkouts, prose-string enforcement.
- **V25** `[ADV-11]` — DataProtection key-ring/cursor continuity is absent from I1a's parity list; topology cutover/rollback invalidates live cursors.
- **V26** `[RW-F8]` — package-vs-source identity divergence (EventStore package `3.95.0` vs source pin `v3.95.0-2-g454b4d10`) is recorded but the pairing deserves explicit owner confirmation.

## Suggested disposition order

1. **Commit-and-annotate (V2)** — mechanical; resolves the only refuted pin and makes §7 durable.
2. **Adopt I16–I18 + I1/I1a amendments (V1, V3, V4, V5)** — one Update pass closes every critical/high; they are three faces of one gap (the gate system speaks "sequenced spec files"; the closure moved work into ledger deferrals).
3. **§2/§5/§7 accuracy sweep (V6, V11, V17, V18, V23, V20)** — table/row edits, no design decisions.
4. **Defer with revisit conditions** — V7–V10, V12–V16, V19, V21–V22, V24–V26 individually small; several (V8, V9, V25) only bite when their G-row APIs land.

Full detail, evidence with file:line, and proposed invariant wording live in the four
review files in this folder.
