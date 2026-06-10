# Test Automation Summary — Story 1.3 (Role-based landing & policy-gated navigation)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Framework:** xUnit v3 + bUnit + Shouldly + NSubstitute
**Date:** 2026-06-10 · **Story status entering QA:** `review` (feature + 11 UI/3 realm tests already green)
**Mode:** gap-fill — auto-applied all discovered coverage gaps against AC1/AC2/AC3.
**Note:** written as `test-summary-1-3.md` (story-scoped) to preserve the prior `test-summary.md` (Story 1.1).

## What this pass added

Story 1.3 shipped with solid tests; this pass closed **four genuine gaps** the existing suite left open.
No production code changed — tests only.

### Generated / extended tests

| File | Type | Cases | Gap closed |
|---|---|---|---|
| `tests/Hexalith.Parties.UI.Tests/PartiesUiNavEntryGatingTests.cs` (NEW) | bUnit (E2E render) | 3 | **A** — "never cross-render" proven **in-repo** |
| `tests/Hexalith.Parties.UI.Tests/NavEntryGatingHarness.razor` (NEW) | bUnit harness | — | reproduces the shell's `<AuthorizeView Policy>` gate |
| `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` (NEW) | reflection | 3 | **D** — per-area `[Authorize(Policy)]` + route gating |
| `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthorizationPolicyTests.cs` (EDIT) | DI-composition | +8 | **B** — every declared role **case-variant** → correct policy |
| `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs` (EDIT) | bUnit (E2E render) | +9 | **C** — case-variant landing + multi-role **precedence** |

### Gap rationale

- **A — Never cross-render (AC1/AC2).** The pre-existing registration test pinned the gating *inputs*
  (`RequiredPolicy`); nothing in this repo rendered the *output*. The new test feeds the **actual
  registered entries** through the same `<AuthorizeView Policy="@entry.RequiredPolicy">` snippet the shell
  uses (`FrontComposerNavigation.razor:51-55`, reproduced in the harness) and asserts an Admin principal
  sees only `/admin`, a Consumer only `/me`, an anonymous principal neither. Each denial is paired with a
  positive render assertion, so it is not vacuous.
- **B — Case-variant role→policy (AC3).** `RequireRole` is **ordinal/case-sensitive**, which is exactly
  why `AdminRoleNames` enumerates `admin / Administrator / administrator / tenantowner`. A data-driven
  `[Theory]` now evaluates **every** declared name through the real `IAuthorizationService` — trimming a
  variant from the single source of truth now fails the build, not a production login.
- **C — Case-variant landing + precedence (AC1/AC2).** `IsInRole` is also ordinal; the landing redirect
  now proves every declared variant routes correctly, plus a new precedence test: a user with **both**
  `Consumer` and `Admin` lands on `/admin` (Admin branch checked first), asserted by ordering Consumer
  first so it tests precedence, not luck.
- **D — Per-area policy gating (AC3).** The Dev Notes call this "the single most likely omission" (a
  Consumer reaching `/admin` by URL). A reflection test now statically pins that `AdminLanding` carries
  `[Route("/admin")]` + `[Authorize(Policy="Admin")]`, `ConsumerLanding` carries `[Route("/me")]` +
  `[Authorize(Policy="Consumer")]`, and `RoleLandingRedirect` carries `[Route("/")]` + `[Authorize]` with
  **no** policy (so anonymous hits fall to the router's challenge path) — the inputs `AuthorizeRouteView`
  enforces.

## Coverage

| Acceptance criterion | Before | After |
|---|---|---|
| AC1 — Admin/TenantOwner → `/admin`, only Admin nav renders | landing routing only | + case variants + **rendered no-cross-render** + `/admin` policy gate |
| AC2 — Consumer → `/me`, never cross-render | landing routing only | + case variants + **rendered no-cross-render** + `/me` policy gate |
| AC3 — Admin+Consumer policies, every nav entry gated | policy facts + registration | + **every role case-variant** evaluated + per-area `[Authorize(Policy)]` pinned |

- UI test suite: **21 → 44 tests** (all pass, 0 skipped).
- No API (HTTP) tests: the UI host has **no public API** (gateway boundary — see project-context.md);
  the API-equivalent surface here is DI/policy composition, covered by the (extended) policy tests.
- Realm topology tests (`PartiesUiRealmRolesTests`, Task 6) unchanged — out of scope for this gap pass.

## Verification

- `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` → **0 Warning(s), 0 Error(s)**
  (single-threaded per the parallel-build-flake memory; solution-wide `TreatWarningsAsErrors`).
- `bash scripts/check-no-warning-override.sh` → **OK**.
- Test EXE (xUnit v3; `dotnet test --filter` reports "Zero tests ran" — run the EXE directly):
  **Total: 44, Failed: 0, Skipped: 0**.

## Quality notes

- Standard framework APIs only (xUnit v3 / bUnit / Shouldly / NSubstitute) — no Moq/FluentAssertions/raw
  `Assert.*`, matching house style.
- No hardcoded waits — async assertions use bUnit `WaitForAssertion`, not `Thread.Sleep`.
- Tests are independent — fresh `BunitContext` / fresh `ServiceProvider` per case; theories are
  data-driven from the production `PartiesUiAuthorization` arrays, so they self-maintain.
- Locators are semantic — `href`/markup for rendered entries, attribute reflection for route/policy gates.

## Next steps

- Run in CI (`scripts/test.ps1 -Lane unit`) — the new cases ride the existing UI lane.
- The optional **live** Keycloak browser flow (Task 6 / Task 8) remains deferred (no Docker/interactive
  env); the automated tests are the binding proof of the ACs, as the story provides.
