---
project_name: parties
user_name: Administrator
date: 2026-06-02
sections_completed:
  ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'quality_rules', 'workflow_rules', 'anti_patterns']
status: 'complete'
rule_count: 51
optimized_for_llm: true
existing_patterns_found: 14
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

**Runtime & build (pinned — do not bump casually):**
- C# / **.NET 10** — `net10.0` TFM, SDK `10.0.300` pinned in `global.json` (`rollForward: latestPatch`).
- **Central Package Management is ON.** All versions live in `Directory.Packages.props`. Project
  `.csproj` files use `<PackageReference Include="..." />` with **NO `Version` attribute** — adding a
  `Version=` to a csproj is a build error. Add/upgrade versions in `Directory.Packages.props` only.
- The solution is `Hexalith.Parties.slnx` — the **XML solution format**, not a classic `.sln`.

**Core stack:**
| Concern | Package | Version |
|---|---|---|
| Orchestration | .NET Aspire (packages / AppHost SDK) | `13.4.0` / SDK pinned `13.3.3` (skew) |
| Actors & pub/sub | `Dapr.Actors` / `Dapr.Client` | `1.18.0-rc02` |
| | `Dapr.AspNetCore` / `Dapr.Actors.AspNetCore` | `1.17.9` |
| Validation | FluentValidation | `12.1.1` |
| Mediation | MediatR | `14.1.0` |
| AuthN | Microsoft.AspNetCore.Authentication.JwtBearer | `10.0.8` |
| MCP | ModelContextProtocol / .AspNetCore | `1.3.0` / `1.3.0` |
| UI | FluentUI Blazor / CustomElements | `5.0.0-rc.3` / `10.0.8` |
| Testing | xUnit **v3** / Shouldly / NSubstitute / bunit / Testcontainers | `3.2.2` / `4.3.0` / `5.3.0` / `2.7.2` / `4.12.0` (declared, unused) |

**Constraints agents must respect:**
- **`Hexalith.EventStore` and `Hexalith.Tenants` are sibling submodules referenced by PROJECT path**,
  not NuGet packages — and are *not* checked out in a fresh clone. Initialise root-level only:
  `git submodule update --init Hexalith.EventStore Hexalith.Tenants`. **Never `--recursive`** (the build
  gate forbids nested-submodule init). `Hexalith.Memories` is optional (rich search).
- DAPR versions are **intentionally mixed** (`1.18.0-rc02` Actors/Client vs `1.17.9` AspNetCore) — do
  not "align" them.
- Versioning is **MinVer from git tags** (prefix `v`); never hand-edit `<Version>` in a csproj.

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
  CustomEvent); AdminPortal is FluentUI + FrontComposer-hosted. Test Blazor with **bunit**.

### Testing Rules

- **xUnit v3** uniformly across all 12 test projects — use the v3 packages/API, not v2.
- **Assertions: Shouldly** (`value.ShouldBe(...)`). **Mocking: NSubstitute** (`Substitute.For<T>()`).
  **Blazor: bunit.** Do not introduce Moq, FluentAssertions, or raw `Assert.*` — match the house style.
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
- **`Contracts` must stay infrastructure-free** — no DAPR, ASP.NET, EventStore-*server*, or persistence
  references; it depends only on `Hexalith.EventStore.Contracts`. This is the organizing principle, pinned
  by a fitness test.
- **The adopter-facing vs internal split is real.** Adopter-facing packages — `Client`, `Contracts`,
  `Picker`, `AdminPortal`, `Mcp`, `ServiceDefaults` — are referenced by consumers; **internal** projects
  (host `Hexalith.Parties`, `Server`, `Projections`, `Security`, `Testing`) are private to the actor host.
  Don't reference internal projects from consumer-facing code, and don't leak internal types through a
  package's public API.
- **Submodule project references are resolved by a computed root property** that probes sibling paths
  (e.g. `HexalithEventStoreRoot` → `..\..\Hexalith.EventStore`). That's why EventStore/Tenants must sit as
  **sibling checkouts** — don't convert them to NuGet `PackageReference`s.
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
- **The build gate is the contract:** a fresh clone (root submodules only, no warnings override) must build
  green. Verify parity locally with `bash scripts/check-no-warning-override.sh`. Never weaken it.
- **Submodules: `git submodule update --init Hexalith.EventStore Hexalith.Tenants` — root-level only,
  never `--recursive`** (CI checks out the same way; the build gate forbids nested-submodule init).
- **Commits follow Conventional Commits** — `feat:`, `fix:`, `chore:`, `docs:`, with optional scope
  (`fix(deploy): …`, `docs(planning): …`). Work on a typed branch (`<type>/<slug>`) and merge via PR.
- **CI** (`.github/workflows/test.yml`): `lint` (warnings-as-errors build + build-gate) → `test` (4 shards)
  → `contract-test` → `report` (quality gate). All four must pass.
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

Last Updated: 2026-06-02
