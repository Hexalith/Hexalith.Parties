# Test Automation Summary — Story 1.7 (Live freshness via SignalR + shared optimistic-reconcile effect)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-06-10 · **Engineer role:** QA automation
**Story:** `_bmad-output/implementation-artifacts/1-7-live-freshness-via-signalr-shared-optimistic-reconcile-effect.md`
**Framework (detected, reused):** xUnit v3 (MTP) + Shouldly + NSubstitute + bUnit · run the test **EXE** directly (`dotnet test --filter` returns "Zero tests ran").

## What this run did

Story 1.7 was already implemented and shipped with 25 dev-authored tests (suite 199 green). This QA pass is a
**coverage-gap fill**: every branch of the four delivered building blocks was traced against the existing tests
and the acceptance criteria, and the uncovered branches were closed with **9 new automated tests**. No production
code was changed — tests only.

## Generated Tests (this pass — 9 new)

### Optimistic-reconcile primitive — `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`
- [x] `ReconcileReturningNonCurrent_AnnouncesDegradedPolitely_KeepsWaiting_ThenReconcilesOnceWhenCurrent` — **AC1c/AC2**: a non-`Current` re-read announces `StatusKind.Degraded` (polite, `role=status`) and keeps waiting; reconciles exactly once when a later confirm returns `Current` (`OptimisticReconcile.cs:185-188`).
- [x] `Cancellation_WhileAwaitingTheSignalRConfirm_DropsSilently_NoRevertNoFailure` — **AC3**: cancellation *after* acceptance while parked on the projection-confirm drops silently — no revert, no failure announce, only the initial polite "Saving…" (`:215-218`).
- [x] `ReconcileReadThrowsTransiently_IsRetriedOnTheNextConfirm_NoRevert` — **AC3**: a transient re-read failure is swallowed (logged), not reconciled, not reverted; the next confirm retries and reconciles once (`:171-175`).

### Degraded-header seam — `tests/Hexalith.Parties.UI.Tests/DegradedResponseHeaderHandlerTests.cs`
- [x] `PostWithDegradedHeaders_DoesNotCapture_GetOnly` — **AC2**: a non-GET response never flips the degraded flag (GET-only branch, `DegradedResponseHeaderHandler.cs:35`).
- [x] `GetDegradedWithoutStaleAgeHeader_IsDegradedWithNullAge` — **AC2**: degraded with a missing `X-Stale-Data-Age` → degraded, null age.
- [x] `GetDegradedWithMalformedStaleAgeHeader_IsDegradedWithNullAge` — **AC2**: an un-parseable age is dropped, degraded still holds (`long.TryParse` false arm).
- [x] `GetWithServiceDegradedFalse_LeavesAccessorNotDegraded` — **AC2**: explicit `X-Service-Degraded: false` → not degraded.
- [x] `SecondHealthyGet_ClearsPriorDegradedState` — **AC2**: a later healthy GET clears the prior degraded snapshot (age included) — `DegradedStateAccessor.Set` reset.

### Polling fallback — `tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessFallbackTests.cs`
- [x] `Cancellation_StopsThePollLoopCleanly` — **AC2**: cancellation mid-wait completes the interval wait and stops the loop cleanly with no extra poll (`ProjectionFreshnessFallback.cs:66-71`).

## Coverage (AC → automated proof)

| AC | Covered by | Status |
|---|---|---|
| AC1 — optimistic + polite "Saving…" → command → reconcile (SignalR **or** `Freshness=Current`, whichever first); reject → revert + `role=alert` | `OptimisticReconcileTests` (happy×2, non-Current-then-Current, rejection×3, cancel×2, dup/late, announce-not-steal) | ✅ |
| AC2 — SignalR subscribe + degraded fallback (poll + freshness; non-`Current` → `Degraded`; capture `X-Service-Degraded`/`X-Stale-Data-Age`) | `ProjectionFreshnessFallbackTests` (4), `DegradedResponseHeaderHandlerTests` (7), Degraded-announce branch | ✅ |
| AC3 — reconnect, no duplicate application; one-shot guard; announce-not-steal; user-cancel silent; transient-read retry | `OptimisticReconcileTests` (dup/late, cancel-on-issue, cancel-on-await, transient-read), `PartiesProjectionSubscriptionTests` (reconnect) | ✅ |
| AC4 — Scoped lifetime, `ValidateScopes=true`, lazy/inert no-hub-URL boot | `ProjectionFreshnessCompositionTests` | ✅ |
| AC5 — placement/purity/scope boundary (structural) | n/a (verified by composition + reuse of Story 1.6 types) | ✅ design |
| AC6 — all of the above without a live hub | entire suite runs over `FakeProjectionStream` + `ManualTimeProvider` (no network) | ✅ |

**Module test count:** Optimistic-reconcile 12 · Subscription wrapper 5 · Degraded header 7 · Polling fallback 4 · Composition 6.

## Validation results

- **Build (Release, `-m:1`, `TreatWarningsAsErrors`):** `tests/Hexalith.Parties.UI.Tests` → `0 Warning(s) 0 Error(s)`.
- **Full UI suite (test EXE):** **208 total, 0 failed, 0 skipped** (199 prior 1.1–1.7 + 9 new — no regression).
- **New-class filters:** `*OptimisticReconcile*` + `*DegradedResponseHeader*` + `*ProjectionFreshnessFallback*` → 23/23 pass.
- **`scripts/check-no-warning-override.sh`:** green (no `TreatWarningsAsErrors` override, no nested-submodule init).

## Checklist (`.claude/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- [x] API/seam tests generated (header handler over a fake `HttpMessageHandler`; primitive/fallback over the `IProjectionStream` seam)
- [x] E2E-of-the-mechanism tests generated (full optimistic→reconcile flows; no live hub required)
- [x] Standard framework APIs (xUnit v3 + Shouldly + NSubstitute; no Moq/FluentAssertions/raw `Assert.*`)
- [x] Happy path covered · [x] critical error/edge cases covered
- [x] All generated tests run successfully (208/208)
- [x] Semantic/accessible assertions (politeness via `StatusPresentation.PolitenessFor`, never hard-coded "polite"/"alert")
- [x] Clear descriptive test names · [x] No hardcoded waits/sleeps (fake `TimeProvider`) · [x] Tests independent (no order dependency)
- [x] Summary created · [x] tests saved under `tests/Hexalith.Parties.UI.Tests/` · [x] coverage metrics included

## Notes / next steps

- The degraded-header **relay** through the EventStore gateway and the live per-circuit **OIDC token capture** remain
  documented runtime residuals (deferred to the first authenticated data screen, Epic 2/4) — `ProjectionFreshnessMetadata`
  stays the **primary** degraded signal; the header seam is captured-when-present. No test can close these without a live run.
- Run the suite in CI; add screen-level bUnit coverage when the first consuming screen wires the primitive (Epic 2+).
