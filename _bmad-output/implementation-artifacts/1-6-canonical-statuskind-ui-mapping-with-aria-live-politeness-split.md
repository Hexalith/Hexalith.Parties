---
baseline_commit: b5a2b710e552ad7c43fa36acbaca53e8d82350f3
---

# Story 1.6: Canonical StatusKind→UI mapping with aria-live politeness split

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want **one shared, pure mapping** from a Parties client outcome (`PartiesClientException` HTTP status + projection freshness) to a small canonical set of UI states, plus a **single politeness rule** turning each state into the correct `aria-live` live region,
so that every screen handles success, validation, authorization, failure, and staleness **identically and accessibly** — with no per-screen remap and no blanket-polite announcements.

## Acceptance Criteria

1. **Given** a `PartiesClientException` (or a bare HTTP status / projection-freshness value), **when** it is mapped to a UI state, **then** the **single canonical mapping** applies — exactly:
   - `200`/`202` → **AcceptedProcessing** (optimistic + reconcile)
   - `400`/`422` → **Validation** (inline, preserve input)
   - `401` → **SignInRequired** (route + return URL)
   - `403` → **Forbidden** by default, or **TenantUnavailable** when the problem is tenant-related ("warming up")
   - `404`/`410` → **Gone** (tombstone, no PII)
   - `408`/timeout (and `429`) → **TransientFailure** (retry + backoff)
   - `≥500` (and any unmapped/unknown status) → **LoadFailure** (retry + support, never a raw 500)
   - projection freshness ≠ `Current` (`Stale`/`Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly`) → **Degraded** (render last-known)

   …and **no screen re-implements** this table — there is exactly **one** mapper type.

2. **Given** a mapped UI state, **when** it is announced, **then** the **politeness split** applies (never blanket-polite):
   - **`role="status" aria-live="polite"`** for `AcceptedProcessing`, `TenantUnavailable`, `Gone`, `Degraded` (status / freshness / accepted-processing);
   - **`role="alert" aria-live="assertive"`** for `Validation`, `Forbidden`, `TransientFailure`, `LoadFailure` (validation-rejected / transient-failure / load-failure / hard denial);
   - **no live region at all** for `SignInRequired` (it routes to sign-in with a return URL — there is nothing to announce in place).

3. **Given** the `403` outcome, **when** mapped from a `PartiesClientException`, **then** the **tenant-vs-role split** uses the same heuristic the existing admin/picker code uses (the problem `Title`/`Detail` contains "tenant", case-insensitive) → `TenantUnavailable`; otherwise → `Forbidden`. The bare-`int` overload (no problem context) defaults `403` → `Forbidden`.

4. **Given** a transport timeout, **when** it is mapped, **then** it resolves to **TransientFailure** — covering both a `PartiesClientException` carrying `Status == 408` **and** a non-user-initiated `TimeoutException`/`TaskCanceledException` (HttpClient timeouts surface as cancellation, **not** as `Status == 408`). A **user-initiated** cancellation is **not** a failure and must not be mapped to a UI failure state.

5. **Given** the canonical mapping and politeness rule, **when** they are placed in the source tree, **then** they live in **one** location reusable across the UI tier (the `Hexalith.Parties.UI` host, namespace `Hexalith.Parties.UI.Status`), are **pure** (no DI, no I/O, no `Program.cs` registration), and a minimal shared **`StatusLiveRegion`** component renders a `StatusKind` into the correctly-polite live region so the DOM semantics have a single source.

6. **Given** the test suite, **when** it runs, **then** **bUnit + xUnit tests assert each status code's UI state and its politeness**:
   - a parameterized test pins **every** listed HTTP status → its `StatusKind` (incl. `429`, an unknown/unmapped status → `LoadFailure`, and both `403` branches);
   - a parameterized test pins **every** `ProjectionFreshnessStatus` → `Degraded` (or "fresh"/none for `Current`);
   - a parameterized test pins **every** `StatusKind` → its politeness;
   - timeout/cancellation cases (AC4) are covered;
   - a **bUnit** test renders `StatusLiveRegion` for each `StatusKind` and asserts the **actual DOM** `role` + `aria-live` attributes (polite vs assertive vs **absent** for `SignInRequired`).

## Tasks / Subtasks

### Part A — The canonical map (pure logic) — AC1, AC3, AC4, AC5

- [x] **Task 1 — Create the canonical `StatusKind` enum** (`src/Hexalith.Parties.UI/Status/StatusKind.cs`, namespace `Hexalith.Parties.UI.Status`) (AC1)
  - [x] Create the `Status/` folder (new — siblings are `Authentication/`, `Services/`, `Composition/`).
  - [x] `public enum StatusKind { AcceptedProcessing, Validation, SignInRequired, TenantUnavailable, Forbidden, Gone, TransientFailure, LoadFailure, Degraded }` — exactly the **9** canonical UI states from the architecture's Communication-Patterns table (no more, no less). **Do NOT** copy the legacy AdminPortal superset (`Loading`/`Loaded`/`DisplayNameOnly`/`RichSearchProbeDegraded`/`NoData`/`AdminRequired`/…) — those are a different, pre-existing private taxonomy (see Dev Notes "Reinvention prevention").
  - [x] **UI host = Allman / next-line braces** (match `PartiesUiAuthorization.cs`). File-scoped namespace; `using`s outside the namespace (none needed here).

- [x] **Task 2 — Create the politeness vocabulary** (`src/Hexalith.Parties.UI/Status/LiveRegionPoliteness.cs`) (AC2)
  - [x] `public enum LiveRegionPoliteness { None, Polite, Assertive }`. `None` = no live region (used by `SignInRequired`).

- [x] **Task 3 — Create the pure mapper `StatusPresentation`** (`src/Hexalith.Parties.UI/Status/StatusPresentation.cs`, `public static class`) (AC1, AC3, AC4)
  - [x] `public static StatusKind FromHttpStatus(int statusCode)` — the canonical `switch` (see Dev Notes for the exact arms). `200 or 202 → AcceptedProcessing`; `400 or 422 → Validation`; `401 → SignInRequired`; `403 → Forbidden`; `404 or 410 → Gone`; `408 or 429 → TransientFailure`; `>= 500 → LoadFailure`; **default (any other value) → LoadFailure** (fail-safe — never surface a raw/unknown status).
  - [x] `public static StatusKind FromClientException(PartiesClientException exception)` — `ArgumentNullException.ThrowIfNull(exception)`; if `exception.Status == 403` **and** the problem is tenant-related, return `TenantUnavailable`, else delegate to `FromHttpStatus(exception.Status)` (AC3). Tenant heuristic = `Title`/`Detail` contains `"tenant"` (`StringComparison.OrdinalIgnoreCase`) — **reuse the exact predicate shape** the admin/picker code uses (`ContainsTenant`), kept here as a small private helper. (`PartiesClientException` lives in `Hexalith.Parties.Client` — already referenced by the host; `add using Hexalith.Parties.Client;`.)
  - [x] `public static StatusKind? FromFreshness(ProjectionFreshnessStatus status)` — `Current → null` (fresh; no degraded treatment), every other value (`Stale`/`Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly`) → `Degraded` (AC1). (`ProjectionFreshnessStatus` lives in `Hexalith.Parties.Contracts.Models` — already referenced; `add using Hexalith.Parties.Contracts.Models;`.) Add a convenience `FromFreshness(ProjectionFreshnessMetadata metadata)` overload delegating to `.Status` (null-guarded).
  - [x] `public static StatusKind FromException(Exception exception)` — convenience for call sites that catch broadly (AC4): `PartiesClientException pce → FromClientException(pce)`; `TimeoutException` / `OperationCanceledException` (incl. `TaskCanceledException`) → **TransientFailure** (an HttpClient timeout surfaces as a `TaskCanceledException`); default → `LoadFailure`. **The mapper does not see the caller's token**, so filtering *user-initiated* cancellation is the **call site's** responsibility (see Dev Notes "Timeout vs cancellation") — only an un-requested cancellation (a timeout) should ever reach `FromException`. Document this contract in an inline comment.
  - [x] `public static LiveRegionPoliteness PolitenessFor(StatusKind kind)` — `AcceptedProcessing`/`TenantUnavailable`/`Gone`/`Degraded → Polite`; `Validation`/`Forbidden`/`TransientFailure`/`LoadFailure → Assertive`; `SignInRequired → None` (AC2). The `switch` expression **must** include a `_ => throw new ArgumentOutOfRangeException(...)` arm — Roslyn never treats an enum switch as provably exhaustive, so omitting the default triggers **CS8509 (warning → error under `TreatWarningsAsErrors`)**. Throwing (rather than a silent `_ => Polite`) guarantees an unmapped future state can **never** default to blanket-polite; the AC6 test that drives `Enum.GetValues<StatusKind>()` is what catches a newly-added-but-unmapped state.
  - [x] `public static (string? Role, string? AriaLive) LiveRegionAttributes(LiveRegionPoliteness politeness)` — `Polite → ("status", "polite")`; `Assertive → ("alert", "assertive")`; `None → (null, null)`. (Single source for the literal ARIA strings — the component and any future caller bind these, never hard-code `"polite"`/`"alert"`.)
  - [x] **PII hygiene:** the mapper takes only a status int / freshness enum / exception **metadata** — never log or echo `Detail`/party/tenant values. The tenant heuristic inspects `Title`/`Detail` but returns only a `StatusKind`; do not surface raw `Detail` from here.

### Part B — The renderable politeness primitive — AC2, AC5, AC6

- [x] **Task 4 — Create `StatusLiveRegion` shared component** (`src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor`) (AC2, AC5)
  - [x] Create the `Components/Shared/` folder (new — the architecture reserves it for cross-area domain components; Story 1.8 will add `PartyStateBadge`/`DataFreshnessIndicator`/`GdprDestructiveButton` here and will **reuse this primitive** for their polite regions).
  - [x] `@namespace Hexalith.Parties.UI.Components.Shared`; `@using Hexalith.Parties.UI.Status`. Parameters: `[Parameter] public StatusKind? Kind { get; set; }` and `[Parameter] public string? Message { get; set; }` (plus an optional `ChildContent` `RenderFragment?` if you prefer composing content — keep it minimal).
  - [x] Render logic: resolve `LiveRegionPoliteness` via `StatusPresentation.PolitenessFor(Kind.Value)`; when `Kind is null` or politeness is `None`, render **nothing** (no element — `SignInRequired` and "no status" must not emit a stray live region). Otherwise emit a single element with `role="@role" aria-live="@ariaLive"` from `StatusPresentation.LiveRegionAttributes(...)` wrapping `@Message`/`@ChildContent`.
  - [x] Allman braces in the `@code` block; no hard-coded `"polite"`/`"alert"`/`"status"`/`"alert"` strings in the markup — bind from `LiveRegionAttributes`.
  - [x] **No CSS/token work here** (that is Story 1.8 / Story 1.9). This is the semantics-only primitive.

### Part C — Tests — AC6 (proving AC1–AC5)

- [x] **Task 5 — Pure-logic tests** (`tests/Hexalith.Parties.UI.Tests/StatusPresentationTests.cs`, xUnit v3 + Shouldly; `sealed` class) (AC1, AC3, AC4)
  - [x] `[Theory]` **HTTP status → StatusKind**: `200,202 → AcceptedProcessing`; `400,422 → Validation`; `401 → SignInRequired`; `403 → Forbidden`; `404,410 → Gone`; `408,429 → TransientFailure`; `500,503 → LoadFailure`; **an unknown status (e.g. `418`, `0`, `300`) → LoadFailure** (AC1 default). Use `[InlineData]`.
  - [x] `[Theory]` **403 tenant-vs-role** via `FromClientException` (AC3): a `PartiesClientException(403, "Tenant warming up", null, null, null)` → `TenantUnavailable`; `(403, "Forbidden", null, "Role not permitted", null)` → `Forbidden`; tenant token in `Detail` (not `Title`) → `TenantUnavailable`; case-insensitivity (`"TENANT"`).
  - [x] `[Theory]` **freshness → StatusKind?** (AC1): `Current → null`; `Stale`/`Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly → Degraded`. Drive **every** `ProjectionFreshnessStatus` member (enumerate `Enum.GetValues<ProjectionFreshnessStatus>()` so a new member fails loudly).
  - [x] `[Theory]` **StatusKind → politeness** (AC2): every member of `StatusKind` mapped to its expected `LiveRegionPoliteness` (drive via `Enum.GetValues<StatusKind>()` cross-checked against an expected map so a new state can't silently default).
  - [x] **Timeout** (AC4): `FromException(new TimeoutException())` → `TransientFailure`; `FromException(new TaskCanceledException())` → `TransientFailure`; `FromException(new PartiesClientException(408, "Request Timeout", null, null, null))` → `TransientFailure`; `FromClientException(new PartiesClientException(408, …))` → `TransientFailure`; an unrelated exception (e.g. `new InvalidOperationException()`) → `LoadFailure`. (User-initiated-cancellation *filtering* is a call-site contract verified in Story 1.7 — see Dev Notes; not tested here.)
  - [x] **`LiveRegionAttributes`**: `Polite → ("status","polite")`; `Assertive → ("alert","assertive")`; `None → (null,null)`.

- [x] **Task 6 — bUnit DOM-politeness tests** (`tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs`, bUnit `BunitContext`; `sealed`) (AC2, AC6)
  - [x] `[Theory]` over the **polite** kinds (`AcceptedProcessing`,`TenantUnavailable`,`Gone`,`Degraded`): `Render<StatusLiveRegion>(p => p.Add(c => c.Kind, kind).Add(c => c.Message, "msg"))`; find the rendered element and assert `GetAttribute("role") == "status"` and `GetAttribute("aria-live") == "polite"`, and the message text renders.
  - [x] `[Theory]` over the **assertive** kinds (`Validation`,`Forbidden`,`TransientFailure`,`LoadFailure`): assert `role == "alert"` and `aria-live == "assertive"`.
  - [x] `SignInRequired` and `Kind == null`: assert **no** live-region element renders (e.g. `cut.FindAll("[role]").ShouldBeEmpty()` / markup has no `aria-live`).
  - [x] Model the bUnit setup on `RoleLandingRedirectTests` (`: BunitContext`, `Render<T>(...)`, Shouldly assertions). No auth/NavigationManager needed for this component.

- [x] **Task 7 — Build + gate verification** (AC: all)
  - [x] Per-project Release build (NOT the full `.slnx` pack — it pre-fails on PolymorphicSerializations NU5118/NU5128 + `*PackageTests`, unrelated): `dotnet build src/Hexalith.Parties.UI -c Release` then `dotnet build tests/Hexalith.Parties.UI.Tests -c Release`. If a clean parallel build flakes (CS0006/MSB4018), re-run with `-m:1` for a reliable verdict.
  - [x] Run the UI test EXE directly (xUnit v3 MTP — **do not** use `dotnet test --filter`, it returns "Zero tests ran"; run the EXE, filter with `-class "*StatusPresentation*"` / `-class "*StatusLiveRegion*"` if needed). Or run `scripts/test.ps1 -Lane unit` (Release).
  - [x] Confirm `bash scripts/check-no-warning-override.sh` stays green (no `TreatWarningsAsErrors` override).

## Dev Notes

### What this story adds — the single source of truth for "how an outcome looks and sounds"

This is the **shared enabler** every later screen consumes (epics.md:347 "the shared enablers … StatusKind→UI map … established once in Epic 1"). It delivers **three** pure, tested artifacts and **one** tiny component:

1. **`StatusKind`** — the canonical 9-state UI vocabulary (the architecture's Communication-Patterns table, verbatim).
2. **`StatusPresentation`** — the pure mapper: client outcome (`PartiesClientException`/HTTP status) **and** projection freshness → `StatusKind`; and `StatusKind` → politeness → concrete `(role, aria-live)`.
3. **`StatusLiveRegion`** — the minimal component that renders a `StatusKind` into the **correctly-polite** live region (the single DOM source for the politeness split).

It is **pure logic + one semantics-only component**: **no DI registration, no `Program.cs` change, no new packages, no CSS/tokens, no network calls.** Stories 1.7 (optimistic-reconcile effect) and 1.8 (domain components) and every Epic 2/4/5 screen then **reuse this verbatim** — "use the `StatusKind` mapping + aria-live split verbatim" (architecture.md:521).

### 🚨 THE #1 DISASTER: a per-screen remap (the exact anti-pattern this story exists to kill)

The architecture pins this as a **forbidden anti-pattern**: "`aria-live="polite"` on a validation error" and remapping per screen (architecture.md:460 "single source — agents must not remap per screen"; 539-541 anti-patterns). The whole point of Story 1.6 is that there is **exactly one** mapper. Concretely:

- **Validation, TransientFailure, LoadFailure, and a hard Forbidden are `alert` (assertive) — never `polite`.** A blanket `aria-live="polite"` on an error is the named anti-pattern. `PolitenessFor` must make a wrong politeness **impossible** (exhaustive switch, no silent default).
- **Do not** let a screen "tweak" the table. The map is the contract; screens call it.

### Reinvention prevention — THREE pre-existing per-screen remaps already exist (do NOT extend them, do NOT delete them in this story)

There are already **three** outcome→state mappings in the repo. They are **pre-existing, richer, screen-specific taxonomies in components that the UI host does not (and cannot) reference**. This story creates the **new canonical** map for the **new** UI tier; it does **not** refactor the legacy ones (that is out of scope — see "Scope boundary").

| Existing remap | Where | Taxonomy | Relationship to this story |
|---|---|---|---|
| `AdminPortalQueryFailureKind` + `AdminPortalGdprOutcome` | `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:504-552` | `MapByStatus`/`MapFailureKind`/`MapGdprOutcome` — `401/403(tenant split)/404/409/410/501/400|422/408|429/≥500` | Closest precedent. **Copy the `403` tenant-heuristic shape** (`ContainsTenant(Title)||ContainsTenant(Detail)`, `:554-555`) and the `>=500`/`408 or 429` style. Has extra states (`Conflict`,`ContractUnavailable`) the canonical set intentionally **collapses** — do not add them to `StatusKind`. |
| `private enum StatusKind` | `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:1271-1287` | `Loading/Loaded/DisplayNameOnly/RichSearchProbeDegraded/Degraded/SignInRequired/TenantUnavailable/AdminRequired/Forbidden/TransientFailure/NoData/LoadFailure/Validation` | **Same name, different type** (private nested, superset). The new `Hexalith.Parties.UI.Status.StatusKind` is a separate namespace — **no collision**. Do **not** import or reuse it. It also shows the correct `role="status" aria-live="polite"` usage (`:17,123,136-147`). |
| `PartyPickerSearchState` mapping | `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs:253-267` | `Unauthorized/Forbidden/NotFound/Gone/TransientFailure(408|429|502|503|504)/Error` | Picker RCL — independent re-skin (D11, Story 2.5). Out of scope here. |

**Why not refactor them now?** They live in the **legacy AdminPortal** and **Picker** RCLs, which reference `Client`/`Contracts` but **not** the host — so they cannot consume a host-placed map, and their richer taxonomies are wired into large existing components. Migrating them is a separate effort that lands when their Epic-2 pages are rebuilt against the canonical map. Story 1.6's job is to **establish the canonical map + prove it**, not to retrofit legacy screens. (Same scope discipline as Stories 1.4/1.5, which delivered the building block + tests and explicitly deferred the broader wiring.)

### The canonical mapping — concrete shape (implement verbatim)

```csharp
// src/Hexalith.Parties.UI/Status/StatusPresentation.cs   (Allman braces — UI house style)
namespace Hexalith.Parties.UI.Status;

using System;

using Hexalith.Parties.Client;                 // PartiesClientException
using Hexalith.Parties.Contracts.Models;       // ProjectionFreshnessStatus / ProjectionFreshnessMetadata

public static class StatusPresentation
{
    public static StatusKind FromHttpStatus(int statusCode)
        => statusCode switch
        {
            200 or 202 => StatusKind.AcceptedProcessing,
            400 or 422 => StatusKind.Validation,
            401 => StatusKind.SignInRequired,
            403 => StatusKind.Forbidden,
            404 or 410 => StatusKind.Gone,
            408 or 429 => StatusKind.TransientFailure,
            >= 500 => StatusKind.LoadFailure,
            _ => StatusKind.LoadFailure,         // fail-safe: never surface a raw/unknown status
        };

    public static StatusKind FromClientException(PartiesClientException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Status == 403 && IsTenantProblem(exception)
            ? StatusKind.TenantUnavailable
            : FromHttpStatus(exception.Status);
    }

    public static StatusKind? FromFreshness(ProjectionFreshnessStatus status)
        => status == ProjectionFreshnessStatus.Current ? null : StatusKind.Degraded;

    public static LiveRegionPoliteness PolitenessFor(StatusKind kind)
        => kind switch
        {
            StatusKind.AcceptedProcessing or StatusKind.TenantUnavailable
                or StatusKind.Gone or StatusKind.Degraded => LiveRegionPoliteness.Polite,
            StatusKind.Validation or StatusKind.Forbidden
                or StatusKind.TransientFailure or StatusKind.LoadFailure => LiveRegionPoliteness.Assertive,
            StatusKind.SignInRequired => LiveRegionPoliteness.None,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null), // never blanket-polite
        };

    public static (string? Role, string? AriaLive) LiveRegionAttributes(LiveRegionPoliteness politeness)
        => politeness switch
        {
            LiveRegionPoliteness.Polite => ("status", "polite"),
            LiveRegionPoliteness.Assertive => ("alert", "assertive"),
            LiveRegionPoliteness.None => (null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(politeness), politeness, null),
        };

    private static bool IsTenantProblem(PartiesClientException exception)
        => Contains(exception.Title) || Contains(exception.Detail);

    private static bool Contains(string? value)
        => value?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true;
}
```

> The `403` tenant-vs-role heuristic mirrors `PartiesAdminPortalApiClient.ContainsTenant` (`:554-555`) and `MapGdprOutcome`/`MapFailureKind` (`:537-552`). Keep the same predicate so admin and the canonical map agree on what "tenant problem" means.

### Timeout vs cancellation (AC4) — the trap

HttpClient timeouts do **not** arrive as `PartiesClientException(Status == 408)`. The Parties client throws `PartiesClientException` only on an HTTP **response** error (`ThrowOnErrorAsync` reads `(int)response.StatusCode`, optionally overridden by problem+json `status` — `HttpPartiesCommandClient.cs:240-278`). A transport timeout throws `TaskCanceledException` (an `OperationCanceledException`) **before** any response. So:

- `FromException` must treat a `TimeoutException` and a **timeout** `TaskCanceledException` as `TransientFailure`, and `Status == 408` (if a gateway ever returns it) also as `TransientFailure` (`FromHttpStatus`).
- A **caller-initiated** cancellation (the user navigated away / the component disposed — the *caller's* token fired) is **not** a failure. Because `FromException` is pure and **does not receive the caller's token**, it cannot itself tell a timeout from a user-cancel — so the policy lives at the **call site**: wrap the call in `catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* abandoned — do not map, do not surface */ }` (the established drop-silently pattern, `PartiesAdminPortal.razor:519-522`), so only an *un-requested* cancellation (a timeout) ever reaches `FromException`. Story 1.6 has **no** call sites (the effect that wires this is Story 1.7); 1.6 ships the mapper + this documented contract. The Task 5 test asserts `FromException(TimeoutException)` and `FromException(TaskCanceledException)` → `TransientFailure` (and `408` → `TransientFailure`); the user-cancellation *filtering* is verified when Story 1.7 wires the effect.

### The component — concrete shape

```razor
@* src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor  (Allman braces in @code) *@
@namespace Hexalith.Parties.UI.Components.Shared
@using Hexalith.Parties.UI.Status

@if (Kind is { } kind && StatusPresentation.PolitenessFor(kind) is var politeness and not LiveRegionPoliteness.None)
{
    var (role, ariaLive) = StatusPresentation.LiveRegionAttributes(politeness);
    <div role="@role" aria-live="@ariaLive">@Message</div>
}

@code {
    [Parameter] public StatusKind? Kind { get; set; }

    [Parameter] public string? Message { get; set; }
}
```

- `SignInRequired` (politeness `None`) and `Kind == null` render **nothing** — no stray live region. This is what the bUnit "absent" assertion proves.
- No CSS class, no tokens — semantics only (1.8/1.9 add visual treatment). Keep the element a plain `<div>` so 1.8's `DataFreshnessIndicator` can wrap/compose it without fighting styles.

### Established patterns you MUST follow (from 1.1–1.5)

- **⚠️ Brace style is per-project (`.editorconfig` + `TreatWarningsAsErrors` trap):** the **UI host (`src/Hexalith.Parties.UI/…`) is Allman / next-line braces** (see `PartiesUiAuthorization.cs`, `SelfScopedPartiesClient.cs`). All new `Status/` and `Components/Shared/` files MUST match. File-scoped namespaces; `using`s **outside** the namespace, `System.*` first; **no unused `using`/`@using`** (build error); nullable enabled (don't silence with `!`).
- **Central Package Management:** no `Version=` in any csproj. **No new package references needed** — `bunit`/`xunit.v3`/`Shouldly`/`NSubstitute` are already in `Hexalith.Parties.UI.Tests.csproj`; `PartiesClientException`/`ProjectionFreshnessStatus` come via the host's existing `Client`/`Contracts` references (Story 1.5 added them).
- **No `Program.cs` change, no DI:** `StatusPresentation` is `static`/pure. Do **not** register it. (Contrast Story 1.5's Scoped accessor — this is the opposite: stateless utility.)
- **`InternalsVisibleTo("Hexalith.Parties.UI.Tests")`** is already in the host csproj (Story 1.5). Keep the new types `public` anyway (they are the cross-tier contract), so no IVT reliance is needed.
- **PII hygiene (pinned):** never log/echo `Detail`/party/tenant from the mapper. The tenant heuristic returns only a `StatusKind`.
- **xUnit v3 + Shouldly + bUnit**; classes `sealed`; descriptive sentence method names; `value.ShouldBe(...)`. No Moq/FluentAssertions/raw `Assert.*`. (NSubstitute available but not needed — there is nothing to mock; the map is pure and the component takes plain parameters.)
- **Don't build the whole `.slnx`** to judge yourself (full Release `pack` pre-fails on PolymorphicSerializations NU5118/NU5128 + `*PackageTests`, unrelated). Verify per-project + the UI test EXE. Clean parallel builds can flake (CS0006/MSB4018) — re-run `-m:1`.

### Source tree — files to create / touch

| Action | File |
|---|---|
| NEW | `src/Hexalith.Parties.UI/Status/StatusKind.cs` |
| NEW | `src/Hexalith.Parties.UI/Status/LiveRegionPoliteness.cs` |
| NEW | `src/Hexalith.Parties.UI/Status/StatusPresentation.cs` |
| NEW | `src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor` |
| NEW | `tests/Hexalith.Parties.UI.Tests/StatusPresentationTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs` |

No edits to `Program.cs`, csproj files, or any existing source are required (no DI, no new packages, no refactor of legacy maps).

### Project Structure Notes

- **Placement decision (see "Open question" below).** The canonical map lives in the **UI host** (`Hexalith.Parties.UI`, namespace `Hexalith.Parties.UI.Status`) and the politeness primitive in `UI/Components/Shared/`. Rationale: (1) the architecture **pins the StatusKind-map tests to `Hexalith.Parties.UI.Tests`** (architecture.md:608), which references the host; (2) the host owns cross-area UI primitives and `Shared/` (architecture.md:563-566, 629); (3) `aria-live` politeness is a **UI presentation** concern that does **not** belong in the transport-layer `Hexalith.Parties.Client`; (4) the **immediate** consumers (Story 1.7 effect, Story 1.8 components) both live in the host. No structural variance from the architecture.
- **Scope boundary — do NOT over-build:** no DI/registration; no `Program.cs` wiring; no CSS/tokens/icons (1.8/1.9); no `Conflict`/`ContractUnavailable`/`Loading`/`NoData` states (the canonical set is intentionally the 9 listed); **no refactor of the legacy AdminPortal/Picker remaps**; no Fluxor/optimistic-reconcile effect (Story 1.7); no wiring the map into any actual page (no pages exist yet — `/admin` and `/me` are stubs). Deliverable = the **map + politeness + primitive + their tests**.

### Open question (placement) — decided for this story; flag for the architect when RCL pages arrive

The canonical map is placed in the **UI host** because that is where the architecture pins its tests (`Hexalith.Parties.UI.Tests`, architecture.md:608) and its immediate consumers (Stories 1.7/1.8) live, and because `aria-live` politeness is a UI concern that must not leak into the transport-layer `Hexalith.Parties.Client`. **Consequence to revisit later (not in 1.6's scope):** the future Epic-2 admin pages (AdminPortal RCL) and Epic-4/5 consumer pages (ConsumerPortal RCL) reference `Client`/`Contracts` but **not** the host, so they cannot consume a host-placed `StatusKind`. When those pages are built, the team must decide whether to (a) **promote** the `Status/` types to a small shared library both RCLs reference (preserving the true "single source"), or (b) keep the map host-side and have the RCLs surface raw outcomes that the host maps at composition. This story does not block that choice — no RCL consumes the map yet (the legacy AdminPortal/Picker keep their own pre-existing taxonomies; ConsumerPortal does not exist). Recommend resolving it in the Epic-2 kickoff / an ADR rather than pre-building a shared lib now.

### References

- [Source: epics.md#Story 1.6 — Canonical StatusKind→UI mapping with aria-live politeness split / lines 529-545] — user story + ACs (canonical status→state table; politeness split; "bUnit tests assert each status code's UI state and its politeness").
- [Source: epics.md#AR-StatusMap / lines 203-204] and [#UX-DR8 — Live-region politeness split / lines 290-291] — the enabler is defined once (architecture "Communication Patterns") and reused; status/freshness/accepted-processing → `role=status aria-live=polite`; validation-rejected/transient/load-failure → `role=alert` (assertive).
- [Source: epics.md#Epic 1 intro / lines 347, 360-361] — "shared enablers (… StatusKind→UI map …) established once in Epic 1"; "the canonical `StatusKind→UI` map + aria-live split".
- [Source: architecture.md#Communication Patterns / lines 458-484] — the **canonical `StatusKind` → UI-state table** (460-472, with politeness column) and the **pinned aria-live politeness split** (474-476): never blanket-polite.
- [Source: architecture.md#Format Patterns / lines 447-456] — error surface: RFC 9457 `problem+json` → `PartiesClientException` → the single `StatusKind` mapping; "Never parse problem+json ad hoc per screen"; freshness always rendered through the shared indicator.
- [Source: architecture.md#Pattern Examples / lines 531-542] — Good vs **anti-patterns** (forbidden: `aria-live="polite"` on a validation error).
- [Source: architecture.md#Project Structure / line 608] — `Hexalith.Parties.UI.Tests` is the home of the **StatusKind map** tests (pins host placement).
- [Source: architecture.md#Integration Points & Data Flow / lines 656-659] — command data flow: optimistic + `aria-live=polite` "Saving…" → reconcile; rejection → revert + `role="alert"` inline reason (consumed by Story 1.7 via this map).
- [Source: src/Hexalith.Parties.Client/PartiesClientException.cs] — the mapper's primary input: `Status` (int), `Title`, `Detail`, `Type`, `CorrelationId`.
- [Source: src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs / lines 240-278] — `ThrowOnErrorAsync` builds `PartiesClientException` from `(int)response.StatusCode` (overridden by problem+json `status`); confirms timeouts do **not** arrive as `Status == 408` (AC4 nuance).
- [Source: src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs] — freshness enum (`Current/Stale/Rebuilding/Degraded/Unavailable/LocalOnly`); only `Current` is "fresh".
- [Source: src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs] — the freshness record (`Status` + `WarningCodes`) the `FromFreshness` overload reads.
- [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs / lines 504-555] — **existing per-screen remaps** to learn from (copy the `403` tenant heuristic + `>=500`/`408 or 429` shape), NOT extend; richer taxonomy the canonical set collapses.
- [Source: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor / lines 17,123,136-147,1271-1287] — legacy private `StatusKind` superset (same name, different namespace/type — no collision) + correct `role="status" aria-live="polite"` usage.
- [Source: src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs / lines 253-267] — Picker's `PartyPickerSearchState` remap (independent RCL; out of scope).
- [Source: tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs] — bUnit style to mirror (`: BunitContext`, `Render<T>(...)`, `[Theory]`/`TheoryData`, Shouldly). [Source: tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj] — bUnit/xUnit.v3/Shouldly/NSubstitute already present.
- [Source: _bmad-output/implementation-artifacts/1-5-consumer-own-data-self-authorization-defense-in-depth.md] — predecessor: per-project-build/test EXE idioms, Allman-in-UI vs K&R-in-host brace trap, scope-discipline pattern, the host's `Client`/`Contracts` references this story relies on.
- [Source: _bmad-output/project-context.md] — CPM (no `Version=`); `TreatWarningsAsErrors`; file-scoped namespaces / `using` ordering / no-unused-using; PII hygiene; xUnit v3 / Shouldly / NSubstitute / bUnit house style; "put code where it belongs".

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code dev-story workflow)

### Debug Log References

- `dotnet build src/Hexalith.Parties.UI -c Release` → Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet build tests/Hexalith.Parties.UI.Tests -c Release` → Build succeeded, 0 Warning(s), 0 Error(s).
- UI test EXE, new classes only (`-class StatusPresentationTests -class StatusLiveRegionTests`) → Total: 65, Failed: 0.
- UI test EXE, full suite (regression) → Total: 174, Failed: 0, Skipped: 0.
- `bash scripts/check-no-warning-override.sh` → OK (no warning-override / nested-submodule regressions); exit 0.

### Completion Notes List

- **AC1/AC3/AC4/AC5 — canonical map (Tasks 1–3).** Added the 9-state `StatusKind` enum, the 3-value `LiveRegionPoliteness` enum, and the pure `static StatusPresentation` mapper, all under the new `src/Hexalith.Parties.UI/Status/` folder, namespace `Hexalith.Parties.UI.Status`. `FromHttpStatus` implements the canonical switch with a fail-safe default → `LoadFailure`; `FromClientException` applies the `403` tenant-vs-role split using the same `Contains("tenant", OrdinalIgnoreCase)` predicate shape as `PartiesAdminPortalApiClient.ContainsTenant`; `FromFreshness` maps `Current → null` and every other freshness value → `Degraded` (plus a null-guarded `ProjectionFreshnessMetadata` overload); `FromException` treats `TimeoutException`/`OperationCanceledException`(incl. `TaskCanceledException`) and a `408` client exception → `TransientFailure`, else → `LoadFailure`, with the documented call-site contract that user-initiated cancellation is filtered upstream (Story 1.7). PII-safe: the mapper returns only a `StatusKind`, never echoes `Detail`.
- **AC2 — politeness split.** `PolitenessFor` is an exhaustive switch with a throwing default arm (guards CS8509 under `TreatWarningsAsErrors` and makes blanket-polite impossible); `LiveRegionAttributes` is the single source for the literal `(role, aria-live)` strings (`status/polite`, `alert/assertive`, `null/null`).
- **AC2/AC5 — primitive (Task 4).** Added `Components/Shared/StatusLiveRegion.razor` (new `Shared/` folder), namespace `Hexalith.Parties.UI.Components.Shared`. Renders the correctly-polite live region by binding `LiveRegionAttributes`; renders **nothing** for `SignInRequired` (politeness `None`) and `null` Kind. Semantics-only plain `<div>` — no CSS/tokens (deferred to 1.8/1.9).
- **AC6 — tests (Tasks 5–6).** `StatusPresentationTests` (xUnit v3 + Shouldly) pins every HTTP status (incl. `429`, unknown→`LoadFailure`, both `403` branches), every `ProjectionFreshnessStatus` (driven by `Enum.GetValues`), every `StatusKind`→politeness (driven by `Enum.GetValues` cross-checked against an expected map so a new state can't silently default), the AC4 timeout/cancellation cases, and `LiveRegionAttributes`. `StatusLiveRegionTests` (bUnit `BunitContext`) asserts the actual DOM `role`+`aria-live` for the polite and assertive kinds and asserts **no** live-region element for `SignInRequired` / null Kind.
- **Scope discipline.** No `Program.cs`/DI changes, no new packages, no CSS/tokens, no refactor of the legacy AdminPortal/Picker remaps, no Story 1.7 effect — exactly the map + politeness + primitive + their tests. All new types are `public` (the cross-tier contract), so no IVT reliance.

### File List

- `src/Hexalith.Parties.UI/Status/StatusKind.cs` (new)
- `src/Hexalith.Parties.UI/Status/LiveRegionPoliteness.cs` (new)
- `src/Hexalith.Parties.UI/Status/StatusPresentation.cs` (new)
- `src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor` (new)
- `tests/Hexalith.Parties.UI.Tests/StatusPresentationTests.cs` (new)
- `tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs` (new)

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (adversarial AI review) · **Date:** 2026-06-10 · **Outcome:** Approve

**Scope verified:** all 6 File-List artifacts read; every bound dependency signature confirmed (`PartiesClientException(int,string,string?,string?,string?)`, `ProjectionFreshnessStatus` = exactly the 6 members, `ProjectionFreshnessMetadata.Create`, bUnit `BunitContext`); both Release builds re-run (host **0W/0E**, tests **0W/0E**); UI test EXE re-run; warning-override gate re-run (exit 0).

**Claims vs reality:**
- File List is exact — `git status` shows precisely the 6 claimed new files, nothing undocumented.
- All 7 tasks marked `[x]` are genuinely done (verified file-by-file, not trusted).
- All 6 ACs implemented **and** test-pinned: AC1 (15 status `[InlineData]` incl. unknown→LoadFailure), AC2 (exhaustive throwing `PolitenessFor` cross-checked vs an independent expected-map; never blanket-polite), AC3 (403 tenant∥role via `IsTenantProblem`, null-safe; bare-int 403→Forbidden), AC4 (`TimeoutException`/`TaskCanceledException`/408→TransientFailure; user-cancel filtering correctly deferred to a documented 1.7 call-site contract), AC5 (pure, `Hexalith.Parties.UI.Status`, no DI/`Program.cs` — git confirms zero host edits), AC6 (every status/freshness/kind driven by `Enum.GetValues`; bUnit asserts real DOM `role`+`aria-live` for all 9 kinds incl. *absent* for SignInRequired).
- Tests are real assertions (exhaustive enum drives, anti-pattern guard, DOM attribute reads) — no placeholders.

**Findings:**
- 🔴 CRITICAL: none · 🟠 HIGH: none · 🟡 MEDIUM: none
- 🟢 LOW-1 (fixed): Debug Log undercounted its own tests (recorded 56 new / 165 full; actual **65 new / 174 full**, all green — the dev added 9 tests beyond what was logged). Counts corrected to match the EXE run.

**Verdict:** 0 critical issues → Status set to **done**. Implementation is a faithful, well-tested single-source mapper exactly as specified.

## Change Log

| Date | Change |
|---|---|
| 2026-06-10 | Story 1.6 implemented: canonical `StatusKind` (9 states) + `LiveRegionPoliteness` + pure `StatusPresentation` mapper + `StatusLiveRegion` primitive, with xUnit/bUnit tests pinning every status→state, freshness→state, state→politeness, the AC4 timeout/cancellation cases, and the DOM aria-live split. Per-project Release builds clean (0 warnings); UI suite 165 passed (56 new); build-gate green. Status → review. |
| 2026-06-10 | Adversarial code review (AI): verified all 6 ACs + 7 tasks against implementation, git, and a fresh build/test run (host & tests 0W/0E; UI suite **174** passed, **65** new; warning-gate exit 0). 0 critical/high/medium; 1 low (stale Debug Log test counts) auto-fixed (56→65 new, 165→174 full). Status → done. |
