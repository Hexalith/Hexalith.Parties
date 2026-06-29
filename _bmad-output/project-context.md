---
project_name: parties
user_name: Administrator
date: 2026-06-21
sections_completed:
  ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'quality_rules', 'workflow_rules', 'anti_patterns']
status: 'complete'
rule_count: 58
optimized_for_llm: true
existing_patterns_found: 14
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

**Runtime & build (pinned — do not bump casually):**
- C# / **.NET 10** — `net10.0` TFM, SDK `10.0.301` pinned in `global.json` (`rollForward: latestPatch`).
  `global.json` also sets `test.runner: Microsoft.Testing.Platform` (MTP) — tests run under MTP, not the
  classic VSTest host (affects how the test EXEs are invoked).
- **Central Package Management is ON.** All versions live in `Directory.Packages.props`. Project
  `.csproj` files use `<PackageReference Include="..." />` with **NO `Version` attribute** — adding a
  `Version=` to a csproj is a build error. Add/upgrade versions in `Directory.Packages.props` only.
- The solution is `Hexalith.Parties.slnx` — the **XML solution format**, not a classic `.sln`.

**Core stack:**
| Concern | Package | Version |
|---|---|---|
| Orchestration | .NET Aspire (packages) / AppHost SDK | `13.4.6` / SDK `13.4.6` (matched — AppHost SDK pin must equal `Aspire.Hosting`; Keycloak·K8s hosting on `13.4.6-preview.1.26319.6`) |
| Actors & pub/sub | `Dapr.Client` / `.AspNetCore` / `.Actors` / `.Actors.AspNetCore` | **`1.18.4` — all four unified** |
| Validation | FluentValidation (+ DI extensions) | `12.1.1` |
| Mediation | MediatR | `14.1.0` |
| AuthN | Microsoft.AspNetCore.Authentication.JwtBearer | `10.0.9` |
| Token model | Microsoft.IdentityModel.Tokens | `8.19.1` (pinned — see constraints) |
| MCP | ModelContextProtocol / .AspNetCore | `1.4.0` / `1.4.0` |
| UI | FluentUI Blazor / CustomElements | `5.0.0-rc.3-26138.1` / `10.0.9` |
| Serialization | MessagePack | `3.1.7` |
| Testing | xUnit **v3** / Shouldly / NSubstitute / bunit / Testcontainers / YamlDotNet | `3.2.2` / `4.3.0` / `6.0.0-rc.1` / `2.8.4-preview` / `4.12.0` / `18.0.0` |

**Constraints agents must respect:**
- **`references/Hexalith.EventStore` and `references/Hexalith.Tenants` are submodules referenced by PROJECT path**,
  not NuGet packages — and are *not* checked out in a fresh clone. Initialise root-repository submodules only:
  `git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants`. **Never `--recursive`** (the build
  gate forbids nested-submodule init). `Hexalith.Memories` is optional (rich search).
- **`Microsoft.IdentityModel.Tokens` is pinned to `8.19.1`** to align with `Hexalith.EventStore` (which
  pins `8.19.0`); JwtBearer's transitive `8.x` otherwise conflicts with `EventStore.dll` (MSB3277). The four
  `Dapr.*` packages are now **unified at one version** (`1.18.4`) — keep them in lockstep; they are no longer
  intentionally mixed. Don't bump either independently.
- Versioning is **MinVer from git tags** (prefix `v`, MinVer `8.0.0-rc.1`); never hand-edit `<Version>` in a csproj.

## Critical Implementation Rules

### Language-Specific Rules (C#)

- **`TreatWarningsAsErrors=true` is solution-wide and effectively absolute for Parties code.** Every
  compiler/analyzer warning breaks the build. The only `WarningsNotAsErrors` exemptions live in
  `Directory.Build.targets` and are scoped to FrontComposer submodule projects — never this repo's code.
  - **Never** disable it globally or on the command line (`-p:TreatWarningsAsErrors=false`). The build gate
    `scripts/check-no-warning-override.sh` fails CI if you do.
  - The sanctioned escape valve is a **narrow `<NoWarn>RuleId</NoWarn>` in the specific `.csproj`** with a
    linked issue — one rule, one project. See `docs/build-gate.md`.
- **File-scoped namespaces only** (`namespace Foo;`). `using` directives go **outside** the namespace,
  `System.*` sorted first. (`.editorconfig`, enforced as warning → error.)
- **Nullable** and **ImplicitUsings** are enabled — don't add redundant `using`s; respect nullability
  rather than silencing it with the `!` null-forgiving operator.
- **Naming is enforced, not just suggested:** interfaces `I*`, private fields `_camelCase`, async methods
  end with `Async`. Violations are warnings → build errors.
- **Contracts are immutable `sealed record`s** — every command, event, and value object in
  `Hexalith.Parties.Contracts`. Prefer `record` with init-only / `required` members over classes.
- Line endings are **CRLF**, 4-space indent, final newline, UTF-8 (`.editorconfig`, `root = true`).

### Framework-Specific Rules (Event Sourcing + CQRS + DAPR behind EventStore)

**Gateway boundary (most important):**
- The `parties` host has **no public API**. Public traffic enters the **Hexalith.EventStore gateway**
  (`POST /api/v1/commands`, `POST /api/v1/queries`, `Domain="party"`); EventStore invokes the host over
  DAPR at **`POST /process`**. Do **not** add public controllers / minimal-API endpoints to the actor host,
  and don't call the host directly from consumers — use `IPartiesCommandClient` / `IPartiesQueryClient`.
- DAPR access control is **deny-by-default**: only `eventstore → POST /process` is permitted.

**Write side (commands):**
- One discriminated aggregate: **`Party` with `PartyType` (Person | Organization)** — never a class
  hierarchy/subtype per kind.
- Command handlers are **static `Handle(command, state)` methods** in
  `Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`. Follow the existing **guard cascade**:
  null → id validity → **idempotency no-op (`DomainResult.NoOp`)** → type/required checks → emit events.
  Return a `DomainResult` of `IEventPayload`s (or rejection events) — handlers never mutate state directly.
- **Validation runs before the handler**: add a FluentValidation validator in `Hexalith.Parties/Validation/`;
  failure emits `PartyCommandValidationRejected`, not an exception.

**Event sourcing:**
- State is rehydrated by **replaying events through `PartyState.Apply(...)`**. To add an event: create the
  record in `Contracts/Events/` implementing `IEventPayload`, add an `Apply(event, state)` overload to
  `PartyState`, then update projection `Handlers/`.
- **Rejection events are persisted and replayed too** — `PartyState` declares **no-op `Apply` overloads**
  for them, ordered *before* the success Applies (suffix-match rehydration). Preserve that ordering.
- **Type resolution is allowlisted to the Contracts assembly** — never `Type.GetType(...)` on wire input;
  ambiguous short names must fail closed.

**Read side (projections / CQRS):**
- Projections **replay from sequence zero on every delivery** and stay idempotent via **per-actor sequence
  checkpoints + set-based apply**. Don't assume single or in-order delivery.
- Two read models: `PartyDetailProjectionActor` (per party, id `{tenant}:party-detail:{partyId}`) and
  `PartyIndexProjectionActor` (per tenant, id `{tenant}:party-index`, **batched writes**). Erased parties
  are removed from the index.
- Every read returns `ProjectionFreshnessMetadata`; stale/degraded reads fall back to a last-known cache —
  preserve that fallback, don't throw on staleness.

**MCP / client / UI:**
- `parties-mcp` is a **separate stateless process** exposing exactly **5 tools** (`create_party`, `get_party`,
  `find_parties`, `update_party`, `delete_party`). `get_party_name_at` does **not** exist — temporal
  name-as-of queries are deferred; don't reference them.
- Host middleware order is **order-sensitive**:
  `CorrelationId → MvpComplianceWarning → ExceptionHandler → DegradedResponse → AuthN → AuthZ → CloudEvents`.
- Blazor: Picker ships as custom element `<hexalith-party-picker>` (dispatches a `party-selected`
  CustomEvent); the `Hexalith.Parties.UI` host is FluentUI + FrontComposer-hosted and serves **both**
  portals (see Consumer/GDPR rules below). Test Blazor with **bunit**.

**Consumer portal, consent & GDPR rights (Epics 4–5):**
- **Two RCL portals, one UI host, two policies — reuse them, don't invent.** `Hexalith.Parties.UI` hosts
  *both* `AdminPortal` (route `/admin/parties*`, `[Authorize(Policy = "Admin")]` — roles incl. `TenantOwner`)
  and `ConsumerPortal` (self-scoped `/me/*`, `[Authorize(Policy = "Consumer")]`). Policy names + role arrays
  are defined once in `Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`.
- **Consumer→Party binding is claim-based and fail-closed.** A consumer maps to their Party via the verified
  IdP claim **`party_id`** (Keycloak `party-id-mapper`), resolved by **`PartyIdClaimResolver`** (Scoped):
  zero/multiple claims → unbound → redirect `/no-party-binding`. Consumer self-service acts on "me" (the
  resolved party) — **never trust a client-supplied party id** on the consumer surface.
- **Consent ≠ lawful basis.** Consent: commands `RecordConsent` / `RevokeConsent` (events `ConsentRecorded` /
  `ConsentRevoked`; rejections `InvalidConsentPurpose`, `ConsentNotFound`). **`LawfulBasis`**
  (`Consent | LegitimateInterest | ContractualNecessity | LegalObligation`, in `Contracts/Security`) is a
  separate enum — "has consent" is not "lawful to process."
- **Restriction (Art.18) guards are subtle.** `RestrictProcessing` / `LiftRestriction` (events
  `ProcessingRestricted` / `RestrictionLifted`). Consent edits stay **allowed while restricted** (Art.18(3))
  — don't reject `RecordConsent` on `IsRestricted`; but consent commands **do** reject while erasure is in
  progress. Preserve both guards.
- **Erasure has two front doors + cross-submodule verification.** Admin: `EraseParty` (UI requires typed-name
  confirmation) → `ErasePartyRequested`. Consumer: `IConsumerPrivacyErasureClient`
  (`RequestMyErasureAsync` / `CancelMyErasureAsync` / `GetMyErasureStatusAsync`); cancel ⇒ `CancelPartyErasure`.
  **Verification reaches into the EventStore submodule** — `IErasureVerificationService.VerifyErasureAsync`
  → `ErasureVerificationReport` / `ErasureCertificate`; commands `MarkErasureVerified` /
  `RetryErasureVerification`. Treat that contract as approval-gated; don't change it unilaterally.
- **Export (Art.20) & processing records (Art.30) are reads, not commands.** `ExportPartyData` →
  `PartyDataPortabilityPackage`; `GetProcessingRecords` → `ProcessingActivityRecord[]` — both in
  **`PartyDetailProjectionQueryActor`**. Consumer clients self-scope (`IConsumerPrivacyExportClient`,
  `…ProcessingClient`); admin (`IAdminPortalGdprClient`) takes an explicit party id. New privacy reads belong
  on the projection/query side.

### Testing Rules

- **xUnit v3** uniformly across all 15 test projects — use the v3 packages/API, not v2. A separate
  **Playwright `tests/e2e`** suite covers a11y/e2e (`npm run test:a11y`), not an xUnit lane.
- **Assertions: Shouldly** (`value.ShouldBe(...)`). **Mocking: NSubstitute** (`Substitute.For<T>()`).
  **Blazor: bunit.** Do not introduce Moq, FluentAssertions, or raw `Assert.*` — match the house style.
- **Tests run under Microsoft.Testing.Platform** (`global.json` → `test.runner`), not VSTest. **`dotnet test
  --filter` silently runs zero tests** — to filter, run the **test EXE directly** with single-dash xUnit v3
  args (`-class <FQN>` / `-method <name>`). Assert AppHost topology without Docker via
  `DistributedApplicationTestingBuilder` + copy `DaprComponents/**` to the test output dir.
- **Async test/UI traps:** `await using` over a *sync* disposable trips CA2007 → use plain `using`; `await
  <Task local>` in a `[Fact]` needs `ConfigureAwait(true)` (not `false`, which trips xUnit1030); async-only
  DI scopes need `CreateAsyncScope()` + `await DisposeAsync()`.
- **Architectural fitness tests pin the boundaries** (e.g. `Contracts` has zero infrastructure deps; the
  tenant-access service is never invoked on the gateway request path). If a fitness test fails, you crossed
  a boundary — **fix the dependency, not the test.**
- Run lanes with `scripts/test.ps1 -Lane <unit|integration|topology|deploy|all|coverage>` (default `unit`,
  Release). `integration` / `topology` use Testcontainers + `Aspire.Hosting.Testing` and **skip gracefully
  when Docker/DAPR is absent** — a skip is not a failure; don't force them green.
- `DeployValidation.Tests` statically validates the real `deploy/` manifests and runs a **credential-leak
  poison-sweep** — never commit secrets/tokens into `deploy/`, or this lane fails.

### Code Quality & Style Rules

- **Put code where it belongs (these boundaries are load-bearing):**
  | To change… | Edit… |
  |---|---|
  | command / event / value-object / read-model shape | `Hexalith.Parties.Contracts/{Commands,Events,ValueObjects,Models}` |
  | business rules & validation | `Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` + `Hexalith.Parties/Validation/` |
  | read models / projections / search | `Hexalith.Parties.Projections/` + `Hexalith.Parties/Queries/` |
  | public client surface | `Hexalith.Parties.Client/` |
  | MCP tools | `Hexalith.Parties.Mcp/Tools/` |
  | auth / tenancy / compliance | `Hexalith.Parties/{Authentication,Authorization,Compliance,Middleware}` |
  | GDPR encryption / erasure | `Hexalith.Parties.Security/` |
  | Admin / Consumer Blazor UI (pages, areas) | `Hexalith.Parties.AdminPortal` (`/admin`) / `Hexalith.Parties.ConsumerPortal` (`/me`); host wiring + UI auth policies in `Hexalith.Parties.UI` |
  | a value shared across projects (claim type, wire JSON options, projection name / actor-id, role array, policy name, text/format helper) | **define it once in `Hexalith.Parties.Contracts`** — `PartiesClaimTypes`, `PartiesJsonOptions.Default`, `PartyProjectionNames` / `PartyActorIds`, `PartiesRoles`, `PartyDisplayFormat` — never re-hardcode the literal/options in a second project (shared-anchor convention, sprint-change-proposal-2026-06-28). Exception: shared JWT `IClaimsTransformation` logic needs `Microsoft.AspNetCore.Authentication`, so it lives in `Hexalith.Parties.Authentication`, not Contracts. |
- **`Contracts` must stay infrastructure-free** — no DAPR, ASP.NET, EventStore-*server*, or persistence
  references; it depends only on `Hexalith.EventStore.Contracts`. This is the organizing principle, pinned
  by a fitness test.
- **The adopter-facing vs internal split is real and is *not* the same as `IsPackable`.** Consumer-referenced
  surface: `Client` + `Contracts` (public shape pinned by `*.Tests/Package` PackageTests), the three UI RCLs
  `Picker` / `AdminPortal` / `ConsumerPortal` (explicit `PackageId`), and `ServiceDefaults` (optional helpers).
  `Mcp` is a **deployable host** (`parties-mcp`), not a referenced library. **Internal** (README-marked, even
  though packable-by-default): `Server`, `Projections`, `Security`, `Testing`, plus the `Hexalith.Parties`
  actor host and the `Hexalith.Parties.UI` host. Don't reference internal projects from consumer-facing code,
  and don't leak internal types through a package's public API. (`IsPackable=true` is the
  `Directory.Build.props` default — only hosts/tests set `false` — so packability alone ≠ "public".)
- **Submodule project references are resolved by a computed root property** that probes `references/` paths
  (e.g. `HexalithEventStoreRoot` → `..\..\references\Hexalith.EventStore`). That's why EventStore/Tenants must sit under
  **`references/` checkouts** — don't convert them to NuGet `PackageReference`s.
- **XML doc comments are not mandatory** — `CS1591` is suppressed on `Contracts` (the docs file is generated
  for the package only). Match the surrounding file's documentation density; don't bulk-add `///` to satisfy
  a non-existent rule.

### Development Workflow Rules

- **Build / run / test:**
  - Build: `dotnet build Hexalith.Parties.slnx -c Release --no-restore` (restore the `.slnx`, not a `.sln`).
  - Run the full local topology: `dotnet aspire run --project src/Hexalith.Parties.AppHost` (Docker Desktop
    must already be running). Treat the system as usable only once `eventstore`, `parties`, and `tenants`
    are healthy. `eventstore-admin-ui` and `parties-mcp` are **explicit-start** — launch from the dashboard.
  - Test lanes via `scripts/test.ps1 -Lane <lane>` (see Testing Rules).
- **The build gate is the contract:** a fresh clone (root-repository submodules under `references/` only, no warnings override) must build
  green. Verify parity locally with `bash scripts/check-no-warning-override.sh`. Never weaken it.
- **Submodules: `git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants` — root-repository submodules only,
  never `--recursive`** (CI checks out the same way; the build gate forbids nested-submodule init).
- **Commits follow Conventional Commits** — `feat:`, `fix:`, `chore:`, `docs:`, with optional scope
  (`fix(deploy): …`, `docs(planning): …`). Work on a typed branch (`<type>/<slug>`) and merge via PR.
- **CI** (`.github/workflows/test.yml`, the only workflow): `lint` (analyzers build + Story 9.8 build-gate)
  → in parallel `test` (**4 named shards**: `contracts-client-security-mcp`, `domain-server`, `projections-ui`,
  `integration-deploy`) **and `ui-a11y`** (UI accessibility gate, bUnit) → `contract-test` → `report` (quality
  gate). **All five jobs must pass.**
- **Deploy to K8s only via the guarded script:** `pwsh deploy/k8s/publish.ps1 -ConfirmContext <ctx>` (aspirate
  `9.1.0`, restored by `dotnet tool restore`). The `-ConfirmContext` guard prevents deploying to the wrong
  cluster — don't bypass it. Authoritative DAPR CRs live in `deploy/dapr/` (run-mode YAML is separate, under
  `AppHost/DaprComponents/`).
- **Runtime config uses `__` nesting** for env vars (e.g. `Parties__CryptoShredding__IsEnabled`,
  `Authentication__JwtBearer__Authority`), mirroring `appsettings*.json` sections.

### Critical Don't-Miss Rules (anti-patterns & gotchas)

- **Never log event payloads or PII.** Projection logs carry only projection name + coarse counts — never
  party/tenant/actor ids (AC7). Tag personal-data properties with `[PersonalData]` and keep them out of
  logs, traces, and error messages.
- **The two GDPR switches are independent and easily confused:**
  - `Parties:CryptoShredding:IsEnabled` (the AES-256-GCM encryption/erasure **feature**) defaults **true** —
    it is implemented and ON.
  - `Parties:Compliance:GdprFeaturesActive` (the MVP **warning** suppressor) defaults **false**.
  - The README's GDPR notice forbids regulated EU personal data until a production KMS is provisioned; this is
    because the default key store is dev-only, **not** because crypto-shredding is disabled. Don't conflate the
    MVP warning switch with the crypto feature, and don't disable crypto-shredding to "match the README."
- **The default key store is `LocalDevKeyStorageBackend` (in-memory, dev-only).** Never ship it to
  production — provision a real KMS/secret store (`docs/deployment-security-checklist.md`). Do **not** store
  regulated EU personal data in the MVP; evaluate with synthetic data.
- **The event contract evolves additively only** — add new optional fields or new event types; **never
  remove or rename** an existing field. Subscribers are forward-compatible and break otherwise.
- **Subscribers must be idempotent and always return `200 OK`** (delivery is at-least-once; dedupe by event
  id). The **`PartyErased` handler is mandatory** for subscribers (dangling-reference cleanup), and
  deserialization must tolerate unknown fields. See `docs/event-handler-patterns.md`.
- **Tenant access fails closed and is eventually consistent** — after a restart the in-memory projection
  starts empty and denies `UnknownTenant` until tenant events replay. Use `ITenantAccessService`
  **projection-side only, never on the gateway request path** (fitness-pinned).
- **Party search has a local fallback — don't make it hard-depend on `Hexalith.Memories`.** Defaults:
  `IPartySearchProvider` → `LocalFuzzyPartySearchProvider`, `IPartySearchService` → `LocalPartySearchService`.
  Rich search activates **only** when `Parties:MemoriesSearch` is configured (validated `PartyMemorySearchOptions`;
  endpoint must be an absolute URI) → `MemoriesPartySearchService`. Keep search working when Memories is absent;
  gate Memories-only paths behind the config and fail closed on misconfiguration.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code in this repository.
- Follow ALL rules exactly as documented. When in doubt, prefer the more restrictive option.
- These rules are *unobvious* and project-specific — they override generic .NET/C# habits where they conflict.
- For depth beyond a rule, follow the linked doc (`docs/architecture.md`, `docs/event-handler-patterns.md`,
  `docs/deployment-security-checklist.md`, `docs/development-guide.md`).

**For Humans:**

- Keep this file lean and focused on what agents miss — delete rules once they become obvious or tooling-enforced.
- Update it when the technology stack, the gateway boundary, or the GDPR posture changes.
- Re-derive from `docs/` (regenerated by `/bmad-document-project`) when the architecture shifts materially.

Last Updated: 2026-06-21
