# Epic 8 Architecture Spine — Validation Report (Reviewer Gate)

- **Target:** `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- **Intent:** Validate (critique only — the spine was not modified)
- **Date:** 2026-09-08
- **Gate composition:** deterministic lint pass + 5 parallel reviewer lenses
  - Lint (`lint_spine.py`): **0 findings** → `reviews/lint-2026-09-08.json`
  - Rubric walker → `reviews/review-2026-09-08-rubric-walker.md` — PASS WITH CONDITIONS (0C/1H/5M/3L)
  - Reality check → `reviews/review-2026-09-08-reality-check.md` — CONDITIONAL PASS (0C/2H/4M/5L)
  - Adversarial → `reviews/review-2026-09-08-adversarial.md` — CONDITIONAL FAIL (1C/4H/7M/2L)
  - Closure-evidence integrity → `reviews/review-2026-09-08-closure-evidence.md` — CONDITIONAL (1C/1H/3M/1L)
  - Parent-inheritance (ad-hoc) → `reviews/review-2026-09-08-parent-inheritance.md` — ALIGNED WITH RECORDED SUPERSESSION (0C/1H/2M/2L)
- **Raw findings:** 45 → **37 after cross-lens dedup** (2 critical, 6 high, 20 medium, 9 low)
- **Prior gate:** 2026-08-18 CONDITIONAL PASS; I16–I18 and I1/I1a amendments still close ADV-1..ADV-4 and P1–P8. This pass does not re-open those doors. It finds a new I16 *enforcement* break at HEAD, a §5 hole the 2026-09-06 ledger children walk through, and several 2026-08-18 deferrals whose revisit condition is now due.

## Gate verdict

**CONDITIONAL FAIL.** The invariant text is still a sound reconciliation contract — I1’s ACL still matches the YAML, every named §7 test class exists, the map-parse fitness tests are green, no deferred item is represented as delivered, the Class A Authentication move remains gated-not-executed, and the 2026-08-18 criticals stay closed. It is **not** safe to treat the spine as the scheduling contract for the first I17 activation specs, and it is **not** load-bearing on identity: HEAD Builds has already walked past the last I16-authorized pin with no `unvalidated` marker, and the printed I4 snapshot is two refresh cycles behind the matrix the row itself names as source of truth.

## What holds (strengths)

- I1’s 13-route deny-default ACL list still matches `accesscontrol.parties.yaml` one-for-one; `DocumentationFitnessTests` still pins the same `ExpectedSdkRoutes`.
- All named §7 `*Tests` classes still exist under `tests/`. `EpicEightClosureFitnessTests` map-parse + deferral-honesty methods: **2/2 green** (direct xUnit v3 assembly invoke).
- I18 baseline commit `2b63ab9` still resolves. Headline “no deferred item is represented as delivered” still holds for the five accepted closure IDs (8.7/8.8/8.9 remain blocked; I13 still “parity not yet discharged”).
- I2 SDK entry points and I10 named EventStore types still exist at checkout `c6efdbba…`. `Hexalith.Parties.Authentication` is still in-repo; 8.3 G7/G9 remains `needs-additive-api`.
- ADV-1 through ADV-4 and post-update P1–P8 remain closed on the adjectives they were written against (I16–I18, I1/I1a, §5 disjointness).
- No child I-n silently executes an ungated override of Epic 7 AD-1..AD-6.

## Critical

### V27 — HEAD Builds gitlink moved past the last I16-authorized pin with no re-validation `[CE-F1 · RC-F2]`
Last authorized Builds identity is `35c3d1e5…` (`v4.27.2-9-g35c3d1e`, signoff + `PlatformApiPrerequisitesTests` + 8.3 matrix, 2026-09-08). `git ls-tree HEAD` records `a32cb422…` (`v4.27.2-10-ga32cb42`) via superproject commit `bf8daf98` (“update subproject reference for Hexalith.Builds”). No new signoff line, no test-constant update, no matrix `unvalidated` marker. I16’s own rule: an unvalidated marker is a **stop**, not a state — no further deletion relying on that dependency may merge. I4 is labeled Executable; that executable surface would fail at current HEAD.

**Disposition:** discuss (tree + spine). Either restore Builds to `35c3d1e5…`, or re-run the named surfaces, stamp `a32cb422…` in matrix/tests/signoff, and refresh the I4 snapshot. Until one of those lands, I16 already forbids deletion-heavy merges.

### V28 — §5 binds the five named deferrals; 2026-09-06 children inherit §4 and evade the order `[ADV-P9]`
Unit A = I17 spec for `8.7-data-protection-extraction` (keep the 18 MOVE files as rollback). Unit B = I17 spec for ledger child `DW-104` (“Delete the retained local crypto… independently shippable and was split from Story 8.7”). Both can author a six-clause §4 spec. §5’s `8.6 → 8.7 → 8.8 → 8.9` order and concurrency rule name the *accepted closure deferrals*, not children. DW-104 is not `8.7-data-protection-extraction`. Same door opens `DW-101` (Art.20 on the current engine) and `DW-100` (G4-B freshness) against 8.6/8.8. I17 made children fill paperwork; it did not put them on the calendar.

**Disposition:** discuss. Tighten §5: the order and concurrency rule bind every ledger item whose `source_spec` / `origin` / `reason` names an accepted closure deferral or a `spec-8-x` parent, and every item that deletes a surface that deferral’s rollback names. Children are not independently schedulable. Clause-6 disjointness is computed from the §7 invariants the *touched files* guard.

## High

### V29 — §7 I4 snapshot prints superseded EventStore and Builds identities `[RW-F1 · RC-F1 · CE-F2]`
Spine I4 still prints EventStore package `3.102.0` / source `acf5c4e4…` / Builds `8db7459d…` (last prose re-reconcile 2026-09-06). Today’s durable record (matrix lines 69–72, `PlatformApiPrerequisitesTests`, `.gitlink-signoff.tsv`) pins `3.103.0` / `c6efdbba…` / `35c3d1e5…`. Commons `6da79aed…` still matches. The row’s hedge (“snapshot, not source of truth”) keeps this from being another V27 — but two units that copy the printed pins still diverge, and frontmatter/memlog still say `updated: 2026-08-18`.

**Disposition:** autofix (after V27 is resolved). Refresh the I4 snapshot to the last *authorized* set; do not print `a32cb422…` until it is signed off.

### V30 — G5 `IPersonalDataPolicy` / `IErasureStateProvider` vs I7: two writers of one legal fact `[ADV-P10]`
§2 keeps GDPR policy in Parties and sends the generic engine to EventStore. The 8.3 G5 row requires `IPersonalDataPolicy` and `IErasureStateProvider` *inside the shared engine package*. 8.7 can adopt that letter; 8.6-residual keeps Art.18 on the aggregate. “Erasure in progress” and “is this personal data” become two oracles. I16 stamps the engine; I18 greens consent tests and the crypto harness separately. SPEC OQ-5 is this hole asked as a question.

**Disposition:** discuss. New I19a (policy oracle): Parties aggregate / GDPR types are the only writer; G5 interfaces are read-only adapters. Drop those two types from the engine package or reclassify them as I2 hooks with a recorded owner decision.

### V31 — I10 pins freshness *presence*; three ratified dialects can each satisfy it `[ADV-P11]` — revisit of 2026-08-18 V8
I10 requires `ProjectionFreshnessMetadata` on every read and names abstractions. It does not name vocabulary, `ProjectionVersion` grammar, or the erasure-time `ProjectedAt` rule. 8.6-residual can close on the spec-8-6 three-state production dialect + `global:N`; DW-100 / 8.8 G6 can bind the six-state wire. Memlog deferred V8 until G6 lands; first I17 activation is now the revisit.

**Disposition:** discuss. Tighten I10 to a single versioned shape (owner: EventStore.Contracts G6 once `available`; until then the spec-8-6 three-state emitter). Presence without a named grammar is not compliance.

### V32 — Memories mapping store: I9/I10 read-model vs D7 operational ledger `[ADV-P12]` — revisit of 2026-08-18 V9
DW-81 tells 8.6-residual to put `PartyMemoryUnitMappingStore` behind EventStore persistence / `IReadModelStore`. 8.7 + DW-82/DW-86 need that store as a rebuild-surviving cleanup ledger. Wrap it as a read model and I9/I10 rebuild-from-zero wipes the receipts D7 needs. I19 was logged as a candidate and never written.

**Disposition:** discuss. New I19 (persistence class): event-derived read model vs operational side-effect ledger. Name `PartyMemoryUnitMappingStore` as class (b).

### V33 — Erasure certificate: 8.6-residual validates identity/status; 8.7 rewrites the producer `[ADV-P13]`
§7 I7 defers certificate identity/status validation to `8.6-residual-review-debt`. 8.7’s exit proof requires passing “erasure certificate/report” on the G5 producer. Sequential under §5, still two owners of one byte layout. I16 re-opens 8.7’s claims on an EventStore bump, not 8.6’s already-merged validation semantics.

**Disposition:** discuss. Tighten I7: `ErasureCertificate` / `ErasureVerificationReport` field semantics are one owned, versioned shape (Parties.Contracts). 8.6-residual may only add tests against the current shape; 8.7 may not change it except by an I5 plan recorded in the 8.3 ledger *before* G5 adoption.

### V34 — Class A supersession is gated and authorized, but labeled as the whole boundary `[PI-F1]`
The 2026-07-16 SCP reroutes Authentication / tenant-claims only and carves out Contracts. §2 calls that “supersession of the Epic 7 Class A anchor boundary.” Authentication remains in-repo (gated-not-executed — that half holds). An executor can read the label as also releasing the Contracts-half “define shared anchors once” rule. The SCP path is not in `related:` / §1.

**Disposition:** autofix. Narrow the label to the Authentication / tenant-claims slice; restate the Contracts carve-out; cite the 2026-07-16 SCP in `related:`.

## Medium (20)

- **V35 · RW-F2 · PI-F2** — No named paradigm and no Inherited Invariants table listing Epic 7 AD-1..AD-6 by original ID. Silent inheritance plus §2 “this table wins” can drop an unlisted parent AD. **Discuss.**
- **V36 · RW-F3** — I1 ignores the brownfield `POST /tenants/events` pub/sub door (`Program.cs` `MapEventStoreDomainEvents`, ACL comments, fitness carve-out). **Discuss.**
- **V37 · RW-F4** — Production KMS / key-backend, infra/provider strategy, and Epic 7 low-cardinality telemetry are silent on the spine (SPEC OQs / memlog only). **Defer** with a Deferred row naming the revisit (first 8.7 / `external-runtime-deployment` spec).
- **V38 · RW-F5 · ADV-P21** — I1a “explicitly approved”, I5 “intentionally versioned”, I2 “hooks the SDK cannot own”, I18 “owner recorded in the spec” still have no recorded approver artifact. Recurrence of V12. **Discuss.**
- **V39 · RW-F6** — I1a Rule lists three AppHost-naming deferrals; §7 I1a row names only `8.8-runtime-boundary-cleanup`. Fitness stays green on one named deferral. **Autofix.**
- **V40 · RC-F3** — Frontmatter `updated: 2026-08-18` and memlog last event are frozen through the 2026-09-05/06 I4 edits and this gate. **Autofix** on Update.
- **V41 · RC-F4 · RW-F7** — I13 “purge FAST/v4 tokens” still false in retained AdminPortal/ConsumerPortal CSS (`--neutral-fill-stealth-rest` with no fallback). Recurrence of V15. **Defer** to `8.9-frontcomposer-ui-consolidation` / token-purity guard.
- **V42 · RC-F5** — Parent Epic 7 stack table still lists SDK 10.0.302 / Dapr 1.18.4 / Aspire 13.4.6 / FluentUI rc.3; live pins are 10.0.400 / 1.18.5 / 13.5.3 / rc.5. Epic 8 I12 does not repeat patch pins. **Defer** to an Epic 7 spine update; not a child override.
- **V43 · RC-F6** — An 8.3 G5 cell still names a superseded “retained” EventStore/Builds pair. Companion drift, not a spine AD. **Defer** to matrix owners.
- **V44 · CE-F3** — I13 remaining-gap text still claims unscoped/dead CSS after the `.parties-main-content` isolation repair. Disposition “parity not yet discharged” remains honest. **Autofix** the stale clause.
- **V45 · CE-F4** — FrontComposer stamps are split: live/matrix `a0acb78f…` vs `EpicEightClosureFitnessTests` / Playwright receipt `f0c3b6fd…`. **Discuss** (I16 on a non-I4 dependency).
- **V46 · CE-F5** — I11 attributes GUID-shaped replay to `PartyAggregateCompositeTests`, which has no GUID ID case. **Autofix** the witness list.
- **V47 · PI-F3** — §4 clause-2 enumeration omits Memories and Tenants; parent submodule governance names both. **Autofix.**
- **V48 · ADV-P14** — I11 “stop rejecting valid ULIDs” vs I10 “IDs permanently unavailable after erasure”: format-valid tombstoned IDs have no named refuse layer. **Discuss** (I11: format ≠ allocation; I10 wins on reuse).
- **V49 · ADV-P15** — I1’s one local ACL file vs `external-runtime-deployment` environment-specific deny-default ACL. **Discuss.**
- **V50 · ADV-P16** — I1a parity list still omits key-ring / cursor / DataProtection continuity. Recurrence of V25. **Defer** to first 8.7/8.8 activation spec.
- **V51 · ADV-P17** — §4.3 still accepts a described rollback; 2026-09-06 children make silent expiry the default. Recurrence of V10. **Discuss.**
- **V52 · ADV-P18** — I7’s two front doors vs spec-8-8 `delete_party` MCP tool: a third mutation path. **Discuss.**
- **V53 · ADV-P19** — I16’s stop is per-dependency; Art.20 / FrontComposer work can proceed while EventStore I8 evidence is void. **Discuss.**
- **V54 · ADV-P20** — §2 says Parties keeps GDPR copy; §7 / 8.9 say 8.9 consolidates it into FrontComposer. **Discuss.**

## Low (9)

- **V55 · RW-F8** — No Stack, Structural Seed, or Capability→Architecture map; I12 names no versions. Seed, not a missed invariant. **Ignore** unless an Update adds a one-line Stack for I12 families.
- **V56 · RW-F9** — I17 `activated_by_spec` is invisible to `ParseDeferrals`. Previously rejected as test-side (memlog P6). **Ignore** (owned outside the spine).
- **V57 · RC-F8** — `ReadModelWritePolicy` is a static helper listed among “target abstractions.” Recurrence of V19. **Autofix** wording.
- **V58 · RC-F9** — `ProjectionFreshnessMetadata` is a Parties contract, not an EventStore type. **Autofix** (name it as the compatibility mapping).
- **V59 · RC-F10** — C# 14+ is the .NET 10 default, not an explicit Parties pin. **Ignore.**
- **V60 · RC-F11** — Fluent UI Blazor V5 is still an RC (`5.0.0-rc.5-26219.1`, current NuGet latest). **Defer** until GA; I13 already owns the UI stack.
- **V61 · CE-F6** — I2 E2E surface is still a silent no-op without Docker/DAPR. Recurrence of V16. **Defer** to `8.6-residual-review-debt` topology proof.
- **V62 · PI-F5** — AD-1 “adapter first” is implied by I3/§4, not named. **Autofix** if V35 adds the Inherited Invariants table.
- **V63 · ADV-P22** — I1’s “today” ACL path dies at I1a retirement with no successor-file protocol. **Defer** to I1a re-approval (inherently future).

## Suggested disposition order (if this rolls into an Update)

1. **Stop the tree (V27)** — restore or re-stamp Builds. This is I16’s own brake, not a spine-prose fix.
2. **Refresh the I4 snapshot + metadata (V29, V40)** — print the last authorized pins; bump `updated`.
3. **Bind children to §5 (V28)** — without this, I17 is paperwork and the 8.6→8.9 order is costume.
4. **I19 + I19a (V32, V30)** — persistence class and single GDPR-policy oracle. These are the two ways remaining work can corrupt erasure.
5. **I10 grammar + I7 certificate shape (V31, V33)** — shared-data shapes G6/G5 will otherwise fork. V8/V9 revisit is due.
6. **Accuracy sweep (V34, V39, V44, V46, V47, V57, V58)** — table/row/label edits, no new design.
7. **Defer with revisit** — V37, V41–V43, V50, V60, V61, V63 stay owned by named deferrals or companion docs.

## What this pass did not change

The spine, memlog, specs, ledger, and tests were not edited. Full evidence with file:line and proposed invariant wording lives in the five review files. The 2026-08-18 report remains the historical record of I16–I18’s adoption.
