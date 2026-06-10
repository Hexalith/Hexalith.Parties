# Test Automation Summary — Story 1.5 (Consumer own-data self-authorization, defense-in-depth)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/1-5-consumer-own-data-self-authorization-defense-in-depth.md`
**Date:** 2026-06-10
**Engineer role:** QA automation
**Framework (existing, reused):** xUnit v3 (3.2.2) · Shouldly · NSubstitute
**Mode:** Auto-apply all discovered gaps.

> Written as `test-summary-1-5.md` (not the default `test-summary.md`) to avoid overwriting the
> existing Story 1.1 QA summary in this folder.

## Context

This story is an **auth/accessor/policy** feature with **no live UI page** (the `/me` consumer
landing is an empty stub) and **no public host API** (the actor host is machine-to-machine over DAPR
at `POST /process`, where DAPR strips the JWT). There is therefore no browser/HTTP surface to drive
with Playwright/bUnit E2E flows. The correct "end-to-end" automation at this layer is **behavioral
unit/contract tests** over the security choke point (`ISelfScopedPartiesClient`) and the host
defense-in-depth building blocks (`IDataSubjectAccessService`, `ConsumerPolicy`). No new framework was
introduced — the project's standard stack is reused.

## Gap analysis (coverage vs. acceptance criteria)

The host-side suite (decision matrix, Consumer policy, request-path fitness boundary) and the UI
tripwire/DI-composition suites were already thorough. The single real gap was in the **behavioral
accessor tests**: only 2 of the 10 accessor methods were proven to inject the resolved `party_id` and
fail closed. Each accessor method is a **distinct delegation** to a different underlying gateway call,
so id-injection and fail-closed must be proven **per method** — a wrong-id, param-swap, or
forgot-to-resolve bug in any of the other 8 GDPR methods would have passed silently.

| AC | Guarantee | Before | After |
|---|---|---|---|
| AC1 | Each accessor method injects the **resolved** `party_id` into the correct underlying call | 2 / 10 | **10 / 10** |
| AC2 | Each accessor method **fails closed** (throws, no client call) when **unbound** | 2 / 10 | **10 / 10** |
| AC2 | Each accessor method fails closed when **ambiguously** bound | 1 / 10 | **10 / 10** |
| AC1 | Caller arguments forwarded unchanged alongside the injected id | `Revoke` only | `Revoke`, `Grant`, `Restrict` |
| AC1 | Accessor returns the underlying result unchanged (no drop/transform) | — | added (`GetMyConsent`) |
| AC1 | Caller `CancellationToken` forwarded (not dropped to `default`) | — | added (`GetMyParty`) |

ACs already well covered, left as-is: **AC3** (host decision matrix + Consumer policy),
**AC4** (request-path fitness boundary), **AC5** (Scoped lifetime + `ValidateScopes`),
**AC6** (tripwire reflection).

## Generated / extended tests

### `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs` (extended)

- [x] **Per-method id-injection (AC1)** — 8 new `[Fact]`s: `GetMyConsent`, `GrantMyConsent`,
      `RequestMyErasure`, `GetMyErasureStatus`, `RestrictMyProcessing`, `LiftMyRestriction`,
      `ExportMyData`, `GetMyProcessingRecords` each pass the resolved `party-123` (never a caller id)
      into the right `IAdminPortalGdprClient` method, forwarding caller args (`channelId`/`purpose`/
      `LawfulBasis`, `reason`) unchanged.
- [x] **Per-method fail-closed (AC2)** — two `[Theory]`s (`[MemberData]` over all 10 method names)
      proving every method throws `InvalidOperationException` and issues **zero** underlying gateway
      calls (`ReceivedCalls().ShouldBeEmpty()` on both clients) for an **unbound** and an
      **ambiguously bound** principal.
- [x] **Return propagation (AC1)** — `GetMyConsentAsync` returns the underlying client's result
      instance unchanged.
- [x] **Cancellation-token forwarding (AC1)** — `GetMyPartyAsync` forwards the caller's token, not
      `default`.

(Pre-existing in the file, retained: bound/unbound/ambiguous for `GetMyParty`; bound/unbound for
`RevokeMyConsent`.)

### Unchanged suites (already satisfying their ACs — no edits)

- `SelfScopedPartiesClientSurfaceTests.cs` — tripwire reflection: no `List*`/`Search*`, no
  `PagedResult<>` return, no `partyId` parameter (AC6).
- `SelfScopedPartiesClientCompositionTests.cs` — Scoped descriptor + root-throws/scope-resolves under
  `ValidateScopes=true` (AC5).
- `Hexalith.Parties.Tests/Authorization/DataSubjectAccessServiceTests.cs` — fail-closed decision
  matrix incl. Ordinal case-sensitivity (AC3).
- `Hexalith.Parties.Tests/Authorization/PartiesConsumerPolicyTests.cs` — `Consumer`/`Admin`/role-less
  posture + every declared role name (AC3).
- `ArchitecturalFitnessTests.PartiesRequestPath_DoesNotUseDataSubjectAccessService` — request-path
  boundary (AC4).

## Results

| Suite / filter | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| UI EXE — `-class "*SelfScopedPartiesClient*"` | 40 | 40 | 0 | 0 |
| UI EXE — full suite | 109 | 109 | 0 | 0 |

- Build: `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` → **0 Warning(s), 0 Error(s)**
  (solution-wide `TreatWarningsAsErrors`).
- Build gate: `bash scripts/check-no-warning-override.sh` → **OK** (no warning-override / nested-submodule
  regression).
- Host project + host tests were **not modified** by this QA pass (Part B was already fully covered);
  their baseline verdict is unchanged. The lone red `AppHostTenantsTopologyTests…` remains a
  pre-existing, unrelated baseline failure (documented in the story Debug Log), not touched here.

Reproduce (xUnit v3 MTP — run the EXE directly; `dotnet test --filter` reports "Zero tests ran"):

```bash
dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1
EXE=tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests
"$EXE" -class "*SelfScopedPartiesClient*"   # 40 passed
"$EXE"                                       # 109 passed
```

## Coverage

- **Accessor methods (AC1 id-injection):** 10 / 10 (was 2 / 10).
- **Accessor methods (AC2 fail-closed — unbound + ambiguous):** 10 / 10 (was ≤ 2 / 10).
- **Acceptance criteria AC1–AC6:** all covered by executable tests.
- **API tests:** N/A — neither host exposes a public API in this story (BFF + M2M actor host).
- **Browser/page E2E:** N/A today — no consumer data page exists (`/me` is an empty stub).

## Next steps

- Run in CI (`scripts/test.ps1 -Lane unit`, Release).
- When the **deferred gateway self-principal** lands and the host-side `IDataSubjectAccessService` is
  wired into a live consumer-principal request path (Epic 4 / Story 1.7), add the request-path
  integration test that the AC4 fitness boundary currently — correctly — forbids today.
- When the first real consumer data page consumes `ISelfScopedPartiesClient`, add a bUnit
  component-level test of the live round-trip.
