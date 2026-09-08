# Reviewer Gate — Closure-Evidence Integrity Review
- reviewer: CLOSURE-EVIDENCE
- date: 2026-09-08
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` §7 map (`<!-- epic-8-invariant-map:start -->` / `:end`, lines 243–262) and §7a I16–I18 (lines 264–292)
- mode: VALIDATE-only
- independent of: `reviews/review-closure-evidence.md` (2026-08-18). That review scored I4 as matching then-current test constants and found no critical/high items. The 2026-09-08 tree does not repeat that I4 fact.

## Verdict

**Conditional.** The §7 map is still honest on the headline that **no deferred item is represented as delivered**: stories 8.7 / 8.8 / 8.9 remain `blocked`, all five accepted deferral IDs remain `status: open` ledger entries with owner / exit_proof / rollback / evidence, and I13 is still labeled `Deferred (parity not yet discharged)`. Every named `*Tests` class exists in a `scripts/test.ps1` runnable project and still guards the claimed slice at name-and-assertion level. I18 baseline commit `2b63ab9` is still resolvable. The map-parse fitness tests passed.

The map is **not load-bearing as of 2026-09-08** on the I4 / I16 identity contract. The I4 row prints 2026-09-06 pins (`3.102.0` / `acf5c4e4…` / `8db7459d…`) while the 2026-09-08 durable record (8.3 matrix, `PlatformApiPrerequisitesTests`, `.gitlink-signoff.tsv`) already moved to `3.103.0` / `c6efdbba…` / `35c3d1e5…`. HEAD then moved Builds again to `a32cb422…` with no signoff line, no test-constant update, and no I16 `unvalidated` marker. I4 is labeled Executable; that executable surface would fail at current HEAD. That is an I16 stop, not a documentation nit.

Counts: **1 critical / 1 high / 3 medium / 1 low**.

## Method

Checked, as of 2026-09-08, against:

- Spine §7 / §7a / I1 route list / I16–I18 prose
- `_bmad-output/implementation-artifacts/deferred-work.md` DW-11, DW-76, DW-96–DW-99, DW-111
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` reconciliation table
- `.gitlink-signoff.tsv`
- `_bmad-output/implementation-artifacts/sprint-status.yaml` epic-8 rows
- `_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md`
- Named test classes under `tests/`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`
- `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css`
- Live git: `git rev-parse --verify 2b63ab9^{commit}`, `git ls-tree HEAD` on root gitlinks, `git log` / `git show bf8daf98`

Fitness command (xUnit v3 in-process runner, two methods only):

```text
tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests -noLogo \
  -method Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests.InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence \
  -method Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests.AcceptedDeferralsAreCompleteAndKeepIncompleteStoriesHonest
```

Result: **Total: 2, Errors: 0, Failed: 0, Skipped: 0, Time: 0.181s, exit 0.**

`dotnet test --filter` was attempted first and ran **zero tests** (exit 5). The assembly was re-invoked directly, which is the repo’s xUnit v3 lane.

`PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` was **not** executed (MSBuild graph evaluation; not cheap). Identity mismatch is established from test constants vs `git ls-tree HEAD` / checkout `rev-parse`.

## Identity ledger (I4 / I16)

| Pin | Spine I4 snapshot (line 250) | Tests (`PlatformApiPrerequisitesTests`) | 8.3 matrix (lines 69–72) | Signoff latest | HEAD gitlink / checkout |
| --- | --- | --- | --- | --- | --- |
| EventStore package | `3.102.0` | `3.103.0` (catalog assert, line 691) | `3.103.0` | n/a (package) | n/a |
| EventStore source | `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6` | `c6efdbba6439370a5c674c12ed866c959624106a` (line 34) | `c6efdbba…` (line 70) | `c6efdbba…` (`.gitlink-signoff.tsv:88`) | `c6efdbba…` |
| Commons HTTP source | `6da79aed2daa4e199689331ee3196f7872c0988a` | `6da79aed…` (line 24) | `6da79aed…` (line 71) | `6da79aed…` (line 57) | `6da79aed…` |
| Builds catalog | `8db7459d065926501ee045b3aaf7b816780905e5` | `35c3d1e5b8a55a74a440b9c2cad4c5e18747b241` (line 23) | `35c3d1e5…` (line 72) | `35c3d1e5…` (line 87) | **`a32cb422749352cce8dec948aa3e78c8f00eb4cf`** |
| FrontComposer (not in I4 row) | n/a | `a0acb78f…` (line 29) | `a0acb78f…` (line 73) | `a0acb78f…` (line 89) | `a0acb78f…` |
| Closure-test FrontComposer stamp | n/a | `EpicEightClosureFitnessTests` `FrontComposerSha` = `f0c3b6fd…` (line 19) | Playwright receipt still `f0c3b6fd…` | superseded by `a0acb78f…` | `a0acb78f…` |

HEAD Builds describe: `v4.27.2-10-ga32cb42`. Last authorized Builds signoff is `v4.27.2-9-g35c3d1e` dated 2026-09-08. The increment is commit `bf8daf984829e99c2d8ed65ced8377c2ff8377fa` (`chore: update subproject reference for Hexalith.Builds to latest commit`), Builds gitlink only.

I16 (`ARCHITECTURE-SPINE.md:156–167`) requires that any retained-identity change re-run named test surfaces at the new identity **or** record the claim as `unvalidated` in the 8.3 matrix before merge. Matrix grep for `unvalidated` / I16 re-validation markers: **no hits**. Signoff has **no** `a32cb422` line.

## I18 baseline

- `git rev-parse --verify 2b63ab9^{commit}` → `2b63ab9ca55ced5f3742b877cd16e01e9b7e37a1`
- `2b63ab9:_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md` exists
- Map markers and I1–I15 rows exist at that commit
- Named test-class set at `2b63ab9` matches today’s named set
- I13 disposition at `2b63ab9` was `Executable + Deferred` without “parity not yet discharged”; current row is stricter, not a silent weakening of a baseline surface
- I4 pins at `2b63ab9` were `3.95.0` / `454b4d10…` / `6fbac0c5…` / `17b1c7aa…` — later amendments are expected; I16 is the rule that those amendments must be identity-stamped

§7a (`ARCHITECTURE-SPINE.md:284–288`) is accurate: `InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence` is necessary-but-not-sufficient (class existence, not weakening, not identity). That test passed today. It cannot see the Builds HEAD drift.

## Headline: no deferred item represented as delivered

Holds.

| Deferral ID | Ledger | Fields | Sprint | Map representation |
| --- | --- | --- | --- | --- |
| `8.6-residual-review-debt` | DW-96, `deferred-work.md:783–788`, `status: open` | owner / exit_proof / rollback / evidence present; `authored_by_spec` set | `8-6-projection-and-query-sdk-migration: done` with residual debt called out; not claimed as 8.6-debt delivered | I1/I2/I3/I7/I9/I10 name it as deferred owner |
| `8.7-data-protection-extraction` | DW-97, `:790–795`, `status: open` | all four fields; `authored_by_spec` set | `8-7-data-protection-extraction: blocked` (`sprint-status.yaml:187`) | I3/I8 deferred |
| `8.8-runtime-boundary-cleanup` | DW-98, `:797–802`, `status: open` | all four fields; `authored_by_spec` set | `8-8-client-mcp-apphost-build-and-deploy-cleanup: blocked` (`:194`) | I1a/I3/I6/I11 deferred |
| `8.9-frontcomposer-ui-consolidation` | DW-99, `:804–809`, `status: open` | all four fields; `activated_by_spec` + `delivered_slices` = G4-F only; I13 still undischarged | `8-9-ui-frontcomposer-and-fluent-consolidation: blocked` (`:206`) | I3/I13/I14 deferred; I13 explicitly not discharged |
| `external-runtime-deployment` | DW-11, `:91–97`, `status: open` | all four fields; `authored_by_spec` set | not a story-done row | I1/I3 deferred |

`EpicEightClosureFitnessTests.ExpectedDeferrals` (`EpicEightClosureFitnessTests.cs:21–28`) is this same five-ID set. `AcceptedDeferralsAreCompleteAndKeepIncompleteStoriesHonest` passed, including 8.7/8.8/8.9 = `blocked`.

SPEC companion (`SPEC.md:52–57`) still treats CAP-5 as remaining G4 slices with only the shell slice delivered. Consistent.

## I1 ACL vs YAML

Spine I1 (`ARCHITECTURE-SPINE.md:73–80`) lists 13 POST routes, deny-default, `eventstore` only, owner file `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`.

YAML (`accesscontrol.parties.yaml:27–73`) matches that set exactly: `/process`, `/query`, `/admin/operational-index-metadata`, `/project`, `/project/v2`, `/project/v2/reconcile`, `/replay-state`, `/project/rebuild/v1`, `/project/rebuild/shared/v1`, `/project/rebuild/stage/v1`, `/project/rebuild/commit/v1`, `/project/rebuild/abort/v1`, `/project/rebuild/verify/v1`.

`DocumentationFitnessTests.ExpectedSdkRoutes` (`DocumentationFitnessTests.cs:26–41`) and `ArchitecturalFitnessTests` expected routes (`ArchitecturalFitnessTests.cs:338–353`) assert the same 13 names, deny ×2, single `appId: eventstore`, POST-only, no `/**`. Honest.

## I13 vs later FrontComposer adoption

Later FrontComposer moves (package `4.4.0`, source `a0acb78f…`) did **not** discharge I13. Matrix FrontComposer primitives row remains `needs-additive-api`. Sprint 8.9 comment (`sprint-status.yaml:195–205`) and DW-99 `delivered_slices` still limit delivery to G4 work package F. Playwright receipt (`test-summary.md:636`) still says I13 is not discharged because forced-colors focuses `.fc-skip-link` (DW-111, `deferred-work.md:903–909`). Disposition is honest.

The I13 remaining-gap sentence that the shell slice left focus-visible and forced-colors **unscoped and therefore dead** (`ARCHITECTURE-SPINE.md:259`) is **stale**. `MainLayout.razor.css:1–20` now scopes those rules under `.parties-main-content`. Sprint 8.9 and DW-99 already record the isolation repair. The honest remaining gap is DW-111 (Playwright content-control focus), not dead CSS.

`MainLayoutAccessibilityTests` (`MainLayoutAccessibilityTests.cs:42–80`) still asserts FrontComposer skip links and landmarks only — matching the “shell slice, not I13 discharge” claim.

## Per-row verification

Legend: **exists** = named class declared in a `scripts/test.ps1` project with `[Fact]`/`[Theory]`. **Coverage honest** = assertions still match the slice the row claims. **Deferral backed** = named ID is an accepted-wait ledger entry with owner / exit_proof / rollback / evidence. **Identity-stamped** = for Executable rows that name a retained dependency identity, stamps match the durable record **and** HEAD.

| Row | Test exists? | Coverage honest? | Deferral backed? | Notes |
| --- | --- | --- | --- | --- |
| I1 | Yes | Yes | Yes | Static ACL/route contract matches YAML and both fitness classes. Runtime ACL still owned by `8.6-residual-review-debt`; env orchestration by `external-runtime-deployment`. Honest static/runtime split. |
| I1a | n/a (Deferred) | n/a | Yes | DW-98 rollback keeps Parties AppHost; exit_proof requires topology/security/publish/rollback parity before retirement. 8.8 remains blocked. |
| I2 | Yes | Partial | Yes | `RetiredLeafProjectFitnessTests` still asserts `AddEventStoreDomainService` / `UseEventStoreDomainService` (`RetiredLeafProjectFitnessTests.cs:91–94`). `EventStoreGatewayE2ETests` still `return`s when `!_fixture.IsAvailable` (`EventStoreGatewayE2ETests.cs:35–38`) — vacuously green without Docker/DAPR. Map discloses topology-gating. Authenticated handler-discovery still deferred to DW-96. |
| I3 | n/a (Deferred) | n/a | Yes | Names all five deferrals; each rollback field retains the local path the invariant requires. Release-recovery is on `external-runtime-deployment` (DW-11). |
| I4 | Yes | **No (stamps)** | n/a | `PlatformApiPrerequisitesTests` exists and still separates package vs source. Printed spine SHAs ≠ 2026-09-08 durable record ≠ HEAD Builds. See F1/F2. Identity-stamped: **fail at HEAD**. |
| I5 | Yes ×5 | Yes (name-level) | n/a | `ContractsPublicApiSnapshotTests`, `ClientPackageTests`, `PartyPickerPackagingTests`, `AdminPortalPackagingTests`, `ConsumerPortalPackagingTests` exist in the matching package test projects. Identity-stamped: N/A (Parties-local public surface). |
| I6 | Yes ×3 | Yes | Yes | Routing / HTTP query / self-scope classes exist. Shared-helper adoption still on DW-98. |
| I7 | Yes ×3 | Yes for named slice | Yes | Consent / erasure / verification classes exist. Residual certificate-identity and Memories-race debt still on DW-96; not claimed delivered. |
| I8 | Yes ×3 | Yes | Yes | Compatibility harness, GDPR guardrail, erasure verification exist. Shared-engine extraction still on DW-97; 8.7 blocked. |
| I9 | Yes | Yes | Yes | `PartySdkProjectionHandlerTests` still covers replay, checkpoint, idempotency, duplicate, out-of-order. Residual quality debt on DW-96. |
| I10 | Yes ×2 | Yes | Yes | Query + freshness/degradation classes still cover rebuild, stale-read, erased-index, tombstone. Open freshness/search-bounds debt on DW-96. |
| I11 | Yes ×3 | **Partial** | Yes | `IdentifierHygieneFitnessTests` bans `Guid.Parse` / `Guid.TryParse`. `IdentifierValidatorTests` accepts ULID and legacy GUID (`IdentifierValidatorTests.cs:14–23`). `PartyAggregateCompositeTests` has **no** GUID/legacy-ID replay case (ULID-only party IDs; “identifier” hits are VAT/SIRET). GUID-shaped replay is over-attributed. Commons-helper adoption still on DW-98. |
| I12 | Yes ×2 | Yes | n/a (open CI item named) | Inventory + publish-workflow classes exist. Always-on CI Playwright lane still DW-76 (`deferred-work.md:620–626`), which the row discloses. Identity-stamped: Playwright receipt stamp is `f0c3b6fd…`, not live FrontComposer `a0acb78f…` (F4). |
| I13 | Yes ×2 | Disposition yes; remaining-gap text stale | Yes | Not discharged. CSS isolation **was** repaired (`MainLayout.razor.css:1–20`). Later FrontComposer `4.4.0` / `a0acb78f…` did not flip 8.9 to done. See F3. |
| I14 | Yes ×2 | Yes | Yes | Consent/privacy page tests exist. Remaining copy consolidation on DW-99; shell slice recorded as delivered without advancing 8.9. |
| I15 | Yes | Yes | n/a | `EpicEightAddsNoPrdFunctionalRequirement` still freezes PRD FR/NFR IDs against `37f4ec8…` and requires the spine’s zero-FR sentences. Identity-stamped: N/A. |

### I16–I18 (not in the parsed map)

| Row | Disposition claimed | Honest? | Notes |
| --- | --- | --- | --- |
| I16 | Partially executable; residual is review-time | **Broken at HEAD** | Enforcement class exists and pins identities, but HEAD Builds `a32cb422` is not that pin and has no `unvalidated` matrix marker. §7a’s “review-time obligation” does not license a silent gitlink bump. |
| I17 | Process gate; ledger vocabulary | Yes | DW-96–99 / DW-11 carry `authored_by_spec` or `activated_by_spec`; DW-99 has `delivered_slices`. Packed `status: accepted` is gone; fitness treats ExpectedDeferrals membership as the accepted-wait set (`EpicEightClosureFitnessTests.cs:41–42`). |
| I18 | Process gate; baseline = map at `2b63ab9` | Yes (resolvable) | Commit and spine path exist. Fitness parser checks the **current** map, not a frozen `2b63ab9` text dump — matching §7a’s necessary-but-not-sufficient caveat. |

## Findings

### F1 — HEAD Builds gitlink moved past the last I16-authorized pin with no re-validation
- Severity: **critical**
- Evidence: `git ls-tree HEAD references/Hexalith.Builds` → `a32cb422749352cce8dec948aa3e78c8f00eb4cf`; checkout `rev-parse` matches; `git show --stat bf8daf98` is Builds-only. Test pin `BuildsSha = "35c3d1e5b8a55a74a440b9c2cad4c5e18747b241"` (`PlatformApiPrerequisitesTests.cs:23`). Signoff last Builds line is `35c3d1e5…|validated-advance|v4.27.2-9-g35c3d1e|jpiquot|2026-09-08` (`.gitlink-signoff.tsv:87`). Matrix Builds identity is `35c3d1e5…` (`story-8-3-platform-api-prerequisite-matrix.md:72`). I16 stop rule: `ARCHITECTURE-SPINE.md:156–167`. No `unvalidated` marker in the matrix.
- Why it matters: I4 is Executable. The named I16 enforcement test asserts gitlink == `BuildsSha`. At HEAD that assertion is false. SPEC CAP-1 (`SPEC.md:31–32`) says a fresh clone of HEAD passes `PlatformApiPrerequisitesTests` with the pinned identities. Later commits after `2b63ab9` did move identities; the 2026-09-08 refresh was recorded. `bf8daf98` was not.

### F2 — I4 snapshot SHAs are two refresh cycles behind the durable record they defer to
- Severity: **high**
- Evidence: spine I4 (`ARCHITECTURE-SPINE.md:250`) still prints EventStore package `3.102.0`, source `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6`, Builds `8db7459d065926501ee045b3aaf7b816780905e5`, last named re-reconcile dates 2026-09-05 and 2026-09-06. Durable record as of 2026-09-08 content: package `3.103.0`, source `c6efdbba…`, Builds `35c3d1e5…` (matrix lines 69–72; `PlatformApiPrerequisitesTests.cs:23–34, 691`; signoff lines 87–88). Commons `6da79aed…` is the one pin that still matches.
- Why it matters: the row’s “snapshot, re-read matrix/signoff” caveat is real, but the printed SHAs are what a reviewer copies. They are not the 2026-09-08 pins. Sprint-status 8.10 commentary also stops at 2026-09-06 round 4 (`sprint-status.yaml:247–255`).

### F3 — I13 remaining-gap text still claims unscoped/dead CSS after the isolation repair
- Severity: **medium**
- Evidence: spine I13 (`ARCHITECTURE-SPINE.md:259`) “unscoped and therefore dead at runtime”. Contrasted with `MainLayout.razor.css:1–20` (scoped `.parties-main-content` + `:focus-visible` + `forced-colors`) and DW-99 `delivered_slices` / sprint 8.9 comment that the isolation wrapper was repaired. Honest remainder is DW-111 (`deferred-work.md:903–909`) and `test-summary.md:636`.
- Why it matters: disposition “parity not yet discharged” is still true. The mechanism sentence is not. Later FrontComposer adoption did not silently flip I13 to delivered.

### F4 — FrontComposer identity stamps are split across fitness tests and the a11y receipt
- Severity: **medium**
- Evidence: `PlatformApiPrerequisitesTests.FrontComposerSha` = `a0acb78f…` (`PlatformApiPrerequisitesTests.cs:29`) matches HEAD and signoff. `EpicEightClosureFitnessTests.FrontComposerSha` = `f0c3b6fd…` (`EpicEightClosureFitnessTests.cs:19`). Playwright receipt evidence (`test-summary.md:636`) is still stamped `f0c3b6fd…` and claims it matches “the current reconciliation table,” which now lists `a0acb78f…` (`story-8-3-platform-api-prerequisite-matrix.md:73`).
- Why it matters: I16 says a11y/parity evidence is valid only at the stamped identity. I12/I13 receipts are stamped at a superseded FrontComposer SHA.

### F5 — I11 attributes GUID-shaped replay to `PartyAggregateCompositeTests`, which has no GUID ID case
- Severity: **medium**
- Evidence: spine I11 (`ARCHITECTURE-SPINE.md:257`). `PartyAggregateCompositeTests.cs` has no `Guid`/`GUID`/`legacy` match; party IDs in that class are `UlidPartyId`. GUID acceptance lives in `IdentifierValidatorTests.cs:14–23`. Hygiene ban is `IdentifierHygieneFitnessTests.cs:25–26`.
- Why it matters: the named surface does not guard the claimed GUID-replay half. ULID acceptance is real; the GUID half is mis-cited.

### F6 — I2 E2E surface is still a silent no-op without topology
- Severity: **low**
- Evidence: `EventStoreGatewayE2ETests.cs:35–38` returns without `Assert.Skip`. Map text already says “runs fully only with Docker/DAPR available” (`ARCHITECTURE-SPINE.md:248`). Same structural gap the 2026-08-18 review filed; still true, still disclosed.

## What still holds

- All 29 named `*Tests` tokens in the marked map resolve to real classes under runnable projects.
- Five accepted deferrals exist, fields complete, none marked delivered, 8.7/8.8/8.9 blocked, epic-8 `in-progress`, 8.10 `review`.
- I1 route list = YAML = both fitness expected-route arrays.
- I13 is not treated as discharged despite FrontComposer `4.4.0` / `a0acb78f…`.
- I18 commit `2b63ab9` still exists; map at that commit still exists.
- Map-parse + deferral-field fitness methods passed (command and result above).
- I17 vocabulary is present on the five closure deferrals.

## Out of scope / not claimed

- Did not re-run `PlatformApiPrerequisitesTests` MSBuild graph tests; HEAD Builds mismatch is established from constants vs git.
- Did not deep-read every I5/I6/I8 assertion body beyond class existence and targeted greps.
- Did not treat the 2026-08-18 closure-evidence review as evidence of current I4 honesty.
