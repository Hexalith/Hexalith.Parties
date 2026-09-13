# Epic 8 Architecture Spine — Post-Update Rubric Walk (2026-09-08)

- **Target:** `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` (as amended 2026-09-08, `updated: 2026-09-08`)
- **Parent (read-only):** `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`
- **Inputs:** `.memlog.md`, `reviews/validation-report-2026-09-08.md` (V27–V63), `_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md`, repository `src/`, `tests/`, `.gitmodules`, `.gitlink-signoff.tsv`, `story-8-3-platform-api-prerequisite-matrix.md`, `deferred-work.md`
- **Lens:** rubric walker, independent. Reviewed the current file text and repository reality only; did not defer to the author or to the prior lenses' premises.
- **Reality checks run:** all 30 `*Tests` classes named in §7 resolve to exactly one class under `tests/`; `git ls-tree HEAD references/` (Builds `a32cb422…`, EventStore `c6efdbba…`, Commons `6da79aed…`, FrontComposer `a0acb78f…`); `PlatformApiPrerequisitesTests` constants (`BuildsSha` = `35c3d1e5…`, `FrontComposerSha` = `a0acb78f…`) vs `EpicEightClosureFitnessTests.FrontComposerSha` = `f0c3b6fd…`; `accesscontrol.parties.yaml` route list (13 routes, `eventstore` only, two `defaultAction: deny`); `Program.cs:69 MapEventStoreDomainEvents()`; `PartiesMcpTools.cs:390-423 DeleteParty`; `PartyAggregate.cs:686-696 Handle(CreateParty)`; `ProjectionFreshnessStatus` enum; Builds catalog pins (`Microsoft.FluentUI.AspNetCore.Components 5.0.0-rc.5-26219.1`, `Dapr.* 1.18.5`, `Aspire.* 13.5.3`, `global.json` SDK `10.0.400`); 8.3 matrix lines 69–72, 90–96, 105, 131; `.gitmodules` root set; `EpicEightClosureFitnessTests.InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence` parse rules (rows I1, I1a, I2–I15; 3 cells; `Executable` rows name existing `*Tests`; `Deferred` rows name one of the five accepted deferrals).

## Verdict

**PASS WITH CONDITIONS.**

The amendment does what §10 says it does. Every previously-open finding I was asked to check is closed by new text, not by mention, with two exceptions: **V52 was "closed" on a false premise and the closing text introduces a new divergence** (F-1), and **V38's closure (I20) is unenforceable until the decision surface it names exists** (F-5). No new invariant weakens or contradicts an inherited Epic 7 AD; the Inherited Invariants table lists the parent by original IDs; the gated supersession is correctly narrowed to the Authentication/tenant-claims slice with the Contracts half explicitly preserved. The §7 map parses under the fitness test's schema (rows I1, I1a, I2–I15; three cells; every named class exists; every `Deferred` row names an accepted deferral; all five deferrals appear). No template comments or placeholders remain (`<!-- epic-8-invariant-map -->` markers are parser anchors, not template residue). The V27 tree condition is correctly recorded as open and not spine-fixable.

Conditions: fix F-1 (high) before any I17 activation spec reads I7; resolve F-2/F-3/F-4/F-5 (medium) in the next amendment or as recorded follow-ups.

---

## Findings

### F-1 — I7 enumerates MCP `delete_party` as an erasure door; the brownfield tool is soft deactivation and explicitly not erasure

- **Severity:** high
- **Disposition if updating:** autofix
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:182-188` — "Erasure mutation paths are the closed set **Admin `EraseParty`, Consumer `RequestMyErasure`/`CancelMyErasure`, MCP `delete_party`**; every path applies the same aggregate command, the same D7 verification, and the same I10 tombstone; … MCP fails closed on missing server-resolved identity (I6) and must not grow a fourth door."
  - `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs:390-391` — `[McpServerTool(Name = PartiesMcpToolNames.DeleteParty, … Destructive = true, Idempotent = true)]` `[Description("Soft deactivates a Parties record for MVP delete intent. This tool does not perform GDPR erasure.")]`
  - `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs:415-416, 421` — result carries `operation = "soft-deactivation", gdprErasurePerformed = false`; the command issued is `commandClient.DeactivatePartyWithResultAsync(partyId, …)` → `DeactivateParty`, not `EraseParty`.
  - `docs/api-contracts.md:169` — "`delete_party` | Destructive, Idempotent | **Soft deactivation** (MVP delete intent) — explicitly *not* GDPR erasure."
  - `_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md:28` — preserves "the exactly-5 MCP tool contracts"; nowhere makes `delete_party` an erasure path.
  - `.memlog.md:35` and `validation-report-2026-09-08.md:92` (V52) — the adversarial premise "MCP delete_party as an unenumerated third erasure door" was adopted verbatim without a source check.
- **Why it fails the checklist:** Item 5 (ratify, don't contradict the brownfield) and Item 2 (rule must prevent its divergence). As written, I7 asserts a fact that is false today and prescribes a rule ("every path applies the same aggregate command … same D7 verification … same I10 tombstone") that can only be satisfied by turning `delete_party` into irreversible GDPR erasure — a behavior change that I6 (command/query behavior preserved) and I5 (public MCP tool contract) forbid and that the MCP tool's own contract disclaims. Two executors reading I7 diverge in exactly the way the spine exists to prevent: one "conforms" `delete_party` to `EraseParty` (a regulated-data regression through an agent-facing tool); the other treats it as already-compliant and skips the D7/tombstone checks for it. The V52 closure is therefore not a closure; it converts a hypothetical third door into a spine-mandated one. **Proposed wording:** "Erasure mutation paths are the closed set Admin `EraseParty`, Consumer `RequestMyErasure`/`CancelMyErasure`. MCP has **no** erasure door: `delete_party` is `DeactivateParty` soft-deactivation (`gdprErasurePerformed = false`) and stays so under I6; adding an MCP erasure door — or re-pointing `delete_party` at `EraseParty` — is an I7 door change requiring an I20 row." Also correct `.memlog.md:35`.

### F-2 — §4 clause 2 claims to enumerate "the root-declared submodule set" but omits a root submodule that Epic 8 already edited, and names a path that is not one

- **Severity:** medium
- **Disposition if updating:** autofix
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:327-330` — "Parties + each of EventStore / Commons / FrontComposer / Memories / Tenants / Builds / `deploy` that the change edits (the root-declared submodule set; an omitted edited submodule makes the spec incomplete)."
  - `.gitmodules` (root) declares eight submodules: EventStore, Memories, FrontComposer, Tenants, AI.Tools, Commons, Builds, **PolymorphicSerializations**. There is no `deploy` submodule and no `deploy/` directory at the root (`ls -d deploy` → none).
  - `story-8-3-platform-api-prerequisite-matrix.md:76` — the PolymorphicSerializations gitlink was advanced twice during Epic 8 (`8aeed1d2…`, StyleCop fixes that unblocked the Release solution build); `.gitlink-signoff.tsv` records it as `validated-advance` 2026-09-06.
  - Spine `ARCHITECTURE-SPINE.md:76` (§1a Submodule governance row) also lists only "EventStore, Commons, Memories, FrontComposer, Tenants, and Builds".
- **Why it fails the checklist:** Item 1 (fix the real divergence points for the level below). The clause's own parenthetical makes the enumeration authoritative ("the root-declared submodule set"), so an executor whose slice touches PolymorphicSerializations — the one submodule that has actually been edited to keep the Release gate green — can read clause 2 as not requiring its declaration, while a stricter reviewer reads the parenthetical. Meanwhile `deploy` cannot be "edited" by anyone. Fix: enumerate the eight `.gitmodules` entries (or say "every entry in root `.gitmodules`, today: …") and drop `deploy` or move it to the `external-runtime-deployment` owner repository wording.

### F-3 — I18 freezes the baseline at closure commit `2b63ab9`, but this amendment edits §7 named surfaces without saying how post-closure §7 edits relate to the baseline

- **Severity:** medium
- **Disposition if updating:** discuss
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:274-277` — "the parity baseline … is the set of named test surfaces in the §7 map as of the Epic 8 closure commit … until it lands, the §7 map in this document as amended 2026-08-18 is the baseline."
  - Spine `ARCHITECTURE-SPINE.md:431-432` (§7a) — "The baseline is the §7 map as of closure commit `2b63ab9`." (`git cat-file -e 2b63ab9^{commit}` → exists.)
  - Spine `ARCHITECTURE-SPINE.md:398` (I11 row, corrected 2026-09-08 per §10 and `.memlog.md:43`) — now names `IdentifierValidatorTests` (exists; `IdentifierValidatorTests.cs:15-23` has the GUID-shaped case) and reassigns `PartyAggregateCompositeTests` to ULID acceptance only. I1a and I13 rows were also rewritten.
  - Spine `ARCHITECTURE-SPINE.md:278-280` — a successor test may be named only when "approved by an owner recorded in the spec"; I20 (`:306-314`) makes that approval a ledger row; §7b (`:455-459`) states the approval table does not exist yet.
- **Why it fails the checklist:** Item 2 (enforceable) and internal consistency. The spine now contains two §7 maps in time — the frozen one at `2b63ab9` and the live one — and the text does not say which an executor's clause-6 checklist and I16 re-run set are computed from. The I11 correction is an accuracy fix (the baseline witness never had a GUID case), yet formally it substitutes a witness for a baseline claim, which I18/I20 say requires a recorded approval that cannot exist yet. The dangling "until it lands … as amended 2026-08-18" conditional at `:276-277` is also dead text now that `2b63ab9` has landed. Fix: state that post-closure §7 edits are additive-only corrections that never remove a `2b63ab9` surface, that the live map is the operative clause-6/I16 set, and that witness *replacements* need an I20 row; delete the "until it lands" clause.

### F-4 — Operational envelope: degraded-response middleware and DAPR health checks have no owner row, no invariant, and no deferral

- **Severity:** medium
- **Disposition if updating:** autofix
- **Evidence:**
  - Spine §2 table `ARCHITECTURE-SPINE.md:85-93` — no row for degraded-response / health-check ownership; `rg -n "health" ARCHITECTURE-SPINE.md` → no matches. Telemetry appears only as the inherited low-cardinality convention (`:78`).
  - `story-8-3-platform-api-prerequisite-matrix.md:104` — G1/G2 "EventStore degraded response and DAPR health checks", status `needs-additive-api`, evidence `src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs`, `HealthChecks/DaprStateStoreHealthCheck.cs`, `HealthChecks/DaprPubSubHealthCheck.cs`; dependent stories 8.5, 8.8, 8.10.
  - `deferred-work.md:802` (DW-98, `8.8-runtime-boundary-cleanup` rollback) — "Keep the Parties degraded middleware and health checks…".
  - `references/Hexalith.AI.Tools/hexalith-llm-instructions.md:131-134` — a domain module "must not re-implement … telemetry sources, health checks, or event-subscription plumbing."
  - `src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs:39` — the middleware also carves out `/tenants/events`, i.e. it is coupled to the I1 pub/sub door the amendment just ratified.
- **Why it fails the checklist:** Item 8 (every dimension the altitude owns is decided, deferred, or an open question). Deployment/environments and infra/provider are now explicitly deferred (§9 rows 1–2); operations is not. The G1/G2 surface is a Parties-local operations mechanism that the house rule says the domain must not own, that 8.8's rollback clause names, and that §2 — the table which "wins" over the parent — is silent on. Two executors (8.6-residual touching the degraded path for the pub/sub door; 8.8 adopting G1/G2) have no owner sentence to arbitrate against. Fix: add a §2 row "Degraded-response middleware + DAPR health checks → EventStore SDK (G1/G2); Parties keeps the status-header and health-tag *policy* until parity" and reference it from the §7 I2 or I3 row, or add a §9 row deferring it to 8.8 with the revisit condition.

### F-5 — I20's decision surface does not exist, so every approval the amendment routes to it is currently undecidable, and the gap is tracked only in prose

- **Severity:** medium
- **Disposition if updating:** defer (record) / discuss
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:306-314` — approvals "decidable only as a row in the Story 8.3 matrix reconciliation ledger's **approval table**".
  - Spine `ARCHITECTURE-SPINE.md:455-459` (§7b) — "The approval table does not yet exist … creating it … is the first companion follow-up of this amendment. Until it exists, no approval those invariants name can be claimed. Owner: the reviewer gate."
  - `story-8-3-platform-api-prerequisite-matrix.md` — `rg -n "approval table|Approval table"` → no matches; the only approval-shaped table is the accepted-deferral table at `:90-96`, which has no decision/human/date columns.
  - §9 `ARCHITECTURE-SPINE.md:484-494` — no row for the table's creation; `deferred-work.md` — no DW item for it (grep `approval table` → none).
  - Dependents: §7 I1a row (`:388`) requires I20 re-approvals; §7b I19a (`:449-454`) requires an I20 row before 8.7 may adopt the engine; I1 route-list changes (`:133-135`).
- **Why it fails the checklist:** Item 2 (enforceable) and Item 3 (deferral cannot let units diverge). V38 is closed in wording but not in mechanism: the approval that unblocks I19a, I1a and I1 changes cannot be recorded anywhere. "Owner: the reviewer gate" is a role, not a named human or ledger item, which is the very pattern I20 was written to reject. Two units can diverge on whether a pre-table "owner approved" line is honored (I20 says no; nothing schedules the table). Fix: add a §9 row (owner: 8.3 matrix owners; revisit: before first I17 activation) or a DW item, and name the eight decision-row headings in it. Also note: I20's list does not cover §2's "recorded SCP or owner authority" precedence claims (`:95-97`) or I17's "explicitly hands over" between specs (`:268-269`); say whether those are SCP artifacts or I20 rows.

### F-6 — I11 states the tombstone-allocation refusal in the present tense; the brownfield `CreateParty` handler returns `NoOp` on any existing state, and §7 defers the refusal to 8.8

- **Severity:** low
- **Disposition if updating:** autofix
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:225-229` — "allocation of an aggregate ID that is tombstoned under I10 **is rejected** at the command handler with a typed, stable outcome".
  - `src/Hexalith.Parties/Domain/PartyAggregate.cs:693-696` — `// AC#3: Idempotent — if state already exists, party was already created` `if (state is not null) { return DomainResult.NoOp(); }` — no erasure-status branch on create.
  - Spine `ARCHITECTURE-SPINE.md:398` (§7 I11 row) — "`8.8-runtime-boundary-cleanup` owns … the command-handler tombstone-allocation refusal."
- **Why it fails the checklist:** Item 5. The rule text and the §7 row disagree on tense; an 8.6-residual executor reading I11 alone may "fix" the NoOp into a typed rejection (an I6-visible behavior change) while §7 gives that change to 8.8. Fix: "…is rejected (target; owner `8.8-runtime-boundary-cleanup`, §7 I11) — today `Handle(CreateParty)` returns `NoOp` on existing state, which is I6-preserved until 8.8 lands the refusal."

### F-7 — Inherited Invariants table omits the parent's Testing and Package-references conventions; §4 clause 6 is weaker than the parent's parity-coverage list, and the package-mode default is an unrecorded soft departure

- **Severity:** low
- **Disposition if updating:** autofix
- **Evidence:**
  - Parent `epic-7…/ARCHITECTURE-SPINE.md:130` — "Package references | Prefer project references to root-declared `references/` submodules…"; `:132` — "Testing | Parity tests must cover old/new code paths, duplicate/out-of-order replay, stale/degraded freshness, key-unavailable reads, erased party behavior, and rollback."
  - Spine `ARCHITECTURE-SPINE.md:64-78` — lists AD-1..6, five parent inherited invariants, and two conventions (public contracts, logs/telemetry) only.
  - Spine `ARCHITECTURE-SPINE.md:340` (§4 clause 6) — "the I5–I10/I8 items relevant to that story" — no mention of key-unavailable reads or rollback as mandatory parity coverage.
  - `story-8-3-platform-api-prerequisite-matrix.md:69` — default Release graph is package mode (`UseHexalithProjectReferences=false`); I4 (`:167-172`) blesses either mode.
- **Why it fails the checklist:** Item 7. The table says parent decisions "bind here read-only, by their original IDs — they are not re-derived below", so an unlisted convention is silently dropped rather than inherited. The Testing convention is the one that most directly constrains clause 6; the Package-references convention is de facto departed from (package default) without a recorded supersession — a benign departure consistent with `hexalith-llm-instructions.md:242-250`, but the spine claims "the only recorded supersession is the gated Authentication/tenant-claims slice". Fix: add both conventions to the table; for Package references, note the dual-mode/I4 stance as the recorded refinement.

### F-8 — §7 heading and intro still date the map 2026-08-18 although four rows were corrected 2026-09-08; the I7 row names no surface for the new door/certificate rules

- **Severity:** low
- **Disposition if updating:** autofix
- **Evidence:**
  - Spine `ARCHITECTURE-SPINE.md:374` — "## 7. Story 8.10 Closure Evidence Map — 2026-08-18"; `:380-381` — "Map corrected 2026-08-18 after spine validation (§8)"; §10 `:510` — "§7 I1a/I4/I11/I13 rows corrected" on 2026-09-08.
  - Spine `ARCHITECTURE-SPINE.md:394` (I7 row) — names `PartyAggregateConsentTests`, `PartyAggregateErasureTests`, `ErasureVerificationServiceTests`; nothing guards the door enumeration or the "MCP fails closed" clause (the MCP tests exist under `tests/Hexalith.Parties.Mcp.Tests/` but are unnamed).
- **Why it fails the checklist:** Internal consistency. Harmless to the parser, but a reader of §7 alone is told the map is a 2026-08-18 artifact; the I18 baseline discussion (F-3) makes the dating load-bearing. Fix: "— 2026-08-18, rows corrected 2026-09-08 (§10)"; once F-1 is fixed, consider naming the MCP tool test class that pins `delete_party` as deactivation.

### F-9 — Companion drift the amendment now contradicts (not spine-fixable; record and offer)

- **Severity:** low
- **Disposition if updating:** defer (offer to user)
- **Evidence:**
  - `_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md:22` — "amended 2026-08-18"; `:67` — "Spine invariants I1–I18 bind every slice"; `:101-113` — OQ-1, OQ-2, OQ-5 still listed as open although I10 (`:213-220`, owner of the freshness grammar), I19 (`:288-297`, mapping store = class (b)), and I20 (`:306-314`, decidable approvals) answer them; OQ-3/OQ-4 are now §9 rows 1 and 3.
  - `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml:16` — comment names `app.MapTenantEventSubscription()`; `src/Hexalith.Parties/Program.cs:69` and spine I1 (`:144`) name `MapEventStoreDomainEvents()`.
  - `_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md:28` — "I1 (… only `eventstore -> POST /process`)" — a one-route reading of a 13-route invariant.
- **Why it fails the checklist:** Item 6 (spec coverage). The spine covers CAP-1..6 and answers OQ-1/2/5, but the SPEC that "cites" it still points executors at I1–I18 and open questions that are closed. Since the SPEC is the work contract executors read first, the drift is a divergence vector even though the spine is right.

---

## Previously-open findings — closure check

| Finding | Closed by | Status |
| --- | --- | --- |
| V28 | §5 `:354-365` binds ledger children by `source_spec`/`origin`/`reason` and rollback-named surfaces (DW-100/101/104 verified in `deferred-work.md:812-850`); disjointness from touched files | Closed |
| V30 | I19a `:298-305`; §7b `:449-454` | Closed (mechanism gated on F-5) |
| V31 | I10 `:213-220` — single grammar, G6 owner once `available`, spec-8-6 three-state emitter (verified: `8-6-projection-and-query-sdk-migration.md:34,149-150`; enum retains six values for wire compat) | Closed |
| V32 | I19 `:288-297` — `PartyMemoryUnitMappingStore` class (b) (verified `DaprClient` direct use, `PartyMemoryUnitMappingStore.cs:80`; DW-81 at `deferred-work.md:664-670`) | Closed |
| V33 | I7 `:189-195` — certificate/report one owned versioned shape (types exist in `Parties.Contracts/Security/`) | Closed |
| V34 | §1a `:75`, §2 `:97-108`, frontmatter `related:` `:13` | Closed |
| V35 | §1a `:50-78` — paradigm + table by original IDs | Closed (F-7 low gap) |
| V36 | I1 `:142-147` — pub/sub door ratified (verified `Program.cs:59-69`) | Closed |
| V38 | I20 `:306-314` | Closed in text; **unenforceable until the table exists** (F-5) |
| V39 | §7 I1a row `:388` names all three AppHost-naming deferrals | Closed |
| V44 | §7 I13 row `:400` — DW-111 runtime-observation remainder (verified `deferred-work.md:904-910`) | Closed |
| V46 | §7 I11 row `:398` — `IdentifierValidatorTests` (verified GUID case `:15-23`) | Closed (F-3 asks how it relates to the frozen baseline) |
| V47 | §4 clause 2 `:327-330` adds Memories, Tenants | Partial — PolymorphicSerializations still omitted, `deploy` spurious (F-2) |
| V48 | I11 `:225-229` — format ≠ allocation | Closed (F-6 tense) |
| V49 | I1 `:136-142` — external ACL copies bound | Closed |
| V51 | §4 clause 3 `:331-335` — exercised rollback, re-exercise on disturbance | Closed |
| V52 | I7 `:182-188` | **Not closed — closed on a false premise; introduces a new divergence** (F-1) |
| V53 | I16 `:258-264` — claim-scoped stop | Closed |
| V54 | §2 `:110-116`, I14 `:240-242` | Closed |
| V57 | I10 `:211-212` — "static write-policy helper" (verified `ReadModelWritePolicy.cs:28 public static partial class`) | Closed |
| V58 | I10 `:205-206` — "Parties compatibility mapping … (Epic 7 AD-2)" | Closed |
| V27 / V29 / V40 | I4 row `:391` prints the authorized set (matches `PlatformApiPrerequisitesTests.cs:23-34`, matrix `:69-72`, signoff `2026-09-08` lines); HEAD `a32cb422…` correctly named as unstamped; frontmatter updated | V29/V40 closed; V27 correctly open |
| V45 | §7a `:420-425` — FrontComposer stamp split recorded (verified constants `a0acb78f…` vs `f0c3b6fd…`) | Recorded, open by design |

New divergence introduced by the amendment: **F-1** (I7 `delete_party`). Near-misses that are wording, not divergence: F-2, F-6.

---

## Checklist disposition

| # | Item | Disposition | Notes |
| --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below; misses none | partial | §5 children, I19/I19a, I10, I7 shape, I16 scope, §4.3 all land. Misses: clause-2 submodule set (F-2), G1/G2 ownership (F-4). |
| 2 | Every invariant's rule is enforceable and prevents its divergence | partial | I7 door rule prevents the wrong thing (F-1); I20 has no decision surface (F-5); I18 baseline vs live §7 undefined (F-3). |
| 3 | Nothing under Deferred (§9) could let two units diverge | pass | Every §9 row names one owner and a revisit condition; none grants permission. Gap is what is *missing* from §9 (I20 table, F-5), not what is in it. |
| 4 | Named tech is verified-current | pass | Spot-checked: Fluent UI `5.0.0-rc.5-26219.1`, Dapr `1.18.5`, Aspire `13.5.3`, SDK `10.0.400` all match the Builds catalog / `global.json`; §9 correctly attributes the stale Stack table to the parent. |
| 5 | Ratifies rather than contradicts the brownfield | fail | F-1 is a direct contradiction with `PartiesMcpTools.cs` and `docs/api-contracts.md`; F-6 is a tense mismatch. Everything else spot-checked (ACL tuples, `MapEventStoreDomainEvents`, three-state freshness, `ReadModelWritePolicy`, mapping store, all 30 test classes, gitlinks) holds. |
| 6 | Covers the driving SPEC's capabilities | pass | CAP-1 (I4/I16/§7), CAP-2 (I1/I2/I3/I7/I9/I10 8.6 rows), CAP-3 (I7/I8/I19a/§9 r1), CAP-4 (I1a/I2/I11/§9 r3-4), CAP-5 (I13/I14/§9 r5), CAP-6 (I1 external ACL/§9 r2). OQ-1 → I10, OQ-2 → I19, OQ-5 → I20 answered; OQ-3/OQ-4 → §9. SPEC itself is stale (F-9, companion). |
| 7 | Parent inheritance: no weakening; original IDs; supersession correctly scoped | pass | AD-1..AD-6 by original ID with local bindings; five parent inherited invariants carried; supersession = Authentication/tenant-claims slice only, Contracts half "fully binding", `Hexalith.Parties.Authentication` still in-repo (verified `ls src`), 8.3 G7/G9 still `needs-additive-api`. Low gap: two parent conventions unlisted (F-7). |
| 8 | Every owned dimension decided, deferred, or open | partial | Deployment/environments and infra/provider explicitly deferred (§9 r1-2); KMS deferred (§9 r1); telemetry inherited (§1a). Operations — degraded-response/health-check ownership — silent (F-4). |
| — | Internal consistency, no template text, no placeholders, no §2/§3/§7/§9 contradictions | partial | Cross-refs §1a/§7a/§7b/§8/§9/§10, I19/I19a/I20, "eight decision rows" all resolve; no placeholders (`TODO|TBD|XXX|{{|[[` → none). Contradictions: I7 vs brownfield (F-1); I11 tense vs §7 I11 row (F-6); I18 `:276-277` dead conditional and §7 dating (F-3, F-8). |

## Finding count

| Severity | Count | IDs |
| --- | --- | --- |
| critical | 0 | — |
| high | 1 | F-1 |
| medium | 4 | F-2, F-3, F-4, F-5 |
| low | 4 | F-6, F-7, F-8, F-9 |
| **total** | **9** | |

Autofix candidates: F-1, F-2, F-4, F-6, F-7, F-8. Discuss: F-3, F-5. Defer/offer: F-9.
