# Reviewer Gate — Rubric Walker Review

- reviewer: RUBRIC WALKER
- date: 2026-08-18
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- mode: VALIDATE-only (no spine or project files modified)

## Verdict

**PASS WITH CONDITIONS.** As a reconciliation spine this document does its core
job well: it honestly ratifies the already-landed 8.2–8.5 work instead of
rewriting history, its invariants I1–I15 are mostly concrete and genuinely
enforceable (I1's route list matches the shipped ACL and two fitness suites
verbatim; I4's dependency-identity rule is the strictest statement of Epic 7
AD-5 in the corpus), the §4 per-spec readiness gate demonstrably propagated
into specs 8.6–8.10 (all five declare prerequisites, block-if conditions,
rollback, validation lanes, non-goals, and parity checklists, and broad
stories were split/hard-gated as required), no invariant weakens an inherited
Epic 7 decision, and §7's closure evidence map is unusually well machine
enforced — all 29 named test classes exist, all 5 named deferral ledger
entries exist with owner/exit-proof/rollback/evidence, and
`EpicEightClosureFitnessTests` fail-closed cross-checks the map itself. The
deviation from AD-n Binds/Prevents/Rule structure costs little in practice:
§7 supplies the invariant→evidence binding, and I1–I15 IDs are used stably
across specs, ledger, and sprint status. The conditions: one HIGH finding —
§7's I4 "Executable" row asserts a Builds identity that exists only in the
uncommitted working tree, contradicting I4's own rule and §7's "no deferred
item is represented as delivered" preamble — plus four MEDIUM findings on
staleness against the epic's real story set (8.11–8.13), two extraction
capabilities missing from the §2 target table, three invariants with
undecidable approval mechanisms, and a silent production key-backend/KMS
dimension. None invalidates the reconciliation; the HIGH item must be
annotated before §7 is treated as closure-grade evidence.

---

## Findings

### F1 — §7 I4 asserts "Executable" pinned identities that violate I4's own rule in the current tree

- **Severity:** HIGH
- **Fails checklist:** 2 (rule actually prevents its divergence), 3 (nothing
  deferred/asserted lets two units diverge), and §7's own integrity claim.
- **Evidence:**
  - Spine line 181 (I4 row): "`PlatformApiPrerequisitesTests` … pins …
    Builds catalog `17b1c7aae3e1854e464f17bd88d527f8350ea203`." Spine lines
    168–173 (§7 preamble): "No deferred item is represented as delivered."
  - `git ls-tree HEAD references/Hexalith.Builds` returns
    `6b7807533cea31aa7592450742a5c94dd1bc1d9f` — the committed superproject
    gitlink is **not** `17b1c7aa`. `git submodule status` shows
    `+17b1c7aa… references/Hexalith.Builds (v4.24.0)`: the pin exists only as
    an uncommitted working-tree checkout (FrontComposer, Memories, and
    PolymorphicSerializations gitlinks are likewise drifted; EventStore
    `454b4d10` and Commons `6fbac0c5` do match their committed gitlinks).
  - `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:18–21`
    hard-codes these SHAs — on a fresh clone the Builds assertion fails, so
    the "executable" evidence is not reproducible from the repository state.
  - The spine's own I4 (lines 83–89) says a row must record "the exact
    released package version or root-declared submodule gitlink SHA" and that
    "a checked-out source file or `available` status alone is not consumption
    evidence." The Builds pin is currently exactly that: a checkout.
  - `sprint-status.yaml:197–201` already admits this: "Closure stays open
    because those fixes remain modified dependency working trees rather than
    immutable owner commits/releases selected by the superproject." §7
    carries no such caveat.
- **Impact:** Two independent builders diverge immediately — one on this
  working tree sees I4 green; one on a fresh clone sees it red. A reader of
  the spine alone would treat dependency identity as settled when the epic's
  own tracking says it is the open closure condition.
- **Remedy:** Annotate the §7 I4 row (and preamble) with the open condition —
  e.g., "Executable in the current tree; Builds/FrontComposer identities
  pending immutable superproject gitlink commit per sprint-status 8.10 note"
  — or withhold the "Executable" disposition for I4 until the gitlinks are
  committed. Do not let §7 read as closure-grade while sprint-status says
  closure stays open on this exact point.

### F2 — Spine is stale against the epic's actual story set; the §4 gate is scoped by enumeration, not by property

- **Severity:** MEDIUM
- **Fails checklist:** 1 (fixes divergence points for the level below and
  misses none), 8 (dimension left silent), 9 (epic discipline).
- **Evidence:**
  - Spine line 155 (§5): "`8.1 → … → 8.10` (unchanged)"; lines 131–151 (§4):
    gate applies to "specs 8.6–8.10" by enumeration.
  - `sprint-status.yaml:202–219`: stories 8.11 (validation ladder), 8.12
    (Zot container publish CI), and 8.13 (**deletion-heavy** retirement of
    in-repo Kubernetes/Dapr/Zot deployment assets) were added by later
    correct-course proposals and are all `done` — none passed through §4,
    and §5 still claims the sequence is unchanged.
  - Spine lines 177 and 189 (§7 I1/I12 rows) silently consume their outputs
    ("absence of retired in-repo deployment assets",
    `PartiesContainerPublishWorkflowTests`) without §2/§4/§5 admitting the
    stories exist.
- **Impact:** The deletion-safety gate the spine exists to provide does not
  bind by construction on any story added after 2026-07-07. 8.13 deleted
  assets under SCP authority alone; a future 8.14+ deletion story escapes the
  gate the same way. I3's rollback list ("projection, query, crypto, release
  recovery") also never covered deployment assets.
- **Remedy:** Reword §4 to gate "any deletion-heavy spec in this epic,
  present or later added" and add a one-line §5 amendment acknowledging
  8.11–8.13 and their SCP authority (2026-07-07/2026-07-08 proposals).

### F3 — §2 target table omits two extraction capabilities the change proposal names; the Class A shared-anchor supersession is never stated

- **Severity:** MEDIUM
- **Fails checklist:** 6 (covers the driving input's capabilities), 7
  (parent-spine inheritance made explicit).
- **Evidence:**
  - `sprint-change-proposal-2026-07-06.md:98–102` (story 8.4): "consume
    platform tenant-claim transformation and delete
    `Hexalith.Parties.Authentication`"; lines 121–125 (story 8.9): replace
    local FrontComposer-like status/freshness/reconcile/grid/picker
    primitives.
  - Spine §2 (lines 50–56) MOVES column names Commons, EventStore SDK,
    DataProtection, "owning modules" for envelopes/paging/MCP, Builds,
    EventStore.Aspire, FrontComposer.AppHost, platform-ops — but neither
    tenant-claims transformation (owner decided 2026-07-16 as
    EventStore.Authentication + Commons `IsValidUlid`, per
    `sprint-status.yaml:395–412`) nor the FrontComposer UI primitives
    (G4 row, `sprint-status.yaml:413–424`) appears.
  - Epic 7 spine line 62 inherits the "Class A shared-anchor boundary":
    "Epic 7 does not re-open in-repo anchors already routed to
    `Hexalith.Parties.Contracts` or `Hexalith.Parties.Authentication`."
    Epic 8 deliberately supersedes this (8.4 deletes Authentication), with
    SCP authority — but the Epic 8 spine never states the supersession, and
    it has no inherited-invariants section at all (the Epic 7 spine is only
    listed as an artifact-set member, line 34).
- **Impact:** The two ownership decisions most likely to be re-litigated by
  an 8.8/8.9 executor live only in companion artifacts; a builder reading
  the spine's own MOVES column could route them elsewhere. The unstated
  Class A supersession invites a false conflict report against the parent.
- **Remedy:** Add two §2 rows (tenant-claims transformation →
  EventStore.Authentication per SCP 2026-07-16; UI
  status/freshness/grid/picker primitives → FrontComposer Contracts.UI/Shell
  per G4 routing), and one sentence noting the deliberate, SCP-authorized
  supersession of the Epic 7 Class A anchor boundary for
  `Hexalith.Parties.Authentication`.

### F4 — Three invariants lack a decidable approval mechanism

- **Severity:** MEDIUM
- **Fails checklist:** 2 (would two independent builders converge?).
- **Evidence:**
  - I1a (spine lines 70–73): "an **explicitly approved** platform AppHost
    owner" — approved by whom, recorded where, is undefined.
  - I5 (line 92): package contracts "stable **or intentionally versioned**" —
    no versioning-approval mechanism, although the driving SCP explicitly
    flagged that moving security contracts out of
    `Hexalith.Parties.Contracts` "may need a major-version strategy"
    (`sprint-change-proposal-2026-07-06.md:160–162`). Epic 7's parent
    Deferred section required "a separate release plan approves the break";
    Epic 8 restates the weaker half only.
  - I2 (lines 74–76): Parties retains "payload-protection hooks the SDK
    **cannot own**" — a judgment call with no required owner-decision record.
- **Impact:** Each phrase is a divergence seam: one builder treats an SCP
  comment as approval, another demands a matrix row; one calls a contract
  break "intentional," another calls it a violation.
- **Remedy:** Bind each to a recorded artifact: AppHost-owner approval and
  "cannot own" hook determinations to a Story 8.3 matrix row or ADR;
  intentional versioning to an approved release/breaking-change plan
  (restoring the parent's stronger wording).

### F5 — Operational envelope: production key-backend/KMS is silent; parent telemetry conventions bind only by reference

- **Severity:** MEDIUM
- **Fails checklist:** 8 (every owned dimension decided, deferred, or open —
  operational/environmental envelope).
- **Evidence:**
  - The spine decides deployment ownership (§2 line 56, §7
    `external-runtime-deployment`) and local topology (I1a), but says nothing
    about the production key backend, even though Story 8.7 moves key
    management and the epic's own tracking names the "production key-backend
    package" as an undelivered G5 component (`sprint-status.yaml:169–177`,
    296–320). The readiness report likewise flags production KMS as an
    operational prerequisite before real regulated data
    (`implementation-readiness-report-2026-07-07.md:290`).
  - Epic 7's "Logs and telemetry: low-cardinality and PII-free" convention
    (Epic 7 spine line 133) is inherited only via the artifact-set listing;
    the Epic 8 spine restates just the narrower "no-leak diagnostics" (I8).
- **Impact:** An 8.7 executor could deliver shared-engine parity against a
  dev-only key backend and read the spine as satisfied; a builder could add
  high-cardinality telemetry without violating any Epic 8 sentence.
- **Remedy:** Add a one-line open-question/deferral naming the production
  key-backend/KMS prerequisite (owner: EventStore payload-protection owners,
  tracked under G5), and one line stating which Epic 7 conventions remain
  binding wholesale.

### F6 — §7 I3 row omits the 8.6 deferral that also retains rollback surfaces

- **Severity:** LOW
- **Fails checklist:** 3 (deferred dispositions must not let two units
  diverge).
- **Evidence:** Spine line 180 (I3 row) names only
  `8.7-data-protection-extraction`, `8.8-runtime-boundary-cleanup`, and
  `8.9-frontcomposer-ui-consolidation`; but
  `deferred-work.md:375–381` (`8.6-residual-review-debt`) also mandates
  retention: "retain source/package selection plus the Parties AppHost and
  gateway topology as switch-back … do not delete further host, query,
  projection, or ACL compatibility seams."
- **Impact:** A reader of the I3 row alone could conclude 8.6-era seams are
  already deletable.
- **Remedy:** Add `8.6-residual-review-debt` to the I3 row's deferral list.

### F7 — §7 I15 evidence claim is broader than what the test verifies

- **Severity:** LOW
- **Fails checklist:** 2 (evidence precision).
- **Evidence:** Spine line 192 says `EpicEightClosureFitnessTests` "verifies
  that Epic 8 changes no PRD functional-requirement artifact," but
  `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:10,154–178`
  diffs `parties-ui-prd.md`/`epics.md` only against
  `BaselineCommit = 37f4ec8` — the closure-era HEAD. It guards forward drift
  from closure, not the whole epic (and `epics.md` legitimately changed
  during Epic 8 to add the maintenance backlog). The static string
  assertions cover the "not reported as MVP delivery" clause only textually.
- **Remedy:** Reword the row: "pins the PRD/epic FR inventory unchanged from
  the closure baseline forward and asserts the zero-FR/maintenance-only
  declarations."

### F8 — Named-tech currency flags (for the tech-currency reviewer; not deep-verified here)

- **Severity:** LOW
- **Fails checklist:** 4 (flag only).
- **Items to verify:** .NET 10 / `.slnx` / CPM / xUnit v3 / Shouldly /
  NSubstitute / bUnit / Playwright / MinVer (I12), Fluent 2 / FAST-v4 purge
  (I13), DAPR gateway (I1). Pins: EventStore package `3.95.0` vs source pin
  `v3.95.0-2-g454b4d10` — the source is **2 commits past the release tag**,
  so package and source identities intentionally diverge (the fitness test
  records them separately per I4, but the pairing deserves owner
  confirmation); Commons HTTP `6fbac0c5` is `v2.30.0-10` (10 past tag);
  Builds `17b1c7aa` is the `v4.24.0` tag itself (but see F1 — not the
  committed gitlink).

---

## Checklist disposition summary

| # | Checklist item | Disposition |
| --- | --- | --- |
| 1 | Fixes real divergence points for 8.6–8.10, misses none | Mostly yes for 8.6–8.10 (I1–I15 + §4 propagated into all five specs, verified); misses the post-spine story set → F2 |
| 2 | Every rule enforceable / two builders converge | Largely yes (I1 route list == shipped ACL; I4, I9, I10 concrete); exceptions → F4, evidence-precision → F1, F7 |
| 3 | Deferred cannot cause divergence | Ledger entries complete (owner/exit-proof/rollback/evidence, machine-checked fail-closed); gaps → F1, F6 |
| 4 | Named tech verified-current | Flagged for the owning reviewer → F8 |
| 5 | Ratifies brownfield | Yes — explicit ratification of 8.2–8.5 with readiness-report evidence; §7 map matches 29 real test classes and the real ACL |
| 6 | Covers the change proposal's capabilities | Yes except two §2 omissions → F3 |
| 7 | Inherits Epic 7 without weakening | No contradictions found (I3≈AD-1/AD-6, I9/I10≈AD-2, I7/I8≈AD-3, §2≈AD-4, I4≥AD-5); unstated deliberate supersession of the Class A anchor boundary → F3; weaker restatement of the break-approval rule → F4 |
| 8 | Every owned dimension decided/deferred/open | Deployment/topology/ops decided or deferred with owners; silent: production key-backend/KMS, telemetry restatement → F5 |
| 9 | Epic-spine discipline | Good — no per-story sprawl (I1's route enumeration is a boundary contract, acceptable); §7-as-evidence-ledger is a deviation but machine-enforced and dated; staleness → F2 |

## Finding count

- Critical: 0
- High: 1 (F1)
- Medium: 4 (F2, F3, F4, F5)
- Low: 3 (F6, F7, F8)
