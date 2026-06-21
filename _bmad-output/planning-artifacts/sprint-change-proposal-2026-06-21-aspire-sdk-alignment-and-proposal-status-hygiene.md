---
project_name: parties
user_name: Administrator
date: 2026-06-21
scope_classification: Minor
status: implemented
implementation_verified: 2026-06-21
supersedes: []
relates_to:
  - sprint-change-proposal-2026-06-21-deferred-work-reconciliation.md
  - sprint-change-proposal-2026-06-09.md
  - implementation-readiness-report-2026-06-21.md
---

# Sprint Change Proposal — Aspire AppHost SDK alignment + 2026-06-09 proposal status hygiene

## 1. Issue Summary

A `correct-course` run was invoked with the directive **"implement any deferred or pending
work."** This is the **third** invocation of essentially the same directive (after the
2026-06-09 package update and the 2026-06-21 deferred-work-reconciliation run).

**Sweep finding — no deferred story or code work exists.** All 30 story specs are `done`,
all 5 epics have retrospectives, and `implementation-readiness-report-2026-06-21.md` is
**✅ READY — 0 critical / 0 major**. The 2026-06-21 deferred-work-reconciliation run had
already reconciled the substantive documentation items (Story 1.10 AC7 + TLS-secret name,
the new tech-debt register/TD-1, the 2026-06-12 proposal status stamp).

The sweep additionally confirmed that the remaining items flagged as "deferred" are
**intentional by-design deferrals that must NOT be implemented**:

- *Out-of-MVP (architecture-deferred):* gateway data-subject self-principal, production KMS,
  Blazor Server scaling, FluentUI RC→GA.
- *Sandbox-blocked → deferred to CI:* Story 1.3 Task 6 live-browser role verification and
  Story 1.10 interactive Playwright a11y (`ui-a11y` job). Neither can run in the local WSL
  sandbox (no Docker/OIDC; `_framework/blazor.web.js` serves 0 bytes). Automated bUnit/SSR
  gates are the binding proof; the interactive gate runs in CI.
- *TD-1 (RCL status/freshness sharing boundary):* `mitigated`; its trigger (a 3rd RCL needs
  the primitives, or the AdminPortal/ConsumerPortal copies drift) has not fired.

**Two genuinely-open items remained, both addressed by this proposal:**

- **(A) — One real technical finding the prior runs missed: an Aspire version skew.**
  `Directory.Packages.props` carried `Aspire.Hosting` = **13.4.6**, but
  `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` pinned
  `Aspire.AppHost.Sdk/`**13.4.3**. The 2026-06-09 package proposal had deliberately kept
  these two **matched** at 13.4.3; a subsequent bump of `Aspire.Hosting` to 13.4.6 left the
  AppHost SDK pin behind. The known failure mode of this mismatch is that DCP dies with
  `unknown flag: --tls-cert-file` and the Aspire dashboard hangs at "Starting dashboard…".
  `project-context.md` documented the gap matter-of-factly as "patch skew", so it was
  ambiguous whether it was intentional.

- **(B) — Cosmetic doc hygiene.** The two **2026-06-09** sprint-change-proposals stated
  their status **inline** (`**Status:** …`) rather than as YAML `status:` frontmatter like
  every later proposal. (Note: contrary to the deferred-work-reconciliation proposal's
  "they lack a status field", both *did* state status — just not in YAML frontmatter.)

**Discovery context:** this completes the proposal-status-hygiene initiative the
2026-06-21 deferred-work-reconciliation run started, and adds the package-version
consistency layer that the deploy-path reconciliation runs did not cover.

## 2. Impact Analysis

**Epic impact:** None. No FR/NFR changes, no epic reorder, no new/obsolete epic. The
readiness verdict (✅ READY, 100% FR/NFR coverage) is unaffected.

**Story impact:** None. No story acceptance criteria change.

**Artifact / code impact (this change):**

- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` — `Aspire.AppHost.Sdk`
  pin bumped `13.4.3` → **`13.4.6`** to match `Aspire.Hosting`.
- `_bmad-output/project-context.md` — version table corrected: the orchestration row no
  longer reports a "patch skew"; it records SDK = Hosting = `13.4.6` and the matched-version
  requirement.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-09.md` — YAML frontmatter
  added (`status: implemented`).
- `_bmad-output/planning-artifacts/sprint-change-proposal-readiness-2026-06-09.md` — YAML
  frontmatter added (`status: implemented`).

**Technical impact:** Build-only. The SDK bump is a compile/restore-time MSBuild SDK
resolution change with no API-surface effect. `Aspire.AppHost.Sdk/13.4.6` confirmed present
on the NuGet feed; restore and a Release build of the AppHost both pass green.

## 3. Recommended Approach

**Direct Adjustment** (checklist Option 1). No PRD/epic reorder, no rollback, no MVP review
— same classification as the preceding 2026-06-16 and 2026-06-21 proposals.

- **Effort:** Low (one csproj line + three documentation edits).
- **Risk:** Low. The build is green, the version is confirmed on the feed, and — after Docker
  and Dapr were brought up — the matched-version contract's payoff was **confirmed live**
  (see §5): DCP started and the dashboard reached "Login to the dashboard at …". The bump
  *reduces* risk relative to the prior skew.

## 4. Detailed Change Proposals

### Edit A — Align the AppHost SDK pin (the real technical finding)

`src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` line 1:

> OLD: `<Project Sdk="Aspire.AppHost.Sdk/13.4.3">`
>
> NEW: `<Project Sdk="Aspire.AppHost.Sdk/13.4.6">`

**Rationale:** the AppHost SDK version must equal the `Aspire.Hosting` package version
(13.4.6) or DCP fails at startup. **Verification:** `13.4.6` confirmed on the NuGet feed;
`dotnet restore` of the AppHost succeeded; `dotnet build … -c Release --no-restore -m:1`
→ **Build succeeded, 0 Warning(s), 0 Error(s)** (solution-wide `TreatWarningsAsErrors`, so
this is a true-green verdict). **Live runtime verification — see §5.**

### Edit B — Correct the project-context.md version table

The orchestration row previously read `13.4.6 / SDK 13.4.3 (patch skew; …)`. Corrected to
`13.4.6 / SDK 13.4.6 (matched — AppHost SDK pin must equal Aspire.Hosting; …)` so the
context file reflects the now-aligned, build-verified state and records the constraint.

### Edit C — YAML status frontmatter on the two 2026-06-09 proposals

Added YAML frontmatter (`status: implemented`, plus `status_detail` preserving the original
inline wording, `scope_classification`, and `relates_to`) to
`sprint-change-proposal-2026-06-09.md` (Minor) and
`sprint-change-proposal-readiness-2026-06-09.md` (Moderate). All six sprint-change-proposals
now carry machine-readable `status:` frontmatter.

## 5. Implementation Handoff

**Scope classification:** Minor.

**Routed to:** Developer agent — **implemented in-line.**

**Success criteria (all met):**

- ✅ `Aspire.AppHost.Sdk` pin equals `Aspire.Hosting` (both `13.4.6`); AppHost restores and
  builds green (0/0) on the aligned version.
- ✅ **Live runtime confirmed.** With Docker + Dapr running, `dotnet run` on the AppHost
  (Release, `http` profile) brought **DCP up and the dashboard reached "Login to the
  dashboard at http://localhost:15100/login?t=…" in ~8 s**, and DCP spawned its containers
  (`keycloak:26.6`, `dcptun_developer_ms:0.24.3`). The `unknown flag: --tls-cert-file`
  mismatch failure mode did **not** occur. The AppHost was then torn down cleanly (DCP
  removed its containers on SIGTERM; Docker returned to its 4 baseline `dapr_*` containers,
  ports `15100`/`21100` freed).
- ✅ `project-context.md` no longer reports a skew; it records the matched version and the
  must-match constraint.
- ✅ All six sprint-change-proposals carry YAML `status:` frontmatter.
- ✅ No story acceptance criteria, FR/NFR coverage, manifests, or live cluster resources
  changed.

**Out of scope (noted, not actioned):** the by-design deferrals enumerated in §1 (out-of-MVP
architecture deferrals, sandbox-blocked-to-CI verification gates, TD-1). These are conscious
deferrals with documented triggers and are tracked where appropriate; they are not pending
work to implement.
