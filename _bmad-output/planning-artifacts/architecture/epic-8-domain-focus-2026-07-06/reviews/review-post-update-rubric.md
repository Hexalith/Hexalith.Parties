# Epic 8 Architecture Spine — Post-Update Rubric Walker Review

- **Target:** `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` (working tree, amended 2026-08-18, `status: final`)
- **Prior report:** `reviews/validation-report-2026-08-18.md` (CONDITIONAL PASS, 26 deduped findings)
- **Parent (binding):** `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`
- **Deferral register honored:** `.memlog.md` (2026-08-18T22:45) — V8–V10, V12–V16 (partial), V19, V21–V22, V24–V26 deferred with revisit conditions; not re-flagged below unless the amendment worsened one (none did).
- **Reviewer:** rubric walker, re-run after the validation-driven UPDATE
- **Date:** 2026-08-18

## Verdict

**PASS — 0 Critical / 0 High / 2 Medium / 3 Low.**

All eleven findings the update claims to close (V1, V3, V4, V5, V6, V7, V11, V17, V18, V20, V23) are genuinely closed with enforceable wording; no amendment introduces a contradiction against I1–I15, §4, §5, §7, or the Epic 7 parent; IDs are stable; the machine-enforced §7 schema and both fitness-test string pins verify clean by direct inspection. The single open closure condition (V2: uncommitted Builds gitlink `17b1c7aa…` vs committed `6b78075`, plus the three untracked closure fitness files) is external to the spine text, correctly annotated in the §7 I4 row, §8, and `sprint-status.yaml:187–202`, and is not double-counted here. Remaining findings are hardening/fragility items, none blocking.

## 1. Do the amendments close the claimed findings? — YES, all 11

| Finding | Claimed closure | Verdict | Evidence |
| --- | --- | --- | --- |
| **V1** (critical, identity-stamped parity) | New I16 | **Closed.** | I16 carries all three operative clauses the report demanded: evidence valid only at the produced-against identity and must record it; any retained-identity change (Builds catalog, root gitlink, package pin) re-opens claims stamped at a different identity of the same dependency, with a decidable obligation on the changing story (re-run named surfaces at the new identity **or** record the claim unvalidated in the 8.3 matrix, *before merging*); no deletion merges into a tree whose retained identity differs from its evidence stamp. The wording names concrete identity carriers and a merge-time gate — enforceable in review, matching the exact failure shape the 8.3 matrix documented twice. |
| **V3** (§4 gated by enumeration) | New I17 + §4 rescope + §5 note | **Closed.** | Forward half: I17 forbids working any accepted closure deferral *or any Epic 8 ledger item* except through a spec declaring all six §4 clauses, with activation annotating the ledger entry (concurrency visibility). The waiting/working contract split is crisp. Backward half: §5 now acknowledges 8.11–8.13 executed under the 2026-07-07/08 SCP authority pre-amendment (the false "(unchanged)" was deleted), and §4's heading/opening rescopes by property: enumerated 8.6–8.10 + "any later-added deletion-heavy Epic 8 spec" + every deferral-activation spec. Coverage is closed: past specs grandfathered explicitly, future specs caught by property, ledger work caught by I17. |
| **V4** (AppHost retirement contradiction; ACL fork) | I1 + I1a amendments | **Closed.** | I1: exactly one authoritative ACL owner file at any time (today's named: `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`), fitness gate asserts (app ID, verb, policy, action) tuples, route-list changes require recorded owner approval referencing the invariant, and the 13-route list is declared "baseline, not a hand-editable ceiling" — killing the spoof-by-tuple and silent-widening paths. Verified real, not aspirational: `ArchitecturalFitnessTests.PartiesAppHost_KeepsPartiesAppIdAndDedicatedDaprAccessControl` (tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:318–386) asserts single `appId: eventstore`, both `defaultAction: deny` policies, exact route set, and per-operation `httpVerb: ['POST']` + `action: allow` with count=1 each; `DocumentationFitnessTests.MaintainedDocumentationDescribesSdkRoutesUnderEventStoreOnlyDenyAcl` independently pins the same file. I1a: retirement now additionally requires every deferral whose exit proof/rollback names the Parties AppHost (currently `8.6-residual-review-debt`) to have passed or been re-approved against the successor topology, and states outright that deferral rollback clauses take precedence over parity-based retirement — the ADV-3 deadlock is resolved in the safe direction. |
| **V5** (parity has no baseline) | New I18 | **Closed.** | Baseline = §7 named surfaces at the Epic 8 closure commit; evidence must enumerate discharged baseline surfaces or name an approved successor test *in the same spec*; deleting/weakening a baseline surface in the changeset that deletes its guarded implementation is invalid as parity evidence; slice-N evidence does not certify later slices (cross-wired to I16 per-slice stamps). This is exactly the rule the `TenantSafeProjectionReadGuardrailsTests` incident lacked. See L1 for the one forward-reference wrinkle. |
| **V6** (two paging/freshness owners) | §2 envelopes row + precedence rule | **Closed.** | The row now splits by shape: command/query envelopes + freshness metadata → EventStore.Contracts (G6) *referencing* Commons paging; paging primitives → Commons (Epic 7 AD-4). No conflict with the parent remains — AD-4/B10's Commons-owns-pure-paging holds verbatim. Arbitration rule added: "Where this table and the Epic 7 spine disagree, this table wins." See M2 for a hardening note on that rule's breadth. |
| **V7** (§5 order binds no one) | §5 amendment | **Closed.** | "The `8.6 → 8.7 → 8.8 → 8.9` order binds the accepted closure deferrals exactly as it bound the stories" forbids the 8.9-before-8.7 scenario outright; the concurrency exception is decidable (disjoint §4 clause-2 touched-repo and clause-6 parity-checklist sets), matching the report's proposed §4.2/§4.6 test. |
| **V11** (§2 omissions; unstated supersession) | Two new MOVES rows + supersession paragraph | **Closed.** | Tenant-claims row (transformation → EventStore.Authentication + Commons ULID helpers, owner decision 2026-07-16, G7/G9) and UI-primitives row (status/freshness/reconcile/grid/picker → FrontComposer, G4) both added with the KEEPS-side policy/semantics retained by Parties. The Class A supersession is now stated in §2 as the *one* deliberate SCP-authorized supersession on record (Story 8.4 deleted `Hexalith.Parties.Authentication`; anchors moved with the transformation). |
| **V17** (I13/I14 one remediation stale) | §7 rows | **Closed** (spine side). | I13 row now attributes `MainLayoutAccessibilityTests` to the adopted FrontComposer shell slice (skip links + landmarks, 2026-08-18) with 8.9 owning only the *remaining* shared-primitive slices; I14 records "shell slice already adopted 2026-08-18". The residual staleness lives in `deferred-work.md`'s 8.9 rollback text — outside the spine, correctly surfaced to the user via `.memlog.md` line 18 rather than hand-edited (per ledger-ownership rules). Not re-counted. |
| **V18** ("Executable"-only rows hide residual debt) | §7 rows I7/I9/I10 (+I12 note) | **Closed.** | I7 now "Executable + deferred" naming the erasure-certificate identity/status validation and Memories cleanup-race debt under `8.6-residual-review-debt`; I9 names the unbounded Art.30 read model and null-dictionary recovery; I10 names freshness-mapping and search-input-bounds debt; I12 discloses the always-on CI a11y-lane ledger item without falsely tagging the row deferred (closure-time receipt gate is fail-closed). |
| **V20** (I11 credits wrong witnesses) | §7 I11 row | **Closed.** | Row now separates roles: `IdentifierHygieneFitnessTests` bans GUID-parser regressions; `IdentifierValidatorTests` + `PartyAggregateCompositeTests` witness ULID-compatible acceptance and GUID-shaped replay — precisely the real witnesses RC-F6 identified. All three classes verified to exist under `tests/`. |
| **V23** (I3 deferral list incomplete) | §7 I3 row | **Closed.** | Row now names all five: `8.6-residual-review-debt` (host/gateway/ACL switch-back seams), 8.7, 8.8, 8.9, and `external-runtime-deployment` (release-recovery rollback path). |

**V2** (not in the closure claim, commit-gated): correctly *annotated*, not closed — the I4 row carries the working-tree/`6b78075` caveat and names sprint-status as the tracker; §8 restates it as the single open closure condition. Honest handling; stays open until the superproject commit lands.

## 2. Internal consistency — no new contradictions

- **I16–I18 vs I1–I15:** I16 strengthens I4 (extends recorded-identity from consumption evidence to parity-evidence lifetime) without contradicting it; I17 and §4 cross-reference each other in both directions (§4 opening cites I17; I17 cites "all six §4 clauses") with no scope mismatch; I18 references the §7 map, which exists and is schema-stable. I18's per-slice clause and I16's identity stamps compose rather than conflict.
- **IDs stable:** I1, I1a, I2–I15 unchanged in number and order (diff vs HEAD confirms only wording amendments inside I1/I1a/I9/I10); I16–I18 appended under a new "Gate integrity" subsection — no renumbering, no reuse.
- **§1/§6 reconciliation character vs new §8:** coherent. §1 still ratifies-not-rederives; §6's "CLOSED for planning purposes, 2026-07-07" is untouched; §8 is purely a record of the 2026-08-18 validation + amendments and correctly restates (rather than contradicts) the sprint-status open condition. Frontmatter `status: final` + `updated`/`amendment` lines match §8's content; nothing in the repo pins the old `approved-reconciled` string (grepped tests/scripts/docs/_bmad-output — zero hits outside this reviews folder).
- **§5 vs §4/I17:** the grandfathering of 8.11–8.13 is consistent with §4's "later-added" property scope; the deferral-order rule is consistent with I17 (order + six-clause spec + activation annotation form one coherent execution protocol).
- Two wording-level drifts noted as L2/L3 below; neither is a contradiction.

## 3. Parent-spine (Epic 7) consistency — clean

- The precedence rule is correctly scoped to the §2 table (not the whole document), and the supersession paragraph names exactly one Class A boundary override, with SCP authority and the destination owner stated — the report's requested shape.
- Sweep for weakening beyond that one supersession: I16/I18 strengthen AD-5/AD-6; the amended I1 strengthens the inherited EventStore-gateway boundary; "actor host"→"domain-service host" (I1) and "per-actor"→"per-read-model" (I9) track the post-8.5 SDK reality without touching AD-2's ownership split; the new §2 rows *agree* with AD-4 (paging→Commons) and AD-2/B9 (freshness vocabulary→EventStore) rather than override them; tenant-claim transformation → EventStore.Authentication is the recorded 2026-07-16 owner decision, exercised under the stated supersession. **No inherited Epic 7 decision is weakened outside the one stated supersession.**
- Residual hardening gap in the precedence rule itself → M2.

## 4. Terse-spine discipline — acceptable, borderline in two spots

- I16–I18 are dense single-block invariants in the house style; each clause is load-bearing. Good.
- I1 has grown to ~16 lines (route baseline + ownership + tuple assertion + change protocol). Every clause was demanded by V4 and none is per-story detail, but I1 is now the ceiling — any further growth should split an "ACL governance" companion rather than extend the invariant.
- §7 rows now carry parenthetical debt inventories ("unbounded Art.30 read model, null-dictionary recovery", "freshness-mapping and search-input-bounds"). This is the closest the amendment comes to per-story drift; it is justified as the V18 honesty fix and stays at named-debt granularity, but the rows should not absorb further debt prose — the ledger owns descriptions.
- Duplication is confined to the record layer (I4 caveat appears in §7 row + §8 + sprint-status; amendment summary in frontmatter + §8) — appropriate for an amendment record, not bloat.
- Nit (uncounted): §5 "Correct-course additions 8.11–8.13 … executed under" reads clipped ("were executed"); harmless.

## 5. Machine-enforcement safety — verified by reading; two fragilities flagged

Verified against `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs` (`InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence`, `EpicEightAddsNoPrdFunctionalRequirement`) by re-implementing the parser over the amended file:

- **Row set:** exactly I1, I1a, I2–I15 (16 rows) inside the `epic-8-invariant-map` markers; I16–I18 are §3 invariants only, **not** map rows — the `rows.Keys` equality check passes. Header row skipped correctly.
- **Cell count:** every row parses to exactly 3 cells (the em-dash-heavy I4 caveat included; no stray pipes).
- **Disposition wording:** every row matches `\b(Executable|Deferred)\b` ("Executable", "Deferred", or "Executable + deferred").
- **Named classes:** all 29 distinct backticked `*Tests` classes across the rows exist as classes under `tests/` (including the newly cited `IdentifierValidatorTests`, `PartyAggregateCompositeTests`, `ArchitecturalFitnessTests`); every Executable row names ≥1.
- **Deferral coverage:** all five expected deferral IDs appear in the combined evidence column; every capital-"Deferred" row names ≥1.
- **String pins:** `zero new PRD FRs` (frontmatter, line 8) and `Epic 8 adds **zero** PRD functional requirements` (I15, line 149) both present byte-exact.
- **I1's tuple claim is true today** (see V4 row above) — the claim is not aspirational.

Fragilities (no current failure): M1 and L3 below.

## Findings

### Medium

- **M1 — Mixed-disposition rows escape the per-row deferral-name guard.** All eleven mixed rows write "Executable + deferred" (lowercase d); `EpicEightClosureFitnessTests` line 144 checks `Disposition.Contains("Deferred", Ordinal)` case-sensitively, so the "must name an accepted deferral" branch fires only for I1a and I3. Today every mixed row does name a real deferral and the aggregate all-five check (lines 151–153) holds, so nothing is false — but a future edit could cite a retired or misspelled deferral ID in a mixed row and stay green. Fix in the *test* (case-insensitive contains), or capitalize "Deferred" in the rows; either is a one-line change. (Fitness-test-side; does not block the spine.)
- **M2 — The §2 precedence rule is an unguarded override channel over the binding parent.** "Where this table and the Epic 7 spine disagree, this table wins" resolves the V6 arbitration gap as requested, but carries no requirement that a *future* disagreement be SCP-authorized and recorded the way the one stated Class A supersession is. As written, an ordinary table edit could silently supersede an inherited Epic 7 decision. One clause closes it: future table-vs-parent supersessions require the same recorded SCP/owner authority and must be stated in the supersession paragraph.

### Low

- **L1 — I18's baseline anchors to a commit that does not yet exist.** "Named test surfaces in the §7 map as of the Epic 8 closure commit" is a forward reference while the closure commit is still pending (the V2 open condition; the three closure fitness files are themselves untracked). Until the superproject commit lands, the baseline is defined only by the working tree. Self-healing at commit time; §8 already acknowledges the dependency — no spine edit needed, but the closure commit should be understood as also *freezing the I18 baseline*.
- **L2 — §3 heading scope is one amendment stale.** "Invariants — must hold across every remaining migration (8.6–8.10)" still enumerates stories, while I16–I18 and the amended §4/§5 deliberately bind deferral executors and later-added specs. Cosmetic drift, worth fixing at the next touch ("…every remaining migration and deferral execution").
- **L3 — I1's fitness-gate claim has no named successor once the ACL moves owner.** Both asserting suites (`ArchitecturalFitnessTests`, `DocumentationFitnessTests`) hard-code the Parties-local path; I1's "exactly one authoritative owner file at any time (today: …)" wording covers the move conceptually, but at I1a retirement the "fitness gate asserts the authoritative copy" obligation must be re-homed and no artifact yet says where. Fold into the 8.8 deferral-activation spec's §4 clauses (relates to deferred V24; the amendment did not worsen it).

### Deferred findings — spot-checked, none made worse

V8 (I10 freshness grammar untouched), V9, V25 (await G6/G5/G8 APIs), V10, V12 (I1a "explicitly approved" phrase unchanged; I1 gained decidability, net improvement), V13, V14 (I4 caveat now *surfaces* the HEAD/checkout divergence — improvement), V15, V19, V21 (I12 row now mentions the a11y-lane ledger item — partial improvement), V22 (I17's "any Epic 8 ledger item" extends the gate to them — partial improvement), V24 (§8 provides the missing amendment record — partial improvement), V26. All consistent with their `.memlog.md` revisit conditions.

## Disposition

The spine is closure-grade as text. Remaining sequence: (1) land the superproject commit (Builds gitlink + untracked closure fitness files) to discharge V2 and freeze the I18 baseline; (2) optional one-line hardening for M1 (test-side) and M2 (§2 clause) at the next authorized touch; L2/L3 ride along with future edits. No re-validation gate required for the amendments themselves.
