---
baseline_commit: 7c880955827300af1970a0ce70f1b4e700d6ca81
---

# Story 1.7: Live freshness via SignalR + shared optimistic-reconcile effect

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want my changes to appear immediately and reconcile silently — and the screen to keep showing live, trustworthy data even when the backend is catching up —
so that eventual consistency is invisible and never alarming.

This story builds the **shared D6 mechanism every later screen consumes** — established once in Epic 1 (epics.md:347 "the shared enablers … SignalR freshness … established once in Epic 1"): (1) a reusable **EventStore projection subscription over SignalR** that reconnects and re-subscribes without duplicate application, (2) a reusable **optimistic-then-reconcile orchestration** (optimistic echo + polite "Saving…" → command → reconcile on SignalR projection-confirm *or* `Freshness=Current` → revert + `role=alert` on rejection), and (3) a **degraded fallback** (poll + freshness metadata, surface `X-Service-Degraded` / `X-Stale-Data-Age`) when the stream is down. It reuses Story 1.6's `StatusKind` / `StatusPresentation` / `StatusLiveRegion` verbatim. **No page consumes it yet** — the Epic 2/4/5 screens (and the generated `[Command]` Fluxor lifecycle) call these building blocks later; this story ships the mechanism + tests, exactly as Stories 1.4/1.5/1.6 shipped their enablers and deferred page wiring.

## Acceptance Criteria

1. **AC1 — Shared optimistic-then-reconcile orchestration (one shared primitive, not per-screen).** **Given** a command issued from any screen, **when** the shared optimistic-then-reconcile primitive runs, **then** it: (a) dispatches/applies **optimistic state** and an **`aria-live="polite"` "Saving…"** signal (`StatusKind.AcceptedProcessing`, **without stealing focus**); (b) issues the command via the supplied **self-scoped / tenant-scoped client delegate**; (c) **reconciles** when the owning projection confirms — on a **SignalR projection-confirm** signal **or** when a re-read returns **`ProjectionFreshnessStatus.Current`** (whichever arrives first), announcing the reconciled state via `aria-live="polite"` (still no focus steal); and (d) on **rejection** (`PartiesClientException` / validation outcome) **reverts** the optimistic state and surfaces an **inline `role="alert"` (assertive) reason** mapped through `StatusPresentation`. The primitive is **slice-agnostic** (driven by delegates), so it is reusable verbatim by every screen and has **exactly one** implementation — no per-screen re-implementation.

2. **AC2 — SignalR subscription + degraded fallback.** **Given** the EventStore projection stream, **when** it is available, **then** the UI subscribes to projection-change notifications via SignalR (reusing `Hexalith.EventStore.SignalR`'s `EventStoreSignalRClient` over `Microsoft.AspNetCore.SignalR.Client` 10.0.8, the FrontComposer Shell transport), keyed by `(projectionType, tenant)`; **and when** the stream is **degraded / unavailable** (the connection is not `Connected`), **then** the mechanism **falls back to polling** the reconcile re-read on a configurable interval **plus** freshness metadata, mapping non-`Current` freshness to **`StatusKind.Degraded`** (`role="status" aria-live="polite"`), and a reusable seam **captures `X-Service-Degraded` / `X-Stale-Data-Age`** from gateway GET responses into a Scoped degraded-state accessor for UI state (NFR8). The reconcile primitive uses freshness metadata as its primary degraded signal; the two headers are the transport-level secondary signal, captured when present.

3. **AC3 — Reconnect without duplicate application; announce-not-steal.** **Given** a transient SignalR disconnect, **when** the connection automatically reconnects, **then** all active group subscriptions are **re-joined automatically** (delegated to `EventStoreSignalRClient`'s `WithAutomaticReconnect` + auto-rejoin) and the reconcile path **applies no duplicate state** — reconcile is an **idempotent re-read** and the optimistic primitive holds a **one-shot reconcile guard** so a late confirm arriving after the state has already reconciled (or after the screen disposed) is a **no-op**. **And** routine optimistic saves announce **only** via `aria-live` and **never move focus**; a **user-initiated cancellation** (the supplied `CancellationToken` fired) is **dropped silently** — never mapped to a UI failure state (this story is the call-site that fulfils the Story 1.6 `FromException` user-cancel contract).

4. **AC4 — Wiring, lifetime & fail-closed/degraded boot.** **Given** the UI host, **when** the new services are registered, **then** the subscription wrapper and the optimistic-reconcile primitive are registered **Scoped** (per-circuit — ADR-030; `ValidateScopes=true` must still boot green), composed **unconditionally** (mirroring `AddSelfScopedPartiesClient` / `AddPartiesUiClaimsResolution`) but **lazily/inertly** — when no SignalR hub URL is configured (test / degraded boot) nothing connects and `IsConnected` is `false` (driving the polling fallback) with **no boot failure**. The AppHost enables the EventStore SignalR hub for normal run mode and injects the hub URL into `parties-ui`; `appsettings.json` carries the (empty-default, `__`-overridable) config keys. **No secrets are committed.**

5. **AC5 — Placement, purity & scope boundary.** **Given** the new artifacts, **when** they are placed in the source tree, **then** they live in the **`Hexalith.Parties.UI` host** under `Services/` (the subscription wrapper `PartiesProjectionSubscription` + the optimistic-reconcile primitive + the degraded-state seam), reuse Story 1.6's `Hexalith.Parties.UI.Status` types **verbatim** (no second StatusKind, no per-screen remap), introduce the **`Hexalith.EventStore.SignalR` project reference** (SignalR.Client flowing transitively — no Central-Package-Management change required), and **do not**: add a page/route, add a Fluxor slice or `[Command]`/`[Projection]` marker, adopt FrontComposer's high-level `AddHexalithEventStore` (which would swap the typed `Parties.Client`), or wire the live per-circuit OIDC token-capture (a documented seam, finalized with the first authenticated data screen). The browser still talks only to the UI host; the host subscribes to the EventStore SignalR hub server-side.

6. **AC6 — Tests prove AC1–AC4 without a live hub.** **Given** the test suite, **when** it runs, **then** **xUnit v3 + Shouldly (+ NSubstitute, + bUnit where DOM is asserted)** cover: the optimistic-reconcile primitive (optimistic + polite "Saving…" → confirm-reconcile via SignalR signal **and** via `Freshness=Current`; rejection → revert + `role=alert`; **user-cancel → silent drop**; **duplicate/late confirm → no double-apply**; announce-without-focus-steal); the subscription wrapper over a **test seam** (subscribe/unsubscribe, **inert when no hub URL**, reconnect re-subscribes via the auto-rejoin, `IsConnected` drives fallback); the **degraded-header** reader (`X-Service-Degraded`/`X-Stale-Data-Age` present → degraded; absent → not); the **polling fallback** (polls while disconnected, stops on reconnect); and a **composition test** (all new services resolve **Scoped**, the host boots under `ValidateScopes=true`, and a **no-hub-URL / no-`Parties:BaseUrl`** boot composes cleanly). The full UI suite stays green (no regression to the 1.1–1.6 tests).

## Tasks / Subtasks

### Part A — SignalR projection subscription wrapper — AC2, AC3, AC4, AC5

- [x] **Task 1 — Reference `Hexalith.EventStore.SignalR`** (`src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`) (AC2, AC5)
  - [x] Add `<ProjectReference Include="$(HexalithEventStoreRoot)\src\Hexalith.EventStore.SignalR\Hexalith.EventStore.SignalR.csproj" />` in the existing reference `ItemGroup` (sits with the `Client`/`Contracts`/`Shell` refs). `$(HexalithEventStoreRoot)` is defined in `Directory.Build.props:4-5` (same property the AppHost uses).
  - [x] **Do NOT add an explicit `<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />`** and **do NOT touch `Directory.Packages.props`.** `Microsoft.AspNetCore.SignalR.Client` (10.0.8) flows **transitively** through the `Hexalith.EventStore.SignalR` project reference **and** through the already-referenced `Hexalith.FrontComposer.Shell` (which also references it). Central Package Management resolves it from the EventStore submodule's own `Directory.Packages.props` (10.0.8). Adding an explicit `PackageReference` to the host would demand a `PackageVersion` in *this* repo's `Directory.Packages.props` — avoid that; the transitive reference makes SignalR.Client types (`HubConnectionState`, etc.) available at compile time. **Verify-live:** confirm `EventStoreSignalRClient` / SignalR.Client types resolve in the host after the project ref is added.

- [x] **Task 2 — Define the testable projection-stream seam** (`src/Hexalith.Parties.UI/Services/IProjectionStream.cs`) (AC3, AC6)
  - [x] `EventStoreSignalRClient` is a **`sealed` concrete class** (`Hexalith.EventStore.SignalR.EventStoreSignalRClient`) with no interface — to unit-test the wrapper and the reconcile primitive **without a live hub**, depend on a thin seam, not the concrete client.
  - [x] `public interface IProjectionStream` exposing exactly what the wrapper needs: `bool IsConnected { get; }`, `Task EnsureStartedAsync(CancellationToken ct = default)`, `Task SubscribeAsync(string projectionType, string tenantId, Action onChanged)`, `Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged)`. Keep the method shapes **identical to `EventStoreSignalRClient`** (`SubscribeAsync(projectionType, tenantId, Action)` / `UnsubscribeAsync(projectionType, tenantId, Action)` / `IsConnected` / `StartAsync`) so the production adapter is a 1:1 forward.
  - [x] Allman braces (UI host house style). File-scoped namespace `Hexalith.Parties.UI.Services`.

- [x] **Task 3 — Production adapter over `EventStoreSignalRClient`** (`src/Hexalith.Parties.UI/Services/EventStoreSignalRProjectionStream.cs`) (AC2, AC3)
  - [x] `internal sealed class EventStoreSignalRProjectionStream : IProjectionStream, IAsyncDisposable` that **constructs and owns one `EventStoreSignalRClient`** built from `EventStoreSignalRClientOptions { HubUrl = <configured>, AccessTokenProvider = <seam>, ... }`. `IsConnected` / `Subscribe`/`Unsubscribe` forward 1:1. `EnsureStartedAsync` calls `EventStoreSignalRClient.StartAsync` **once** (idempotent guard) and is a **no-op when the hub URL is unconfigured** (degraded/test boot → never connect, `IsConnected` stays false).
  - [x] Reconnect/auto-rejoin is **entirely the client's job** — `EventStoreSignalRClient` wires `WithAutomaticReconnect` and `OnReconnectedAsync → JoinAllGroupsAsync()` internally (subscriptions survive reconnect; FR59). The adapter adds **no** reconnect logic and **no** dedup (the signal-only re-query design + the primitive's one-shot guard handle "no duplicate application").
  - [x] `DisposeAsync` forwards to `EventStoreSignalRClient.DisposeAsync()`.
  - [x] **Access-token seam:** the `AccessTokenProvider` (`Func<Task<string?>>`) must yield the **circuit's server-side OIDC access token**. In a live Blazor Server circuit `HttpContext` is **null** (see Dev Notes "Blazor-Server token trap"), so the live per-circuit token capture is **deferred to the first authenticated data screen** (Epic 2/4). For this story, supply a pluggable provider that returns the token when one is available and `null` otherwise (the hub may be configured to allow the server-side caller without a per-user bearer in dev). **Do not** read `HttpContext.GetTokenAsync` from inside the connect/reconnect callback.

- [x] **Task 4 — The subscription wrapper** (`src/Hexalith.Parties.UI/Services/PartiesProjectionSubscription.cs`) (AC2, AC3, AC4)
  - [x] `public sealed class PartiesProjectionSubscription(IProjectionStream stream, ...) : IAsyncDisposable` — the **architecture-named** D6 service (`architecture.md:572`, `646`). **Scoped** (per circuit — see Dev Notes "Lifetime").
  - [x] API the optimistic primitive + future screens call: `bool IsConnected`, `Task EnsureStartedAsync(CancellationToken)`, `IDisposable Subscribe(string projectionType, string tenant, Action onConfirmed)` (returns a disposable that unsubscribes — the Blazor `IAsyncDisposable`/`@implements`-friendly shape), and dispose-time teardown of all live subscriptions.
  - [x] On first `Subscribe`, call `EnsureStartedAsync` (lazy connect). If the stream is unconfigured/disconnected, the subscription still registers (so it auto-joins on a later connect) but the **polling fallback** owns refresh meanwhile.
  - [x] **PII hygiene:** never log `projectionType`/`tenant`/party values; logs carry only coarse connection-state transitions.

### Part B — Degraded fallback + header surfacing — AC2, AC4

- [x] **Task 5 — Degraded-state accessor + header-reading handler** (`src/Hexalith.Parties.UI/Services/DegradedState*.cs`) (AC2, AC4)
  - [x] `public interface IDegradedStateAccessor` + Scoped impl holding the last-seen `{ bool IsDegraded; long? StaleDataAgeSeconds; }` (per-circuit). Provide a `StatusKind?` convenience that returns `StatusKind.Degraded` when degraded (reuse `StatusPresentation`; **do not** invent a state).
  - [x] `internal sealed class DegradedResponseHeaderHandler : DelegatingHandler` that reads response headers **`X-Service-Degraded`** (`"true"` → degraded) and **`X-Stale-Data-Age`** (integer seconds) and writes them into `IDegradedStateAccessor`. Header names are produced by `Hexalith.Parties.Middleware.DegradedResponseMiddleware` (`src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs:54-60,71-78`) on **GET** responses only.
  - [x] Register the handler on the gateway `HttpClient` **only when `Parties:BaseUrl` is configured** (alongside `AddPartiesClient`) — see Task 9. **Verify-live:** confirm whether the EventStore gateway **relays** the Parties-host degraded headers to the UI host's client responses; if it does not, the handler simply never sets degraded and **`ProjectionFreshnessMetadata` remains the primary degraded signal** (already carried end-to-end on `PartyDetail.Freshness`). Document the finding in Dev Agent Record. Do **not** block the story on the relay; the building block + its test are the deliverable.

- [x] **Task 6 — Polling fallback coordinator** (`src/Hexalith.Parties.UI/Services/ProjectionFreshnessFallback.cs`) (AC2)
  - [x] A small Scoped coordinator the reconcile primitive uses when `!IsConnected`: invokes the supplied reconcile re-read on a **configurable interval** (`Parties:Freshness:PollingIntervalSeconds`, default **30** — mirrors EventStore Admin UI's `DashboardRefreshService` 30 s loop) using a `PeriodicTimer`; **stops** when the stream reports `IsConnected` (SignalR resumed) or the operation completes/disposes.
  - [x] Use `TimeProvider` (already a Singleton in the FrontComposer graph) so the timer is **testable** (fake time → assert "polls while disconnected, stops on reconnect") — do not use `Task.Delay`/`DateTime.UtcNow` directly.

### Part C — The shared optimistic-reconcile primitive — AC1, AC3

- [x] **Task 7 — `OptimisticReconcile` primitive** (`src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs`) (AC1, AC3)
  - [x] `public sealed class OptimisticReconcile(PartiesProjectionSubscription subscription, ProjectionFreshnessFallback fallback, ...)` — **Scoped**. The single shared optimistic-then-reconcile orchestration (architecture.md:479 "one shared effect pattern, not per-screen"; 656-659 command data flow).
  - [x] A `record OptimisticReconcileRequest` (or method params) carrying **delegates** so the primitive is **slice-agnostic**: `Action applyOptimistic` (caller dispatches optimistic slice state); `Func<CancellationToken, Task> issueCommand` (calls the self-scoped/tenant-scoped client); `Func<CancellationToken, Task<ProjectionFreshnessStatus>> reconcile` (caller's idempotent re-read; returns the freshness so the primitive knows when `Current`); `Action revert` (caller reverts optimistic state); `(string ProjectionType, string Tenant) projectionKey` (to await SignalR confirm); and an `Action<StatusKind, string?> announce` callback (caller renders via `StatusLiveRegion` — the primitive never touches the DOM/focus itself).
  - [x] **Flow:** `applyOptimistic()` → `announce(AcceptedProcessing, "Saving…")` (**polite, no focus steal**) → `await issueCommand(ct)`. On success: subscribe **once** to `projectionKey` via `PartiesProjectionSubscription` **and** (when disconnected) start `ProjectionFreshnessFallback`; on the **first** of {SignalR confirm, a re-read returning `Current`} → run `reconcile`, `announce` the reconciled status (polite), then **unsubscribe / stop polling**. Hold a **one-shot reconcile guard** (e.g. an `Interlocked`/bool) so a duplicate or late confirm (after reconcile, or after dispose) is a **no-op** (AC3 "no duplicate application").
  - [x] **Rejection:** `catch (PartiesClientException pce)` → `revert()` → `announce(StatusPresentation.FromClientException(pce), <inline reason>)` (**`role=alert`**). For GDPR calls returning `AdminPortalGdprCommandResult`, map a non-`Accepted` `Outcome` the same way (revert + alert); `Accepted` enters the reconcile path.
  - [x] **User-cancel:** `catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* abandoned — drop silently, no revert-announce, no UI failure */ }` — this is the **call-site contract Story 1.6 deferred** (`StatusPresentation.FromException` only ever sees an *un-requested* cancellation = a timeout → `TransientFailure`). A timeout (token did **not** fire) propagates to the general catch → `StatusPresentation.FromException` → `TransientFailure` (`role=alert`).
  - [x] Reuse Story 1.6 **verbatim**: `StatusKind`, `StatusPresentation.From*`, `PolitenessFor`, and render through `StatusLiveRegion` at the call site — **never** a second mapping, **never** `aria-live="polite"` on the rejection.

### Part D — Composition / config / AppHost — AC4

- [x] **Task 8 — DI registration extension** (`src/Hexalith.Parties.UI/Services/ProjectionFreshnessServiceCollectionExtensions.cs` or fold into an existing extension) (AC4)
  - [x] `AddPartiesProjectionFreshness(this IServiceCollection, IConfiguration)` registering **Scoped**: `IProjectionStream → EventStoreSignalRProjectionStream`, `PartiesProjectionSubscription`, `IDegradedStateAccessor`, `ProjectionFreshnessFallback`, `OptimisticReconcile`. All **Scoped** (per-circuit; ADR-030). Bind the hub URL (`EventStore:SignalR:HubUrl`) and polling interval (`Parties:Freshness:PollingIntervalSeconds`) — the stream stays **inert when the hub URL is empty/whitespace** (no connect).
  - [x] Register **unconditionally** in `Program.cs` (mirroring `AddSelfScopedPartiesClient` / `AddPartiesUiClaimsResolution`), placed after `AddSelfScopedPartiesClient()`. Lazy/inert composition means it composes in test/degraded boot.

- [x] **Task 9 — `Program.cs` wiring** (`src/Hexalith.Parties.UI/Program.cs`) (AC4)
  - [x] Call `builder.Services.AddPartiesProjectionFreshness(builder.Configuration);` unconditionally (after line 53 `AddSelfScopedPartiesClient()`), with a comment matching the surrounding 1.4/1.5 registration commentary (Scoped, ADR-030, `ValidateScopes=true`, lazy/inert when unconfigured).
  - [x] In the existing `if (partiesClientEnabled) { builder.Services.AddPartiesClient(...); }` block, add the **degraded-header `DelegatingHandler`** to the gateway client (`.AddHttpMessageHandler<DegradedResponseHeaderHandler>()` against the named `HttpClient` `AddPartiesClient` configures — **verify the exact named client / builder shape** in `Hexalith.Parties.Client.Extensions`). Register `DegradedResponseHeaderHandler` (transient) so the handler factory can resolve it. Keep it inside the gate so degraded/test boot is unaffected.

- [x] **Task 10 — Config keys** (`src/Hexalith.Parties.UI/appsettings.json`) (AC4)
  - [x] Add an `EventStore` section: `{ "SignalR": { "HubUrl": "" } }` (empty default → inert; `__`-overridable as `EventStore__SignalR__HubUrl`). Add `Parties:Freshness:PollingIntervalSeconds: 30`. No secrets. Do **not** add real URLs to committed appsettings (the AppHost injects the run-mode value).

- [x] **Task 11 — AppHost wiring** (`src/Hexalith.Parties.AppHost/Program.cs`) (AC4)
  - [x] **Enable the hub for normal run mode:** today `eventstore.WithEnvironment("EventStore__SignalR__Enabled", "true")` is set **only inside** the EventStore-sample block (`Program.cs:178`). Move/duplicate so the hub is enabled whenever `parties-ui` runs (set it unconditionally on the `eventstore` resource near its definition, ~line 27-33, or guard on `parties-ui` presence). **Verify-live** the EventStore hub path/route name (`/hubs/projection-changes`, `ProjectionChangedHub.HubPath`) before composing the URL.
  - [x] **Inject the hub URL into `parties-ui`** in its block (`Program.cs:116-120`): `_ = partiesUi.WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}/hubs/projection-changes"));` (mirror the `parties-mcp` `EventStoreGatewayBaseUrl` pattern at `Program.cs:69`). Keep `.WithReference(eventStore)` / `.WaitFor(eventStore)` as-is.

### Part E — Tests — AC6 (proving AC1–AC4)

- [x] **Task 12 — Optimistic-reconcile primitive tests** (`tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs`, xUnit v3 + Shouldly + NSubstitute; `sealed`) (AC1, AC3)
  - [x] **Happy path via `Freshness=Current`:** a fake reconcile returns `Current` immediately → assert order: `applyOptimistic` ran, `announce(AcceptedProcessing, "Saving…")` emitted, `issueCommand` ran, `reconcile` ran, final `announce` is **polite** (a status kind, not an alert), `revert` **not** called.
  - [x] **Happy path via SignalR confirm:** reconcile returns non-`Current` until a fake `IProjectionStream` raises the subscribed `(projectionType, tenant)` callback → assert reconcile runs **once** on the confirm.
  - [x] **Duplicate / late confirm → no double-apply (AC3):** raise the confirm twice (and once after a simulated dispose) → `reconcile` runs **exactly once**; one-shot guard holds.
  - [x] **Rejection:** `issueCommand` throws `PartiesClientException(422, "Validation", …)` → `revert` called, final `announce` uses `StatusPresentation.FromClientException` (→ `Validation`, **assertive**); a `403` tenant problem → `TenantUnavailable` (polite); a GDPR `AdminPortalGdprCommandResult` with a non-`Accepted` `Outcome` → revert + alert.
  - [x] **User-cancel → silent drop (AC3):** `issueCommand` throws `OperationCanceledException` with `ct.IsCancellationRequested == true` → **no** `revert`-announce, **no** failure status; an `OperationCanceledException`/`TimeoutException` with the token **not** requested → `TransientFailure` (assertive).
  - [x] **Announce-not-steal:** assert the primitive never invokes any focus API — it only calls the `announce` delegate (there is no focus call to make; the test pins that the contract is announce-only).

- [x] **Task 13 — Subscription wrapper tests** (`tests/Hexalith.Parties.UI.Tests/PartiesProjectionSubscriptionTests.cs`, `sealed`) (AC2, AC3, AC4)
  - [x] Over a **fake `IProjectionStream`** (records subscribe/unsubscribe calls, exposes a settable `IsConnected`, lets the test raise the `onChanged` callback): `Subscribe` registers and `EnsureStartedAsync` is invoked once; disposing the returned handle unsubscribes; disposing the wrapper tears down all subscriptions.
  - [x] **Inert when no hub URL:** with an unconfigured/disconnected fake stream, `IsConnected` is false and `Subscribe` still registers (so a later connect auto-joins) without throwing.
  - [x] **Reconnect re-subscribes (AC3):** simulate the stream toggling `IsConnected` false→true and re-raising the callback after reconnect → the registered `onConfirmed` still fires (the wrapper relied on the client's auto-rejoin; no manual re-subscribe needed) and no duplicate registration occurs.

- [x] **Task 14 — Degraded-header + polling-fallback tests** (`tests/Hexalith.Parties.UI.Tests/DegradedResponseHeaderHandlerTests.cs`, `ProjectionFreshnessFallbackTests.cs`; `sealed`) (AC2)
  - [x] Header handler over a **fake `HttpMessageHandler`**: a GET response with `X-Service-Degraded: true` + `X-Stale-Data-Age: 45` → accessor reads `IsDegraded == true`, `StaleDataAgeSeconds == 45`, and the `StatusKind?` convenience returns `Degraded`; **absent** headers → not degraded, `StatusKind?` null.
  - [x] Fallback over a **fake `TimeProvider`** + a stub stream: while `IsConnected == false`, advancing time by the interval invokes the reconcile callback each tick; flipping `IsConnected = true` **stops** further ticks.

- [x] **Task 15 — Composition test** (`tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessCompositionTests.cs`, `sealed`) (AC4)
  - [x] Build a host/service collection via `AddPartiesProjectionFreshness` (mirror `SelfScopedPartiesClientCompositionTests`): assert `OptimisticReconcile`, `PartiesProjectionSubscription`, `IProjectionStream`, `IDegradedStateAccessor`, `ProjectionFreshnessFallback` are **Scoped**; that the provider builds under **`ValidateScopes=true`**; and that a **no-`EventStore:SignalR:HubUrl` / no-`Parties:BaseUrl`** configuration composes and resolves without connecting (lazy/inert).

- [x] **Task 16 — Build + gate verification** (AC: all)
  - [x] Per-project Release build (NOT the full `.slnx` pack — it pre-fails on PolymorphicSerializations NU5118/NU5128 + `*PackageTests`, unrelated): `dotnet build src/Hexalith.Parties.UI -c Release` then `dotnet build tests/Hexalith.Parties.UI.Tests -c Release`. If a clean parallel build flakes (CS0006/MSB4018), re-run with `-m:1` for a reliable verdict.
  - [x] Run the UI test EXE directly (xUnit v3 MTP — **not** `dotnet test --filter`, which returns "Zero tests ran"; run the EXE, filter with `-class "*OptimisticReconcile*"` / `-class "*ProjectionFreshness*"` / `-class "*ProjectionSubscription*"` / `-class "*DegradedResponseHeader*"` as needed). Confirm the **full** UI suite stays green (no regression to the 174 1.1–1.6 tests).
  - [x] Confirm `bash scripts/check-no-warning-override.sh` stays green (no `TreatWarningsAsErrors` override, no nested-submodule init).
  - [x] (Optional, if Docker present) AppHost topology smoke — but note the pre-existing baseline red (`AppHostTenantsTopologyTests`, memory `apphost-topology-test-preexisting-red`); a single pre-existing failure there is **not** a regression.

## Dev Notes

### What this story adds — the shared D6 "live freshness + optimistic-reconcile" mechanism

This is the Epic-1 **shared enabler** for eventual-consistency UX (epics.md:347, 360-363; architecture D6 + AR-StatusMap + UX-DR8). It ships **four reusable building blocks** in the UI host and proves them — **no page consumes them yet** (the Epic 2/4/5 screens, and the generated `[Command]` Fluxor lifecycle, call them later):

1. **`PartiesProjectionSubscription`** — the architecture-named D6 service (`architecture.md:572`, `646`): a per-circuit wrapper over the EventStore SignalR client that subscribes to projection-change notifications keyed by `(projectionType, tenant)`, surviving auto-reconnect.
2. **`OptimisticReconcile`** — the single shared optimistic-then-reconcile primitive (`architecture.md:479`, `656-659`): optimistic echo + polite "Saving…" → command → reconcile on SignalR-confirm **or** `Freshness=Current` → revert + `role=alert` on rejection. Slice-agnostic (delegate-driven), so every screen reuses it verbatim.
3. **Degraded fallback** — `ProjectionFreshnessFallback` (poll while disconnected) + `IDegradedStateAccessor` / `DegradedResponseHeaderHandler` (capture `X-Service-Degraded` / `X-Stale-Data-Age`).
4. **Reuse of Story 1.6** — `StatusKind` / `StatusPresentation` / `StatusLiveRegion` are consumed **verbatim**; this story is also the **call-site** that fulfils 1.6's deferred user-cancel `FromException` contract.

It is **mechanism + tests**: no page, no route, no Fluxor slice, no `[Command]`/`[Projection]` marker, no live per-circuit token capture. Same scope discipline as Stories 1.4/1.5/1.6.

### 🚦 KEY DECISION — wrap the **low-level** `EventStoreSignalRClient`, NOT the high-level `AddHexalithEventStore` (flag for the architect)

The FrontComposer Shell (already referenced by the host) exposes a **high-level** EventStore stack via `EventStoreServiceExtensions.AddHexalithEventStore(...)` — it registers `IProjectionSubscription` / `IProjectionChangeNotifier` / `IProjectionConnectionState` / `IProjectionFallbackRefreshScheduler` / `ProjectionSubscriptionService` (all Scoped) **plus** it **swaps the command/query stack** for FrontComposer's `EventStoreCommandClient` / `EventStoreQueryClient` and registers `ICommandService` / `IQueryService` (`references/Hexalith.FrontComposer/.../Extensions/EventStoreServiceExtensions.cs:33-108`).

**This story does NOT call `AddHexalithEventStore`.** Rationale (record for the architect, mirroring 1.6's placement flag):

- The architecture **pins the typed `Hexalith.Parties.Client`** (`AddPartiesClient`, `IPartiesCommandClient`/`IPartiesQueryClient`/`IAdminPortalGdprClient`/`ISelfScopedPartiesClient`) as the gateway path (`architecture.md:324-329`, `488-491`). Adopting `AddHexalithEventStore` would install a **parallel** FrontComposer command/query client the Parties screens don't use, and would have to run **last** after the Quickstart (a fragile bootstrap-ordering coupling enforced by `FrontComposerBootstrapValidator`).
- The architecture **names the low-level `Hexalith.EventStore.SignalR`** for D6 (`architecture.md:182`, `330-334`) and defines a **host-authored `UI/Services/PartiesProjectionSubscription.cs`** (`architecture.md:572`) — i.e. *the host wraps the low-level client*, it does not adopt the Fc stack wholesale.
- The named precedent — **`eventstore-admin-ui`** ("like parties-mcp / eventstore-admin-ui", `architecture.md:380`) — uses `EventStoreSignalRClient` **directly** (`AdminUIServiceExtensions.cs`), not `AddHexalithEventStore`.

So: reuse only the **transport** (`EventStoreSignalRClient`) and re-implement the thin subscription/fallback/reconcile glue host-side. **Consequence to revisit later (not this story's call):** if the team later wants the Fc connection-status badge (`FcProjectionConnectionStatus`) or `IProjectionConnectionState`, decide in an ADR whether to adopt the Fc projection-subscription half without its command/query swap (it is not cleanly separable today). This story keeps the surface minimal and typed-client-aligned.

### `EventStoreSignalRClient` — the exact transport API to wrap (implement against this verbatim)

`Hexalith.EventStore.SignalR.EventStoreSignalRClient` (`references/Hexalith.EventStore/src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`) — **`sealed`**, so wrap it behind `IProjectionStream` (Task 2) for testability:

```csharp
public EventStoreSignalRClient(EventStoreSignalRClientOptions options, ILogger<EventStoreSignalRClient>? logger = null);
public bool IsConnected { get; }                 // _connection.State == HubConnectionState.Connected
public Task StartAsync(CancellationToken ct = default);
public Task SubscribeAsync(string projectionType, string tenantId, Action onChanged);   // projectionType/tenantId MUST NOT contain ':'
public Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged); // remove one callback (ref-equality)
public Task UnsubscribeAsync(string projectionType, string tenantId);                    // remove the whole group
public ValueTask DisposeAsync();
```

- **Options** (`EventStoreSignalRClientOptions`): `required string HubUrl` (absolute), `Func<Task<string?>>? AccessTokenProvider`, `IRetryPolicy? RetryPolicy` (default `[0s,2s,10s,30s]` then permanent — consider an **infinite-retry** policy for a long-lived Blazor Server circuit), `Action<HttpConnectionOptions>? ConfigureHttpConnection` (dev TLS bypass; the sample sets `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` **and** the WebSocket `RemoteCertificateValidationCallback` in Development only).
- **Signal shape:** the hub method is **`"ProjectionChanged"`** carrying `(string projectionType, string tenantId)` — **signal-only, no payload**. The reconcile path **re-reads** the projection; this is *why* there is no duplicate-application risk (idempotent re-query), and it is the basis for the AC3 one-shot guard.
- **Reconnect (FR59):** the client wires `WithAutomaticReconnect` and `OnReconnectedAsync → JoinAllGroupsAsync()` — subscriptions survive reconnect automatically. **The wrapper adds no reconnect/dedup logic.** Auth-denied group joins are pruned (don't retry forever).
- **Hub path:** `/hubs/projection-changes` (`ProjectionChangedHub.HubPath`); server-side enablement is `EventStore:SignalR:Enabled` (the EventStore host's `AddEventStoreSignalR`).
- **Test seam precedent:** `references/Hexalith.EventStore/tests/Hexalith.EventStore.Admin.UI.Tests/TestSignalRClient.cs` wraps the real client with a dummy `HubUrl` + `NullLogger`; here we instead test against the `IProjectionStream` fake (no network at all).

### Freshness, command results & degraded headers — the exact data shapes the primitive reads

- **Freshness** (`Hexalith.Parties.Contracts.Models`): `ProjectionFreshnessStatus { Current, Stale, Rebuilding, Degraded, Unavailable, LocalOnly }`; `ProjectionFreshnessMetadata { ProjectionFreshnessStatus Status; IReadOnlyList<string> WarningCodes }` (factory `Create(status, params warningCodes)`). **`Current` = fresh** (reconcile complete); any other value → degraded. `PartyDetail.Freshness` (`PartyDetail.cs`) carries it end-to-end from `PartyDetailProjectionActor.GetDetailReadAsync()` (`Current` on a successful state-store read; `Stale`/`Rebuilding`/`Unavailable` otherwise). **This is the primary degraded signal** — `StatusPresentation.FromFreshness(metadata)` already maps it (`Current → null`, else `Degraded`).
- **Command acceptance:** `IPartiesCommandClient` exposes paired methods — `*Async` (returns `string` correlationId) and `*WithResultAsync` (returns `PartiesCommandResult<PartyDetail>(string CorrelationId, PartyDetail? Payload)`). A non-throwing return = accepted (200/202 → `StatusKind.AcceptedProcessing`); a thrown `PartiesClientException(int Status, string? Title, string? Detail, …)` carries the HTTP/problem+json status the primitive maps via `StatusPresentation.FromClientException`.
- **GDPR:** `IAdminPortalGdprClient` methods return `AdminPortalGdprCommandResult(AdminPortalGdprOutcome Outcome, string? CorrelationId, string? Detail)`. `Outcome == Accepted` → reconcile path; any other (`ValidationRejected`/`Forbidden`/`ErasureInProgress`/…) → revert + alert. (`ISelfScopedPartiesClient` already wraps these for the consumer self-scope, Story 1.5.)
- **Degraded headers:** `DegradedResponseMiddleware` (`src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs:54-78`) emits **`X-Service-Degraded: "true"`** + **`X-Stale-Data-Age: "<seconds>"`** on **GET** responses while the Parties host is partially degraded (state-store unavailable / pub-sub degraded / projection-actors degraded), stripping them on `>=500`. The typed `Hexalith.Parties.Client` does **not** currently parse these into a model → Task 5's `DelegatingHandler` captures them. **Open verify-live:** these are set on the **Parties host** responses; confirm the **EventStore gateway** relays them through to the UI host's client. If not relayed, leave the handler as a captured-when-present building block and rely on `ProjectionFreshnessMetadata`.

### ⚠️ Blazor-Server token trap (why the live token capture is deferred)

In a live Interactive Server **circuit**, `HttpContext` is **null** (it exists only during the initial HTTP request). The SignalR connection is started during the circuit (interactive), so an `AccessTokenProvider` that calls `HttpContext.GetTokenAsync(...)` from inside the connect/reconnect callback will fail. The correct pattern (FrontComposer / Tenants do this) is to **capture the server-side OIDC access token at circuit start** (from the auth ticket / a server-side token store) and feed it to the provider. That live capture is the **deferred token-relay residual** Story 1.5 flagged ("lands with the first consumer screen that fetches data, Epic 4 / Story 1.7", `Program.cs:56-61`). **This story builds the seam** (a pluggable `AccessTokenProvider`/provider abstraction, defaulting to "token if available, else null") and documents the trap; the live per-circuit capture is finalized with the first authenticated data screen. `SelfScopedPartiesClient` already models the circuit-correct principal source (`AuthenticationStateProvider`, **not** `IHttpContextAccessor`) — follow that precedent when the live capture lands.

### Lifetime — Scoped per-circuit (NOT Singleton), and why

Register all new services **Scoped** (per Blazor circuit; ADR-030, `ValidateScopes=true`). Although `eventstore-admin-ui` registers `EventStoreSignalRClient` as a **Singleton** (+ `IHostedService` start), that is a **single-tenant** admin tool. The Parties UI is a **multi-tenant** BFF: each circuit has its **own tenant claim + user token**, so a process-wide Singleton connection would share one tenant/token across all users — wrong. FrontComposer's own `ProjectionSubscriptionService` is **Scoped** for exactly this reason. So: a Scoped `PartiesProjectionSubscription` owns a per-circuit `EventStoreSignalRProjectionStream` (→ one `EventStoreSignalRClient` per circuit), **lazily started** on first `Subscribe` and **`IAsyncDisposable`** on circuit teardown. `ValidateScopes=true` (Program.cs:15) will fail the boot if a Singleton captures any of these — keep them out of singletons.

### Reinvention prevention — do NOT rebuild what already exists

| Temptation | Reuse instead |
|---|---|
| A second `StatusKind` / status mapper / aria-live logic | **Story 1.6** `Hexalith.Parties.UI.Status.{StatusKind,StatusPresentation,LiveRegionPoliteness}` + `Components/Shared/StatusLiveRegion.razor` — **verbatim** (`StatusPresentation.cs`). The architecture forbids a per-screen remap (`architecture.md:460`, `539-541`). |
| A hand-rolled `HubConnection` + reconnect + group rejoin | **`EventStoreSignalRClient`** (`Hexalith.EventStore.SignalR`) — it already does `WithAutomaticReconnect` + auto-rejoin (FR59). Wrap it; don't reimplement. |
| Adopting FrontComposer's `AddHexalithEventStore` for the subscription | Rejected — it swaps the typed `Parties.Client` (see KEY DECISION). Use only the low-level transport. |
| A bespoke per-screen polling loop | One shared `ProjectionFreshnessFallback` (`architecture.md:482` "No bespoke per-screen polling"). |
| A new freshness model | `ProjectionFreshnessMetadata` / `ProjectionFreshnessStatus` (Contracts) — already carried on `PartyDetail.Freshness`. |
| A Fluxor `[EffectMethod]` now | There are **zero** `[Command]`/`[Projection]` markers/slices yet (HFC1001 suppressed, csproj:8-15). The generated 5-state command lifecycle (Submitted→Acknowledged→Syncing→Confirmed/Rejected) arrives with the first `[Command]` (Story 1.8 / Epic 2+). Build the **slice-agnostic delegate-driven primitive** now; the future generated/hand-authored effects **call** it. |

### Established patterns you MUST follow (from 1.1–1.6)

- **⚠️ Brace style is per-project:** the **UI host (`src/Hexalith.Parties.UI/…`) is Allman / next-line braces** (see `SelfScopedPartiesClient.cs`, `StatusPresentation.cs`). All new `Services/` files MUST match. File-scoped namespaces; `using`s **outside** the namespace, `System.*` first; **no unused `using`** (build error under `TreatWarningsAsErrors`); nullable enabled (don't silence with `!`); interfaces `I*`, private fields `_camelCase`, async methods `Async`-suffixed.
- **Central Package Management:** **no `Version=` in any csproj; no `Directory.Packages.props` edit needed** — SignalR.Client flows transitively (Task 1). If you find a type genuinely missing at compile, prefer fixing the project reference over adding a `PackageReference`.
- **Composition mirrors 1.4/1.5:** register Scoped, **unconditionally**, lazily/inertly; gate only the gateway-`HttpClient` handler on `Parties:BaseUrl`. Match the existing `Program.cs` comment density (the 1.3/1.4/1.5 blocks).
- **`InternalsVisibleTo("Hexalith.Parties.UI.Tests")`** is already in the host csproj (Story 1.5) — `internal` adapters/handlers are visible to the test project; keep the **contract** types (`IProjectionStream`, `IDegradedStateAccessor`, `OptimisticReconcile`, `PartiesProjectionSubscription`) `public` (they are the cross-tier seam).
- **PII hygiene (pinned):** never log `projectionType`/`tenant`/party/correlation values from any new service; logs carry only coarse connection-state. `[PersonalData]` on `PartyDetail.DisplayName`/`SortName`/`NameHistory` — never echo them in announcements (the announce delegate receives a status + a non-PII reason).
- **xUnit v3 + Shouldly + NSubstitute + bUnit**; classes `sealed`; descriptive sentence method names; `value.ShouldBe(...)`. No Moq/FluentAssertions/raw `Assert.*`. Model bUnit setup (if any DOM) on `RoleLandingRedirectTests`/`StatusLiveRegionTests`; model composition tests on `SelfScopedPartiesClientCompositionTests`. Use `TimeProvider` (fake) for the fallback timer.
- **Don't build the whole `.slnx`** to judge yourself (full Release `pack` pre-fails on PolymorphicSerializations NU5118/NU5128 + `*PackageTests`, unrelated). Verify per-project + the UI test EXE. Clean parallel builds can flake (CS0006/MSB4018) — re-run `-m:1`. `dotnet test --filter` returns "Zero tests ran" (xUnit v3 MTP) — run the test **EXE** with `-class`.

### Source tree — files to create / touch

| Action | File |
|---|---|
| NEW | `src/Hexalith.Parties.UI/Services/IProjectionStream.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/EventStoreSignalRProjectionStream.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/PartiesProjectionSubscription.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs` (+ `OptimisticReconcileRequest`) |
| NEW | `src/Hexalith.Parties.UI/Services/ProjectionFreshnessFallback.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/DegradedStateAccessor.cs` (`IDegradedStateAccessor` + impl) |
| NEW | `src/Hexalith.Parties.UI/Services/DegradedResponseHeaderHandler.cs` |
| NEW | `src/Hexalith.Parties.UI/Services/ProjectionFreshnessServiceCollectionExtensions.cs` (`AddPartiesProjectionFreshness`) |
| EDIT | `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` (add `Hexalith.EventStore.SignalR` ProjectReference) |
| EDIT | `src/Hexalith.Parties.UI/Program.cs` (register freshness services + degraded handler on the gateway client) |
| EDIT | `src/Hexalith.Parties.UI/appsettings.json` (add `EventStore:SignalR:HubUrl` + `Parties:Freshness:PollingIntervalSeconds`) |
| EDIT | `src/Hexalith.Parties.AppHost/Program.cs` (enable hub for run mode + inject `EventStore__SignalR__HubUrl` into parties-ui) |
| NEW | `tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/PartiesProjectionSubscriptionTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/DegradedResponseHeaderHandlerTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessFallbackTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessCompositionTests.cs` |

(File names are suggestions; keep them descriptive and grouped under `Services/`. You may co-locate `OptimisticReconcileRequest` in `OptimisticReconcile.cs`.)

### Project Structure Notes

- **Placement:** all new code lives in the **UI host** `Services/` (architecture.md:567-572 reserves `UI/Services/` for `PartiesProjectionSubscription` and cross-area services; the optimistic-reconcile primitive is host-owned shared logic both areas reuse). Reuses `Hexalith.Parties.UI.Status` (host, Story 1.6) and `Hexalith.Parties.Contracts.Models` (freshness) — no new project, no RCL. No structural variance from the architecture.
- **Scope boundary — do NOT over-build:** no page/route/`@page`; no Fluxor slice / `[Command]` / `[Projection]` marker (HFC1001 stays suppressed); no live per-circuit token capture (seam only); **no** `AddHexalithEventStore` adoption; **no** refactor of legacy AdminPortal/Picker remaps; **no** CSS/tokens/icons (1.8/1.9); **no** new package version in `Directory.Packages.props`. Deliverable = the **subscription wrapper + optimistic-reconcile primitive + degraded fallback/header seam + their wiring + tests**.
- **Open questions (decided for this story; flag for the architect):** (1) low-level vs high-level EventStore seam — decided low-level (see KEY DECISION); (2) the degraded-header **relay** through the EventStore gateway is **verify-live** (primary signal remains `ProjectionFreshnessMetadata`); (3) per-circuit Scoped lifetime vs Singleton — decided Scoped (multi-tenant + ADR-030); (4) the **infinite-retry** `IRetryPolicy` for a long-lived circuit — recommended, confirm against the EventStore client default before shipping.

### References

- [Source: epics.md#Story 1.7 — Live freshness via SignalR + shared optimistic-reconcile effect / lines 547-563] — user story + the 3 ACs (optimistic + polite "Saving…" → SignalR projection-confirm / `Freshness=Current` reconcile; revert + `role=alert`; degraded → poll + freshness metadata + `X-Service-Degraded`/`X-Stale-Data-Age`; announce-not-steal; reconnect re-subscribes without duplicate application).
- [Source: epics.md#AR-D6 Live freshness / lines 181-183] and [#AR-StatusMap / 203-207] and [#UX-DR8 / 290-292] — SignalR subscription (`Hexalith.EventStore.SignalR` + `SignalR.Client` 10.0.8); polling/freshness fallback when degraded; the canonical StatusKind map + politeness split reused.
- [Source: epics.md#Epic 1 intro / lines 347, 360-363] — "shared enablers (… SignalR freshness) established once in Epic 1".
- [Source: architecture.md#API & Communication Patterns / lines 330-334] — D6: subscribe to EventStore projection updates via SignalR (FrontComposer Shell pattern); polling/freshness fallback when degraded; surface `X-Service-Degraded` / `X-Stale-Data-Age`.
- [Source: architecture.md#Communication Patterns / lines 477-484] — optimistic-then-reconcile is **one shared effect pattern, not per-screen** (reconcile on SignalR projection-confirm or `Freshness=Current`; on rejection revert + inline reason; **do not steal focus** to a toast — announce via aria-live); "No bespoke per-screen polling."
- [Source: architecture.md#Integration Points & Data Flow / lines 651-659] — command data flow: optimistic + `aria-live=polite` "Saving…" → command via (self-scoped) client → projection updates → **SignalR confirm** → reconcile → freshness `Current`; rejection → revert + `role="alert"`.
- [Source: architecture.md#Project Structure / lines 567-572, 608, 646] — `UI/Services/PartiesProjectionSubscription.cs` (D6 SignalR subscription + reconcile dispatch); `Live freshness (D6) → UI/Services/PartiesProjectionSubscription + per-slice Effects`; UI.Tests is the home of these tests.
- [Source: architecture.md#Infrastructure & Deployment / lines 377-383] and [#Decision Impact / 396, 405] — parties-ui is a BFF over HTTP + SignalR, **no DAPR sidecar**, like `parties-mcp`/`eventstore-admin-ui`; D6 underpins reconcile + async export-ready signaling.
- [Source: references/Hexalith.EventStore/src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs] — the transport to wrap: `IsConnected`, `StartAsync`, `SubscribeAsync(projectionType, tenantId, Action)` / `UnsubscribeAsync(...)`, `DisposeAsync`; `WithAutomaticReconnect` + `OnReconnectedAsync → JoinAllGroupsAsync` (FR59); signal-only `"ProjectionChanged"(projectionType, tenantId)`. [Source: …/EventStoreSignalRClientOptions.cs] — `HubUrl`/`AccessTokenProvider`/`RetryPolicy`/`ConfigureHttpConnection`. [Source: …/Hexalith.EventStore.SignalR.csproj] — refs `Microsoft.AspNetCore.SignalR.Client` (10.0.8 in the EventStore submodule CPM).
- [Source: references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:76-96,184-193] — the precedent wiring (options→client registration, `StartSignalRAsync` startup, `EventStore:SignalR:HubUrl` config key, Aspire `https+http://eventstore/hubs/projection-changes`). [Source: .../Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs:29] — 30 s `PeriodicTimer` polling-fallback precedent. [Source: .../tests/Hexalith.EventStore.Admin.UI.Tests/TestSignalRClient.cs] — client test-seam precedent.
- [Source: references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/EventStoreServiceExtensions.cs:33-108] — the high-level `AddHexalithEventStore` (`IProjectionSubscription`/`IProjectionChangeNotifier`/`IProjectionConnectionState`/`ProjectionSubscriptionService`, Scoped) that **also swaps** command/query clients — the considered-and-rejected alternative. [Source: .../Hexalith.FrontComposer.Shell.csproj] — Shell references `Microsoft.AspNetCore.SignalR.Client` + `Fluxor.Blazor.Web` (transitively available to the host).
- [Source: src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs] — `Current/Stale/Rebuilding/Degraded/Unavailable/LocalOnly` (only `Current` = fresh). [Source: .../ProjectionFreshnessMetadata.cs] — `Status` + `WarningCodes` + `Create(...)`. [Source: .../PartyDetail.cs] — `ProjectionFreshnessMetadata? Freshness` carried end-to-end.
- [Source: src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs · PartiesCommandResult.cs · IPartiesQueryClient.cs] — command pairs (`*Async`/`*WithResultAsync`), `PartiesCommandResult<TPayload>(CorrelationId, Payload?)`, `GetPartyAsync`. [Source: .../AdminPortal/IAdminPortalGdprClient.cs · AdminPortalGdprCommandResult.cs · AdminPortalGdprOutcome.cs] — GDPR outcomes (`Accepted` → reconcile; others → revert+alert). [Source: .../PartiesClientException.cs] — `Status`/`Title`/`Detail`/`CorrelationId` (the rejection input).
- [Source: src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs:54-78] — produces `X-Service-Degraded` / `X-Stale-Data-Age` on GET responses while partially degraded (stripped on `>=500`).
- [Source: src/Hexalith.Parties.UI/Status/StatusPresentation.cs] — reuse verbatim: `FromHttpStatus`/`FromClientException`/`FromFreshness`/`FromException`/`PolitenessFor`/`LiveRegionAttributes`. [Source: src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor] — the polite/assertive live-region primitive the call site renders.
- [Source: src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs] — Scoped/fail-closed/circuit-principal precedent (`AuthenticationStateProvider`, not `IHttpContextAccessor`); the self-scoped client the consumer reconcile path calls. [Source: src/Hexalith.Parties.UI/Program.cs:24,45-67] — unconditional Scoped registration pattern; the `Parties:BaseUrl` gate; the access-token-provider/token-relay deferral note (lines 56-61).
- [Source: src/Hexalith.Parties.AppHost/Program.cs:27-33,69,112-120,178] — `eventstore` resource; `parties-mcp` `WithEnvironment(... ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}"))` pattern; the `parties-ui` block; `EventStore__SignalR__Enabled` currently set only in the sample block. [Source: Directory.Build.props:4-5] — `$(HexalithEventStoreRoot)` resolution.
- [Source: _bmad-output/implementation-artifacts/1-6-canonical-statuskind-ui-mapping-with-aria-live-politeness-split.md] — the StatusKind map this story consumes; the **user-cancel `FromException` call-site contract this story fulfils** (1.6 Dev Notes "Timeout vs cancellation"); per-project-build / test-EXE / Allman-brace / scope-discipline idioms.
- [Source: _bmad-output/implementation-artifacts/1-5-consumer-own-data-self-authorization-defense-in-depth.md] — the self-scope accessor (`ISelfScopedPartiesClient`) + the deferred OIDC→gateway token relay ("Epic 4 / Story 1.7") + composition-test idiom.
- [Source: _bmad-output/project-context.md] — CPM (no `Version=`); `TreatWarningsAsErrors`; file-scoped namespaces / `using` order / no-unused-using; PII hygiene; xUnit v3 / Shouldly / NSubstitute / bUnit; "put code where it belongs"; sibling-submodule project refs (never NuGet); subscribers idempotent / additive-only.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (claude-opus-4-8) — BMAD dev-story workflow.

### Debug Log References

- Per-project Release builds (NOT the full `.slnx` pack): `dotnet build src/Hexalith.Parties.UI -c Release -m:1`, `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`, `dotnet build src/Hexalith.Parties.AppHost -c Release -m:1` — all `0 Warning(s) 0 Error(s)`.
- UI test EXE (xUnit v3 MTP, run directly — `dotnet test --filter` returns "Zero tests ran"): **full suite 199 total, 0 failed** (174 prior 1.1–1.6 + 25 new — no regression). New classes (`-class` filter): 25/25 pass.
- `bash scripts/check-no-warning-override.sh` → green (no `TreatWarningsAsErrors` override, no nested-submodule init).
- **Two `TreatWarningsAsErrors` gotchas hit & fixed:** (1) `await using` on `ITimer`/`CancellationTokenRegistration`/`CancellationTokenRegistration` trips **CA2007** — used synchronous `using` (those disposables expose `Dispose()`); (2) in `[Fact]` methods, awaiting a `Task` local (`await run;`) trips CA2007 while `ConfigureAwait(false)` trips **xUnit1030** — used `ConfigureAwait(true)` (satisfies both). Composition test: a scope holding `IAsyncDisposable`-only services must be disposed via `CreateAsyncScope()` + `await DisposeAsync()` (sync `IServiceScope.Dispose()` throws).

### Completion Notes List

Shipped the shared **D6 live-freshness + optimistic-reconcile mechanism** (mechanism + tests only — **no page consumes it yet**, same scope discipline as 1.4/1.5/1.6):

- **`OptimisticReconcile`** (AC1/AC3) — the single slice-agnostic, delegate-driven primitive: apply optimistic → polite `AcceptedProcessing` "Saving…" (no focus steal) → issue command → reconcile on the **first** of {SignalR projection-confirm, a re-read returning `Current`} (polling owns the re-read while disconnected) → polite reconciled announce; rejection (`PartiesClientException` **or** a non-`Accepted` `CommandAcceptance` "validation outcome", e.g. GDPR) → revert + mapped inline reason (kind drives `role=alert`); **one-shot guard** (Interlocked CAS) makes duplicate/late confirm a no-op; **user-cancel** (`ct.IsCancellationRequested`) dropped silently — fulfils the Story 1.6 deferred `FromException` user-cancel contract (an un-requested OCE/timeout → `TransientFailure`). Reuses Story 1.6 `StatusKind`/`StatusPresentation`/`StatusLiveRegion` **verbatim** — no second mapping.
- **`PartiesProjectionSubscription`** (AC2/AC3/AC4) — the architecture-named D6 wrapper over the `IProjectionStream` seam; Blazor-friendly `IDisposable Subscribe(...)`; reconnect/auto-rejoin delegated to the transport (no manual re-subscribe/dedup); disposes all subscriptions on teardown.
- **`EventStoreSignalRProjectionStream`** — production adapter owning one per-circuit `EventStoreSignalRClient`; **inert when no hub URL** (never connects, `IsConnected=false`); infinite-retry policy for the long-lived circuit (open-Q4). Wrapped behind `IProjectionStream` because the concrete client is `sealed` (testable without a live hub).
- **Degraded fallback** — `ProjectionFreshnessFallback` (one shared `TimeProvider`-driven poll loop, default 30 s; no bespoke per-screen polling) + `IDegradedStateAccessor`/`DegradedResponseHeaderHandler` (capture `X-Service-Degraded`/`X-Stale-Data-Age` on GET responses → canonical `StatusKind.Degraded`).
- **Wiring** — `AddPartiesProjectionFreshness` registers everything **Scoped** (ADR-030; boots green under `ValidateScopes=true`), called **unconditionally** in `Program.cs`; the degraded handler is gated inside the `Parties:BaseUrl` block on the typed clients (`nameof(IPartiesQueryClient)`/`nameof(IAdminPortalGdprClient)`). `appsettings.json` carries empty-default `EventStore:SignalR:HubUrl` + `Parties:Freshness:PollingIntervalSeconds:30` (no secrets). AppHost enables the hub unconditionally and injects `EventStore__SignalR__HubUrl = {eventstore http}/hubs/projection-changes` into `parties-ui`.

**Verify-live findings (documented, not blocking — per the story):**
1. **`EventStoreSignalRClient`/SignalR.Client types resolve** in the host purely via the transitive `Hexalith.EventStore.SignalR` project ref — **no** explicit `PackageReference`, **no** `Directory.Packages.props` edit (confirmed by clean compile). Hub path `/hubs/projection-changes` confirmed against `references/Hexalith.EventStore/src/Hexalith.EventStore/Program.cs:54` (`MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath)`); server enable key is `EventStore:SignalR:Enabled` (`AddEventStoreSignalR`).
2. **Degraded-header relay** — the headers are produced on the **Parties host** GET responses; whether the EventStore **gateway** relays them through to this UI host's typed-client responses could not be confirmed without a live run. The handler is a captured-when-present building block; **`ProjectionFreshnessMetadata` on `PartyDetail.Freshness` remains the primary degraded signal** (already mapped by `StatusPresentation.FromFreshness`).
3. **Handler-scope ≠ circuit-scope caveat** — `DegradedResponseHeaderHandler` is resolved within the `IHttpClientFactory`'s pooled handler scope, **not** the Blazor circuit scope, so the `IDegradedStateAccessor` it writes is a different instance than the circuit's. This is acceptable for this story (the seam + its unit test are the deliverable, and the primary signal is freshness-metadata); the live per-circuit bridge lands with the deferred OIDC token-capture on the first authenticated data screen (Epic 2/4) — same residual Story 1.5 flagged. No boot failure under `ValidateScopes=true` (the handler resolves its Scoped dep inside the factory scope, lazily, never at boot).

**Design note flagged for the architect:** the `IssueCommand` delegate returns `Task<CommandAcceptance>` (not `Task`) so the primitive can observe a **non-throwing** "validation outcome" (AC1(d)) — e.g. a GDPR `AdminPortalGdprCommandResult` whose `Outcome != Accepted` — without the primitive referencing any screen's result type (stays slice-agnostic; the call site maps its own result into `CommandAcceptance`).

### File List

**New (source — `src/Hexalith.Parties.UI/Services/`):**
- `IProjectionStream.cs`
- `ProjectionFreshnessOptions.cs`
- `IProjectionAccessTokenProvider.cs` (`IProjectionAccessTokenProvider` + `NullProjectionAccessTokenProvider`)
- `EventStoreSignalRProjectionStream.cs`
- `PartiesProjectionSubscription.cs`
- `DegradedStateAccessor.cs` (`IDegradedStateAccessor` + impl)
- `DegradedResponseHeaderHandler.cs`
- `ProjectionFreshnessFallback.cs`
- `OptimisticReconcile.cs` (`CommandAcceptance` + `OptimisticReconcileRequest` + `OptimisticReconcile`)
- `ProjectionFreshnessServiceCollectionExtensions.cs` (`AddPartiesProjectionFreshness`)

**Modified (source):**
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj` (add `Hexalith.EventStore.SignalR` ProjectReference)
- `src/Hexalith.Parties.UI/Program.cs` (register freshness services unconditionally + degraded handler on the gateway clients)
- `src/Hexalith.Parties.UI/appsettings.json` (add `EventStore:SignalR:HubUrl` + `Parties:Freshness:PollingIntervalSeconds`)
- `src/Hexalith.Parties.AppHost/Program.cs` (enable hub unconditionally + inject `EventStore__SignalR__HubUrl` into `parties-ui`)

**New (tests — `tests/Hexalith.Parties.UI.Tests/`):**
- `FakeProjectionStream.cs` (test double)
- `ManualTimeProvider.cs` (hand-rolled fake `TimeProvider` — CPM carries no `TimeProvider.Testing`, and the story adds no package)
- `OptimisticReconcileTests.cs`
- `PartiesProjectionSubscriptionTests.cs`
- `DegradedResponseHeaderHandlerTests.cs`
- `ProjectionFreshnessFallbackTests.cs`
- `ProjectionFreshnessCompositionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-06-10 | 1.0 | Implemented Story 1.7 — shared D6 live-freshness + optimistic-reconcile mechanism (8 new `Services/` types + wiring + AppHost/appsettings) and 5 test files (25 new tests). Full UI suite 199/199 green; build-gate green; AppHost builds. Status → review. | Claude Opus 4.8 (dev-story) |
| 2026-06-10 | 1.1 | Adversarial senior-developer review (auto-fix mode). All 6 ACs verified IMPLEMENTED against source; File List matches git exactly (no discrepancies); 0 CRITICAL / 0 HIGH. Independently reproduced gates: UI test EXE **208/208 green**, Release build 0W/0E, warning-override gate OK, CPM untouched, AC5 purity holds. 1 MEDIUM + 2 LOW findings are all explicitly-deferred-by-design or test-pinned (no in-scope auto-fix). Status → done. | Claude Opus 4.8 (review) |

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-10 · **Mode:** adversarial / auto-fix · **Outcome:** ✅ Approve

### Verdict

Validated every story claim against the actual source — not the Dev Agent Record prose. **All 6 acceptance criteria are genuinely IMPLEMENTED**, every `[x]` task has real code/test evidence, and the **File List matches `git status` exactly** (10 new `Services/` types, 4 edits, 7 test files — no undocumented changes, no phantom claims). Independently reproduced gates rather than trusting the summary:

- **UI test EXE (xUnit v3 MTP):** `208 total, 0 failed, 0 skipped` — matches the QA summary exactly.
- **Release build** (`tests/Hexalith.Parties.UI.Tests -c Release -m:1`): `0 Warning(s) 0 Error(s)`.
- **`scripts/check-no-warning-override.sh`:** green.
- **AC5 purity:** no `@page`/`[Command]`/`[Projection]`/`AddHexalithEventStore`; `Directory.Packages.props` untouched (SignalR.Client flows transitively, confirmed by clean compile).
- **AppHost wiring:** the injected `{eventStore.GetEndpoint("http")}/hubs/projection-changes` is identical to the established sample-block precedent (`AppHost/Program.cs:211`); `EventStore__SignalR__Enabled` correctly hoisted unconditional.

**0 CRITICAL · 0 HIGH.** The code is genuinely high quality: the one-shot reconcile guard (`Interlocked.CompareExchange`), the user-cancel-vs-timeout split, the slice-agnostic delegate design, and the `TimeProvider`-driven fallback are all correct and properly tested with real assertions (ordering, politeness via `StatusPresentation.PolitenessFor`, exact call counts — no placeholder tests).

### Findings (1 MEDIUM, 2 LOW — none block automation; all design-deferred or test-pinned)

- 🟡 **[MED][AI-Review] Degraded-header seam is functionally inert in a live circuit (handler-scope ≠ circuit-scope)** — `DegradedResponseHeaderHandler` (Transient, on the `IHttpClientFactory` handler chain) writes to an `IDegradedStateAccessor` resolved in the factory's pooled handler scope, a *different* instance than the per-circuit Scoped accessor a screen reads (`Program.cs:84-86`; `DegradedResponseHeaderHandler.cs:24,57`). So the captured `X-Service-Degraded`/`X-Stale-Data-Age` never reaches the UI today. **Not auto-fixed:** the dev disclosed this honestly (Completion Notes #3) and the story scoped the seam as a "captured-when-present building block + its unit test" with `ProjectionFreshnessMetadata` as the primary signal; the real bridge depends on the deferred per-circuit OIDC token-capture infra (Epic 2/4). Fixing now would expand scope and override a documented decision. → carry into the first authenticated data screen.

- 🟢 **[LOW][AI-Review] Reconciled-success announced as `StatusKind.AcceptedProcessing` (null message)** (`OptimisticReconcile.cs:181`) — the canonical 9-state vocabulary has no terminal "Confirmed/Success" state and the story forbids inventing one, so the reconcile-complete announce reuses the in-flight kind with a null message (effectively "clear to idle"). Defensible under the constraint; flag for the architect when a real screen renders the reconciled state.

- 🟢 **[LOW][AI-Review] Connected path has no immediate reconcile re-read; relies solely on the SignalR confirm** (`OptimisticReconcile.cs:200-213`) — a very fast backend whose projection-change signal lands in the narrow window before the fire-and-forget group subscription is active could be missed, leaving the connected path awaiting the next signal. This is an **intentional, test-pinned** choice (`HappyPath_ViaSignalRConfirm` asserts 0 reconcile calls before the confirm) within a defensible reading of AC1(c). Note for the architect, not a defect.

### Action items

None requiring code change in this story. The MEDIUM finding is a tracked residual to resolve with the deferred per-circuit token-capture work (Epic 2/4); the two LOW items are architect notes for the first consuming screen. No CRITICAL/HIGH issues → **Status: done**.
