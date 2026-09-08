# Reviewer Gate — Adversarial Review
- reviewer: ADVERSARIAL
- date: 2026-09-08
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` (updated 2026-08-18; I16–I18 and I1/I1a/§4/§5 amendments in force)
- mode: VALIDATE-only
- companions read: spine `.memlog.md`; Epic 7 parent spine; `deferred-work.md` (DW-96–DW-104 and accepted-closure fields); `story-8-3-platform-api-prerequisite-matrix.md` (G4–G9/G11 rows); `spec-epic-8-domain-focus/SPEC.md` (CAP-2–6, OQ-1–5); landed `spec-8-6` / `spec-8-7` / `spec-8-8` / `spec-8-9`; project-context GDPR rules
- prior closed IDs not re-litigated: ADV-1/I16, ADV-2/I17, ADV-3 I1/I1a, ADV-4/I18, P1–P8 (re-tested only; residuals filed as P9+)

## Verdict

**CONDITIONAL FAIL** for authorizing the first remaining-work I17 activation specs.

The 2026-08-18 doors stay closed on today's letter: an identity bump cannot silently bless an earlier deletion (I16), a ledger sweep cannot work an accepted deferral without a six-clause spec (I17), AppHost retirement cannot ignore the three named rollback clauses (I1a), and a baseline surface cannot be weakened then cited as the parity that deletes its implementation (I18). Those unit-pairs are not reconstructible as stated.

What the amendments never bound — and what the 2026-09-06 ledger migration made first-class — is a second population of units: independently shippable children (`DW-100`–`DW-104`) that inherit the §4 *working* contract (I17) but not the §5 *order/concurrency* contract (that sentence still names only the five accepted closure deferrals). Combined with the still-open V8/V9/V10/V12/V25 holes the memlog parked until "G-row APIs land" / "first deferral-spec authoring", two letter-compliant units can still ship incompatible shared-data shapes, two owners of one entity, conflicting state-mutation paths, and GDPR semantic splits. SPEC.md OQ-1 through OQ-5 are the spine admitting the same gaps in the work contract and then leaving them as questions.

Do not treat "I16–I18 exist" as a license to author `8.6-residual` / `8.7` / `8.8` / `8.9` / `DW-10x` activation specs. Close P9–P13 in the spine first.

## Closed prior attacks (one line each: still-closed / residual)

- **ADV-1 / I16 identity stamp** — still-closed: a later EventStore/Builds/gitlink bump re-opens every claim stamped at a different identity of that dependency; unvalidated is a stop for further deletions *relying on that dependency*. Residual → **P19**.
- **ADV-2 / I17 deferral gate inheritance** — still-closed for accepted closure deferrals and for any item that deletes or weakens an I18 baseline surface or I1–I15-guarded code: work requires a six-clause spec. Residual → **P9** (§5 sequence did not travel with the gate).
- **ADV-3 I1 / I1a ACL + AppHost** — still-closed for the two-copy local ACL and for retirement that ignores `8.6-residual-review-debt` / `8.8-runtime-boundary-cleanup` / `external-runtime-deployment`. Residual → **P15**, **P22**.
- **ADV-4 / I18 baseline weaken** — still-closed: weaken/delete is invalid in the same changeset *or across changesets* while the guarded deletion is pending or later relies on the surface; successor approval cannot be the authoring executor alone. Residual → **P21**.
- **P1 unvalidated parking lot** — still-closed as a permanent disposition: "An unvalidated marker is a stop, not a state". Residual → **P19** ("relying on that dependency" is still self-scoped).
- **P2 same-changeset / self-approved successor** — still-closed on both original adjectives. Residual → **P21** (owner *named* in the spec is still not an approval artifact).
- **P3 I17 label scope** — still-closed: "Labels do not scope this rule". Residual → **P9** (§5 is still name-scoped to "accepted closure deferrals").
- **P4 I1a AppHost enumeration** — still-closed: today's parenthetical lists all three AppHost-naming deferrals.
- **P5 I18 empty/unnominated closure commit** — still-closed: I18 defines the closure commit as the first superproject commit that lands the §7 map and the closure fitness tests; §8 names `2b63ab9`; until then the 2026-08-18 map governs.
- **P6 same-deferral double activation** — still-closed: "a second spec may activate the same deferral only after the first activation closes or explicitly hands over". Residual → **P9** (DW-100–104 are *not* "the same deferral"; they need no handover from DW-99's `activated_by_spec`).
- **P7 §5 Parties-in-every-set vacuity** — still-closed: disjointness is "beyond Parties itself". Residual → **P9** (clause-6 "relevant" remains self-declared; path-level overlap inside Parties is still unregulated).
- **P8 I1 approval home** — still-closed: route-list approval lives in the Story 8.3 matrix reconciliation ledger. Residual → **P21** (approver identity still unnamed).

## Findings (new holes only)

### P9 — §5 binds the five named deferrals; 2026-09-06 children inherit §4 and evade the order

- Severity: critical
- Units: A = I17 activation spec for accepted deferral `8.7-data-protection-extraction` (DW-97) vs B = I17 activation spec for ledger child `DW-104` ("Delete the retained local crypto and key-management engine… independently shippable and was split from Story 8.7")
- Each obeys:
  - **I17:** "an accepted Epic 8 closure deferral (or any Epic 8 ledger item) may be worked only through a spec file declaring all six §4 clauses" and "any spec or ledger item — Epic 8-labeled or not — that deletes or weakens an I18 baseline surface or code guarded by I1–I15 inherits the §4 gate the same way." Both units author a six-clause spec. DW-104 deletes I8-guarded `Hexalith.Parties.Security` code, so the gate attaches.
  - **§4.2 / §4.6 / §5 concurrency:** "two deferrals may execute concurrently only if their §4 clause-2 touched-repo sets (beyond Parties itself, which every set contains) and their clause-6 parity-checklist sets are disjoint." Unit A clause-2 = Parties + EventStore, clause-6 = I8. Unit B clause-2 = Parties only (local MOVE-file deletion), clause-6 = I8 — *or*, if an auditor reads "two deferrals" as the five named IDs only, Unit B is not a deferral and §5's concurrency sentence does not apply at all. Either reading lets B proceed.
  - **§5 order:** "`8.6 → 8.7 → 8.8 → 8.9` order binds the *accepted closure deferrals* exactly as it bound the stories." DW-104 is not `8.7-data-protection-extraction`. The order does not name children.
  - **I3 / deferral rollback (A):** DW-97 rollback: "Keep Hexalith.Parties.Security, all 18 MOVE files, all 5 KEEP files, EventStorePartyPayloadProtectionAdapter, local DI selection, and the compatibility harness." Unit A adopts G5 behind reversible DI and does not delete — I3 and §4.5 non-goals satisfied.
  - **I8 / I18 (B):** Unit B enumerates `CryptoKeyManagementCompatibilityHarnessTests` (the §7 I8 baseline surface), stamps the current EventStore identity (I16), and deletes the MOVE files after "shared-provider adoption proves parity" — the child's own `reason` text, not an invariant.
- Incompatible outcome: one ratified rollback clause requires the 18 MOVE files to remain the switch-back path; an independently shippable child, letter-compliant with I17/§4/I18, deletes them in a concurrent or earlier changeset. Two owners of `Hexalith.Parties.Security`. The same door opens `DW-101` (Art.20 download primitive on the *current* local engine) against Unit A's later G5 swap, and `DW-100` (G4-B freshness indicator) against `8.6-residual` / `8.8` G6 — because those children are also "independently shippable," already split from `8.9-frontcomposer-ui-consolidation`, and DW-99 is already `activated_by_spec: spec-8-10-…` for the shell slice only, so I17's handover rule never sees them. SPEC.md CAP-3/CAP-5 and spec-8-9's "HALT unless CAP-2 → CAP-3 → CAP-4" bind *those* documents, not a later I17 spec written for the child.
- Close-with: tighten **§5** — "The `8.6 → 8.7 → 8.8 → 8.9` order and the concurrency rule bind every ledger item whose `source_spec`, `origin`, or `reason` names an accepted closure deferral or a `spec-8-x` parent, and every item that deletes a surface that deferral's rollback names. Children are not independently schedulable. Clause-6 disjointness is computed from the §7 invariants the *touched files* guard, not from the adjectives the spec author lists as 'relevant'."

### P10 — G5 `IPersonalDataPolicy` / `IErasureStateProvider` vs I7/§2 Parties-owned GDPR policy: two writers of one legal fact

- Severity: high
- Units: A = `8.7-data-protection-extraction` adopting the G5 engine vs B = `8.6-residual-review-debt` (aggregate Art.18 / erasure-in-progress guards) or the still-local `PartyAggregateConsentTests` / `PartyAggregateErasureTests` owner
- Each obeys:
  - **§2 KEEPS:** "GDPR **policy** + legal semantics" stay in Parties; "Generic crypto/key-management engine → EventStore/shared DataProtection."
  - **I2:** "Parties retains only domain registrations, Parties-specific policy, and payload-protection hooks the SDK cannot own."
  - **I7:** "consent ≠ lawful basis; Art.18 restriction guards (consent edits allowed while restricted, rejected while erasure in progress); two-front-door erasure + cross-submodule verification (D7)."
  - **I8:** "`json+pdenc-v1`, `json-redacted`, legacy unprotected reads, key zeroing, typed-unreadable outcomes, no-leak diagnostics, Art.20 exports, Art.30 processing records, erasure reports/certificates."
  - **8.3 G5 row (authoritative companion):** proof required includes "`IPersonalDataPolicy`, `IErasureStateProvider`, AAD binding in `pdenc-v2`" *inside the shared engine package*. Unit A therefore places personal-data policy and erasure-state behind the EventStore provider — that is the G5 letter, not a stretch.
  - **spec-8-7 Always / ownership split (B's reading):** "Parties retains party-specific commands, tenant/party rotation policy, GDPR/legal policy, natural-person classification, erasure orchestration, D7 verification, certificates/reports, and UX/copy." Unit B keeps Art.18 on the aggregate (`RestrictProcessing` / `ErasePartyRequested` in project-context.md:138–141).
- Incompatible outcome: "erasure in progress" and "is this personal data / may it be unprotected" become two mutation paths. The engine's `IErasureStateProvider` can refuse to unprotect a consent-edit payload that I7 *requires* to succeed while restricted (Art.18(3)), or can treat a party as erasable/unreadable while the aggregate has not yet rejected `RecordConsent`. Consent ≠ lawful basis can hold in Parties.Contracts (`LawfulBasis` enum) and be collapsed inside a generic `IPersonalDataPolicy`. I16 stamps the engine identity; I18 greens `PartyAggregateConsentTests` and `CryptoKeyManagementCompatibilityHarnessTests` separately; neither invariant requires the two oracles to name the same party, the same instant, or the same lawful-basis field. SPEC.md OQ-5 (I2 hook vs engine) is this hole asked as a question.
- Close-with: new **I19a (policy oracle):** "There is exactly one writer of erasure-in-progress, restriction, and personal-data classification: the Parties aggregate / GDPR policy types. `IPersonalDataPolicy` and `IErasureStateProvider` are read-only adapters over that writer. A G5 engine that decides those facts is out of bounds; the 8.3 G5 row must drop them from the engine package or reclassify them as Parties-owned hooks under I2, with a recorded owner decision — not an executor guess."

### P11 — I10 pins freshness *presence*; three ratified dialects can each satisfy it

- Severity: high
- Units: A = `8.6-residual-review-debt` closing DW-23 / index `ProjectionVersion` remarks (local grammar) vs B = `DW-100` (G4-B "UI-normalized per-record freshness indicator without redefining EventStore/G6 wire semantics") or the `8.8-runtime-boundary-cleanup` G6 envelope slice
- Each obeys:
  - **I10:** "`ProjectionFreshnessMetadata` on every read; … Target abstractions: `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`." Presence and abstraction names only. No vocabulary, no `ProjectionVersion` grammar, no erasure-time stamping rule.
  - **§2:** "command/query envelopes + freshness metadata → EventStore.Contracts (G6), referencing Commons paging."
  - **spec-8-6 Always / changelog 2026-08-03 (A):** production path emits SDK-native `Current` / `Stale` / `Unavailable`; store-outage last-known is `Stale` + `Metadata.IsDegraded`; `Rebuilding` / `Degraded` / `LocalOnly` "remain on the enum for wire compatibility but are not produced." Index version scheme is the still-open ledger item (`global:N` / `{id}:{seq}` / keep-current).
  - **8.3 G6 row (B):** "additive or approved adapter proof for G6 Parties `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` freshness semantics." Consumers listed: **8.6, 8.8, 8.9, 8.10**.
  - **8.3 G4-B (B):** "without redefining EventStore/G6 wire semantics."
  - **I16 / I17 / §4:** each unit stamps its own identity and lists I10 (A) or I13/I14 (B) as the relevant checklist. If B is DW-100, §5 order does not bind it (P9).
- Incompatible outcome: one read model, three legal freshness dialects. Unit A can "close" AC7 with erasure-time vs pre-erasure `ProjectedAt` and `global:N` on the index (I10 satisfied: metadata present). Unit B's indicator, built against the G6 six-state wire, renders index rows blank/wrong (`global:N` where it expects `{id}:{seq}`) and treats `Stale+IsDegraded` as a different UX from first-class `Degraded`. §2 says EventStore owns the envelope *once G6 lands*; I10 does not require Unit A to wait, or Unit B to consume Unit A's documented grammar. SPEC.md **OQ-1** is this hole verbatim. Memlog V8/ADV-7: still constructible; I16–I18 never touched semantics.
- Close-with: tighten **I10** — "The freshness contract is semantic and singly owned: state vocabulary, per-read-model `ProjectionVersion` grammar, and the erasure-time `ProjectedAt` rule are one versioned shape. Owner: EventStore.Contracts G6 envelope once `available`; until then the spec-8-6 three-state production dialect is the only legal emitter. A UI consumer (G4-B) may bind only to that named shape version. Presence of `ProjectionFreshnessMetadata` without a named grammar is not I10 compliance."

### P12 — Memories mapping store: I9/I10 read-model vs D7 operational ledger (I19 still unwritten)

- Severity: high
- Units: A = `8.6-residual-review-debt` executing **DW-81** ("Move the Memories mapping ledger behind EventStore persistence" — `PartyMemoryUnitMappingStore` "persists operational state directly through `DaprClient`, outside the EventStore read-model and write-policy abstractions required for domain-module persistence") vs B = `8.7-data-protection-extraction` certifying D7 erasure (exit proof: "erasure certificate/report") plus DW-82/DW-86 (zero remote Memories units before cleanup is complete / validate certificates before certifying store cleanup)
- Each obeys:
  - **I9:** "Replay-from-zero on every delivery; per-read-model sequence checkpoints + set-based idempotency."
  - **I10:** "A full rebuild is executed and verified against aggregate replay before local code deletion." Target abstractions include `IReadModelStore` / `ReadModelWritePolicy` — exactly what DW-81 demands Unit A wrap the store in.
  - **I7:** D7 cross-submodule verification; §7 I7 deferred slice owns "Memories cleanup-race review debt."
  - **I17 / §4:** both work through six-clause specs. Clause-6: A lists I9/I10; B lists I7/I8. If §5 applies (both are named deferrals, 8.6 then 8.7), they are sequential, not concurrent — the clash is the *classification* of one persisted entity, not a race. I16 stamps EventStore; I18 greens `PartySdkProjectionHandlerTests` (A) and `ErasureVerificationServiceTests` (B).
- Incompatible outcome: wrap the mapping ledger in `IReadModelStore` and it becomes, by I9/I10, rebuild-from-zero and rebuild-vs-replay verified. The store records *external side effects* (Memories units created/deleted in another submodule). Replay cannot reconstruct it; a rebuild overwrite destroys the cleanup receipts D7 / DW-82 / DW-86 need to certify erasure. Keep it as an operational ledger and Unit A has not done DW-81. The spine still has no persistence-class vocabulary. SPEC.md **OQ-2** and memlog V9/ADV-8: unchanged since 2026-08-18; I18's baseline mechanics do not classify stores.
- Close-with: new **I19 (persistence class)** — "Persisted state is (a) an event-derived read model — rebuildable from zero, subject to I9/I10 — or (b) an operational side-effect ledger — never rebuilt from replay, excluded from rebuild-vs-replay, retained across rebuilds, covered by its own consistency tests. Every store names its class in the activating spec. `PartyMemoryUnitMappingStore` is class (b). DW-81 may place it behind an EventStore *persistence* seam only if that seam is not `IReadModelStore`/`ReadModelWritePolicy`, or I19 is explicitly reclassified with its own parity evidence."

### P13 — Erasure certificate: 8.6-residual validates identity/status; 8.7 rewrites the producer

- Severity: high
- Units: A = `8.6-residual-review-debt` (DW-86 / §7 I7: "owns the residual erasure-certificate identity/status validation") vs B = `8.7-data-protection-extraction` (exit proof: "pass … Art.20, Art.30, erasure certificate/report, and exercised switch-back parity")
- Each obeys:
  - **I7:** two-front-door erasure + D7; §7 I7 executable surfaces are `PartyAggregateConsentTests`, `PartyAggregateErasureTests`, `ErasureVerificationServiceTests` — certificate identity/status is *deferred* to 8.6-residual, not those tests.
  - **I8:** erasure reports/certificates are in the protected-payload compatibility packet Unit B must pass.
  - **spec-8-7 ownership split:** "Parties retains … D7 verification, certificates/reports." Unit B can change the *producer* (shared engine / `IErasureStateProvider` / key-destruction evidence) and still claim Parties owns the *types*.
  - **§5:** 8.6 before 8.7 — sequential, both named deferrals. I16/I17/I18 all hold: A stamps today's identity and greens whatever certificate fixture it adds; B stamps the G5 identity and greens the I8 harness against a new byte layout.
  - **project-context.md:145–147:** "`IErasureVerificationService.VerifyErasureAsync` → `ErasureVerificationReport` / `ErasureCertificate` … Treat that contract as approval-gated; don't change it unilaterally." Unit B's G5 packet is an approved story, not a "unilateral" drive-by — the letter of I5 ("intentionally versioned") is available if the DTO moves.
- Incompatible outcome: Unit A locks identity/tenant/status/destroyed-key-version checks onto the *current* certificate shape (the exact gap `review-closure-evidence.md` recorded). Unit B's G5 engine emits a new certificate/report (exit proof names it). D7 Admin verification (Story 3.6 consumer) and Memories cleanup certification (DW-86) validate the old shape against the new bytes — or Unit B "versions" the contract (I5) while Unit A's residual tests are the I18 baseline for I7 and still compile against leftover fixtures. Two owners of one certificate. I16 re-opens B's claims on an EventStore bump, not A's already-merged validation semantics.
- Close-with: tighten **I7** — "ErasureCertificate / ErasureVerificationReport field semantics (identity, tenant, status, destroyed key versions, cleanup completeness) are one owned, versioned shape. Owner: Parties.Contracts. 8.6-residual may only add tests against the current shape; 8.7 may not change the shape except by an I5 versioning plan recorded in the 8.3 ledger and approved by the I7 test-architect owner *before* G5 producer adoption. D7 verification and Memories cleanup consume that same shape."

### P14 — I11 "stop rejecting valid ULIDs" vs I10 "IDs permanently unavailable after erasure"

- Severity: medium
- Units: A = `8.8-runtime-boundary-cleanup` G9 slice (`UniqueIdHelper.IsValidUlid` / `AggregateIdentity.IsValid`) vs B = `8.6-residual-review-debt` I10 tombstone / erased-index work
- Each obeys:
  - **I11:** "Stop rejecting valid ULID-compatible aggregate IDs; retain replay compat for GUID-shaped IDs; use Commons unique-ID helpers where semantics require."
  - **G7/G9 SCP (2026-07-16):** `AggregateIdentity.IsValid(string)` "must continue to accept valid ULID-shaped identifiers, existing GUID-shaped identifiers, and other valid semantic aggregate IDs used for replay compatibility"; `UniqueIdHelper.IsValidUlid` is strict ULID recognition and "must not be substituted for EventStore aggregate-id validation."
  - **I10:** "erased parties excluded from the index, and Party identifiers remain permanently unavailable for reuse after erasure — sequence/checkpoint state must preserve that tombstone under delayed or same-ID events."
  - **I6:** command/query behavior preserved. Neither I6 nor I11 mentions tombstones.
  - **§5:** 8.6 before 8.8 — sequential. I16/I17/§4 hold.
- Incompatible outcome: Unit A adopts the Commons/EventStore predicates on the create/validate path — an erased party's ULID is *format-valid*, so I11 forbids rejecting it as an identifier. Unit B preserves the tombstone in checkpoint/index state — I10 forbids reuse. A `CreateParty` (or MCP `create_party`) with a client-supplied erased ULID is accepted at I11 validation and then either (i) applied onto a tombstone (same-ID delayed event — I10 told B to preserve the tombstone, not to reject the command) or (ii) rejected only in the projection, leaving a command-success / query-invisible split. No invariant names the layer that must refuse reuse. I11's "stop rejecting" and I10's "permanently unavailable" are opposite instructions on the same string.
- Close-with: tighten **I11** — "Format acceptance is not allocation. `IsValidUlid` / `AggregateIdentity.IsValid` decide shape only. Allocation of an aggregate ID that is tombstoned under I10 is rejected at the command handler with a typed, stable outcome; helpers must not be wired as the sole create-time gate. I10 wins on reuse."

### P15 — I1's one local ACL file vs `external-runtime-deployment`'s environment-specific deny-default ACL

- Severity: medium
- Units: A = `8.8-runtime-boundary-cleanup` retiring the Parties AppHost after I1a proofs and moving the authoritative ACL with the successor topology vs B = `external-runtime-deployment` (DW-10 / DW-100-adjacent ledger; accepted deferral) writing "environment-specific DAPR components, subscriptions, resiliency, deny-default access control, ingress…" in the owner repository
- Each obeys:
  - **I1:** "The ACL has exactly one authoritative owner file at any time (today: `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`); the fitness gate asserts the authoritative copy as (app ID, verb, policy, action) tuples … route-list changes require an owner approval recorded in the Story 8.3 matrix reconciliation ledger … the list above is the baseline, not a hand-editable ceiling."
  - **I1a:** retirement after topology/security/publish/rollback parity *and* every AppHost-naming deferral (including `external-runtime-deployment`) "has passed that proof or been re-approved by that deferral's recorded owner against the successor topology." Unit A obtains that re-approval — letter done.
  - **B's accepted exit proof:** environment-specific deny-default ACL lives in the *owner* repository. That is a different file in a repository the Parties fitness gate does not read. I1's "one owner file" is the local topology ACL; B never edits it.
- Incompatible outcome: after retirement, Unit A's fitness asserts FrontComposer.AppHost (or whatever successor Unit A recorded). Unit B's production ACL can admit an extra app ID, GET-on-`/query`, or ingress-fronted route the I1 baseline never listed — "environment-specific" is the deferral's own word. I1's 8.3-ledger approval never fires because B did not change the Parties/successor *local* file. Production traffic is not the tuple set the invariant claims is deny-default. ADV-3's two-copy hole was closed for *in-repo* copies; the post-re-approval external copy was left unnamed.
- Close-with: tighten **I1** — "Deny-default EventStore-only tuples bind every ACL that admits traffic to a Parties domain-service host — local successor file *and* any environment-specific copy named by `external-runtime-deployment`. The 8.3 ledger records the successor path after I1a retirement. An external copy that adds an app ID, verb, or route is an I1 route-list change and needs the same recorded approval. Fitness asserts the local successor; the external owner attests tuple-equality (or an approved delta) in the deferral evidence field."

### P16 — I1a parity list still omits key-ring / cursor / DataProtection state continuity

- Severity: medium
- Units: A = retained Parties host after 8.6 (`AddEventStoreDataProtection` / `DaprXmlRepository` / `IQueryCursorCodec` against the Parties AppHost statestore — 8.3 DataProtection row) vs B = `8.8-runtime-boundary-cleanup` G8-C successor topology, optionally after `8.7-data-protection-extraction` has pointed keys at the G5 Azure Key Vault backend
- Each obeys:
  - **I10:** both use `IQueryCursorCodec`. Purpose/scope compatibility is what 8.6 already proved at `c590590b`.
  - **I1a:** "retired only after topology, security, publish, and rollback parity are proven." Four nouns. No key-ring, no cursor, no DataProtection repository, no invalidation window.
  - **I8:** key zeroing / `pdenc` formats — not cursor purposes.
  - **I17 / §4.3:** B describes rollback ("revert the slice"; keep Parties AppHost until parity). Description, not a rehearsal of cursor readability.
- Incompatible outcome: cursors (and any DataProtection payload outside the I8 `pdenc` names) issued on topology A are `InvalidCursor` / unreadable on topology B, and switch-back invalidates every cursor issued after cutover. Three key rings are legal: Parties `DaprXmlRepository`, G5 KMS, FrontComposer.AppHost statestore. SPEC.md **OQ-4** and memlog V25/ADV-11: still constructible. I1a's new deferral-precedence sentence is a checkpoint, not an obligation to prove continuity.
- Close-with: tighten **I1a** — add "persisted key-ring and cursor continuity" to the parity list: "the successor must unprotect DataProtection payloads (cursor purposes included) written by the predecessor, or the activating spec must declare the invalidation window, the user-visible `InvalidCursor` / typed-unreadable behavior, and that I8 `pdenc` histories remain readable across the window."

### P17 — §4.3 still accepts a described rollback; children make silent expiry the default style

- Severity: medium
- Units: A = `DW-100` (G4-B/C freshness/live-region/optimistic-reconciliation) vs B = `DW-103` (Fluent 2 / accordion conformance "across all Parties UI RCLs")
- Each obeys:
  - **§4.3:** "which local code stays until parity, and how to revert." A description. No exercise, no re-validation when a later slice touches the same files.
  - **I17:** both children that delete/replace I13/I14-guarded UI inherit §4. Each authors a six-clause spec. DW-99 rollback already models the intended style: "revert a failed slice independently."
  - **I13 / I18:** each enumerates `MainLayoutAccessibilityTests` / `PartiesAccessibilitySpecimenTests` / picker bUnit as relevant and keeps them green on the *forward* tree.
  - **Evidence the hole is not theoretical:** DW-99's own rollback field already admits the delivered shell slice "reinstates the duplicate skip-link strict-locator ambiguity the slice resolved, so it must be paired with a Playwright rerun." The ledger is documenting described-rollback rot *after* one child landed.
- Incompatible outcome: Unit A lands shared freshness/live-region primitives and writes "git-revert this slice" as clause 3 — never executed. Unit B retargets the same RCL fixtures/CSS tokens to Fluent 2 accordion rules. A's revert no longer compiles or brings back the live-region contract; B never re-declared A's rollback. Memlog V10/ADV-9: unchanged, and I17 *widened* the population of executors that rely on clause 3. First-activation-spec revisit trigger is now.
- Close-with: tighten **§4.3** — "Rollback path — which local code stays, how to revert, and evidence the revert was exercised once (build + named I18 surfaces green on the reverted tree). A later spec that edits a prior slice's Parties paths must re-exercise or explicitly supersede every still-live rollback path it disturbs, recorded in both specs."

### P18 — I7's two front doors vs spec-8-8's `delete_party` MCP tool: a third mutation path

- Severity: medium
- Units: A = `8.8-runtime-boundary-cleanup` G11 MCP slice vs B = `8.6-residual-review-debt` / `8.7-data-protection-extraction` D7 verification plus `DW-102` (typed-name confirmation on *Admin* erasure only)
- Each obeys:
  - **I7:** "two-front-door erasure + cross-submodule verification (D7)." project-context.md:142–144 names the doors: Admin `EraseParty` (typed-name confirmation) and Consumer `RequestMyErasureAsync` / `CancelMyErasureAsync`.
  - **I13:** "typed destructive confirmation" — a UI contract, not an MCP contract.
  - **spec-8-8 Always:** "Keep the exactly-5 MCP tool contracts (`create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`; no `get_party_name_at`)." Unit A must preserve `delete_party`.
  - **I6:** command/query behavior; MCP delete can be the same `EraseParty` command and still satisfy I6.
  - **I17 / §4.5:** Unit A non-goals include "do not migrate … crypto (8.7) or G4 UI (8.9)" — so it will not add typed confirmation or D7 coverage to MCP.
- Incompatible outcome: `delete_party` is a third front door. It does not get I13 typed-name confirmation, does not get the consumer cancel path, and is not in D7's two-door verification set Unit B implements. An MCP-initiated erasure can skip Memories cleanup races Unit B is closing, or can mark verified while the MCP caller never saw certificate identity checks. I7's "two" is a closed count the 8.8 letter immediately falsifies. I16/I18 green MCP contract tests and `ErasureVerificationServiceTests` as disjoint checklists.
- Close-with: tighten **I7** — "Erasure mutation paths are an enumerated set: Admin, Consumer, and MCP `delete_party`. Every path applies the same aggregate command, the same D7 verification, and the same I10 tombstone. UI-only controls (typed-name, cancel) stay on their doors; MCP must fail closed on missing server-resolved identity (I6) and must not grow a fourth door. Adding or removing a path is an I7 change recorded in the 8.3 ledger."

### P19 — I16's stop is per-dependency; Art.20 can move on FrontComposer while EventStore I8 evidence is void

- Severity: medium
- Units: A = a catalog/gitlink bump of EventStore that records I8 Art.20 / rebuild / switch-back claims `unvalidated` in the 8.3 matrix vs B = `DW-101` (FrontComposer browser-download / Art.20 export UX) deleting Parties download helpers
- Each obeys:
  - **I16:** "the changing story re-runs the affected named test surfaces at the new identity or records the claim as unvalidated in the 8.3 matrix before merging. … An unvalidated marker is a stop, not a state: while any parity claim on a dependency stands unvalidated, no further deletion *relying on that dependency* may merge."
  - Unit A relies on the second disjunct and names the EventStore owner who will re-run — I16 satisfied. The stop applies to deletions *relying on EventStore*.
  - Unit B's clause-2 is Parties + FrontComposer; its deletion "relies on" the G4-D FrontComposer identity, not EventStore. I16's stop does not fire. I17/§4/I18 hold if B enumerates `MyPrivacyPageTests` / packaging tests (I14/I5), not `CryptoKeyManagementCompatibilityHarnessTests`.
- Incompatible outcome: the Art.20 *bytes* (I8, EventStore-stamped, now unvalidated) and the Art.20 *download framing* (filename, content-type, typed-unreadable failure UX) are one user-visible export. Unit B replaces the framing against the still-local engine while the packet that authorized any later I8 deletion is void. P1's parking lot is closed for same-dependency deletions; the cross-dependency composition is not. This is the 2026-08-18 I16 adjective "relying on that dependency" doing the damage.
- Close-with: tighten **I16** — "Unvalidated is a stop for every deletion or replacement of a rollback seam, receipt, or user-visible path that the unvalidated claim's original evidence covered, regardless of which gitlink the changing spec says it 'relies on.' 'Affected' is stamp-derived: every claim that names any identity of the moved dependency is affected. Art.20 export bytes and export UX are one covered path."

### P20 — §2 says Parties keeps GDPR copy; §7 / 8.9 say 8.9 consolidates it into FrontComposer

- Severity: medium
- Units: A = `8.9-frontcomposer-ui-consolidation` / remaining GDPR-copy slice (exit proof: "pass … GDPR copy") vs B = a later I7/I14 fix on `MyConsentPageTests` / `MyPrivacyPageTests` that treats Parties strings as the owned semantics
- Each obeys:
  - **§2 KEEPS:** "Domain UI **semantics** (labels, GDPR copy, flows)" stay in Parties; FrontComposer gets "Status/freshness/reconcile/grid/picker UI primitives (G4)."
  - **§7 I14:** "`MyConsentPageTests` and `MyPrivacyPageTests` guard GDPR copy and stale-read honesty; `8.9-frontcomposer-ui-consolidation` owns the remaining shared-copy consolidation."
  - **I14:** "no consent dark patterns, no over-promised export latency, cancellation-vs-permanence distinction, stale reads show last-known."
  - **§2 vs Epic 7 precedence:** "Where this table and the Epic 7 spine disagree, this table wins" — arbitration vs Epic 7 only. No precedence between §2 KEEPS and §7 / the deferral exit proof.
- Incompatible outcome: Unit A moves consent / lawful-basis / cancellation-vs-permanence strings into a FrontComposer shared-copy catalog (that is what "shared-copy consolidation" plus the 8.9 exit proof say). Unit B, reading §2, keeps editing Parties `MyPrivacyPage` / `PartyGdprOperationsPanel` copy as the legal source. Two string sources: one can ship a merged "consent" toggle (generic primitive) while I7 requires consent ≠ lawful basis; I14 is satisfied on each side's tests because each suite binds to its own strings. Dark-pattern risk is exactly I14's first clause, with a compliance receipt on both units.
- Close-with: tighten **§2 and I14** — "GDPR *words* (consent vs lawful basis, restriction, cancellation vs permanence, export latency) are Parties-owned domain semantics. FrontComposer may own layout/live-region/download *mechanics* only. 8.9 'shared-copy consolidation' means the primitives; it does not move the legal strings. §7 I14's 'owns consolidation' is mechanics, not copy ownership. Where §2 KEEPS and a deferral exit proof disagree, §2 wins."

### P21 — "Owner recorded in the spec" is still a name-drop; I5 / I2 / I1a / I18 share one undecidable approval

- Severity: low
- Units: A = any I17 spec that writes `owner: Murat (Test Architect)` and `successor: PartiesFreshnessIndicatorTests (approved)` vs B = an auditor or a later spec that expects an approval artifact (matrix row, SCP, signed deferral field)
- Each obeys:
  - **I18:** "name a successor test approved by an owner recorded in the spec, never by the authoring executor alone." Unit A is Amelia; the spec *records* Murat. The sentence does not require Murat's recorded decision outside the spec text.
  - **I5:** public shape "stable or intentionally versioned" — no named approver, no ledger home (V12).
  - **I2:** "hooks the SDK cannot own" — Unit A classifies `IPersonalDataPolicy` as a hook or as engine (P10); I2 does not say who decides.
  - **I1a:** "explicitly approved platform AppHost owner" and "re-approved by that deferral's recorded owner" — the re-approval's artifact home is still unnamed (V12). I1's *route* approval home was fixed (P8); these were not.
  - **SPEC.md OQ-5:** asks this question; the spine never answered it.
- Incompatible outcome: Unit A ships a self-written "Murat approved" line; Unit B refuses the successor / versioning / AppHost / hook call. Both readings are available on today's text. Low only because it is visible in review — it is the solvent that makes P10, P13, and P15 look letter-compliant.
- Close-with: one **approval table** in the 8.3 reconciliation ledger: rows for I1a successor-host, I1a re-approval, I5 versioning, I2 hook-vs-engine, I18 successor test. Each row: artifact, named human, date. "Recorded in the spec" is a pointer to that row, not the approval.

### P22 — I1's "today" path dies at I1a retirement with no successor-file protocol

- Severity: low
- Units: A = `8.8-runtime-boundary-cleanup` deleting `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml` after I1a vs B = `DocumentationFitnessTests` / `ArchitecturalFitnessTests` still cited by §7 I1 as the executable ACL evidence
- Each obeys:
  - **I1:** one authoritative file "at any time (today: `…/accesscontrol.parties.yaml`)." "Today" is descriptive. Unit A, after retirement, can claim the authoritative file is now in FrontComposer and update fitness in the same changeset — or claim I1a's retirement *is* the change protocol and leave the spine sentence stale.
  - **I1a:** retirement after proofs; does not require amending I1's path in the same changeset.
  - **I18:** deleting a baseline *test* surface with the implementation is invalid; retargeting fitness to a new path is not obviously a weaken if the tuples are re-asserted. Unit A can keep the class names in §7.
- Incompatible outcome: Unit B's fitness still opens a path that no longer exists (suite red, or skip), or Unit A points fitness at a FrontComposer file this repository's tests cannot see on a package-mode CI clone. I1's "fitness gate asserts the authoritative copy" becomes vacuously true or unenforceable. Rubric L3 (2026-08-18) called this future; I1a is now the story that makes it present-tense. Distinct from P15 (external env ACL): this is the *local* successor file the spine never names.
- Close-with: amend **I1** — "On I1a retirement the authoritative path is rewritten in this invariant and in the 8.3 ledger in the same changeset as the deletion; the fitness classes named in §7 I1 must open that path (or a recorded identity of the owning repo) or the changeset is invalid. 'Today' is not an excuse to leave a dead path."

## What this pass did not re-open

ADV-1 through ADV-4 and P1–P8 remain closed on the adjectives they were written against. V8/V9/V10/V12/V25 were deferred, not closed; they are filed above as P11/P12/P17/P21/P16 with today's units (including the 2026-09-06 DW children) as fresh evidence, not as recycled IDs.

## Close-with priority

1. **§5 child-binding (P9)** — without this, I17 is a paperwork generator and the 8.6→8.9 order is costume jewelry.
2. **I19 + I19a (P12, P10)** — persistence class and single GDPR-policy oracle. These are the two ways remaining work can corrupt erasure.
3. **I10 grammar + I7 certificate shape (P11, P13)** — shared-data shapes the G6/G5 rows will otherwise fork.
4. **I11 reuse, I1 external+successor ACL, I1a continuity, §4.3 exercise, I7 door count, I16 cross-dependency, §2 copy (P14–P20, P22)** — tighten in the same amendment pass; do not wait for "G-row APIs land."
5. **Approval table (P21)** — cheap, and it is the solvent for the rest.

## Summary table

| # | Severity | Units | Hole |
|---|----------|-------|------|
| P9 | critical | `8.7-data-protection-extraction` vs `DW-104` (same door: `DW-101`/`DW-100`) | §5 order/concurrency name the five deferrals; 2026-09-06 children inherit §4 only — two owners of Security / Art.20 / freshness |
| P10 | high | `8.7` G5 vs `8.6-residual` Art.18 | `IPersonalDataPolicy`/`IErasureStateProvider` vs Parties GDPR policy — two oracles for one legal fact |
| P11 | high | `8.6-residual` vs `DW-100` / `8.8` G6 | I10 presence-only; 3-state vs 6-state vs `global:N` |
| P12 | high | `DW-81` vs `8.7` D7 | Memories mapping: read-model rebuild vs operational ledger |
| P13 | high | `8.6-residual` DW-86 vs `8.7` certificate producer | two writers of ErasureCertificate |
| P14 | medium | `8.8` G9 vs `8.6-residual` I10 | ULID accept vs permanent ID unavailability |
| P15 | medium | `8.8` I1a retirement vs `external-runtime-deployment` | local tuple ACL vs env-specific deny-default |
| P16 | medium | Parties `DaprXmlRepository` vs `8.8` successor / `8.7` KMS | I1a parity omits key-ring/cursor continuity |
| P17 | medium | `DW-100` vs `DW-103` | §4.3 described rollback; children expire it |
| P18 | medium | `8.8` `delete_party` vs `8.6`/`8.7`/`DW-102` D7 | I7 says two doors; MCP is a third |
| P19 | medium | EventStore I16 unvalidated vs `DW-101` | stop is per-dependency; Art.20 UX moves anyway |
| P20 | medium | `8.9` copy consolidation vs §2 KEEPS | two owners of GDPR strings |
| P21 | low | any I17 spec vs auditor | name-drop "owner recorded in the spec" |
| P22 | low | `8.8` deletes I1 path vs §7 I1 fitness | no successor-file protocol |

Findings: 14 new (1 critical, 4 high, 7 medium, 2 low). Zero prior IDs reopened.
