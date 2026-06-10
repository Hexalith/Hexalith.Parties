---
baseline_commit: 671e8d9f61cd24b97af2b135276104d5731f228c
---

# Story 1.4: Fail-closed `party_id` claim resolution

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the system,
I want to resolve a Consumer's `party_id` claim fail-closed,
so that a consumer without a binding never reaches a data screen.

## Acceptance Criteria

1. **Given** a Consumer principal carrying a verified `party_id` claim, **when** `PartyIdClaimResolver` runs (Scoped), **then** it resolves exactly one bound `party_id`, the tenant claim is normalized via `PartiesClaimsTransformation`, and the consumer's effective scope is `{tenant, party_id}`.
2. **Given** a Consumer principal with **no** (or an invalid) `party_id` claim, **when** resolution runs, **then** the user is routed to a fail-closed `NoPartyBinding` onboarding/error state — **never** a data screen (`/me`).
3. **Given** the resolution path, **when** a principal carries **more than one** `party_id` claim (ambiguous binding), **then** resolution fails closed (treated as no binding) — a single bound `party_id` is required.
4. **Given** the Parties UI host boots in any mode (full sign-in, tests, degraded/no-OIDC), **when** services resolve, **then** `PartyIdClaimResolver` (Scoped) and `PartiesClaimsTransformation` (`IClaimsTransformation`) are registered and resolve under `ValidateScopes=true` without a captive-dependency failure.
5. **Given** the test suite, **when** it runs, **then** bUnit tests cover the **present-claim** path (Consumer + valid `party_id` → lands `/me`) and the **absent-claim** path (Consumer, no `party_id` → routed to `NoPartyBinding`, never `/me`), plus a DI-composition test of `PartyIdClaimResolver` for the present / absent / empty / multiple-claim cases.

> _The mechanism that **issues** the `party_id` claim is **AR-Gap-Binding** — **decided** in Story 4.1 and **implemented** in Story 4.2. This story only **consumes** an existing claim; its happy path is end-to-end verifiable once 4.2 lands. The binding proof for THIS story is the bUnit + DI tests with injected principals (the same posture Story 1.3 used for role routing)._

## Tasks / Subtasks

- [x] **Task 1 — Add claim-type constants to the single source of truth** (AC: 1, 2)
  - [x] In `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`, add `public const string PartyIdClaimType = "party_id";` and `public const string TenantClaimType = "eventstore:tenant";`. **Never hardcode these claim strings** anywhere else — the resolver, transformation, page, and tests reference the constants (mirrors how `AdminRoleNames`/`ConsumerRoleNames` are the only home for role/policy strings).

- [x] **Task 2 — Create `PartiesClaimsTransformation` (UI-host-local, idempotent)** (AC: 1, 4)
  - [x] Create `src/Hexalith.Parties.UI/Authentication/PartiesClaimsTransformation.cs`, namespace `Hexalith.Parties.UI.Authentication`, `sealed class PartiesClaimsTransformation : IClaimsTransformation` (`Microsoft.AspNetCore.Authentication`).
  - [x] **DO NOT reference the existing `Hexalith.Parties.Authentication.PartiesClaimsTransformation`** in the internal actor-host project — that project is **not referenced** by `Hexalith.Parties.UI` and referencing it crosses the adopter-facing↔internal boundary (fitness-pinned; build error). Build a UI-local class; you may mirror the internal one's logic (reproduced verbatim in **Dev Notes → Reference implementation**).
  - [x] **Must be idempotent / fail-closed**: `IClaimsTransformation.TransformAsync` can run **multiple times per request**. Short-circuit and return the principal unchanged if the `eventstore:tenant` claim is already present (it normally is — the `hexalith-parties-ui` Keycloak client emits `eventstore:tenant` directly). Otherwise derive it from `tenants` (JSON array or space-delimited) / `tenant_id` / `tid`, adding only missing claims. No throw on missing input.

- [x] **Task 3 — Create `PartyIdClaimResolver` (Scoped)** (AC: 1, 2, 3, 4)
  - [x] Create `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`, namespace `Hexalith.Parties.UI.Authentication`. A `sealed class` with one method, e.g. `PartyBindingResult Resolve(ClaimsPrincipal user)`.
  - [x] Create the result type `PartyBindingResult` (sealed record, same folder) with `bool IsBound`, `string? Tenant`, `string? PartyId` and factory members `PartyBindingResult.Unbound` and `PartyBindingResult.Bound(string tenant, string partyId)`.
  - [x] **Fail-closed resolution rule** — `IsBound == true` **only** when the principal carries **exactly one** non-null/non-whitespace `PartyIdClaimType` claim. Zero claims → `Unbound`. Two or more `party_id` claims → `Unbound` (ambiguous; AC3). Empty/whitespace value → `Unbound`. Capture the normalized `TenantClaimType` value into `Tenant` (may be present alongside) so the effective scope is `{tenant, party_id}` for downstream self-scoping (Story 1.5).
  - [x] No DI dependencies required (it reads the passed-in principal). Keep it pure so the DI-composition test is trivial.

- [x] **Task 4 — Register both services** (AC: 4)
  - [x] In `PartiesUiAuthorization.cs` (or a sibling extension in the same `Authentication/` namespace), add `AddPartiesUiClaimsResolution(this IServiceCollection)` that registers `services.AddScoped<PartyIdClaimResolver>()` and `services.AddScoped<IClaimsTransformation, PartiesClaimsTransformation>()`; return `services`.
  - [x] Call it **unconditionally** in `src/Hexalith.Parties.UI/Program.cs`, immediately after `builder.Services.AddPartiesUiAuthorization();` (line 33) — **not** gated on `authEnabled` (so tests and degraded boot resolve it; `IClaimsTransformation` is only *invoked* when `UseAuthentication` is wired, so unconditional registration is harmless). Mirrors the existing unconditional `AddPartiesUiAuthorization()` rationale (Program.cs lines 29-33).
  - [x] **Scoped, not singleton** (ADR-030): `ValidateScopes=true` (Program.cs line 13) fails the boot if a singleton captures these. Do not register as singleton.

- [x] **Task 5 — Create the `NoPartyBinding` page** (AC: 2)
  - [x] Create `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor`, `@page "/no-party-binding"`, `@attribute [Authorize(Policy = PartiesUiAuthorization.ConsumerPolicy)]` (the unbound user is an authenticated Consumer; gating on the Consumer policy keeps non-Consumers out and is enforced by the existing `<AuthorizeRouteView>` in `Routes.razor`).
  - [x] **It is NOT a data screen** — render only static onboarding/error copy; never fetch or display party data. Lead with an `<h1>` (the `<FocusOnNavigate Selector="h1">` in `Routes.razor` moves focus to it). Consumer copy register is **plain / reassuring, neutral-info (never success-green)**. Mirror the hardcoded-English stub style of `RoleLandingRedirect.razor`'s no-area block (these scaffold pages are not yet localized; do not introduce `IStringLocalizer` here — stay consistent with the predecessor). Suggested copy: heading "We're still setting up your profile" + a short reassuring line that their account isn't linked to a profile yet and to contact support/their administrator. No PII.

- [x] **Task 6 — Route Consumers through fail-closed binding resolution** (AC: 1, 2)
  - [x] Edit `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor`. Add `@inject PartyIdClaimResolver Resolver`. In the **Consumer branch only** (currently `Nav.NavigateTo("/me")`, line 37): resolve `PartyBindingResult binding = Resolver.Resolve(authState.User);` → if `binding.IsBound` navigate `/me`, else navigate `/no-party-binding`.
  - [x] **Leave the Admin branch and the no-role (`_noArea`) fail-closed state unchanged** — `NoPartyBinding` is the Consumer *data-binding* fail-closed state, distinct from the existing role-less "No area assigned" state (see the comment already at RoleLandingRedirect.razor line 42).

- [x] **Task 7 — Tests (bUnit + DI-composition)** (AC: 5, and proves 1/2/3/4)
  - [x] **`tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs`** (plain xUnit + Shouldly, model: `PartiesUiAuthorizationPolicyTests.cs`): build a provider with `AddPartiesUiClaimsResolution()` under `ValidateScopes=true`, resolve `PartyIdClaimResolver` **inside a scope** (`provider.CreateScope()`), and assert: present single `party_id` → `IsBound` true + correct `PartyId`/`Tenant`; absent → `Unbound`; empty/whitespace value → `Unbound`; **two `party_id` claims → `Unbound`** (AC3).
  - [x] **Routing tests** — extend `RoleLandingRedirectTests.cs` or add `NoPartyBindingRoutingTests.cs` (`: BunitContext`): register the resolver in the test container (`Services.AddPartiesUiClaimsResolution();` before `Render<RoleLandingRedirect>()`). Present-claim: `auth.SetAuthorized("consumer"); auth.SetRoles("Consumer"); auth.SetClaims(new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-123"));` → asserts nav ends with `/me`. Absent-claim: same but **no** `SetClaims` → asserts nav ends with `/no-party-binding` and **`ShouldNotEndWith("/me")`** (the fail-closed negative, exactly like the existing no-area test at `RoleLandingRedirectTests.cs:64`). **Verify the bUnit API**: `BunitAuthorizationContext.SetClaims(params Claim[])` is the intended call — if the installed bUnit (2.8.4-preview) names it differently, adapt (the project already uses `SetAuthorized`/`SetRoles`).
  - [x] **`PartiesClaimsTransformationTests.cs`** (optional but recommended): short-circuits (returns same principal) when `eventstore:tenant` already present; derives it from a `tenants`/`tid` token when absent; idempotent across repeated `TransformAsync` calls.
  - [x] **Pin the policy attribute**: extend `PartiesUiAreaAuthorizationTests.cs` so `NoPartyBinding` is asserted to carry `[Authorize(Policy = ConsumerPolicy)]` (it reflects routable components for their `[Authorize]` attributes).

- [x] **Task 8 — `_Imports.razor` / build hygiene** (AC: all)
  - [x] `Components/_Imports.razor` already imports `Microsoft.AspNetCore.Authorization`, `Hexalith.Parties.UI.Authentication`, and `Components.Account` — `NoPartyBinding`, `[Authorize(Policy=…)]`, and `PartiesUiAuthorization`/`PartyIdClaimResolver` references resolve without new `@using`s. Add a new `@using` only if you reference a not-yet-imported namespace (and remove any unused `@using` — an unused one is a build error under `TreatWarningsAsErrors`).
  - [x] Build per-project (NOT the full `.slnx` pack — it pre-fails, see Dev Notes): `dotnet build src/Hexalith.Parties.UI -c Release` then `dotnet build tests/Hexalith.Parties.UI.Tests -c Release`. If a clean parallel build flakes (CS0006/MSB4018), re-run with `-m:1`. Confirm `bash scripts/check-no-warning-override.sh` stays green.

- [x] **Task 9 (OPTIONAL, forward-looking — NOT required for AC) — Keycloak `party_id` mapper for a live dev flow**
  - [x] The realm issues **no `party_id` today**, and the issuing mechanism is AR-Gap-Binding (deferred to 4.1/4.2). If you want a live (non-test) Consumer to reach `/me`, add a `party_id` `oidc-usermodel-attribute-mapper` to the `hexalith-parties-ui` client and a `party_id` attribute to the consumer test user in `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` (mirror the 4 existing attribute mappers added in Story 1.2 and the roles mapper added in 1.3). **Preserve LF line endings** in that file (it is LF; the UI host + tests are CRLF). Do not block AC on this — the bUnit/DI tests are the binding proof.

## Dev Notes

### What this story actually adds (and the trap to avoid)

Story 1.4 makes the Consumer landing path **fail closed on the data binding**. Stories 1.2/1.3 already gave you: server-side OIDC + cookie session, the `eventstore:tenant` tenant claim wired through the FrontComposer bridge, the `Admin`/`Consumer` role policies, and `RoleLandingRedirect` (`@page "/"`) that role-routes Admin→`/admin`, Consumer→`/me`. **Today every Consumer unconditionally reaches `/me`.** This story interposes a fail-closed `party_id` check: a Consumer with no/invalid binding is diverted to a new `NoPartyBinding` page instead of the data area.

**🚨 The #1 disaster to prevent — boundary violation via the name collision.** An `IClaimsTransformation` named `PartiesClaimsTransformation` **already exists** at `src/Hexalith.Parties/Authentication/PartiesClaimsTransformation.cs`, but it lives in the **internal actor-host** project (`Hexalith.Parties`), which is **private to the host** and **not referenced** by `Hexalith.Parties.UI` (the BFF). The UI csproj references only FrontComposer projects (`Hexalith.Parties.UI.csproj` lines 22-31). Reusing the internal class would (a) require a project reference that crosses the adopter-facing↔internal boundary the project-context pins, and (b) likely trip a fitness test. **Create a new, UI-host-local `PartiesClaimsTransformation` in `Hexalith.Parties.UI.Authentication`.** You may copy the logic — it is reproduced below.

### Reference implementation — internal `PartiesClaimsTransformation` (copy the shape, new namespace)

The internal version (`src/Hexalith.Parties/Authentication/PartiesClaimsTransformation.cs`) short-circuits when `eventstore:tenant` is already present, else derives it from a `tenants` JSON-array / space-delimited claim or `tenant_id`/`tid`:

```csharp
// Hexalith.Parties.Authentication (INTERNAL — do not reference; reproduce in UI namespace)
public sealed class PartiesClaimsTransformation(ILogger<PartiesClaimsTransformation> logger) : IClaimsTransformation
{
    internal const string TenantClaimType = "eventstore:tenant";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.HasClaim(c => c.Type == TenantClaimType))   // ← idempotent short-circuit
            return Task.FromResult(principal);
        var identity = new ClaimsIdentity();
        AddTenantClaims(principal, identity);                     // tenants[] / tenant_id / tid → eventstore:tenant
        if (identity.Claims.Any()) principal.AddIdentity(identity);
        return Task.FromResult(principal);
    }
    // AddTenantClaims / AddClaimsFromJwt: JSON-array OR space-delimited parsing — see source for the full body
}
```

For the UI-local copy: use `PartiesUiAuthorization.TenantClaimType` instead of a private const, keep the idempotent short-circuit, and inject `ILogger<PartiesClaimsTransformation>` if you log (log **coarse counts only — never the tenant value or any PII**, per the project's PII-hygiene rule). In the UI host this transformation is mostly a **defensive no-op** because the `hexalith-parties-ui` Keycloak client already emits `eventstore:tenant` directly — but it satisfies AC1 literally and hardens against a provider that emits `tenants`/`tid` instead.

### The resolver and routing — concrete shape

`PartyIdClaimResolver` (Scoped, pure):
```csharp
namespace Hexalith.Parties.UI.Authentication;
public sealed class PartyIdClaimResolver
{
    public PartyBindingResult Resolve(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var partyIds = user.FindAll(PartiesUiAuthorization.PartyIdClaimType)
            .Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (partyIds.Length != 1) return PartyBindingResult.Unbound;          // 0 or >1 ⇒ fail closed (AC2, AC3)
        string? tenant = user.FindFirst(PartiesUiAuthorization.TenantClaimType)?.Value;
        return PartyBindingResult.Bound(tenant ?? string.Empty, partyIds[0]); // effective scope {tenant, party_id}
    }
}
public sealed record PartyBindingResult(bool IsBound, string? Tenant, string? PartyId)
{
    public static PartyBindingResult Unbound { get; } = new(false, null, null);
    public static PartyBindingResult Bound(string tenant, string partyId) => new(true, tenant, partyId);
}
```

`RoleLandingRedirect.razor` Consumer branch (replace line 37 `Nav.NavigateTo("/me");`):
```csharp
else if (PartiesUiAuthorization.ConsumerRoleNames.Any(authState.User.IsInRole))
{
    PartyBindingResult binding = Resolver.Resolve(authState.User);
    Nav.NavigateTo(binding.IsBound ? "/me" : "/no-party-binding");   // fail closed: no binding ⇒ never a data screen
}
```
Add `@inject PartyIdClaimResolver Resolver` at the top (after the existing `@inject` lines).

### Established patterns you MUST follow (from 1.1–1.3)

- **Single source of truth for auth strings** — role/policy names live ONLY in `PartiesUiAuthorization`. Put the new `PartyIdClaimType`/`TenantClaimType` consts there too; reference them everywhere (resolver, transformation, page, tests). Story 1.3 proved this with data-driven theories that fail the build if a name is trimmed.
- **Unconditional registration** of policy/claims infra (not gated on `authEnabled`) so tests + degraded boot resolve them. `AddAuthorizationCore` and `AddScoped` are additive.
- **Fail-closed is proven, not asserted** — the binding test must assert the **negative** (`ShouldNotEndWith("/me")` for the absent-claim case), exactly like `RoleLandingRedirectTests.cs:64` (`AuthenticatedUserWithNoRole_IsNotRoutedToADataArea`).
- **`<AuthorizeRouteView>` is load-bearing** (`Routes.razor`) — it (not plain `RouteView`) enforces `[Authorize(Policy=…)]` on `NoPartyBinding`. Already in place; don't change it.
- **Three distinct "scope" notions — do not conflate**: (1) role-claim `Admin`/`Consumer` policies (`RequireRole`); (2) tenant-membership admin (`AdminPortalAuthorizationService`, out of scope here); (3) **this story** — Consumer effective scope `{tenant, party_id}` from claims. Keep them separate.
- **DI lifetime (ADR-030, pinned)**: auth/self accessors are **Scoped**, never captured by singletons; `ValidateScopes=true` catches violations at boot (Program.cs:13). Resolve the resolver inside a scope in tests.
- **`IClaimsTransformation` runs per-request and possibly multiple times** — the transformation must be idempotent (short-circuit when the normalized claim already exists). This is the single biggest claims-transformation footgun.

### Source tree — files to create / touch

| Action | File |
|---|---|
| EDIT | `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs` — add `PartyIdClaimType`, `TenantClaimType`, `AddPartiesUiClaimsResolution()` |
| NEW | `src/Hexalith.Parties.UI/Authentication/PartiesClaimsTransformation.cs` (UI-local) |
| NEW | `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs` (+ `PartyBindingResult`) |
| NEW | `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor` |
| EDIT | `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor` — Consumer branch + `@inject` |
| EDIT | `src/Hexalith.Parties.UI/Program.cs` — call `AddPartiesUiClaimsResolution()` after line 33 |
| NEW | `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs` |
| NEW/EDIT | `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs` (or extend `RoleLandingRedirectTests.cs`) |
| NEW (opt) | `tests/Hexalith.Parties.UI.Tests/PartiesClaimsTransformationTests.cs` |
| EDIT (opt) | `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` — pin `NoPartyBinding` policy |
| EDIT (opt, Task 9) | `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` — `party_id` mapper (LF endings) |

### Testing standards

- **xUnit v3 + Shouldly + NSubstitute + bUnit only.** No Moq, no FluentAssertions, no raw `Assert.*`. Test classes are `sealed`; bUnit tests inherit `BunitContext`; descriptive sentence method names (`ConsumerWithPartyId_LandsOnConsumerArea`, `ConsumerWithoutPartyId_IsRoutedToNoPartyBinding_NeverToMe`).
- **bUnit principal-with-claims idiom** (the new bit vs 1.3): `var auth = AddAuthorization(); auth.SetAuthorized("consumer"); auth.SetRoles("Consumer"); auth.SetClaims(new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-123"));` then `Render<RoleLandingRedirect>()` and assert `Services.GetRequiredService<NavigationManager>().Uri` via `cut.WaitForAssertion(...)` (navigation happens in `OnInitializedAsync`).
- **DI-composition idiom** (model `PartiesUiAuthorizationPolicyTests.cs`): `BuildProvider()` → `AddLogging(); AddPartiesUiClaimsResolution();` → `BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })`; resolve the Scoped resolver inside `using var scope = provider.CreateScope();`.
- **Run lane**: `scripts/test.ps1 -Lane unit` (Release). Do **not** use `dotnet test --filter` (xUnit v3 MTP returns "Zero tests ran"); run the test EXE with `-class`/`-method` if filtering, per the project's known-issue note.

### Build / gotchas (carried forward, will bite otherwise)

- `TreatWarningsAsErrors` is solution-wide: file-scoped namespaces; `using`s outside the namespace, `System.*` first; private fields `_camelCase`; async methods `…Async`; **no unused `@using`/`using`** (build error). Nullable enabled — don't silence with `!`.
- **Central Package Management**: never add a `Version=` to a csproj (build error). No new packages needed — bUnit/xUnit/Shouldly/NSubstitute already referenced (`Hexalith.Parties.UI.Tests.csproj` lines 15-21).
- **Don't build the whole `.slnx` to judge yourself** — the full Release `pack` pre-fails (PolymorphicSerializations NU5118/NU5128 + `*PackageTests`), unrelated to your change. Verify with per-project `dotnet build` of `Hexalith.Parties.UI` + the UI test project. Clean parallel builds can flake (CS0006/MSB4018) — re-run with `-m:1` for a reliable verdict.
- **Line endings**: UI host + UI tests are **CRLF**; the AppHost realm JSON is **LF** — preserve per file, keep diffs minimal.
- **No public API / controllers** on the UI host (it's a BFF) — this story adds none; it's all auth/claims/component wiring.
- **PII hygiene**: never log the `party_id`, tenant value, or any claim value; logs carry coarse counts only.

### Project Structure Notes

- `PartyIdClaimResolver` + `PartiesClaimsTransformation` go in `src/Hexalith.Parties.UI/Authentication/` and `NoPartyBinding` in `src/Hexalith.Parties.UI/Components/Account/` — **exactly** the placement the architecture's project-structure map prescribes (`Authentication/PartyIdClaimResolver.cs` "D2 — resolve party_id claim (fail-closed)"; `Account/ … NoPartyBinding (fail-closed onboarding/error state)`). No structural variance.
- The architecture also lists a Parties-host-side `IDataSubjectAccessService` + `ConsumerPolicy.cs` and a `ISelfScopedPartiesClient` accessor — those are **Story 1.5 (D3 defense-in-depth)**, NOT this story. 1.4 is the BFF-side claim resolution only.
- **Scope boundary — do NOT over-build.** This story's fail-closed guarantee is enforced at the **landing redirect** (`RoleLandingRedirect` diverts an unbound Consumer away from `/me` to `NoPartyBinding`). It does **not** need to block an unbound Consumer who manually types `/me` — `/me` (`ConsumerLanding`) is an empty stub today (no party data), and the deeper "never act on data you aren't bound to" hardening is **Story 1.5** (the self-scoped accessor + fail-closed `IDataSubjectAccessService` on the Parties host, defense-in-depth). Do not add server-side data-operation enforcement or self-scoped clients here — that is the next story and would be scope creep.

### References

- [Source: epics.md#Story 1.4 — Fail-closed `party_id` claim resolution / lines 493-509] — user story + ACs (the present-claim, absent-claim, and AR-Gap-Binding deferral note).
- [Source: architecture.md#Authentication & Security / lines 289-300] — D2: verified `party_id` IdP claim → exactly one Party; resolution fail-closed; `eventstore:tenant` normalized by `PartiesClaimsTransformation`; effective scope `{tenant, party_id}`; `Admin` + new `Consumer` policy role routing.
- [Source: architecture.md#Project Structure & Boundaries / lines 559-575] — placement of `PartyIdClaimResolver` (`Authentication/`) and `NoPartyBinding` (`Components/Account/`).
- [Source: architecture.md#Gap Analysis / lines 720-724] and [#Implementation Handoff / lines 799-803] — AR-Gap-Binding: the claim-issuing mechanism is undesigned (Story 4.1/4.2); the host/self-scope plumbing (this story) is unaffected and can proceed.
- [Source: architecture.md / lines 282-284] — the consumer identity→party binding lives in the IdP claim and/or a small binding store, **never the event stream**.
- [Source: src/Hexalith.Parties.UI/Program.cs / lines 13, 29-55, 76-81] — `ValidateScopes=true`; unconditional `AddPartiesUiAuthorization()`; OIDC bridge with `tenantClaimType: "eventstore:tenant"`; `RoleClaimType = "roles"`.
- [Source: src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs] — single-source-of-truth pattern for policy/role names (extend with claim-type consts + `AddPartiesUiClaimsResolution`).
- [Source: src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor / lines 34-44] — Consumer branch to modify; the no-area state is explicitly "distinct from Story 1.4's party-binding NoPartyBinding".
- [Source: src/Hexalith.Parties/Authentication/PartiesClaimsTransformation.cs] — internal reference implementation to reproduce (DO NOT reference the project).
- [Source: tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs / lines 22-78] — bUnit `BunitContext` + `AddAuthorization().SetAuthorized()/SetRoles()` + `WaitForAssertion` nav idiom; the fail-closed negative-assertion pattern.
- [Source: tests/Hexalith.Parties.UI.Tests/PartiesUiAuthorizationPolicyTests.cs / lines 21-128] — DI-composition under `ValidateScopes=true`; `PrincipalWithRole` / claims-principal construction; model for `PartyIdClaimResolverTests`.
- [Source: _bmad-output/project-context.md] — adopter-facing vs internal split; CPM; `TreatWarningsAsErrors`; PII hygiene; xUnit v3 / Shouldly / NSubstitute / bUnit; `ValidateScopes=true` (ADR-030).
- [Source: predecessor stories 1.2 (OIDC) & 1.3 (role landing + policies)] — `_bmad-output/implementation-artifacts/1-2-*.md`, `1-3-*.md` for the OIDC/claims wiring and the role-routing precedent this story extends.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code, BMAD dev-story workflow)

### Debug Log References

- Initial UI + test build: clean (0 warnings, 0 errors).
- First full test run failed 13/60 — adding `@inject PartyIdClaimResolver` to `RoleLandingRedirect`
  broke the **existing** `RoleLandingRedirectTests` (resolver unregistered; and Consumer cases no longer
  reach `/me` without a `party_id` — the intended behavior change). Fixed by registering the resolver in
  that class's constructor and giving the Consumer-landing cases a `party_id` claim (a *bound* Consumer
  still lands on `/me`; the unbound/ambiguous negatives are proven in the new `NoPartyBindingRoutingTests`).
- Final test run (full UI test EXE, Release): **69/69 pass, 0 skipped**. Per-class: `PartyIdClaimResolverTests`
  11 cases (present / absent / empty+whitespace theory / multiple / bound-without-tenant / both-services-resolve /
  scoped-lifetime / null-guard / transformation+resolver composition), `PartiesClaimsTransformationTests`
  10 cases (short-circuit / tid / tenant_id / single+multi JSON-array / space-delimited / no-source / idempotent /
  null-guard), `NoPartyBindingRoutingTests` 3 (present → `/me`, absent + ambiguous → `/no-party-binding`,
  `ShouldNotEndWith("/me")`), `NoPartyBindingPageTests` 2 (single focusable `<h1>`, static non-data copy).
- Build gate `scripts/check-no-warning-override.sh`: green. Realm JSON re-validated (valid, LF preserved);
  `party-id-mapper` confirmed on the `hexalith-parties-ui` client and `readonly-user` carries
  `party_id=[party-readonly-001]` with realm role `Consumer`.

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- ✅ AC1/AC4 — `PartyIdClaimResolver` (Scoped, pure) resolves a single bound `party_id` and captures the
  normalized `eventstore:tenant` into the effective scope `{tenant, party_id}`; UI-local
  `PartiesClaimsTransformation` (`IClaimsTransformation`, idempotent short-circuit) normalizes the tenant
  claim. Both registered Scoped via `AddPartiesUiClaimsResolution()`, called **unconditionally** in
  `Program.cs`; the DI-composition test resolves both inside a scope under `ValidateScopes=true`.
- ✅ AC2 — `RoleLandingRedirect` Consumer branch now diverts an unbound Consumer to the new
  fail-closed `/no-party-binding` page (a non-data onboarding screen) instead of `/me`.
- ✅ AC3 — resolution fails closed on **zero, empty/whitespace, and ≥2** `party_id` claims (ambiguous
  binding). Proven in `PartyIdClaimResolverTests` and the ambiguous-routing bUnit case.
- ✅ AC5 — bUnit present-claim (→ `/me`) and absent-claim (→ `/no-party-binding`, `ShouldNotEndWith("/me")`)
  routing tests + DI-composition resolver tests (present/absent/empty/multiple). `NoPartyBinding`'s
  `[Authorize(Policy = ConsumerPolicy)]` is pinned in `PartiesUiAreaAuthorizationTests`.
- Boundary respected: a **new UI-local** `PartiesClaimsTransformation` was created — the internal
  `Hexalith.Parties.Authentication.PartiesClaimsTransformation` was NOT referenced (adopter↔internal split).
- Claim-type literals (`party_id`, `eventstore:tenant`) live only in `PartiesUiAuthorization` consts;
  PII hygiene preserved (transformation logs coarse counts only, never claim values).
- Task 9 (optional, forward-looking): added a single-valued `party-id-mapper` to the `hexalith-parties-ui`
  Keycloak client and a `party_id` attribute to the consumer test user (`readonly-user`) so a live
  Consumer can reach `/me`. Realm JSON kept LF; AC binding proof remains the bUnit/DI tests.

### File List

**Created**
- `src/Hexalith.Parties.UI/Authentication/PartiesClaimsTransformation.cs`
- `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs` (+ `PartyBindingResult`)
- `src/Hexalith.Parties.UI/Components/Account/NoPartyBinding.razor`
- `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs`
- `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/NoPartyBindingPageTests.cs` (page-render proof: single focusable `<h1>`, static non-data copy)
- `tests/Hexalith.Parties.UI.Tests/PartiesClaimsTransformationTests.cs`

**Modified**
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs` (claim-type consts + `AddPartiesUiClaimsResolution`)
- `src/Hexalith.Parties.UI/Components/Account/RoleLandingRedirect.razor` (Consumer branch fail-closed resolve + `@inject`)
- `src/Hexalith.Parties.UI/Program.cs` (unconditional `AddPartiesUiClaimsResolution()`; review fix: OIDC-bridge `tenantClaimType` now references `PartiesUiAuthorization.TenantClaimType` instead of a re-hardcoded `"eventstore:tenant"` literal)
- `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs` (register resolver; bound-Consumer `party_id` claim)
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAreaAuthorizationTests.cs` (pin `NoPartyBinding` Consumer policy)
- `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` (Task 9: `party-id-mapper` + consumer `party_id` attribute)

## Senior Developer Review (AI)

**Reviewer:** Administrator · **Date:** 2026-06-10 · **Outcome:** Approve (auto-fix applied)

Adversarial validation of every AC and `[x]` task against the actual implementation and git reality.
Verification performed: per-project Release builds (UI + tests) clean at **0 warnings / 0 errors**; full UI
test EXE **69/69 pass, 0 skipped**; `check-no-warning-override.sh` green; realm JSON re-parsed (LF preserved,
mapper + user binding confirmed); single-source-of-truth grep across `src`/`tests`.

**AC coverage — all 5 IMPLEMENTED and proven**

- **AC1** — `PartyIdClaimResolver` (Scoped, pure) resolves a single bound `party_id` and captures the
  normalized `eventstore:tenant`; composition test `NormalizedTenantFromTransformation_IsCapturedIntoBoundScope`
  proves transformation→resolver yields the effective scope `{tenant, party_id}`.
- **AC2** — `RoleLandingRedirect` Consumer branch diverts an unbound Consumer to `/no-party-binding`
  (`NoPartyBinding`, a non-data page); `ConsumerWithoutPartyId_…_NeverToMe` asserts the negative.
- **AC3** — zero / empty / whitespace / ≥2 `party_id` all fail closed; resolver + bUnit ambiguous case.
- **AC4** — both services register Scoped and resolve under `ValidateScopes=true`; lifetime pinned directly.
- **AC5** — present + absent bUnit routing, DI-composition (present/absent/empty/multiple), policy attribute pinned.

**Findings (0 CRITICAL, 0 HIGH) — all auto-fixed**

1. **[MEDIUM] File List incomplete (git-vs-story).** `tests/Hexalith.Parties.UI.Tests/NoPartyBindingPageTests.cs`
   was delivered but absent from the File List. → Added under Created.
2. **[LOW] Stale Debug Log counts.** Record claimed "60/60" with understated per-class counts and omitted the
   page-render tests; the suite is now **69**. → Debug Log + Change Log corrected.
3. **[LOW] Single-source-of-truth leak.** `Program.cs:61` re-hardcoded `"eventstore:tenant"` despite the new
   `PartiesUiAuthorization.TenantClaimType` const living in the same (already-imported) namespace, contradicting
   the const's own documented contract. → Now references the const (rebuilt clean).

**Observations (no change — spec-conformant, deferred by design)**

- A bound Consumer with a `party_id` but no tenant claim binds with `Tenant = string.Empty` (explicit in the
  story's reference impl + pinned by `BoundPartyId_WithoutTenantClaim_BindsWithEmptyTenant`). Tenant-scoped
  enforcement is **Story 1.5** (defense-in-depth); not a 1.4 gap.
- The transformation short-circuits on tenant-claim *presence* (not value), so an empty-valued provider claim
  would pass through un-normalized — an unlikely provider behavior outside this story's scope.

## Change Log

| Date | Version | Description |
|---|---|---|
| 2026-06-10 | 0.1 | Story 1.4 implemented — fail-closed `party_id` claim resolution: Scoped `PartyIdClaimResolver` + UI-local idempotent `PartiesClaimsTransformation`, unconditional DI registration, `NoPartyBinding` fail-closed page, `RoleLandingRedirect` Consumer-branch diversion, and bUnit + DI-composition tests. Status → review. |
| 2026-06-10 | 0.2 | Senior Developer Review (AI): 0 critical. Auto-fixed File List omission (`NoPartyBindingPageTests.cs`), corrected stale test counts (now **69/69 green**), and tightened single-source-of-truth (`Program.cs` uses `PartiesUiAuthorization.TenantClaimType`). Status → done. |
