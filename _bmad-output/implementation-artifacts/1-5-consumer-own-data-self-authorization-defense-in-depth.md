---
baseline_commit: e45466305a23f138b66f992ac3a6b8ec981625b9
---

# Story 1.5: Consumer own-data self-authorization (defense-in-depth)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a security owner,
I want the Parties UI host to funnel every Consumer data operation through a single self-scoped accessor (injecting the resolved `party_id`, never list/search) **and** the Parties actor host to carry a fail-closed `IDataSubjectAccessService` + a server-side `Consumer` policy,
so that own-data-only holds even if the BFF is ever bypassed (defense-in-depth).

## Acceptance Criteria

1. **Given** a Consumer principal, **when** any consumer data operation is issued from the UI host, **then** it flows through the **single** `ISelfScopedPartiesClient` accessor, which resolves and injects the consumer's own `party_id` and only ever issues **self-scoped** operations (`GetMyPartyAsync()` → `GetPartyAsync(myPartyId)`, consumer GDPR commands on `myPartyId`). The accessor's surface **structurally excludes** list/search — a Consumer principal **never** calls `ListPartiesAsync`/`SearchPartiesAsync`.

2. **Given** a Consumer with **no / ambiguous** `party_id` binding, **when** any accessor method is invoked, **then** the accessor **fails closed** — it throws (never falls back to an arbitrary or caller-supplied `party_id`, and never calls the underlying client). Reuses Story 1.4's `PartyIdClaimResolver` (`IsBound == true` ⇒ exactly one `party_id`).

3. **Given** the Parties **actor host** (`Hexalith.Parties`), **when** services are composed, **then** a fail-closed `IDataSubjectAccessService` is registered that asserts `aggregateId == party_id` (ordinal) and **denies** on null/empty/mismatch (defense-in-depth), **and** a server-side **`Consumer`** authorization policy is registered alongside the existing `Admin` policy (same posture: registered + policy-resolvable, role-claim based).

4. **Given** the architectural boundary, **when** the new `IDataSubjectAccessService` is added, **then** it is **NOT** wired into the actor host's gateway **request path** (`Program.cs`, `PartyDomainServiceInvoker.cs`) — the host is machine-to-machine over DAPR and carries no end-user principal at `/process` (the EventStore gateway owns request-path RBAC; the host-side check is the deferred-gateway-self-principal building block). A **fitness test** pins the new service out of the request path, mirroring `PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator`.

5. **Given** the DI container, **when** the host boots with `ValidateScopes=true`, **then** the Scoped `ISelfScopedPartiesClient` resolves inside a scope and a Singleton **cannot** capture it (captive-dependency caught at boot) — the accessor is **Scoped**, never Singleton.

6. **Given** the test suite, **when** it runs, **then**:
   - a **tripwire** reflection test fails if `ISelfScopedPartiesClient` ever exposes a list/search-shaped member (name `List*`/`Search*`, or a return type of `PagedResult<>`);
   - a **DI-composition** test proves the accessor is Scoped and resolves under `ValidateScopes=true` (AC5);
   - **self-scope** tests prove the accessor passes the **resolved** `party_id` (not a caller-supplied id) to the underlying `IPartiesQueryClient.GetPartyAsync`, and **fails closed** (throws, no client call) for an unbound/ambiguous principal (AC1/AC2);
   - host-side **fail-closed** unit tests cover the `IDataSubjectAccessService` decision matrix (equal→allow; mismatch/null/empty→deny) and a `Consumer`-policy registration test (Consumer-role principal satisfies `Consumer` only; Admin-role does not; role-less satisfies neither) (AC3);
   - the request-path **fitness** test (AC4) is green.

> **Why AC4 looks different from the epic wording.** The epic AC2 reads "when a request reaches the domain **for a Consumer principal**, the fail-closed `IDataSubjectAccessService` asserts `aggregateId == party_id`." On today's host that precondition **is never met on the request path**: the `parties` actor host has no public API, the EventStore gateway invokes it over DAPR at `POST /process`, and **DAPR strips the JWT** — there is no authenticated end-user principal at the host (`tests/.../Gateway/PartiesProcessEndpointTests.cs` exercises `/process` with no auth; `app.UseAuthentication()` runs but finds no bearer token). The architecture documents this as the **deferred residual** (architecture.md lines 310-314): "Removing the consumer's tenant-level reach entirely would require the (deferred) gateway self-principal." This story therefore delivers the host-side check as the **registered, unit-tested, fail-closed building block + the `Consumer` policy** (exactly the posture of the existing `ITenantAccessService` + `Admin` policy, both registered but kept off the request path), and the **active enforcement today is the BFF self-scope accessor (AC1)**. Wiring the host check into a live consumer-principal request path lands with the deferred gateway self-principal — do **not** wire it into `/process` here (it would break the fitness boundary).

## Tasks / Subtasks

### Part A — Parties **UI host** (BFF) self-scope choke point — AC1, AC2, AC5, AC6

- [x] **Task 1 — Reference the adopter-facing client + contracts from the UI host** (AC1)
  - [x] In `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`, add `<ProjectReference>`s to `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj` and `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj` (relative paths, like the FrontComposer refs already there — use `..\Hexalith.Parties.Client\…`). **This is allowed**: `Client` and `Contracts` are the **adopter-facing** half of the split (project-context "adopter-facing vs internal"); the UI BFF is a consumer of them. Do **NOT** reference any **internal** host project (`Hexalith.Parties`, `.Server`, `.Projections`, `.Security`, `.Testing`) — that crosses the fitness-pinned boundary (the same trap Story 1.4 avoided with the `PartiesClaimsTransformation` name collision).
  - [x] No `Version=` attributes (Central Package Management). No new package references needed.

- [x] **Task 2 — Create the self-scope accessor interface** (`src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`) (AC1)
  - [x] Namespace `Hexalith.Parties.UI.Services`. Create the `Services/` folder (does not exist yet — architecture project-structure prescribes `UI/Services/ISelfScopedPartiesClient.cs / .cs`).
  - [x] Expose **only self-scoped** members — **no `partyId` parameter on any method** (the accessor injects the resolved id) and **no list/search**:
    - `Task<PartyDetail> GetMyPartyAsync(CancellationToken ct = default);`
    - Consumer GDPR self-service (delegating to the existing GDPR gateway client — see Task 4): `GetMyConsentAsync`, `GrantMyConsentAsync(channelId, purpose, LawfulBasis, …)`, `RevokeMyConsentAsync(consentId, …)`, `RequestMyErasureAsync`, `GetMyErasureStatusAsync`, `RestrictMyProcessingAsync(reason?, …)`, `LiftMyRestrictionAsync`, `ExportMyDataAsync`, `GetMyProcessingRecordsAsync`. Each returns the underlying client's result type (`IReadOnlyList<ConsentRecord>`, `AdminPortalGdprCommandResult`, `PartyErasureStatusRecord?`, `AdminPortalExportDownload`, `IReadOnlyList<ProcessingActivityRecord>`) and takes **no** `partyId`.
  - [x] **Tripwire-by-construction:** the interface must contain **zero** members named `List*`/`Search*` and **zero** members returning `PagedResult<>`. (AC6 reflection test pins this.)
  - [x] Profile-WRITE (`IPartiesCommandClient.UpdatePersonDetails…`) is **Epic 4 / FR-Consumer-2 (Story 4.5)** — do **NOT** add edit-my-profile commands to the accessor here (build them with the page that needs them). Keep this story's surface to read + GDPR self-service.

- [x] **Task 3 — Implement the accessor, fail-closed + Scoped** (`src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`) (AC1, AC2, AC5)
  - [x] `internal sealed class SelfScopedPartiesClient : ISelfScopedPartiesClient`. Constructor-inject: `AuthenticationStateProvider` (the Blazor-correct principal source — **not** `IHttpContextAccessor`, whose `HttpContext` is null after the interactive circuit starts), `PartyIdClaimResolver` (Story 1.4, Scoped), `IPartiesQueryClient`, and `IAdminPortalGdprClient`.
  - [x] **Resolve the bound `party_id` fail-closed on every call** (private `async Task<string> ResolveMyPartyIdAsync()`): `AuthenticationState state = await _authStateProvider.GetAuthenticationStateAsync(); PartyBindingResult b = _resolver.Resolve(state.User); if (!b.IsBound) throw …;` — return `b.PartyId!`. On unbound/ambiguous (`IsBound == false`): **throw** a clear exception (e.g. `InvalidOperationException` with a non-PII message like "No bound party for the current principal."). **Never** fall back to an arbitrary id and **never** call the underlying client when unbound (AC2).
  - [x] Every method calls `ResolveMyPartyIdAsync()` and passes **that** id to the underlying client (e.g. `GetMyPartyAsync` → `_queryClient.GetPartyAsync(myId, ct)`; `RevokeMyConsentAsync(consentId)` → `_gdprClient.RevokeConsentAsync(myId, consentId, ct)`). The caller can never supply a `party_id`.
  - [x] **Register Scoped** (ADR-030): add an extension `AddSelfScopedPartiesClient(this IServiceCollection)` → `services.AddScoped<ISelfScopedPartiesClient, SelfScopedPartiesClient>(); return services;`. Place it next to the class in `Services/` (e.g. a `SelfScopedPartiesClientServiceCollectionExtensions` static class) or fold into the same file — match the codebase's one-extension-per-concern style. **Never Singleton** — `ValidateScopes=true` (Program.cs:13) fails the boot if a Singleton captures it, and the resolver + auth-state are per-request/per-circuit.
  - [x] **PII hygiene:** never log the resolved `party_id`, tenant, or any claim/party value; if you log, log coarse outcome only.

- [x] **Task 4 — Wire registration in `Program.cs`, gating the live gateway client on config** (AC1, AC5)
  - [x] Register the Scoped accessor **unconditionally**: `builder.Services.AddSelfScopedPartiesClient();` (after the Story 1.4 `AddPartiesUiClaimsResolution()` at line 41), mirroring the unconditional-registration rationale of 1.3/1.4 (tests + degraded boot must compose it).
  - [x] Register the **underlying** gateway clients **only when configured**: `AddPartiesClient(configuration)` **throws at registration time** if `Parties:BaseUrl` is missing (`GetValidatedBaseAddress` throws `InvalidOperationException`). So gate it exactly like the existing `authEnabled` block: `bool partiesClientEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["Parties:BaseUrl"]);` then `if (partiesClientEnabled) builder.Services.AddPartiesClient(builder.Configuration);`. Do **NOT** call `AddPartiesClient` unconditionally — it breaks degraded/test boot.
  - [x] **Do NOT wire the live OIDC→gateway token relay here** — attaching the server-side access token to the consumer's gateway HTTP calls is the deferred residual (Story 1.2 deferred the relay; it lands with the first consumer screen that fetches data, Epic 4 / Story 1.7 SignalR). 1.5's deliverable is the **structural choke point + fail-closed/Scoped guarantees**, proven by tests; a live consumer round-trip is not in scope (no consumer data page exists yet — `/me` is an empty stub).
  - [x] Because `AddPartiesClient` is gated, in a no-`BaseUrl` boot `ISelfScopedPartiesClient`'s dependencies (`IPartiesQueryClient`/`IAdminPortalGdprClient`) are unregistered — that is fine: nothing resolves the accessor until a consumer page exists. `ValidateScopes` validates at **resolution** time, so boot stays green. (Tests resolve it with substituted clients — Task 7.)

### Part B — Parties **actor host** defense-in-depth (`IDataSubjectAccessService` + `Consumer` policy) — AC3, AC4, AC6

- [x] **Task 5 — Add the fail-closed data-subject self-authorization service** (mirror the `ITenantAccessService` family) (AC3)
  - [x] Create in `src/Hexalith.Parties/Authorization/` (namespace `Hexalith.Parties.Authorization`, **K&R/same-line brace style** to match the existing files in that folder — see brace-style note in Dev Notes):
    - `DataSubjectAccessDenialReason.cs` — `public enum DataSubjectAccessDenialReason { None, MissingPartyBinding, MissingAggregateId, AggregateMismatch }`.
    - `DataSubjectAccessDecision.cs` — `public sealed record DataSubjectAccessDecision(bool IsAllowed, DataSubjectAccessDenialReason Reason, string? DiagnosticText = null)` with `static Allowed` and `static Denied(reason, diagnosticText = null)` factories (copy the exact shape of `TenantAccessDecision.cs`).
    - `IDataSubjectAccessService.cs` — `DataSubjectAccessDecision CheckSelfAccess(string? boundPartyId, string? aggregateId);` (pure/synchronous — no projection lookup needed; this is a string-equality decision, unlike the async tenant lookup).
    - `DataSubjectAccessService.cs` — `public sealed class DataSubjectAccessService : IDataSubjectAccessService`. Fail-closed cascade: `string.IsNullOrWhiteSpace(boundPartyId)` → `Denied(MissingPartyBinding)`; `string.IsNullOrWhiteSpace(aggregateId)` → `Denied(MissingAggregateId)`; `!string.Equals(boundPartyId, aggregateId, StringComparison.Ordinal)` → `Denied(AggregateMismatch)`; else `Allowed`. **Ordinal** comparison (ids are opaque tokens — never culture-aware).
  - [x] Register in `PartiesServiceCollectionExtensions.AddParties` as **Singleton** (it is pure/stateless — mirrors the `ITenantAccessService` Singleton at line 99; no captive-dependency concern). Place the registration **next to** the `ITenantAccessService` registration with a short comment that it is the D3 defense-in-depth self-authorization decision service and is **kept off the request path** (see Task 6 fitness test).

- [x] **Task 6 — Register the server-side `Consumer` policy + pin the request-path boundary** (AC3, AC4)
  - [x] Create `src/Hexalith.Parties/Authorization/ConsumerPolicy.cs` as the **single source of truth + the reusable registration helper** (so the policy is testable in isolation, not only via the monolithic `AddParties`): a static class exposing `public const string Name = "Consumer";`, `public static readonly string[] RoleNames = ["Consumer", "consumer"];`, and `public static void Add(AuthorizationOptions options) => options.AddPolicy(Name, policy => policy.RequireRole(RoleNames));` (per the architecture's `Authorization/ConsumerPolicy.cs` entry).
  - [x] In `PartiesServiceCollectionExtensions.AddParties`, extend the existing `AddAuthorization` block (lines 67-70) to call `ConsumerPolicy.Add(options);` alongside the existing inline `Admin` policy. (Do **not** refactor the existing `Admin` policy — only the new `Consumer` policy gets the const/helper treatment.) The `Consumer`-policy unit test (Task 8) then exercises `ConsumerPolicy.Add` through a minimal `AddAuthorizationCore`, with no submodules/config.
  - [x] **Do NOT add any `[Authorize]` to `/process`** and **do NOT** reference `IDataSubjectAccessService` from `Program.cs` or `PartyDomainServiceInvoker.cs` — that is the M2M request path (AC4). The policy is **registered** (resolvable), matching the existing `Admin` policy posture (registered but not attached to `/process`).
  - [x] **Add a companion fitness test** to `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` (a new `[Fact]` in the existing `sealed class ArchitecturalFitnessTests`, reusing its private `ReadRepoFile`/`StripCommentsAndStringLiterals` helpers), e.g. `PartiesRequestPath_DoesNotUseDataSubjectAccessService()`: assert `StripCommentsAndStringLiterals(domainInvoker).ShouldNotContain("IDataSubjectAccessService")` and the same for `program`. Model it on `PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator` (lines 369-406). This keeps the new service off the gateway request path forever.

### Part C — Tests — AC6 (+ proving AC1-AC5)

- [x] **Task 7 — UI host tests** (`tests/Hexalith.Parties.UI.Tests/`, xUnit v3 + Shouldly + NSubstitute; classes `sealed`)
  - [x] **`SelfScopedPartiesClientSurfaceTests.cs` (TRIPWIRE — AC6):** reflection over `typeof(ISelfScopedPartiesClient).GetMethods()`: assert **no** method name starts with `List` or `Search`, and **no** method's return type (unwrapping `Task<>`) is `PagedResult<>` or otherwise list/search-shaped. A descriptive failure message: "Consumer self-scope accessor must never expose list/search." (This is the AC1 "never calls list/search" guarantee made structural.)
  - [x] **`SelfScopedPartiesClientCompositionTests.cs` (DI / ValidateScopes — AC5):** build a provider with `AddLogging()`, substituted `IPartiesQueryClient`/`IAdminPortalGdprClient` (`Substitute.For<…>()`), a fake `AuthenticationStateProvider`, `AddPartiesUiClaimsResolution()`, `AddSelfScopedPartiesClient()`, `BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })`. Assert: resolving `ISelfScopedPartiesClient` at the **root** throws (Scoped) / resolves **inside** `provider.CreateScope()`; assert the registered `ServiceDescriptor.Lifetime == ServiceLifetime.Scoped`. Model: `PartiesUiAuthorizationPolicyTests.BuildProvider()` posture.
  - [x] **`SelfScopedPartiesClientTests.cs` (self-scope + fail-closed — AC1/AC2):** with NSubstitute clients + a fake `AuthenticationStateProvider` returning a principal carrying a single `party_id` claim ("party-123") → `await sut.GetMyPartyAsync()` ⇒ assert `await _queryClient.Received(1).GetPartyAsync("party-123", Arg.Any<CancellationToken>(), Arg.Any<…>())` (the **resolved** id was injected). Unbound principal (no `party_id`) and ambiguous (two `party_id` claims) → `await Should.ThrowAsync<…>(() => sut.GetMyPartyAsync())` **and** `_queryClient.DidNotReceive().GetPartyAsync(…)` (fail-closed, no client call). Repeat the self-scope-injection assertion for at least one GDPR method (e.g. `RevokeMyConsentAsync("c1")` ⇒ `_gdprClient.Received(1).RevokeConsentAsync("party-123", "c1", …)`).
  - [x] **bUnit-faked `AuthenticationStateProvider`:** the simplest fake is a tiny test double `sealed class FakeAuthStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider { public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user)); }`. (Do **not** pull in a real OIDC stack.) Build principals with `new ClaimsPrincipal(new ClaimsIdentity([new Claim(PartiesUiAuthorization.PartyIdClaimType, "party-123")], "test"))` — same claim-construction idiom as `PartyIdClaimResolverTests`.

- [x] **Task 8 — Actor-host tests** (`tests/Hexalith.Parties.Tests/`, xUnit v3 + Shouldly)
  - [x] **`Authorization/DataSubjectAccessServiceTests.cs` (AC3):** model `TenantAccessServiceTests.cs`. `[Theory]` matrix: `("p1","p1") → allowed/None`; `("p1","p2") → denied/AggregateMismatch`; `(null,"p1")` / `(""/" ","p1") → denied/MissingPartyBinding`; `("p1",null)` / `("p1","")` → `denied/MissingAggregateId`; case-sensitivity (`"P1"` vs `"p1"` → `AggregateMismatch`, proving Ordinal). Assert `decision.IsAllowed` and `decision.Reason`.
  - [x] **`Authorization/PartiesConsumerPolicyTests.cs` (AC3):** build a provider with `AddLogging()` + `AddAuthorizationCore(options => { ConsumerPolicy.Add(options); options.AddPolicy("Admin", p => p.RequireRole("Admin", "admin", "administrator", "Administrator")); })` under `ValidateScopes=true` (exercises the **real** `ConsumerPolicy.Add` helper — no submodules/config). Resolve `IAuthorizationService` and assert a `Consumer`-role principal satisfies `Consumer` but **not** `Admin`; an `Admin`-role principal satisfies `Admin` but **not** `Consumer`; a role-less principal satisfies **neither** (fail-closed). Mirror `PartiesUiAuthorizationPolicyTests` shape (host-side analogue).
  - [x] **Fitness test (AC4):** the new `PartiesRequestPath_DoesNotUseDataSubjectAccessService` from Task 6 — confirm green.

- [x] **Task 9 — Build + gate verification** (AC: all)
  - [x] Build per-project (NOT the full `.slnx` pack — it pre-fails on PolymorphicSerializations NU5118/NU5128 + `*PackageTests`, unrelated): `dotnet build src/Hexalith.Parties.UI -c Release`, `dotnet build src/Hexalith.Parties -c Release`, then the two test projects `tests/Hexalith.Parties.UI.Tests` and `tests/Hexalith.Parties.Tests`. If a clean parallel build flakes (CS0006/MSB4018), re-run with `-m:1` for a reliable verdict.
  - [x] Run the UI test EXE and the host test EXE directly (xUnit v3 MTP — **do not** use `dotnet test --filter`, it returns "Zero tests ran"; run the EXE with `-class`/`-method` if filtering). Or run the lane: `scripts/test.ps1 -Lane unit` (Release).
  - [x] Confirm `bash scripts/check-no-warning-override.sh` stays green (no `TreatWarningsAsErrors` override).

## Dev Notes

### What this story adds — and the #1 disaster to prevent

Story 1.5 builds the **own-data-only security choke point** that Story 1.4 explicitly deferred ("the self-scoped accessor + fail-closed `IDataSubjectAccessService` on the Parties host, defense-in-depth … is the next story"). It has **two layers**:

- **Layer 1 (ACTIVE today) — UI BFF self-scope accessor.** `ISelfScopedPartiesClient` is the **single** data path for a Consumer. It injects the resolved `party_id` and, by the **shape of its interface**, can never issue list/search. This is the architectural choke point ("for a Consumer, the `ISelfScopedPartiesClient` is the **only** data path; it is the architectural choke point for own-data-only" — architecture.md:632-633).
- **Layer 2 (defense-in-depth building block) — Parties host `IDataSubjectAccessService` + `Consumer` policy.** Fail-closed `aggregateId == party_id` decision service + the server-side `Consumer` policy registration.

**🚨 THE #1 DISASTER: wiring the host-side check into the `/process` request path.** The `parties` actor host has **no public API** and **no end-user principal on the request path**. The EventStore gateway invokes it over DAPR at `POST /process`, and **DAPR strips the JWT** — `app.UseAuthentication()` runs but `/process` arrives with no bearer token (machine-to-machine; the gateway already authorized the caller and validated the tenant). A fitness test (`PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator`, `ArchitecturalFitnessTests.cs:369-406`) **pins access services OUT of the request path** (`Program.cs`, `PartyDomainServiceInvoker.cs`). Adding `IDataSubjectAccessService` to either of those would (a) be dead code (no principal to check) and (b) violate the boundary. **Register it + the `Consumer` policy (resolvable, unit-tested), keep them off the request path, and add a companion fitness test.** Its live request-path invocation awaits the **deferred gateway self-principal** (architecture.md:310-314). The active enforcement today is Layer 1.

> Why register a service nobody calls yet? Same reason `ITenantAccessService` and the host `Admin` policy already exist as registered-but-request-path-excluded surfaces: they are the tested, fail-closed building blocks the deferred gateway self-principal will consume. The architecture's project-structure map explicitly lists both files in `Hexalith.Parties/Authorization/` (architecture.md:602-605). Building them now (with the boundary pinned) is in-scope and expected; over-building a `/process` integration is **not**.

### Current state — what exists, what's a stub, what's missing

- **UI host references only FrontComposer** today (`Hexalith.Parties.UI.csproj`): FluentUI package + `FrontComposer.Shell/.Mcp/.SourceTools`. **No `Hexalith.Parties.Client` reference, no `Services/` folder, no gateway client wired.** Task 1 adds the Client + Contracts refs (adopter-facing — allowed).
- **`/me` (`ConsumerLanding.razor`) is an empty stub** ("Your personal space is coming soon") gated by `[Authorize(Policy = ConsumerPolicy)]`. The real consumer pages (MyProfile, MyConsent, MyDataPrivacy) are **Epic 4/5** — do not build them.
- **Story 1.4 gave you:** `PartyIdClaimResolver` (Scoped, pure, fail-closed — `PartyBindingResult { IsBound, Tenant, PartyId }`), `PartiesClaimsTransformation`, `PartiesUiAuthorization` (the `Admin`/`Consumer` UI policies + `PartyIdClaimType`/`TenantClaimType` consts), `AddPartiesUiClaimsResolution()`. **Reuse the resolver** — do not reinvent claim parsing.
- **Parties host already has the analogous family:** `Authorization/ITenantAccessService.cs`, `TenantAccessService.cs`, `TenantAccessDecision.cs`, `TenantAccessDenialReason.cs`, `TenantAccessRequirement.cs`. **Copy this exact shape** for the data-subject service. The host `Admin` policy is registered at `PartiesServiceCollectionExtensions.cs:67-70`; `ITenantAccessService` is registered Singleton at line 99 with the projection-side-only rationale you should echo.

### The accessor — concrete shape

```csharp
// src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs  (Allman braces — UI house style)
namespace Hexalith.Parties.UI.Services;

public interface ISelfScopedPartiesClient
{
    Task<PartyDetail> GetMyPartyAsync(CancellationToken ct = default);                       // → GetPartyAsync(myId)
    Task<IReadOnlyList<ConsentRecord>> GetMyConsentAsync(CancellationToken ct = default);
    Task<AdminPortalGdprCommandResult> GrantMyConsentAsync(string channelId, string purpose, LawfulBasis lawfulBasis, CancellationToken ct = default);
    Task<AdminPortalGdprCommandResult> RevokeMyConsentAsync(string consentId, CancellationToken ct = default);
    Task<AdminPortalGdprCommandResult> RequestMyErasureAsync(CancellationToken ct = default);
    Task<PartyErasureStatusRecord?> GetMyErasureStatusAsync(CancellationToken ct = default);
    Task<AdminPortalGdprCommandResult> RestrictMyProcessingAsync(string? reason, CancellationToken ct = default);
    Task<AdminPortalGdprCommandResult> LiftMyRestrictionAsync(CancellationToken ct = default);
    Task<AdminPortalExportDownload> ExportMyDataAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProcessingActivityRecord>> GetMyProcessingRecordsAsync(CancellationToken ct = default);
    // NO partyId params. NO List*/Search*. NO PagedResult<> returns. (AC1 + AC6 tripwire.)
}
```

```csharp
// SelfScopedPartiesClient.cs — fail-closed self-scope, Scoped
internal sealed class SelfScopedPartiesClient(
    AuthenticationStateProvider authStateProvider,
    PartyIdClaimResolver resolver,
    IPartiesQueryClient queryClient,
    IAdminPortalGdprClient gdprClient) : ISelfScopedPartiesClient
{
    private async Task<string> ResolveMyPartyIdAsync()
    {
        AuthenticationState state = await authStateProvider.GetAuthenticationStateAsync();
        PartyBindingResult binding = resolver.Resolve(state.User);
        return binding.IsBound
            ? binding.PartyId!
            : throw new InvalidOperationException("No bound party for the current principal."); // fail closed — no PII
    }

    public async Task<PartyDetail> GetMyPartyAsync(CancellationToken ct = default)
        => await queryClient.GetPartyAsync(await ResolveMyPartyIdAsync(), ct);

    public async Task<AdminPortalGdprCommandResult> RevokeMyConsentAsync(string consentId, CancellationToken ct = default)
        => await gdprClient.RevokeConsentAsync(await ResolveMyPartyIdAsync(), consentId, ct);
    // … remaining GDPR methods follow the identical inject-myId pattern …
}
```

- **Wrapping `IAdminPortalGdprClient` is intentional reuse, not a wrong library.** Its name is historical/packaging — it is simply the GDPR HTTP client over the EventStore gateway, and **every method already takes `partyId` first**. The self-scope is enforced by the accessor (the consumer can never pass an arbitrary id). Do **NOT** build a parallel "consumer GDPR HTTP client" — that is wheel-reinvention. Inject `myPartyId` and delegate.
- **Principal source = `AuthenticationStateProvider`**, not `IHttpContextAccessor`. In Blazor Server interactive, `HttpContext` is null once the circuit is live; `AuthenticationStateProvider` is the per-circuit principal source the FrontComposer auth bridge provides. The accessor is Scoped (per-circuit).

### The host service — concrete shape (copy `TenantAccess*`)

```csharp
// src/Hexalith.Parties/Authorization/IDataSubjectAccessService.cs  (K&R braces — host house style)
namespace Hexalith.Parties.Authorization;

public interface IDataSubjectAccessService {
    DataSubjectAccessDecision CheckSelfAccess(string? boundPartyId, string? aggregateId);
}

// DataSubjectAccessService.cs
public sealed class DataSubjectAccessService : IDataSubjectAccessService {
    public DataSubjectAccessDecision CheckSelfAccess(string? boundPartyId, string? aggregateId) {
        if (string.IsNullOrWhiteSpace(boundPartyId)) {
            return DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.MissingPartyBinding);
        }
        if (string.IsNullOrWhiteSpace(aggregateId)) {
            return DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.MissingAggregateId);
        }
        return string.Equals(boundPartyId, aggregateId, StringComparison.Ordinal)
            ? DataSubjectAccessDecision.Allowed
            : DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.AggregateMismatch);
    }
}
```
`DataSubjectAccessDecision` / `DataSubjectAccessDenialReason` are byte-for-byte the `TenantAccessDecision` / `TenantAccessDenialReason` shape (`static Allowed`, `static Denied(reason, diagnosticText = null)`).

### ⚠️ Brace / formatting style differs per project — match the file you're in

This is a real `TreatWarningsAsErrors`/`.editorconfig` trap:
- **Host (`src/Hexalith.Parties/…`) uses K&R / same-line braces** (`public sealed class X : IX {`), as in `TenantAccessService.cs`. New `Authorization/` files MUST match.
- **UI host (`src/Hexalith.Parties.UI/…`) uses Allman / next-line braces**, as in `PartiesUiAuthorization.cs`. New `Services/` files MUST match.
- Both: file-scoped namespaces; `using`s **outside** the namespace, `System.*` first; private fields `_camelCase` (or primary-ctor params); async methods end `Async`; **no unused `using`/`@using`** (build error); nullable enabled (don't silence with `!` except the proven-bound `binding.PartyId!`).

### Established patterns you MUST follow (from 1.1-1.4)

- **Reuse `PartyIdClaimResolver`** for binding resolution — it already encodes fail-closed (0 or >1 `party_id` ⇒ `Unbound`). The accessor's fail-closed behavior is "resolver says unbound ⇒ throw, no client call."
- **Single source of truth for auth strings** — the new `Consumer` policy/role names get a `ConsumerPolicy` const holder in the host `Authorization/` (architecture lists `ConsumerPolicy.cs`). UI policy/claim consts already live in `PartiesUiAuthorization` — reference, never re-hardcode.
- **Fail-closed is proven, not asserted** — tests must assert the **negative** (unbound ⇒ throws **and** `DidNotReceive()` on the client; mismatch ⇒ `Denied`). Mirror Story 1.4's `ShouldNotEndWith("/me")` discipline.
- **DI lifetime (ADR-030, pinned):** the UI self accessor is **Scoped** (claims/circuit-derived); the host decision service is **Singleton** (pure/stateless, like `ITenantAccessService`). `ValidateScopes=true` catches a Singleton capturing the Scoped accessor at boot.
- **Adopter-facing vs internal split** — UI may reference `Client`/`Contracts` (adopter-facing); never the internal host projects. Pinned by fitness tests.
- **Gateway boundary** — the UI BFF talks only to the EventStore gateway via the typed client; it never calls the actor host directly and adds no public API. The host trusts the gateway for request-path RBAC.

### Source tree — files to create / touch

| Action | File |
|---|---|
| EDIT | `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` — add `Hexalith.Parties.Client` + `Hexalith.Parties.Contracts` ProjectReferences |
| NEW | `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs` (+ `AddSelfScopedPartiesClient` extension) |
| EDIT | `src/Hexalith.Parties.UI/Program.cs` — `AddSelfScopedPartiesClient()` (unconditional) + gated `AddPartiesClient(configuration)` |
| NEW | `src/Hexalith.Parties/Authorization/IDataSubjectAccessService.cs` |
| NEW | `src/Hexalith.Parties/Authorization/DataSubjectAccessService.cs` |
| NEW | `src/Hexalith.Parties/Authorization/DataSubjectAccessDecision.cs` |
| NEW | `src/Hexalith.Parties/Authorization/DataSubjectAccessDenialReason.cs` |
| NEW | `src/Hexalith.Parties/Authorization/ConsumerPolicy.cs` (policy + role-name single source of truth) |
| EDIT | `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` — register `IDataSubjectAccessService` (Singleton) + add `Consumer` policy to `AddAuthorization` |
| NEW | `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` (tripwire) |
| NEW | `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientCompositionTests.cs` (DI/ValidateScopes) |
| NEW | `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs` (self-scope + fail-closed) |
| NEW | `tests/Hexalith.Parties.Tests/Authorization/DataSubjectAccessServiceTests.cs` |
| NEW | `tests/Hexalith.Parties.Tests/Authorization/PartiesConsumerPolicyTests.cs` |
| EDIT | `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` — add `PartiesRequestPath_DoesNotUseDataSubjectAccessService` |

### Testing standards

- **xUnit v3 + Shouldly + NSubstitute** (UI also has bUnit, but these tests need only plain xUnit). No Moq/FluentAssertions/raw `Assert.*`. Test classes `sealed`; descriptive sentence method names.
- **NSubstitute idiom:** `Substitute.For<IPartiesQueryClient>()`; assert injection with `await client.Received(1).GetPartyAsync("party-123", Arg.Any<CancellationToken>(), Arg.Any<Func<HttpRequestMessage, CancellationToken, ValueTask>?>())` and fail-closed with `client.DidNotReceive().GetPartyAsync(default!, default, default)` (match the real signature — `GetPartyAsync` has an optional `requestCustomizer` param).
- **ValidateScopes idiom:** `BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })`; resolve the accessor inside `using var scope = provider.CreateScope();` (model `PartiesUiAuthorizationPolicyTests`).
- **Run lane:** `scripts/test.ps1 -Lane unit` (Release). Do **not** `dotnet test --filter` (xUnit v3 MTP "Zero tests ran"); run the EXE with `-class`/`-method`.

### Build / gotchas (carried forward, will bite otherwise)

- **`AddPartiesClient` throws at registration when `Parties:BaseUrl` is absent** — gate it (Task 4). Never unconditional.
- **Don't build the whole `.slnx`** to judge yourself — full Release `pack` pre-fails (PolymorphicSerializations NU5118/NU5128 + `*PackageTests`), unrelated. Verify per-project + sibling test EXEs. Clean parallel builds can flake (CS0006/MSB4018) — re-run `-m:1`.
- **Central Package Management:** no `Version=` in any csproj. No new packages needed.
- **PII hygiene:** never log/throw the `party_id`, tenant, consent ids, or any claim/party value. The fail-closed exception message is generic ("No bound party for the current principal.").
- **No public API / controllers** on either host from this story — it's auth/accessor/policy wiring only.

### Project Structure Notes

- `Services/ISelfScopedPartiesClient.cs` + `.cs` land **exactly** where the architecture's project-structure map prescribes (`UI/Services/ISelfScopedPartiesClient.cs / .cs # D3 — the SINGLE consumer self-scope accessor`, architecture.md:571). The `Authorization/IDataSubjectAccessService.cs` + `ConsumerPolicy.cs` land exactly where prescribed (architecture.md:602-605). No structural variance.
- **Scope boundary — do NOT over-build:** no consumer pages (Epic 4/5), no profile-edit commands on the accessor (Story 4.5), no live token relay / consumer gateway round-trip (deferred residual), no `IDataSubjectAccessService` on the `/process` request path (AC4). The deliverable is the **structural choke point + fail-closed/Scoped guarantees + host-side defense-in-depth building blocks**, all proven by tests.

### References

- [Source: epics.md#Story 1.5 — Consumer own-data self-authorization (defense-in-depth) / lines 511-527] — user story + ACs (single self-scoped accessor; fail-closed `IDataSubjectAccessService` `aggregateId == party_id`; `Consumer` policy server-side; tripwire on list/search + Scoped-capture).
- [Source: architecture.md#Authentication & Security / D3 / lines 301-314] — the two-part D3 design (BFF self-scope + Parties host self-authorization) **and the deferred gateway-RBAC residual** that explains why AC4 keeps the host check off the request path.
- [Source: architecture.md#Process Patterns / lines 488-496] — "a Consumer principal **never** calls list/search … one self-scoped accessor that injects the resolved `party_id`"; Scoped-never-Singleton (ADR-030); `ValidateScopes=true`.
- [Source: architecture.md#Enforcement Guidelines / lines 525-529] — "a storage/self-scope **tripwire test** (FrontComposer NFR17 pattern)".
- [Source: architecture.md#Architectural Boundaries / lines 631-633] and [#Requirements→Structure / line 645] — `ISelfScopedPartiesClient` is the **only** consumer data path (the choke point); D3 lives in `UI/Services/SelfScopedPartiesClient` + `Hexalith.Parties/Authorization/`.
- [Source: architecture.md#Project Structure / lines 570-571, 602-605] — exact file placement for the accessor, `IDataSubjectAccessService`, and `ConsumerPolicy`.
- [Source: src/Hexalith.Parties/Program.cs / lines 34-67] — M2M middleware pipeline + `/process` minimal-API (no `[Authorize]`; DAPR-only access). Confirms no end-user principal on the request path.
- [Source: src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs / lines 67-70, 87-99] — the existing `Admin` policy to mirror for `Consumer`; the `ITenantAccessService` Singleton + projection-side-only rationale to mirror for `IDataSubjectAccessService`.
- [Source: src/Hexalith.Parties/Authorization/TenantAccessService.cs, TenantAccessDecision.cs, TenantAccessDenialReason.cs, ITenantAccessService.cs] — the exact family shape (K&R braces, fail-closed factories) to copy for the data-subject service.
- [Source: tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs / lines 369-406] — `PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator` + helpers (`ReadRepoFile`/`StripCommentsAndStringLiterals`) to mirror for the new request-path boundary test (Task 6).
- [Source: tests/Hexalith.Parties.Tests/Authorization/TenantAccessServiceTests.cs] — model for `DataSubjectAccessServiceTests` (fail-closed `[Theory]` matrix).
- [Source: src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs + PartiesUiAuthorization.cs] — Story 1.4 resolver (`PartyBindingResult { IsBound, Tenant, PartyId }`) + `PartyIdClaimType`/`ConsumerPolicy` consts to reuse.
- [Source: src/Hexalith.Parties.UI/Program.cs / lines 13, 41, 50-53] — `ValidateScopes=true`; where to add `AddSelfScopedPartiesClient()`; the `authEnabled` gate pattern to mirror for `partiesClientEnabled`.
- [Source: src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs] — `GetPartyAsync(partyId, ct, requestCustomizer?)` (INCLUDE) vs `ListPartiesAsync`/`SearchPartiesAsync` (EXCLUDE — return `PagedResult<>`).
- [Source: src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs] — the GDPR gateway client the accessor wraps (every method takes `partyId` first); result types (`AdminPortalGdprCommandResult`, `PartyErasureStatusRecord?`, `AdminPortalExportDownload`, `ConsentRecord`, `ProcessingActivityRecord`).
- [Source: src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs] — `AddPartiesClient` **throws at registration** without `Parties:BaseUrl` (the gating rationale in Task 4).
- [Source: src/Hexalith.Parties.UI/Components/Areas/ConsumerLanding.razor] — `/me` is an empty stub; confirms no consumer data page consumes the accessor yet (scope boundary).
- [Source: _bmad-output/implementation-artifacts/1-4-fail-closed-party-id-claim-resolution.md] — predecessor: the resolver, the adopter↔internal boundary trap, the unconditional-registration + `ValidateScopes` test idioms, and the explicit forecast of THIS story (1.4 Dev Notes "Scope boundary" + Project Structure Notes).
- [Source: _bmad-output/project-context.md] — gateway boundary (no public host API; `eventstore → POST /process` only); adopter-facing vs internal split; CPM; `TreatWarningsAsErrors`; PII hygiene; xUnit v3 / Shouldly / NSubstitute / bUnit; ADR-030 `ValidateScopes`; `ITenantAccessService` projection-side-only.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- Per-project Release builds (`-m:1` for a reliable verdict): `src/Hexalith.Parties`, `src/Hexalith.Parties.UI`, `tests/Hexalith.Parties.Tests`, `tests/Hexalith.Parties.UI.Tests` — **all 0 Warning(s) / 0 Error(s)** (TreatWarningsAsErrors is solution-wide; clean = no analyzer breaches).
- UI test EXE (full suite): **109 passed, 0 failed**. New self-scope classes run in isolation (`-class "*SelfScopedPartiesClient*"`): **40 passed, 0 failed**. (Counts re-verified during the senior review; an earlier draft of this log under-reported them as 79/10 before the per-method fail-closed theories and pass-through/cancellation facts were added.)
- Host test EXE (full suite): **485 total, 484 passed, 1 failed**. The single failure —
  `FitnessTests.AppHostTenantsTopologyTests.AppHostProjectReferencesEventStoreTenantsAndAspireProjects` —
  is **pre-existing and unrelated to Story 1.5**: it asserts the literal substring
  `Hexalith.EventStore\src\…` in `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`, but the
  committed AppHost csproj uses the `$(HexalithEventStoreRoot)\src\…` property form (no `Hexalith.EventStore\src`
  literal). Neither the AppHost csproj nor that test file is touched by this story (`git status` confirms);
  the test was already red at baseline `e454663`. New host classes in isolation
  (`DataSubjectAccessServiceTests` + `PartiesConsumerPolicyTests`): **15 passed, 0 failed**;
  `PartiesRequestPath_DoesNotUseDataSubjectAccessService`: **1 passed**.
- Build-gate `bash scripts/check-no-warning-override.sh`: **OK** (no warning-override / nested-submodule regressions).

### Completion Notes List

**Part A — UI BFF self-scope choke point (Layer 1, ACTIVE):**
- Added adopter-facing `Hexalith.Parties.Client` + `Hexalith.Parties.Contracts` ProjectReferences to the UI csproj (relative paths, no `Version=`); no internal host project referenced (boundary preserved).
- `Services/ISelfScopedPartiesClient.cs` (Allman/UI style) — the single Consumer data path. Tripwire-by-construction: **no `partyId` param on any method**, **zero `List*`/`Search*`**, **zero `PagedResult<>` returns**. Surface = read (`GetMyPartyAsync`) + GDPR self-service; profile-WRITE deliberately deferred to Story 4.5.
- `Services/SelfScopedPartiesClient.cs` — `internal sealed`, fail-closed `ResolveMyPartyIdAsync()` reuses Story 1.4's `PartyIdClaimResolver` over `AuthenticationStateProvider` (not `IHttpContextAccessor`); `IsBound==false` ⇒ throws a **non-PII** `InvalidOperationException` and never touches the underlying client. Every method injects the resolved id. `ConfigureAwait(false)` throughout (CA2007 is warning→error). `AddSelfScopedPartiesClient()` extension registers it **Scoped** (ADR-030).
- `Program.cs` — registers the accessor **unconditionally** (after `AddPartiesUiClaimsResolution()`); gates `AddPartiesClient(configuration)` on `Parties:BaseUrl` (it throws at registration otherwise). No live OIDC→gateway token relay (deferred residual).
- Added `InternalsVisibleTo("Hexalith.Parties.UI.Tests")` so the test project can pin the registered lifetime and construct the internal accessor directly.

**Part B — Parties actor-host defense-in-depth (Layer 2, registered building block):**
- `Authorization/{DataSubjectAccessDenialReason,DataSubjectAccessDecision,IDataSubjectAccessService,DataSubjectAccessService}.cs` (K&R/host style) — byte-for-byte the `TenantAccess*` shape. Fail-closed cascade: null/empty `boundPartyId` ⇒ `MissingPartyBinding`; null/empty `aggregateId` ⇒ `MissingAggregateId`; **Ordinal** inequality ⇒ `AggregateMismatch`; else `Allowed`.
- `Authorization/ConsumerPolicy.cs` — single source of truth (`Name`, `RoleNames`, `Add(AuthorizationOptions)` helper; `ThrowIfNull` guard keeps CA1062 satisfied while staying expression-equivalent).
- `PartiesServiceCollectionExtensions.AddParties` — registers `IDataSubjectAccessService` **Singleton** next to `ITenantAccessService` (pure/stateless), and calls `ConsumerPolicy.Add(options)` inside the existing `AddAuthorization` block alongside the untouched `Admin` policy.
- **AC4 boundary honored:** `IDataSubjectAccessService` is NOT wired into `Program.cs` or `PartyDomainServiceInvoker.cs` (M2M `/process` carries no end-user principal — DAPR strips the JWT). New fitness test `PartiesRequestPath_DoesNotUseDataSubjectAccessService` pins it out of the request path forever (mirrors `PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator`). Active own-data-only enforcement today is Layer 1 (the BFF accessor).

**Part C — tests (AC6 + proving AC1–AC5):** tripwire reflection (no list/search/`PagedResult`/`partyId`), DI/ValidateScopes (Scoped descriptor + root-throws/scope-resolves), self-scope injection + fail-closed (`Received(1)` with the resolved id; `DidNotReceiveWithAnyArgs` + `ThrowAsync` for unbound/ambiguous), host decision matrix (`[Theory]` incl. Ordinal case-sensitivity), and the real `ConsumerPolicy.Add` exercised through `AddAuthorizationCore` (Consumer/Admin/role-less fail-closed).

### File List

**Source (new):**
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs` (+ `AddSelfScopedPartiesClient` extension)
- `src/Hexalith.Parties/Authorization/IDataSubjectAccessService.cs`
- `src/Hexalith.Parties/Authorization/DataSubjectAccessService.cs`
- `src/Hexalith.Parties/Authorization/DataSubjectAccessDecision.cs`
- `src/Hexalith.Parties/Authorization/DataSubjectAccessDenialReason.cs`
- `src/Hexalith.Parties/Authorization/ConsumerPolicy.cs`

**Source (edited):**
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` (Client + Contracts ProjectReferences; `InternalsVisibleTo` for the UI test project)
- `src/Hexalith.Parties.UI/Program.cs` (`AddSelfScopedPartiesClient()` unconditional; gated `AddPartiesClient`)
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (`IDataSubjectAccessService` Singleton; `ConsumerPolicy.Add` in `AddAuthorization`)

**Tests (new):**
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/FakeAuthStateProvider.cs`
- `tests/Hexalith.Parties.Tests/Authorization/DataSubjectAccessServiceTests.cs`
- `tests/Hexalith.Parties.Tests/Authorization/PartiesConsumerPolicyTests.cs`

**Tests (edited):**
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` (added `PartiesRequestPath_DoesNotUseDataSubjectAccessService`)

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-10 · **Outcome:** ✅ Approve · **Mode:** autonomous (story-automator-review, auto-fix)

### Scope verified
Adversarial re-validation of every story claim against the actual implementation and a clean rebuild/retest from baseline `e454663` (HEAD == baseline; no drift).

### AC verification (all met)
- **AC1 — single self-scope accessor, structurally list/search-free.** `ISelfScopedPartiesClient` (`src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`) exposes read + GDPR self-service only; **no method takes a `partyId`**, none is named `List*`/`Search*`, none returns `PagedResult<>`. The three reflection tripwires (`SelfScopedPartiesClientSurfaceTests`) pin this. `SelfScopedPartiesClient` injects the **resolved** id into every underlying call — confirmed against the real `IPartiesQueryClient`/`IAdminPortalGdprClient` signatures.
- **AC2 — fail-closed.** `ResolveMyPartyIdAsync` reuses Story 1.4 `PartyIdClaimResolver`; `IsBound == false` ⇒ throws a non-PII `InvalidOperationException` and never touches the client. Proven for **every** accessor method (unbound + ambiguous theories assert `ReceivedCalls().ShouldBeEmpty()`).
- **AC3 — host defense-in-depth.** `DataSubjectAccessService` fail-closed cascade (Ordinal) byte-for-byte mirrors `TenantAccessDecision`; registered Singleton beside `ITenantAccessService`. `ConsumerPolicy` single-source-of-truth helper added to the existing `AddAuthorization` block beside the untouched `Admin` policy. Decision matrix + Consumer/Admin/role-less mutual-exclusion tests green.
- **AC4 — request-path boundary honored.** `IDataSubjectAccessService` is absent from `Program.cs` and `PartyDomainServiceInvoker.cs`; new fitness test `PartiesRequestPath_DoesNotUseDataSubjectAccessService` (mirrors the tenant-access pin) is green.
- **AC5 — Scoped / ValidateScopes.** Descriptor pinned `ServiceLifetime.Scoped`; root resolution throws, scope resolution succeeds under `ValidateScopes=true`.
- **AC6 — test surface.** All six required test categories present and green.

### Build & test (re-verified, not trusted)
- Per-project Release builds (`-m:1`): `Hexalith.Parties.UI`, `Hexalith.Parties`, both test projects — **0 Warning(s) / 0 Error(s)**.
- UI test EXE: **109 passed, 0 failed** (self-scope classes in isolation: **40 passed**).
- Host test EXE: **485 total, 484 passed, 1 failed**. The single failure — `FitnessTests.AppHostTenantsTopologyTests.AppHostProjectReferencesEventStoreTenantsAndAspireProjects` — is **pre-existing and unrelated**: `git diff e454663` shows the AppHost csproj and that test file are byte-identical to baseline (the test asserts a literal `Hexalith.EventStore\src\…` substring while the csproj uses the `$(HexalithEventStoreRoot)\…` property form). New host classes in isolation: **15 passed**; new fitness test: **1 passed**.
- `scripts/check-no-warning-override.sh`: **OK**.
- Git File List cross-check: every changed/new source & test file is documented; no undocumented changes, no phantom claims.

### Findings
- **[LOW · fixed]** Dev Agent Record → Debug Log References under-reported the UI test counts (79/10) versus the delivered suite (109/40). Corrected in this pass; host counts (485/1, 15, 1) were already accurate.

No CRITICAL/HIGH/MEDIUM findings. The deliverable is the structural choke point + fail-closed/Scoped guarantees + host-side building blocks, all proven by tests, with the M2M request-path boundary correctly preserved.

## Change Log

| Date | Version | Description |
|---|---|---|
| 2026-06-10 | 1.0 | Story 1.5 implemented — consumer own-data self-authorization (defense-in-depth): the UI BFF `ISelfScopedPartiesClient` self-scope choke point (fail-closed, Scoped, structurally list/search-free) + the Parties actor-host fail-closed `IDataSubjectAccessService` and server-side `Consumer` policy, both registered and kept off the M2M request path (companion fitness test). All ACs covered by tests; per-project builds clean, all new tests green, build-gate green. Status → review. |
| 2026-06-10 | 1.1 | Senior Developer Review (AI, autonomous auto-fix) — all 6 ACs re-verified against the implementation; clean rebuild + retest from baseline (UI 109/0, host 484 pass / 1 pre-existing-unrelated fail, build-gate OK). One LOW finding (stale UI test counts in the Debug Log) auto-fixed. No CRITICAL/HIGH/MEDIUM issues. Outcome: Approve → Status `done`. |
