# Reviewer Gate — Rubric Walker Review
- reviewer: RUBRIC WALKER
- date: 2026-09-08
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- mode: VALIDATE-only
- companions judged: `.memlog.md`; parent `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`; spec `_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md`; lint `reviews/lint-2026-09-08.json` (0 findings)
- historical reviews in `reviews/` treated as history only; every item below was re-verified against today's tree

## Verdict

**PASS WITH CONDITIONS.** The reconciliation spine still does its core job: I1–I18 plus the §4 / I17 gate still bind stories and deferral executors; the Class A / G7–G9 supersession is still written as approved-but-gated and `Hexalith.Parties.Authentication` is still in-repo; SPEC CAP-1..6 are covered in substance; I-n IDs are stable; lint is clean; no placeholders. The condition that blocks an unconditional pass is a reopened identity-map failure: the §7 I4 cell still prints EventStore `3.102.0` / `acf5c4e…` and Builds `8db7459…`, while today's 8.3 matrix, `PlatformApiPrerequisitesTests`, and `.gitlink-signoff.tsv` pin EventStore `3.103.0` / `c6efdbba…` and Builds `35c3d1e5…`. I4's Rule correctly names the matrix as source of truth, so this is HIGH (stale snapshot), not CRITICAL (missing rule). Four MEDIUM gaps remain on the spine itself: no named paradigm / Inherited Invariants table, I1 silent on the brownfield pub/sub `/tenants/events` door, operational dimensions that live only in SPEC OQs / memlog, and still-undecidable I1a / I5 / I2 approval phrases. None of those undoes the gate; each can still let two units one level down choose incompatibly if they read only this file.

## Findings

### F1 — §7 I4 snapshot prints superseded EventStore and Builds identities
- Severity: high
- Disposition if updating: autofix
- Evidence: `ARCHITECTURE-SPINE.md:250`; `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:69-72`; `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:23-34`; `.gitlink-signoff.tsv:87-88`; I4 Rule at `ARCHITECTURE-SPINE.md:107-112`; I16 at `ARCHITECTURE-SPINE.md:156-167`
- Why it fails the checklist item: Checklist 2 (the published evidence must not contradict the Rule), 4 (named tech current), and 5 (ratify today's brownfield). The I4 Rule says the 8.3 matrix records the exact package version or gitlink SHA. The §7 I4 cell still asserts package `3.102.0`, source `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6`, and Builds `8db7459d065926501ee045b3aaf7b816780905e5` (last re-reconciled 2026-09-06). Today's matrix, fitness constants, and signoff ledger record package `3.103.0`, source `c6efdbba6439370a5c674c12ed866c959624106a`, and Builds `35c3d1e5b8a55a74a440b9c2cad4c5e18747b241` (2026-09-08). Commons `6da79aed2daa4e199689331ee3196f7872c0988a` still matches. The cell's hedge ("this row is a snapshot… not the identity source of truth") keeps this from being the 2026-08-18 CRITICAL/HIGH "Executable pin that only exists in the working tree" failure — I4 + I16 still point at the matrix — but two units that copy the spine map as the pin list still diverge, and I16's "identity change re-opens claims" is not reflected in this document (frontmatter `updated: 2026-08-18` at line 5; `.memlog.md` last event is the 2026-08-18 closure commit). Same failure class as historical F1, reopened by later catalog moves.

### F2 — Named paradigm absent; inherited Epic 7 ADs not listed by original ID
- Severity: medium
- Disposition if updating: discuss
- Evidence: spine has no `## Design Paradigm` and no `## Inherited Invariants` (headings at `ARCHITECTURE-SPINE.md:19,47,70,189,213,226,233,294`); parent paradigm and ADs at `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md:47-124`; Epic 8 artifact-set mention at `ARCHITECTURE-SPINE.md:32-39`; Class A supersession at `ARCHITECTURE-SPINE.md:62-68`
- Why it fails the checklist item: Checklist 7 and the extra "named paradigm present?" test. The parent names "adapter-first strangler migration over event-sourced platform boundaries" and binds AD-1..AD-6. This spine never restates that paradigm and never lists those AD IDs as inherited. Inheritance is "read together" prose plus one gated Class A paragraph. Two story / deferral executors who treat this file as the contract can miss that AD-1 / AD-5 / AD-6 still bind except the one recorded G7/G9 exception. The supersession itself is still correctly gated-not-executed (`Hexalith.Parties.Authentication` remains at `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj`); that part of checklist 7 holds.

### F3 — I1's Rule ignores the brownfield pub/sub `/tenants/events` door
- Severity: medium
- Disposition if updating: discuss
- Evidence: `ARCHITECTURE-SPINE.md:73-80`; `src/Hexalith.Parties/Program.cs:55-69`; `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml:13-20`; `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:379-385`
- Why it fails the checklist item: Checklist 2 (Rule must prevent the stated divergence) and 5 (ratify brownfield). I1 says there is no public API, traffic enters via the EventStore gateway, and the ACL admits only the `eventstore` app ID to the listed POST routes. The host still maps `POST /tenants/events` via `MapEventStoreDomainEvents()`; the ACL YAML and the I1-cited fitness test treat that path as pub/sub delivery, not service-invocation ACL. Two CAP-2 executors of `8.6-residual-review-debt` can add `/tenants/events` to the I1 allow-list, treat it as out of scope, or delete it as a "public API." I1's route-change ledger would catch a list edit, but it does not bind the documented second door.

### F4 — Operational and cross-cutting dimensions are silent on the spine
- Severity: medium
- Disposition if updating: discuss
- Evidence: spine headings (no Deferred / Open Questions / Stack / Structural Seed); SPEC OQ-1..5 at `_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md:103-114`; memlog deferrals at `.memlog.md:19`; Epic 7 telemetry convention at parent `ARCHITECTURE-SPINE.md:133`; I8 no-leak clause at `ARCHITECTURE-SPINE.md:122-125`
- Why it fails the checklist item: Checklist 8 (every owned dimension decided, deferred, or an open question — especially the operational/environmental envelope). Deployment and local topology are decided or deferred (I1a, §2 last row, `external-runtime-deployment`). Production key-backend / KMS, infra/provider strategy, Epic 7 low-cardinality telemetry, freshness-grammar ownership, and the Memories mapping persistence class (I19 candidate) do not appear on the spine. They exist as SPEC OQs and memlog revisit notes. A unit that reads only this file can deliver CAP-3 against a dev-only key backend, add high-cardinality telemetry, or classify the Memories mapping store as a replay-from-zero read model (I9) while another treats it as a rebuild-surviving operational ledger. Companion OQs do not satisfy the spine-altitude rule.

### F5 — I1a / I5 / I2 still lack a decidable approval mechanism
- Severity: medium
- Disposition if updating: discuss
- Evidence: `ARCHITECTURE-SPINE.md:88-90` ("explicitly approved platform AppHost owner"); `ARCHITECTURE-SPINE.md:114-116` ("stable or intentionally versioned"); `ARCHITECTURE-SPINE.md:98-100` ("payload-protection hooks the SDK cannot own"); SPEC OQ-5 at `SPEC.md:111-114`; `.memlog.md:19` (V12)
- Why it fails the checklist item: Checklist 2 (two independent builders must converge). I1a now names the AppHost-tied deferrals and says rollback clauses win over parity retirement — that 2026-08-18 hole stays closed. The remaining phrases still have no recorded approver, artifact, or decision record. SPEC OQ-5 restates the same gap; the spine Rule does not.

### F6 — §7 I1a row names only `8.8-runtime-boundary-cleanup`
- Severity: medium
- Disposition if updating: autofix
- Evidence: I1a Rule at `ARCHITECTURE-SPINE.md:93-96` (lists `8.6-residual-review-debt`, `8.8-runtime-boundary-cleanup`, `external-runtime-deployment`); §7 I1a row at `ARCHITECTURE-SPINE.md:248` (names only 8.8); I1 row at `ARCHITECTURE-SPINE.md:246`; `EpicEightClosureFitnessTests.cs:148-152` (Deferred rows must name ≥1 expected deferral, not the full I1a set)
- Why it fails the checklist item: Checklist 2 and 3. The binding Rule forbids AppHost retirement until every AppHost-naming deferral has passed or been re-approved. The map row a CAP-4 executor will copy names only 8.8. The closure fitness test stays green because one named deferral is enough. Two units can therefore "obey" I1a differently: one waits for all three proofs; one retires the AppHost when 8.8 topology parity lands.

### F7 — I13 still says purge FAST/v4 tokens; retained CSS still consumes them
- Severity: low
- Disposition if updating: defer
- Evidence: `ARCHITECTURE-SPINE.md:145-146`; I13 row at `ARCHITECTURE-SPINE.md:259` (owns focus-visible / Playwright, not token purity); `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css:74-75` (`var(--neutral-fill-stealth-rest)` with no fallback); also `CreateEditPartyPage.razor.css:51`, `MyProfilePage.razor.css:143`; `.memlog.md:19` (V15)
- Why it fails the checklist item: Checklist 2 and 5. The I13 Rule is not fully realized and has no named purity gate. The I13 row is honest that parity is not discharged, so this stays LOW (owned by 8.9 / historical V15), not a reopened HIGH.

### F8 — No Stack, Structural Seed, or Capability→Architecture map; I12 names no versions
- Severity: low
- Disposition if updating: ignore
- Evidence: missing sections vs template; I12 at `ARCHITECTURE-SPINE.md:142-144`; `global.json:3` (`10.0.400`); `references/Hexalith.Builds/Props/Directory.Packages.props` (Dapr `1.18.5`, Aspire `13.5.3`, FluentUI `5.0.0-rc.5-26219.1`, `xunit.v3` `4.0.0`); SPEC CAP-1..6 at `SPEC.md:27-63`
- Why it fails the checklist item: Extra tests (seed vs invariant split; named tech pinned). I12 is a discipline invariant, not a version table. CAP-1..6 are covered in substance by I1–I18 and the §7 deferral rows, so the missing map is structural, not a coverage miss. Deep version currency is a sibling lens.

### F9 — I17's activation annotation is not visible to the closure parser
- Severity: low
- Disposition if updating: ignore
- Evidence: `ARCHITECTURE-SPINE.md:168-176`; `EpicEightClosureFitnessTests.cs:391-398` (`ParseDeferrals` reads `owner` / `exit_proof` / `rollback` / `evidence` only); DW-99 `activated_by_spec` at `deferred-work.md:808`; `.memlog.md:23` (P6 rejected as test-side)
- Why it fails the checklist item: Checklist 3. I17's waiting-versus-working contract is still the right Rule. A second spec can activate 8.9 in prose while the fitness parser cannot see `activated_by_spec`. Previously recorded; not worsened; not a spine-text defect.

## Closed in today's files (not re-opened)

- Class A / G7–G9 supersession still stated as gated, not executed (`ARCHITECTURE-SPINE.md:62-68`); `Hexalith.Parties.Authentication` still present.
- I16–I18 still present with enforceable substance (identity stamp, deferral §4 inheritance, baseline / weaken-across-changesets).
- §4 is scoped by property; §5 binds `8.6→8.7→8.8→8.9` and the concurrency disjointness test.
- I1 service-invocation route list matches `accesscontrol.parties.yaml:35-73` (13 POST routes).
- I3 row names all five accepted closure deferrals (`ARCHITECTURE-SPINE.md:249`).
- I13/I14 rows remain honest about the 2026-08-18 shell slice and undischarged I13 parity.
- §2 precedence still requires recorded SCP / owner authority (`ARCHITECTURE-SPINE.md:62-64`).
- Mixed §7 dispositions use capital "Deferred"; I-n IDs are I1, I1a, I2–I18 with no reuse.
- All 29 named `*Tests` classes in §7 exist under `tests/`; all five `ExpectedDeferrals` exist in `deferred-work.md` with owner / exit_proof / rollback / evidence.
- No TBD/TODO/FIXME/placeholder tokens; lint-2026-09-08.json reports 0 findings.
- SPEC CAP-1..6 are each bound by at least one I-n plus a named deferral or closure artifact (no formal map — see F8).

## Checklist disposition summary

| # | Checklist item | Disposition |
| --- | --- | --- |
| 1 | Fixes real divergence points for stories / deferral executors | Yes for the deletion-heavy path (I1–I18, §4, I17, §5). Misses the pub/sub door (F3) and the incomplete I1a map row (F6). |
| 2 | Every I-n Rule enforceable / two builders converge | Mostly. I1–I15 have substance (Binds/Prevents/Rule without those labels). Gaps: F1, F3, F5, F6, F7. |
| 3 | Deferred / ledger items cannot diverge without a gate | I17 is the gate. Hole: I1a map vs Rule (F6); I17 activation not machine-visible (F9). Silent OQs are not in a spine Deferred section (F4). |
| 4 | Named tech verified-current | Spot-check: .NET 10 / xUnit v3 / Fluent 2 generation still current. I4 snapshot versions are not (F1). Deep pass is a sibling lens (F8). |
| 5 | Ratifies brownfield | Yes for ACL routes, SDK host (`Program.cs:18,75`), Authentication rollback surface, and named tests. No for I4 snapshot pins (F1), I1 pub/sub wording (F3), residual FAST tokens (F7). |
| 6 | Covers SPEC CAP-1..6 | Yes in substance (CAP-1↔I4/I16/I18/§7; CAP-2↔I1/I2/I7/I9/I10; CAP-3↔I8; CAP-4↔I1a/I2/I6/I11; CAP-5↔I13/I14; CAP-6↔I1/I3 + `external-runtime-deployment`). No CAP→I-n table (F8). |
| 7 | Parent Epic 7 ADs not weakened except gated Class A | No weakening found beyond the recorded gated G7/G9 exception, which is still gated-not-executed. Missing Inherited Invariants table (F2). |
| 8 | Every owned dimension decided / deferred / open | Deploy/topology/ops partly covered. Silent on the spine: KMS, telemetry restatement, freshness grammar, persistence class (F4). |
| extra | Named paradigm; seed vs invariant; placeholders; I-n stability | Paradigm missing (F2). Seed mixed into I4/I12 (F1, F8). Placeholders none. IDs stable. |

## Finding count

- Critical: 0
- High: 1 (F1)
- Medium: 5 (F2, F3, F4, F5, F6)
- Low: 3 (F7, F8, F9)
