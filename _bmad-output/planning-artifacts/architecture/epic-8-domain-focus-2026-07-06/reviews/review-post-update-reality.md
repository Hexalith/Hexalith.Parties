# Post-Update Reality-Check Review — Epic 8 Architecture Spine (2026-08-18 amendment)

- Reviewer lens: REALITY-CHECK (re-run after the validation-driven UPDATE)
- Target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- Scope: ONLY claims added or changed by the 2026-08-18 amendment (isolated via
  `git diff HEAD -- …/ARCHITECTURE-SPINE.md`). The pre-update spine was fully
  verified in `review-reality-check.md` and is not re-litigated here.
- Method: every new/amended claim checked against the working tree, `git ls-tree
  HEAD` (no submodule init/update performed), the Story 8.3 matrix,
  `sprint-status.yaml`, `deferred-work.md`, `tests/test-summary.md`, the Epic 7
  spine, and the named test sources.

## Verdict

**CONDITIONAL PASS — one High refutation.** The amendment is overwhelmingly
grounded: the I1 route list, the single-ACL-owner claim, the tuple-asserting
fitness gate, all four I4 identity pins and both halves of the I4 caveat, the
§2 G6/G11/G7-G9/G4/AD-4 routings, the §5 correct-course provenance, the §7 test
surfaces (all 27 named classes exist, ULID and shell-slice claims verified),
and every cited open debt in the ledger check out against the repo. One amended
§2 claim is factually wrong and must be corrected: Story 8.4 did **not** delete
`Hexalith.Parties.Authentication`.

## Per-Claim Verification Table

| # | Claim (new/amended text) | Verdict | Evidence |
|---|---|---|---|
| 1a | §2: envelopes + freshness → EventStore.Contracts (G6) routing exists in the 8.3 matrix | **Verified** | Matrix row "EventStore client envelopes/freshness/error codes" (line 95): owner `Hexalith.EventStore`, G6 label, cited files under `Hexalith.EventStore.Contracts` (`SubmitCommandRequest`, `QueryResponseMetadata`, `QueryProblemReasonCodes`), stories 8.6/8.8/8.9/8.10. Note: `sprint-status.yaml` itself never names "G6" (its 8.8 comment lists G5/G7-G9/G8/G11/Commons/Builds); G6 is carried by the matrix and by `deferred-work.md` 8.8-runtime-boundary-cleanup exit proof ("G6 envelopes/freshness"). Not a spine defect — see Finding F3. |
| 1b | §2: paging primitives → Commons (Epic 7 AD-4) | **Verified** | Epic 7 spine `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`: AD-4 "Utility Destination Discipline" (line 96, "Commons for pure paging and string helpers") and B10 row (line 199: "Commons generic paging result plus Parties compatibility adapter | AD-4, AD-6"). |
| 1c | §2: MCP plumbing → FrontComposer MCP host on Commons.Http (G11) | **Verified** | Matrix row "MCP, deep-link, and search probes" (line 100): `Hexalith.FrontComposer.Mcp` owns auth/context/relay policy, `Hexalith.Commons.Http` owns domain-neutral header/URI/bounded-health mechanics; routed by `sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`. sprint-status action (lines 416-422) restates the same G11 split. |
| 1d | §2: tenant-claim transformation → EventStore.Authentication + Commons ULID helpers (owner decision 2026-07-16; G7/G9) | **Verified** | Matrix row 96: "G7/G9 ownership was confirmed on 2026-07-16 in `sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`: … a lightweight `Hexalith.EventStore.Authentication` package owns the reusable `EventStoreTenantClaimsTransformation`; Commons owns `UniqueIdHelper.IsValidUlid(string)`." sprint-status carries the same decision as a `done` action ("G7/G9 tenant-claims ownership confirmed on 2026-07-16 with no redirect"). "Commons ULID helpers" is an accurate rendering of the recorded `UniqueIdHelper.IsValidUlid(string)` predicate (both sources also note the API remains **undelivered** — the spine presents this as target routing, which is consistent). |
| 1e | §2: status/freshness/reconcile/**grid**/picker UI primitives → FrontComposer (G4) | **Verified with one unsupported token** | Matrix G4 row (line 98) routes picker (`FcEntityPicker<T>`), freshness indicator, live-region status contract, safe download, typed-name destructive confirmation, and skip-link parity; "optimistic reconcile" appears in the keep/rollback list. **No G4 record anywhere mentions a grid primitive** — `grid` has zero matches in the matrix, `epic-8-context.md`, and the G4/G11 routing SCP. See Finding F2 (Low). |
| 2a | §2: "Story 8.4 deleted `Hexalith.Parties.Authentication`" | **REFUTED** | (1) `git ls-tree HEAD src/` shows `src/Hexalith.Parties.Authentication` tracked at HEAD (`.csproj` + `PartiesClaimsTransformation.cs` present in the working tree). (2) `spec-8-4-leaf-project-retirement.md` records the exact opposite: "record `Hexalith.Parties.Authentication` as an **explicitly gated non-retirement**" and "Preserved `Hexalith.Parties.Authentication`". Story 8.4 actually retired `Hexalith.Parties.Server` and `Hexalith.Parties.ServiceDefaults` (per `RetiredLeafProjectFitnessTests.RetiredProductionProjectPaths`). (3) Matrix row 96: "Keep the `Hexalith.Parties.Authentication` rollback path; **it remains after Story 8.4**"; the 2026-07-31 G7/G9 receipt says "deferred Story 8.4 Authentication deletion". (4) sprint-status keeps the G7/G9 delivery action open: "Blocks **deferred 8.4 deletion**/8.8." (5) The spine's own referenced ledger contradicts §2: `deferred-work.md` 8.8-runtime-boundary-cleanup rollback says "Keep the … **Authentication project** …". See Finding F1 (High). |
| 2b | §2: Epic 7 spine carries the Class A anchor-boundary statement being superseded | **Verified** | Epic 7 spine line 62: "Class A shared-anchor boundary … Epic 7 does not re-open in-repo anchors already routed to `Hexalith.Parties.Contracts` or `Hexalith.Parties.Authentication`." The statement exists; only the claimed supersession **event** (deletion) has not occurred — the move is an approved future routing gated on undelivered G7/G9 APIs. |
| 3 | I1: `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml` is the current authoritative ACL path; route list; tuple-asserting fitness gate | **Verified** | File exists and is the **only** `accesscontrol.parties.yaml` in-repo (excluding `references/`, `bin/`, `obj/`). Its content matches I1 exactly: `defaultAction: deny` at both levels, single policy for `appId: eventstore`, and precisely the 13 listed operations, each `httpVerb: ['POST']` / `action: allow`. `ArchitecturalFitnessTests.PartiesAppHost_KeepsPartiesAppIdAndDedicatedDaprAccessControl` asserts single-appId, deny-default counts, the exact ordered 13-route set, and per-operation-block verb/action tuples (exactly one `httpVerb`/`action` per operation) — matching I1's "(app ID, verb, policy, action) tuples" claim. `AppHostTenantsTopologyTests` additionally pins the AppHost reference to the file. |
| 4a | §7 I4 caveat: committed Builds gitlink is `6b78075…` | **Verified** | `git ls-tree HEAD references/Hexalith.Builds` → `160000 commit 6b7807533cea31aa7592450742a5c94dd1bc1d9f`. Working-tree checkout `git -C references/Hexalith.Builds rev-parse HEAD` → `17b1c7aae3e1854e464f17bd88d527f8350ea203`, exactly the "working-tree checkout" identity the caveat names. |
| 4b | §7 I4 caveat: sprint-status records closure staying open on immutable-identity grounds | **Verified** | sprint-status 8.10 note (lines ~187-202): identical four identities (EventStore package 3.95.0 + source `454b4d10…`, Commons HTTP source `6fbac0c5…`, Builds catalog `17b1c7aa…`) and "Closure stays open because those fixes remain modified dependency working trees rather than immutable owner commits/releases selected by the superproject." `PlatformApiPrerequisitesTests` pins all four constants (`BuildsSha`, `CommonsSha`, `PayloadProtectionEventStoreSha`, `3.95.0`) in source. |
| 5 | §7 I11: `IdentifierValidatorTests` and `PartyAggregateCompositeTests` exist with positive ULID coverage | **Verified** | `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs`: `PartyIdValidators_AcceptUlidAndLegacyGuid` asserts `Validate(UlidPartyId).IsValid.ShouldBeTrue` with `UlidPartyId = "01HYX7QS3NP8M4KQJR5A7CVWKM"`, ULID used across ~15 further command validations. `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`: `Handle_CreatePartyComposite_WithUlidPartyId_EmitsSuccessEvents` drives the aggregate with the ULID id and asserts success. |
| 6a | §7 I13: `MainLayoutAccessibilityTests` asserts FrontComposer-provided skip links/landmarks | **Verified** | `tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs` uses `Hexalith.FrontComposer.Shell.Extensions` / `AddHexalithFrontComposerQuickstart` and asserts skip links target the FrontComposer shell ids `#fc-main-content` / `#fc-nav` as the first two focusable anchors, programmatic focus targets, and exactly one navigation landmark (`data-testid="fc-navigation-rail"`) plus one `role="main"` (`id="fc-main-content"`). |
| 6b | §7 I13/I14: "shell slice adopted 2026-08-18" matches test-summary | **Verified** | `tests/test-summary.md` § "Authorized dependency and shell remediation — 2026-08-18": "Parties now adopts the shared FrontComposer shell instead of emitting duplicate skip links, landmarks, and status selectors"; Playwright a11y 6/6 pass recorded, resolving the earlier 2-pass/4-fail shell blocker. Corroborated by working tree: `MainLayout.razor` modified, `MainLayout.razor.css` deleted. Note this adoption is uncommitted working-tree state riding the same pending superproject commit as the I4 caveat (Finding F4, Info). |
| 7 | §7 I2/I7/I9/I10/I12: referenced open debts exist in `deferred-work.md` | **Verified — all seven** | Erasure-certificate identity/status/key-version validation ("Validate erasure-certificate identity, status, and destroyed key versions before certifying store cleanup", `ErasureVerificationService.VerifyErasureAsync` evidence); Memories cleanup races (ETag-unaware `ReplaceMappingsAsync`, cancelled-token `finally` persistence, missing compensating delete); unbounded Art.30 read model ("Bound and page the per-party Article 30 processing-activity read model", `PartyProcessingActivityFold` ever-growing list); null-dictionary recovery ("Handle null dictionary properties in persisted Party index read models with a bounded recovery result"); freshness-mapping AC7 ("entangled with the open AC7 freshness-mapping gap", 8-6 code-review defer section); search-input bounds ("Bound Party search query, mode, and CaseId inputs before cursor-scope construction"); CI Playwright a11y lane ("Restore the Playwright browser accessibility lane as a required CI gate"). All un-struck (open) entries; the 8.6-residual-review-debt umbrella deferral (status: accepted) owns them, as §7 states. |
| 8 | §5: 8.11–8.13 added by 2026-07-07/08 correct-course proposals | **Verified** | sprint-status annotations: 8-11 "Added by Correct Course 2026-07-07 (sprint-change-proposal-2026-07-07-validation-ladder-runner.md)"; 8-12 "Added by Correct Course 2026-07-08 (…parties-only-zot-ci-container-publish.md)"; 8-13 "Added by Correct Course 2026-07-08 (…retire-legacy-deployment-artifacts.md)". Descriptions (validation ladder, container-publish CI, deployment-asset retirement) match. |
| — | §7 map: all other named test surfaces exist | **Verified** | All 27 distinct test classes named in the §7 map resolve to real files under `tests/` (existence sweep; incl. the three new untracked closure files `DocumentationFitnessTests.cs`, `EpicEightClosureFitnessTests.cs`, `ClosureDeferral.cs` — untracked status is already covered by the I4/§8 pending-commit caveat). |

## Findings

### F1 — HIGH — §2 supersession claim asserts a deletion that never happened

§2: "One deliberate, SCP-authorized supersession of the Epic 7 Class A anchor
boundary is on record: **Story 8.4 deleted `Hexalith.Parties.Authentication`**,
and its anchors moved with the tenant-claim transformation to the platform
owner named above."

Reality: the project is tracked at HEAD (`git ls-tree HEAD src/` →
`src/Hexalith.Parties.Authentication`); Story 8.4's own spec records it as an
"explicitly gated non-retirement" (8.4 retired `Hexalith.Parties.Server` and
`Hexalith.Parties.ServiceDefaults` only); the 8.3 matrix G7/G9 row says "it
remains after Story 8.4"; sprint-status keeps "deferred 8.4 deletion" open,
blocked on undelivered G7/G9 owner APIs; and the spine's own referenced
8.8-runtime-boundary-cleanup deferral's rollback clause requires keeping the
Authentication project. The Epic 7 Class A statement being "superseded" does
exist (Epic 7 spine line 62), and the 2026-07-16 G7/G9 SCP does route the
future move — but that is an approved, gated **plan**, not an executed,
on-record supersession. As written, §2 could be read as authorizing deletion
of a rollback surface that I3/I17 and the accepted deferrals require to stay.
Fix: restate as "the 2026-07-16 G7/G9 ownership SCP authorizes superseding the
Epic 7 Class A anchor boundary; `Hexalith.Parties.Authentication` remains in
place as the gated rollback path (deferred 8.4 deletion) until the G7/G9 APIs
are delivered and parity passes."

### F2 — LOW — "grid" in the §2 G4 routing row is unsupported

"Status/freshness/reconcile/**grid**/picker UI primitives → FrontComposer
(G4)": no G4 work package (A-F), matrix cell, `epic-8-context.md` entry, or the
G4/G11 routing SCP mentions a grid primitive (`grid` has zero matches in those
sources). Every other token in the row is grounded. Drop "grid" or add the
record that routes it.

### F3 — INFO — G6 label absent from sprint-status.yaml itself

The spine cites G6 for the envelopes/freshness routing; the routing is fully
recorded in the 8.3 matrix (row 95) and the deferred-work 8.8 exit proof, but
`sprint-status.yaml` never names G6 (its 8.8-blocked comment enumerates
G5/G7-G9/G8/G11/Commons/Builds). Also pre-existing matrix-internal tension, not
introduced by this amendment: matrix row 91 records G6 "freshness mapping" as
closed by Story 8.6 at v3.89.0 while row 95 remains `needs-additive-api`. No
spine change required.

### F4 — INFO — I13/I14 shell-adoption evidence is uncommitted working-tree state

The 2026-08-18 FrontComposer shell adoption (modified `MainLayout.razor`,
deleted `MainLayout.razor.css`, updated `MainLayoutAccessibilityTests`) and the
new closure fitness tests are working-tree changes riding the same pending
superproject commit that the I4 caveat and §8 already flag for the dependency
identities. The caveat's wording covers "the closure fitness tests"; the
shell-slice rows inherit the same durability condition implicitly. No change
strictly required; a parenthetical in I13 would make it explicit.

## Counts

| Severity | Count |
|---|---|
| High | 1 |
| Medium | 0 |
| Low | 1 |
| Info | 2 |

Claims checked: 14 groups — 13 verified, 1 refuted (F1).
