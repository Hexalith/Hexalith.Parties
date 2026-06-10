# Test Automation Summary — Story 1.6 (Canonical StatusKind→UI mapping + aria-live politeness split)

**Date:** 2026-06-10
**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/1-6-canonical-statuskind-ui-mapping-with-aria-live-politeness-split.md`
**Feature under test:** `Hexalith.Parties.UI.Status.StatusPresentation` (pure mapper) + `Hexalith.Parties.UI.Components.Shared.StatusLiveRegion` (semantics-only Blazor primitive)
**Framework (existing, reused):** xUnit v3 `3.2.2` + Shouldly `4.3.0` + bUnit `2.7.2` — no new packages
**Mode:** Auto-apply all discovered gaps.

## Scope note

This story ships **pure UI-tier logic + one Blazor component** — there are **no HTTP endpoints, controllers, or actor surfaces** (the `parties` host exposes only `POST /process` to the EventStore gateway, unrelated to this feature). Therefore **API tests do not apply**; the appropriate automation is component/logic tests (bUnit + xUnit), which is what this QA pass extended.

The story arrived in `review` with strong existing coverage (56 tests across the two new classes). The QA pass acted as a coverage auditor and **auto-applied 9 gap tests** for untested public surface and behavioral guarantees.

## Gaps Discovered → Closed

| # | AC | Gap in existing tests | Fix |
|---|----|----|----|
| 1 | AC2/AC5 | `StatusLiveRegion.ChildContent` (a public `RenderFragment?` rendered into the live region via `@Message@ChildContent`) had **zero** coverage. | bUnit test renders `ChildContent` and asserts it lands as real DOM inside the correctly-polite region. |
| 2 | AC2 | `Message` + `ChildContent` concatenation untested — nothing pinned that both surface together. | bUnit test asserts both render inside an assertive region. |
| 3 | AC2 | The `SignInRequired` "render nothing" guarantee was never proven **when content is supplied** — a stray live region for sign-in is the named anti-pattern. | bUnit test passes both `Message` and `ChildContent` to `SignInRequired` and asserts no region renders. |
| 4 | AC4 | `FromException` was only proven for `408 → TransientFailure`; it was never asserted to route a **non-408** `PartiesClientException` through the **full** `FromClientException` mapping (incl. the `403` tenant split) — the exact path AC4 broad-catch call sites depend on. | `[Theory]` over `401→SignInRequired`, `403-tenant→TenantUnavailable`, `403-role→Forbidden`, `404→Gone`, `500→LoadFailure` via `FromException`. |
| 5 | AC3 | A `403` carrying **no** `Title`/`Detail` was untested — the tenant heuristic's null-safety (`Contains(null)` → no NRE → `Forbidden`) was unproven. | `Fact` asserts a `403` with null problem text degrades to `Forbidden`, not a crash. |

## Generated / Modified Tests

### Component (bUnit) — `tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs`
- [x] `ChildContent_renders_as_markup_inside_the_live_region` — child `RenderFragment` renders as real DOM inside the `role=status`/`aria-live=polite` region
- [x] `Message_and_ChildContent_both_render_together` — both surface inside the `role=alert`/`aria-live=assertive` region
- [x] `SignInRequired_renders_nothing_even_with_child_content` — no-announce contract holds regardless of supplied content
- (pre-existing, retained) polite-kinds / assertive-kinds DOM assertions, `SignInRequired_renders_no_live_region`, `NullKind_renders_no_live_region`

### Logic (xUnit) — `tests/Hexalith.Parties.UI.Tests/StatusPresentationTests.cs`
- [x] `FromException_routes_a_client_exception_through_the_full_mapping` — `[Theory]`, 5 cases (broad-catch preserves the whole mapping incl. the `403` tenant split)
- [x] `FromClientException_treats_a_403_with_no_problem_text_as_Forbidden` — null-safe tenant heuristic
- (pre-existing, retained) every HTTP status arm, both `403` branches, every `ProjectionFreshnessStatus`, every `StatusKind`→politeness, the AC4 timeout/cancellation matrix, `LiveRegionAttributes`

## Results

| Suite | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| `StatusPresentationTests` + `StatusLiveRegionTests` (affected) | 65 | 65 | 0 | 0 |
| `Hexalith.Parties.UI.Tests` (full regression) | 174 | 174 | 0 | 0 |

- Build: `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` → **Build succeeded, 0 Warning(s), 0 Error(s)** under solution-wide `TreatWarningsAsErrors`.
- Test runner: UI test EXE directly (xUnit v3 MTP; `dotnet test --filter` returns "Zero tests ran" and is **not** used).
- Build gate `scripts/check-no-warning-override.sh` → **OK** (exit 0; no warning override, no nested-submodule regression).

## Coverage

- **`StatusPresentation` public surface:** **7/7 methods** — `FromHttpStatus`, `FromClientException`, both `FromFreshness` overloads, `FromException`, `PolitenessFor`, `LiveRegionAttributes`; including every status arm, every freshness value, every `StatusKind`→politeness, the AC4 timeout/cancellation cases, and now the broad-catch full-mapping path + null-safe `403`.
- **`StatusLiveRegion` parameters:** **3/3** — `Kind`, `Message`, `ChildContent`; polite/assertive DOM `role`+`aria-live`, the three absent-region cases, and `Message`/`ChildContent` rendering.
- **API tests:** N/A — no public HTTP/actor surface in this feature.

## Next Steps

- No further action required for Story 1.6 — coverage is complete for the shipped surface.
- The user-initiated-cancellation **filtering** contract (AC4) is verified at the **call site** when Story 1.7 wires the optimistic-reconcile effect — out of scope here (no call sites exist yet).
