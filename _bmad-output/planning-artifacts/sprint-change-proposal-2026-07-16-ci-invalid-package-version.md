---
title: Sprint Change Proposal — Repair Invalid Shared Package Version Blocking CI
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approval_required: false
approved_by: Administrator
approved_at: 2026-07-16
implementation_status: in_progress
trigger: >
  GitHub Actions run 29467970597 fails during dotnet restore because
  Hexalith.Builds supplies the NuGet-invalid version string v1.16.3 for
  Hexalith.PolymorphicSerializations.
related:
  - https://github.com/Hexalith/Hexalith.Parties/actions/runs/29467970597
  - https://github.com/Hexalith/Hexalith.Parties/actions/runs/29468665570
  - _bmad-output/implementation-artifacts/spec-gh-87517913711-fix-ci-commons-http-release-output.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - docs/ci.md
  - docs/build-gate.md
---

# Sprint Change Proposal — Repair Invalid Shared Package Version Blocking CI

## 1. Issue Summary

GitHub Actions run `29467970597`, triggered by root commit `d857bf7`, fails in
the shared `ci / build-and-test` job during:

```text
dotnet restore Hexalith.Parties.slnx
```

NuGet reports:

```text
error : 'v1.16.3' is not a valid version string.
```

The later run `29468665570` at root commit `c715eae` fails at the same step with
the same error. Build, package-consumer validation, and all test tiers are
therefore skipped.

### Trigger and classification

- **Triggering work:** the focused CI/package-validation bugfix stream ending in
  `spec-gh-87517913711-fix-ci-commons-http-release-output.md`, followed by root
  submodule-pointer adoption in `d857bf7`.
- **Issue type:** failed dependency-update approach / technical configuration
  defect discovered during CI execution.
- **Problem statement:** `Hexalith.Builds` commit `d600df2` changed
  `HexalithPolymorphicSerializationsVersion` from `1.16.2` to tag-shaped
  `v1.16.3`. NuGet package versions do not accept the Git tag prefix. Builds
  releases `v4.18.9` and `v4.18.10` retain the invalid value, so merely advancing
  to current Builds `main` does not repair Parties CI.

### Evidence

1. Requested run `29467970597` checked out Builds `63d3221` (`v4.18.9`) and
   failed restore with `'v1.16.3' is not a valid version string`.
2. Latest observed Parties run `29468665570` failed identically.
3. Current Builds `main`/`v4.18.10` still contains:

   ```xml
   <HexalithPolymorphicSerializationsVersion
       Condition="'$(HexalithPolymorphicSerializationsVersion)' == ''">v1.16.3</HexalithPolymorphicSerializationsVersion>
   ```

4. NuGet publishes `Hexalith.PolymorphicSerializations` version `1.16.3`; the
   package version is correct without the tag prefix.
5. Local exact reproduction fails:

   ```text
   dotnet restore Hexalith.Parties.slnx
   -> error: 'v1.16.3' is not a valid version string
   ```

6. The diagnostic-only override proves the isolated correction:

   ```text
   dotnet restore Hexalith.Parties.slnx \
     -p:HexalithPolymorphicSerializationsVersion=1.16.3
   -> success; all solution projects restored
   ```

7. The existing deferred-work ledger had already recorded that Builds checkout
   `63d3221` supplied `v1.16.3` and could break a fresh restore if adopted.

## 2. Impact Analysis

### Epic impact

- Epic 8 remains viable and correctly sequenced.
- The failure is consistent with Story 8.1's baseline-stabilization intent and
  Story 8.10's final release-readiness gate, but neither story needs to be
  reopened or rewritten.
- No epic is added, removed, redefined, invalidated, or reprioritized.
- Epics 1–5 remain the completed PRD feature/MVP baseline. Epics 6–8 remain
  maintenance scope with no new functional-requirement coverage.

### Story impact

- No numbered story changes are required.
- Create a focused CI incident spec for run `29467970597`, following the existing
  `spec-gh-*` pattern. It owns the cross-repository fix, regression guard, parent
  gitlink update, and validation evidence without broadening Epic 8 scope.
- Story 8.6 and later migration stories retain their current statuses and gates.

### Artifact conflicts

- **PRD:** no conflict and no edit. Product scope and MVP remain achievable and
  already delivered.
- **Epics:** no semantic conflict and no edit.
- **Architecture:** no semantic conflict and no edit. Existing shared-platform
  ownership and root-gitlink governance already require a shared fix plus an
  owner-validated pointer.
- **UX:** not applicable. No surface, flow, component, copy, responsive, or
  accessibility behavior changes.
- **Sprint status:** no epic/story row changes; no backlog reorganization.
- **CI documentation:** `docs/ci.md` and `docs/build-gate.md` remain accurate and
  need no semantic edit.

### Technical impact

- `Hexalith.Builds` central package metadata changes from tag syntax to NuGet
  version syntax.
- Builds release validation gains a guard that prevents tag-prefixed, blank, or
  unresolved shared version properties from reaching a release.
- Parties advances the root-declared Builds gitlink to the fixed commit and
  records real-owner signoff in `.gitlink-signoff.tsv`.
- No Parties production C#, public API, package inventory, test semantics,
  deployment behavior, or runtime state changes.
- No nested submodule initialization is required or authorized.

## 3. Recommended Approach

**Selected approach: Direct Adjustment at the shared source, followed by an
owner-validated Parties gitlink advance.**

1. Correct `Hexalith.Builds/Props/Directory.Packages.props` from `v1.16.3` to
   `1.16.3`.
2. Add a Builds-side pre-release validator and focused fixture tests so invalid
   shared version properties fail before semantic release publishes a broken
   Builds revision.
3. Commit and push the Builds fix using Conventional Commits; allow its release
   workflow and validator to pass.
4. Advance the Parties Builds gitlink from the failing root pointer to the fixed
   Builds commit, preserving the current forward checkout rather than discarding
   it.
5. Update `.gitlink-signoff.tsv` to the fixed SHA only after explicit owner
   approval. The existing evidence identifies `jpiquot` as the Builds release/
   signoff owner; use that handle only if this proposal is approved without a
   different owner being named.
6. Mark the deferred invalid-version warning resolved by the incident spec.
7. Run exact local CI parity, then push and require a green remote Actions run.

### Effort, risk, and schedule

- **Implementation effort:** Low, approximately 1–2 hours plus CI runtime.
- **Coordination effort:** Moderate because one shared repository and the Parties
  parent gitlink must be committed in the correct order.
- **Technical risk:** Low. The behavior change is a one-character normalization
  to an already-published package version, protected by restore/build/package
  validation.
- **Timeline impact:** CI remains blocked until the shared fix and parent pointer
  land; no product/MVP timeline or feature scope changes.
- **Rollback:** reset the Parties Builds gitlink to the last known-good compatible
  Builds release if the fixed pointer introduces an unrelated regression. Do not
  retain a command-line override as permanent configuration.

### Alternatives considered

#### Option 2 — Potential rollback

Rolling Builds back to `v4.18.8` would remove the invalid value and is technically
viable as an emergency fallback, but it would discard later shared advances and
leave Builds `main` broken for other consumers. It is not recommended as the
primary fix.

- Effort: Low.
- Risk: Medium due to dependency-version regression and renewed gitlink drift.

#### Option 3 — PRD/MVP review

Not viable or necessary. The incident is entirely in build metadata and has no
effect on product requirements, UX, or delivered feature scope.

#### Root-only version override

A root property override to `1.16.3` would unblock Parties but mask the broken
shared catalog and duplicate centrally owned version state. The successful CLI
override is retained only as causal proof and an emergency diagnostic, not as an
implementation choice.

## 4. Detailed Change Proposals

### 4.1 Shared Builds version normalization

Artifact: `references/Hexalith.Builds/Props/Directory.Packages.props`

Section: shared Hexalith version properties.

**OLD:**

```xml
<HexalithPolymorphicSerializationsVersion Condition="'$(HexalithPolymorphicSerializationsVersion)' == ''">v1.16.3</HexalithPolymorphicSerializationsVersion>
```

**NEW:**

```xml
<HexalithPolymorphicSerializationsVersion Condition="'$(HexalithPolymorphicSerializationsVersion)' == ''">1.16.3</HexalithPolymorphicSerializationsVersion>
```

**Rationale:** Git tags use the `v` prefix; NuGet package versions do not. Package
`1.16.3` exists and is the intended dependency.

### 4.2 Builds release regression guard

Artifacts:

- `references/Hexalith.Builds/Tools/validate-central-package-versions.ps1` — new.
- `references/Hexalith.Builds/Tools/test-central-package-version-validator.ps1`
  — new.
- `references/Hexalith.Builds/.github/workflows/build-release.yml` — invoke both
  before `Create Release`.

**OLD:**

```yaml
- name: Validate Dapr package versions
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/validate-dapr-package-versions.ps1

- name: Test Dapr package validator
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/test-dapr-package-version-validator.ps1

- name: Create Release
```

**NEW:**

```yaml
- name: Validate central package versions
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1

- name: Test central package version validator
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/test-central-package-version-validator.ps1

- name: Validate Dapr package versions
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/validate-dapr-package-versions.ps1

- name: Test Dapr package validator
  shell: pwsh
  run: pwsh -NoProfile -File ./Tools/test-dapr-package-version-validator.ps1

- name: Create Release
```

The validator must evaluate the central catalog and fail closed for at least:

- tag-prefixed `v1.2.3` / `V1.2.3` property values;
- blank version properties;
- unresolved MSBuild expressions;
- malformed NuGet/SemVer values.

Fixture tests must cover valid stable/prerelease versions, the exact `v1.16.3`
regression, malformed evaluator output, and workflow wiring.

**Rationale:** the defect must be rejected in the owning repository before it can
break every consuming repository at restore time.

### 4.3 Focused CI incident spec

Artifact:
`_bmad-output/implementation-artifacts/spec-gh-29467970597-fix-invalid-builds-package-version.md`

**OLD:** no incident spec exists for run `29467970597`.

**NEW:** create a frozen intent contract that owns only:

- the Builds version normalization;
- the Builds pre-release regression guard and tests;
- the Parties Builds gitlink/signoff update;
- exact restore/build/package/test/remote-run evidence;
- deferred-work resolution.

It must explicitly prohibit root-only hardcoded version overrides, weakened
restore gates, nested submodule initialization, unrelated dependency bumps, and
changes to Parties product/API behavior.

**Rationale:** recent CI repairs use focused `spec-gh-*` artifacts; this preserves
scope and reviewability without inventing a new epic/story.

### 4.4 Parties Builds gitlink and release-candidate signoff

Artifacts:

- `references/Hexalith.Builds` gitlink in the Parties root repository.
- `.gitlink-signoff.tsv`.

**OLD:**

```text
Root HEAD gitlink: 63d3221262bc520cf80c3e2601d21179d28ed03c
Working checkout:  9e79c5d5a38e83c6eb6f2d7b6a0bb9e405dd4ca1
Ledger Builds row: 2b7aec1fd1ba254900ccf715bd8fec5d54f0c37e
```

**NEW:**

```text
Root gitlink: <fixed-Builds-SHA based on current origin/main>
Working checkout: <same fixed-Builds-SHA>
Ledger Builds row:
references/Hexalith.Builds|<fixed-Builds-SHA>|validated-advance|<release-or-describe>|jpiquot|2026-07-16
```

Use a different real owner handle if supplied during approval. Do not fabricate a
signoff or leave a placeholder owner.

**Rationale:** the current root commit consumes the broken catalog, while the
working checkout has already advanced to a later but still-broken Builds release.
The parent commit must point to the actual fixed revision and satisfy the existing
RC gate.

### 4.5 Deferred-work resolution

Artifact: `_bmad-output/implementation-artifacts/deferred-work.md`

Entry: invalid `v1.16.3` warning sourced from
`spec-gh-87517913711-fix-ci-commons-http-release-output.md`.

**OLD:**

```text
summary: Correct and validate the advanced Hexalith.Builds checkout before adopting its package-version changes.
evidence: ... checkout 63d3221 supplies v1.16.3 as a NuGet version ...
```

**NEW:**

```text
summary: Correct and validate the advanced Hexalith.Builds checkout before adopting its package-version changes.
evidence: ... checkout 63d3221 supplied v1.16.3 and caused Actions runs 29467970597 and 29468665570 to fail during restore.
status: resolved
resolved_by: spec-gh-29467970597-fix-invalid-builds-package-version.md; Builds <fixed-SHA>; Parties <parent-SHA>
```

Leave the two unrelated Memories regression entries unchanged.

### 4.6 PRD, epics, architecture, UX, and sprint status

No text changes. The existing artifacts already classify build quality,
cross-repository ownership, and gitlink signoff correctly. Adding feature or
backlog text for this incident would misrepresent a focused maintenance repair.

## 5. Implementation Handoff

**Scope classification: Moderate.** The code/configuration correction is small,
but it spans the shared Builds repository and the Parties parent gitlink and must
be landed in dependency order.

| Recipient | Responsibility |
| --- | --- |
| Hexalith.Builds Developer/Owner | Normalize the version, add and run the central-version validator/tests, commit and push the Builds change, and confirm the Builds release workflow is green. |
| Parties Developer | Create the focused incident spec, advance the parent gitlink only to the fixed Builds SHA, preserve unrelated worktree changes, resolve the deferred entry, and run local CI parity. |
| Release owner (`jpiquot` unless revised) | Explicitly approve the fixed Builds revision for `.gitlink-signoff.tsv`; do not sign off a merely advanced but still-invalid pointer. |
| Test Architect | Verify exact restore without overrides, Release build with warnings as errors, package-consumer validation, CI contract tests, and a green remote Actions run. |
| Product Owner | Keep PRD/epics/sprint status unchanged and accept the incident spec as maintenance evidence rather than new feature scope. |

### Required implementation sequence

1. Implement and validate the Builds fix.
2. Commit/push Builds and capture its immutable SHA.
3. Update the Parties gitlink and owner ledger to that exact SHA.
4. Run Parties restore/build/package/test validation.
5. Commit/push Parties and monitor the resulting CI run through completion.
6. Record remote evidence and close the deferred warning.

### Success criteria

1. `HexalithPolymorphicSerializationsVersion` evaluates to exactly `1.16.3`.
2. The Builds release gate rejects `v1.16.3` and equivalent invalid central
   versions before semantic release.
3. Parties root gitlink and checked-out Builds SHA match the fixed revision, with
   real-owner signoff.
4. `dotnet restore Hexalith.Parties.slnx` passes with no property override.
5. `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore
   -warnaserror` passes.
6. Package generation, NuGet metadata validation, and package-only consumer
   restore/build pass.
7. Relevant CI contract tests pass and no nested submodules are initialized.
8. The next Parties GitHub Actions CI run reaches and passes the previously
   skipped gates; final remote CI conclusion is successful.
9. No PRD, UX, public API, runtime behavior, or feature coverage changes.

## 6. Change-Analysis Checklist

Implementation note: while the proposal awaited approval, owner-authored Builds
commit `7cd855c` and release `v4.18.11` landed the source normalization. The
approved implementation retains that correction and adds the preventive release
guard at pushed Builds commit `6516faf` before parent adoption.

Post-adoption evidence: Parties run `29482004796` passed Restore, Release Build,
and package-consumer validation, then exposed a separate shared runner-contract
defect: VSTest-only evidence arguments were sent to Microsoft.Testing.Platform.
The implementation therefore adds an explicit backward-compatible test-platform
route in Builds `v4.19.0` and opts Parties into MTP-native TRX/trait arguments;
no test tier is removed or weakened.

Second post-adoption evidence: Parties run `29482841788` passed restore, build,
package-consumer validation, and all 1,649 Tier 1 tests through the new MTP
route. Tier 2 then exposed accepted G4/G8/G11 planning edits that had outpaced
their fitness contract plus newly registered Dapr-backed projection activation
inside isolated gateway tests. The implementation reconciles those contracts
and test seams without changing production behavior or skipping any gate; the
exact local Tier 2 sequence now passes 650 tests.

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story/work | [x] Done | CI/package-validation bugfix stream plus root submodule adoption exposed the recorded Builds defect. |
| 1.2 Core problem | [x] Done | Failed dependency update: Git tag `v1.16.3` was used as a NuGet version. |
| 1.3 Evidence | [x] Done | Two remote failures, exact local reproduction, successful corrected-value restore, NuGet `1.16.3` availability, and deferred warning. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable; no story needs reopening. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope or acceptance change. |
| 2.3 Remaining epics | [x] Done | No future epic dependency changes. |
| 2.4 New/obsolete epics | [N/A] Skip | None. |
| 2.5 Priority/order | [N/A] Skip | No resequencing. CI repair is an immediate maintenance blocker. |
| 3.1 PRD conflict | [N/A] Skip | No product/MVP impact. |
| 3.2 Architecture conflict | [x] Done | Existing shared ownership and gitlink governance apply; no architecture edit. |
| 3.3 UX conflict | [N/A] Skip | No UI/UX impact. |
| 3.4 Other artifacts | [!] Action-needed | Builds catalog/validation, incident spec, parent gitlink/signoff, deferred ledger, and CI evidence. |
| 4.1 Direct adjustment | [x] Viable | Low effort/risk; durable shared-source fix. |
| 4.2 Potential rollback | [x] Not recommended | Emergency fallback only; discards later advances and leaves shared main broken. |
| 4.3 MVP review | [x] Not viable | Not applicable to build metadata. |
| 4.4 Recommended path | [x] Done | Shared correction + pre-release guard + owner-validated parent advance. |
| 5.1 Issue summary | [x] Done | Included above. |
| 5.2 Impact summary | [x] Done | Included above. |
| 5.3 Recommendation | [x] Done | Included above with alternatives and trade-offs. |
| 5.4 MVP/action plan | [x] Done | No MVP change; ordered implementation plan defined. |
| 5.5 Handoff | [x] Done | Builds owner, Parties developer, release owner, test architect, and PO responsibilities defined. |
| 6.1 Checklist review | [x] Done | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | [x] Done | Root cause is directly reproduced and corrected-value restore is proven. |
| 6.3 User approval | [x] Done | Administrator approved implementation on 2026-07-16. |
| 6.4 Sprint-status update | [N/A] Skip | No epic/story rows added, removed, or renumbered. |
| 6.5 Next steps/handoff | [!] Action-needed | Confirm on approval, then route to implementation in dependency order. |
