# Reviewer Gate — Parent-Inheritance Review
- reviewer: PARENT-INHERITANCE
- date: 2026-09-08
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- parent: `_bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`
- child-memlog: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/.memlog.md`
- authority-searched: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`
- mode: VALIDATE-only
- brownfield: `Hexalith.Parties.Authentication` still exists in-repo (tracked rollback surface)

## Verdict

**ALIGNED WITH RECORDED SUPERSESSION**

The Epic 7 parent remains binding in substance. Child I1–I18 and §4–§5 do not
silently weaken AD-1..AD-6, the EventStore gateway, consumer self-scope/GDPR,
event-sourced replay, or submodule-release sequencing. They usually tighten
those rules (identity-stamped parity, six-clause readiness gate, ACL owner
file, deferral-rollback precedence).

The sole recorded divergence from a parent inherited invariant is the Class A
shared-anchor statement that Epic 7 would not re-open
`Hexalith.Parties.Authentication`. Child §2 states that supersession, cites
the 2026-07-16 G7/G9 owner decision, and keeps it **gated, not executed**.
Brownfield matches that gate: the Authentication project is still tracked,
solution-referenced, and the Story 8.3 G7/G9 row remains
`needs-additive-api`.

This is not CONFLICT: no child I-n / §2 owner row / §4–§5 rule authorizes an
ungated override of a parent AD. Residual defects are (1) an over-broad Class
A label that does not restate the Contracts-half carve-out the SCP itself
requires, and (2) missing formal Inherited Invariants / parent AD-ID listing
required at epic altitude.

## Mapping

Parent loaded first. Child coverage is from the 2026-08-18 spine text (no
local override invented). Relation key: **inherits** = restated or implied
without relaxing the parent rule; **tightens** = adds a stricter gate;
**supersedes (gated)** = recorded owner-authorized destination change that
is still blocked on parity/rollback; **CONFLICT** = weakens or contradicts
without a gated, cited authority.

| Parent AD / inherited invariant | Child coverage | Relation |
| --- | --- | --- |
| AD-1 adapter-first (parent spine 67–74): introduce/consume a Parties-compatible adapter, prove old/new parity, delete later | I3 (103–106) keeps local rollback paths until parity + proven rollback; I16–I18 (156–187) stamp and baseline that evidence; §4 clauses 3/4/6 (202–207) require rollback + parity before deletion. The word “adapter” is not restated (see F5). | inherits / tightens |
| AD-2 projection ownership (76–84): EventStore owns checkpoint/rebuild/freshness primitives; Parties owns mapping to `ProjectionFreshnessMetadata` / UI `StatusKind` until a separate public-contract story | §2 (55): Parties keeps projection/query **semantics**; mechanics → EventStore SDK. I9–I10 (128–137) keep replay, checkpoints, `ProjectionFreshnessMetadata`, last-known. G6 row (57) names EventStore.Contracts for envelopes/freshness; G4 (59) names FrontComposer for status/freshness UI primitives — both still `needs-additive-api` in the 8.3 matrix. I5 (115–116) freezes Client/Contracts/UI RCL public shape. | inherits / tightens |
| AD-3 crypto placement (86–94): generic payload-protection in EventStore/shared security; Parties retains GDPR policy, party commands, key semantics, adapters until the 7.6 harness approves migration | §2 (56): GDPR **policy** stays; generic crypto/key engine → EventStore/shared DataProtection. I7–I8 (119–125) keep legal semantics and payload compatibility. I3 keeps the crypto rollback path. §7 I8 (254) still names `CryptoKeyManagementCompatibilityHarnessTests` and defers extraction to `8.7-data-protection-extraction`. Child does not cite the Epic 7 “7.6 harness” ID; 8.7 + I8 are the successor gate. | inherits / tightens |
| AD-4 utility destinations (96–105): Commons ServiceDefaults / diagnostics / HTTP / paging / string helpers; Memories only for search scoring; FrontComposer for UI lifecycle | §2 (54–59) routes service defaults, correlation/ProblemDetails → Commons; paging primitives → Commons **and cites “Epic 7 AD-4”** (57); MCP plumbing → FrontComposer MCP host on Commons.Http (G11); UI status/freshness/grid/picker → FrontComposer (G4). Additional destinations (Builds, EventStore.Aspire, EventStore.Authentication, EventStore.Contracts, platform-ops) are named owners for other capability classes, not a reroute of AD-4’s listed utilities into arbitrary packages. | inherits / tightens |
| AD-5 release-before-reference (107–114): land additive API in the owning submodule, validate its gates, update root pointer/CPM, then adopt in Parties | I4 (107–112): no Parties source migration from an unapproved/unidentified dependency; `available` or a checked-out file is not consumption evidence; exact package version or root gitlink SHA required. I16 (156–167) re-opens parity when a retained identity changes. §4 clause 1 (196–199) requires the consuming story’s identity to match the 8.3 matrix before source changes. | tightens |
| AD-6 compatibility and rollback (116–123): each story names adapter switch / pointer rollback / dual-read / deferred deletion; preserve data, projection state, gateway routes, PII redaction | I3 (103–106) local rollback until parity; I1a (88–97) AppHost remains a rollback surface and deferral rollback beats parity-based retirement; §4 clause 3 (202) requires a named revert; §2 (64–68) keeps Authentication as the gated rollback surface. §1 (44–45) refuses rollback of already-ratified 8.2–8.5 work (Correct Course §4.2) — historical ratification, not a future-story exemption. | inherits / tightens |
| Class A shared-anchor boundary (parent 62; architecture.md 696–707; 2026-06-28 SCP A3): Epic 7 does not re-open anchors already routed to `Hexalith.Parties.Contracts` or `Hexalith.Parties.Authentication` | §2 (62–68) records a G7/G9 owner-authorized supersession: tenant-claim **anchors route to** EventStore.Authentication, while `Hexalith.Parties.Authentication` **remains in-repo** until `8.8-runtime-boundary-cleanup` proves parity. Authority: `sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md` (approved 2026-07-16). Contracts half is not in that SCP’s deletion grant (SCP 132–134, 305–309). Child I5 keeps Contracts public shape but does not restate the “define once in Contracts / do not re-hardcode” rule (see F1). | supersedes (gated) — Authentication/tenant-claims only |
| EventStore gateway boundary (parent 59; architecture.md 684–686): no public Parties host API; commands/queries enter through EventStore gateway | I1 (73–87): no public API on the domain-service host; EventStore gateway over DAPR; deny-by-default ACL; single `eventstore` app ID; closed route list; single authoritative ACL file; route-list changes need recorded owner approval. I2 (98–100): EventStore SDK host shape. | inherits / tightens |
| Consumer self-scope and GDPR (parent 60): platform adoption must not weaken own-data checks, erasure, PII, or regulated copy | I6 (117–118) `aggregateId == party_id` defense in depth; I7 (119–121) consent ≠ lawful basis, Art.18 guards, two-front-door erasure; I8 (122–125) payload formats, key zeroing, no-leak diagnostics, Art.20/30; I14 (150–151) GDPR copy honesty. | inherits / tightens |
| Event-sourced projection replay (parent 61): at-least-once, duplicate tolerance, last-known fallback, degraded freshness | I9 (128–129) replay-from-zero, per-read-model checkpoints, set-based idempotency, duplicate/out-of-order; I10 (130–137) stale/degraded last-known, freshness metadata, erased-index tombstone, rebuild-before-delete. “Replay-from-zero” is stricter than “at-least-once”; it does not relax it. | inherits / tightens |
| Submodule governance (parent 63): EventStore, Commons, Memories, FrontComposer, Tenants require explicit story ownership, non-recursive handling, release sequencing | I12 (142–144) root submodules only; I4/I16 pin gitlink/package identity; §4 clause 2 (200–201) requires listing touched repos. The clause-2 enumeration is `EventStore / Commons / FrontComposer / Builds / deploy` — **Memories and Tenants are omitted** (see F3). | inherits / incomplete listing |
| Parent convention — public contracts additive only (131–132) | I5 (115–116) freezes Client + Contracts + three UI RCL public shapes; §3 header (114) allows “intentionally versioned” behavior. Versioning is not a silent rename: I5 still binds the public surface. | inherits |
| Parent convention — testing parity coverage (133) | I8–I10 + §4 clauses 4/6 name old/new, replay, freshness, key-unreadable, erased-party, and rollback evidence. I18 forbids deleting the baseline test that guards a deletion. | tightens |
| Parent convention — logs/telemetry PII-free, low-cardinality (134) | I8 “no-leak diagnostics” only. No child convention restates low-cardinality / no party names / no raw key aliases (see F4). | inherits (silent) |
| Parent convention — named per-story rollback (135) | §4 clause 3; I3; I1a. | inherits / tightens |
| Parent paradigm — adapter-first strangler (49–53) | Child purpose (17–18) is domain-focus extraction after Epic 7. Remaining deletions are gated by I3/I4/§4, not by a new un-gated delete-first rule. | inherits sequentially |

### Inherited Invariants table — decision

The child has **no** `## Inherited Invariants` table and no `binds:` listing of
parent AD IDs.

The epic-altitude template requires that table, with parent AD IDs never
renumbered (`.claude/skills/bmad-architecture/assets/spine-template.md` 11,
24–30; skill inheritance rule at `SKILL.md` 31). The parent spine itself
models the table (parent 55–63).

§1 (36–37) and frontmatter `related` (14) cite the Epic 7 spine as the
platform-adoption boundary Epic 8 continues from. The memlog (9, 21) says the
parent stays binding read-only. §2 (57) cites AD-4 by ID. That is **not**
equivalent to listing AD-1..AD-6 and the five parent inherited invariants
under their original IDs.

**Decision:** the absence is a finding (silent inheritance / re-derivation as
I1–I18), not acceptable solely because §2 + memlog mention the parent. See F2.
It is not CONFLICT: the substance of those parent rules is present and, except
for the recorded Class A slice, not relaxed.

## Authority check — G7/G9 Class A supersession

Searched `_bmad-output/planning-artifacts/` for the 2026-07-16 tenant-claims
ownership SCP. Found:

`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`

| Claim | Evidence |
| --- | --- |
| Status approved | Frontmatter `status: approved`, `approval: approved`, `approved_at: 2026-07-16T00:43:36+02:00` (SCP 8–10); §8 (297–304) Administrator approval |
| Destination | EventStore owns public tenant claim constant + lightweight `Hexalith.EventStore.Authentication` transformation; Commons owns `UniqueIdHelper.IsValidUlid(string)` (SCP 48–57, 301–304) |
| Deletion still gated | Story 8.4 “deliberately preserving `Hexalith.Parties.Authentication`” (SCP 23–24, 97–99); success criterion 8: “Rollback to `Hexalith.Parties.Authentication` is exercised before deletion” (263); §8: approval “closes only the ownership decision” — delivery, pins, parity, and exercised rollback remain open before Parties may delete the project (305–309) |
| Contracts half not granted | “`PartiesClaimTypes.EventStoreTenant` remains a compatibility alias until an intentionally versioned public-contract removal is approved; deleting the authentication implementation does not authorize an accidental Contracts breaking change” (SCP 132–134) |

Child §2 (62–68) matches the gated destination and the in-repo rollback
surface. It does **not** quote the SCP path in `related:` (child 11–15) or
restate the Contracts carve-out. Precedence (“this table wins — provided the
divergence carries recorded SCP or owner authority”, 62–64) is itself gated
and does not invent a blanket override.

Memlog line 9 originally claimed Story 8.4 **deleted** Authentication; line 21
corrects that. The current spine follows the correction, not the superseded
memlog sentence.

## Brownfield check — `Hexalith.Parties.Authentication`

The gated rollback surface **still exists** in the Parties repo. It has not
been silently retired.

| Check | Result |
| --- | --- |
| `git ls-tree HEAD src/Hexalith.Parties.Authentication` | tracked tree `6f26e457…` |
| Tracked sources | `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj`; `PartiesClaimsTransformation.cs` |
| Focused tests | `tests/Hexalith.Parties.Authentication.Tests/Hexalith.Parties.Authentication.Tests.csproj` tracked |
| Solution | `Hexalith.Parties.slnx` 26 includes the project |
| Implementation | `PartiesClaimsTransformation` still implements `IClaimsTransformation` and reads `PartiesClaimTypes.EventStoreTenant` (`src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs` 11–18) |
| 8.3 matrix G7/G9 | row “Tenant claims transformation” still `needs-additive-api`; “Keep the `Hexalith.Parties.Authentication` rollback path; it remains after Story 8.4” (`story-8-3-platform-api-prerequisite-matrix.md` 107) |

This confirms §2’s “approved but gated, not executed” sentence against the
tree. No finding that the supersession has been executed.

## Findings

### F1 — Class A supersession is gated and authorized, but labeled as the whole boundary
- Severity: high
- Parent AD: Inherited Invariant “Class A shared-anchor boundary” (parent spine 62) covering **both** `Hexalith.Parties.Contracts` and `Hexalith.Parties.Authentication`; fuller rule in `architecture.md` 696–707 (define shared anchors once in Contracts; Authentication is the JWT-transform exception, not a license to re-open Contracts).
- Child text: §2 62–68 — “That supersession of the Epic 7 Class A anchor boundary is approved but gated, not executed (owner decision 2026-07-16, G7/G9): the tenant-claim anchors route to EventStore.Authentication, while `Hexalith.Parties.Authentication` remains in-repo…”
- Why the supersession is still gated: the operational sentence keeps the project in-repo until `8.8-runtime-boundary-cleanup` proves parity; I3/I4/§4 still block deletion; the 2026-07-16 SCP (305–309) and the 8.3 G7/G9 row remain `needs-additive-api`; brownfield confirms the project is tracked. This is **not** a silent execution.
- Why it still weakens the parent record: (1) the label “the Epic 7 Class A anchor boundary” names the **whole** two-package invariant, while recorded authority only reroutes the Authentication/tenant-claims slice; (2) the SCP’s Contracts carve-out (132–134) is not restated, and I5 freezes public shape without restating “never re-hardcode shared anchors in a second project”; (3) `related:` / §1 authoritative set (32–39) omit the 2026-07-16 SCP path, citing only “owner decision 2026-07-16”. An executor could treat Contracts Class A as also superseded. No local override of that Contracts half is invented here — the gap is the missing carve-out.

### F2 — No Inherited Invariants table listing parent AD IDs
- Severity: medium
- Parent AD: all of AD-1..AD-6 plus the five-row Inherited Invariants table (parent 55–63). Template/skill: list parent AD IDs, never renumbered (spine-template.md 24–30; SKILL.md 31). Frontmatter `binds` at epic altitude should include inherited parent AD ids (template 11).
- Child text: no `## Inherited Invariants` section; no `binds:`; I1–I18 re-derive gateway, GDPR, replay, rollback, and release-sequencing under new IDs. Parent is cited in frontmatter 14, §1 36–37, §2 57 (AD-4 only), and memlog 9/21.
- Why it weakens/contradicts: this is silent inheritance, not a rule conflict. Combined with §2’s “this table wins” precedence (62–64), a later §2 edit could be mistaken for authority to drop an unlisted parent AD. The memlog companion cannot substitute for the spine table the epic-altitude template requires. Substance of the parent ADs is otherwise present (see Mapping), which is why this is not CONFLICT.

### F3 — §4 touched-repo list drops Memories and Tenants
- Severity: medium
- Parent AD: Inherited Invariant “Submodule governance” (parent 63) — EventStore, Commons, **Memories**, FrontComposer, and **Tenants** changes require explicit story ownership, non-recursive handling, and release sequencing.
- Child text: §4 clause 2 (200–201) — “Parties + each of EventStore / Commons / FrontComposer / Builds / `deploy` that the change edits.” I12 (142–144) says “root submodules only” but does not name Memories or Tenants. §7 I7 (253) still assigns Memories cleanup-race debt to `8.6-residual-review-debt`.
- Why it weakens/contradicts: the readiness-gate enumeration can be read as exhaustive. A spec that edits Memories or Tenants could omit them from clause 2 and still look §4-complete. That weakens the parent’s explicit-ownership convention for those two modules. I4/I16 still bind any dependency identity if someone names it; the gap is the gate list, not an invented permission to skip release sequencing.

### F4 — Parent logs/telemetry convention is not restated
- Severity: low
- Parent AD: Consistency convention “Logs and telemetry” (parent 134) — low-cardinality, PII-free; no event payloads, party names, identifiers, raw key aliases, destroyed-key details, or decrypted values.
- Child text: I8 (122–125) “no-leak diagnostics” in the protected-payload invariant only. No conventions table.
- Why it weakens/contradicts: silent inheritance. I8 covers payload-protection diagnostics, not general host/UI/MCP telemetry cardinality. The 2026-07-16 SCP (85) also requires bounded claim-transformation logs; child §2 does not carry that sentence. Not a contradictory child rule.

### F5 — AD-1’s adapter-introduction step is implied, not named
- Severity: low
- Parent AD: AD-1 (67–74) — every implementation story introduces or consumes a Parties-compatible **adapter first**, proves parity, then removes local code.
- Child text: I2 (98–100) retains “payload-protection hooks the SDK cannot own”; I3 (103–106) keeps “local rollback paths”; §4 is parity-then-delete. “Adapter” does not appear.
- Why it weakens/contradicts: remaining Epic 8 work is extraction after 8.3 APIs. I3’s rollback path is the deferred-deletion half of AD-1/AD-6, not a delete-first rule. The missing adapter-first wording is a naming gap under F2’s silent inheritance, not an authorization to skip a compatibility surface. No CONFLICT.

## Non-findings (checked, not raised)

- AD-2 vs G4/G6: destination change is the “separate public-contract story” AD-2 already allowed; both matrix rows remain `needs-additive-api` except delivered G4 slice F (shell), which I13 still does not treat as full parity.
- AD-3 vs 8.7: successor gate (I8 harness + 8.7 deferral) replaces the parent’s 7.6 harness ID; G5 in the 8.3 matrix remains `needs-additive-api`; no child text authorizes crypto deletion without parity.
- AD-4 new destinations (Builds, EventStore.Aspire, platform-ops): different capability classes with named owners; not a reroute of paging/ServiceDefaults/correlation into arbitrary packages.
- I9 “replay-from-zero” vs parent “at-least-once”: stricter, not weaker.
- §1 “no rollback of completed 8.2–8.5 work”: Correct Course ratification of landed hygiene/cutover, not a §4 exemption for remaining deletion-heavy specs.
- Memlog line 9 vs line 21: append-only self-correction; current spine matches the correction.

## Counts

| Severity | Count |
| --- | --- |
| critical | 0 |
| high | 1 |
| medium | 2 |
| low | 2 |

## Reviewer close

VALIDATE-only. No spine, memlog, or project file was modified except this
review file. No local override of a parent AD was invented. The recorded
G7/G9 Class A supersession remains explicitly gated and is still visible in
the brownfield tree.
