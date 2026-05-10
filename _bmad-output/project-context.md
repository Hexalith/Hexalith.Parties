---
project_name: 'Hexalith.Parties'
user_name: 'Jérôme'
date: '2026-05-10'
sections_completed: ['technology_stack', 'language_specific_rules', 'framework_specific_rules', 'testing_rules', 'code_quality_style_rules', 'development_workflow_rules', 'critical_dont_miss_rules']
existing_patterns_found: 8
status: 'complete'
rule_count: 65
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- Runtime: .NET SDK 10.0.103 via `global.json`, target framework `net10.0`.
- Build defaults: nullable enabled, implicit usings enabled, `TreatWarningsAsErrors=true`, MinVer `7.0.0`, central package management through `Directory.Packages.props`.
- Service architecture: Hexalith.Parties is a Dapr/Aspire actor-hosted domain service built on Hexalith.EventStore, with sibling root-level submodules for EventStore, Tenants, Memories, FrontComposer, and AI.Tools.
- Dapr: `Dapr.Client` and `Dapr.AspNetCore` `1.17.7`; `Dapr.Actors` and `Dapr.Actors.AspNetCore` `1.16.1`.
- Aspire: `Aspire.Hosting` `13.2.2`, Redis/Docker/Azure App Containers hosting `13.2.2`, Keycloak/Kubernetes previews `13.2.2-preview.1.26207.2`.
- Core application packages: MediatR `14.1.0`, FluentValidation `12.1.1`, Microsoft.Extensions `10.x`, JWT Bearer `10.0.0`, Fluent UI Blazor `5.0.0-rc.2-26098.1`.
- MCP packages: ModelContextProtocol `1.0.0`, but the main `Hexalith.Parties` actor host must not expose in-process MCP tools.
- Observability/service defaults: OpenTelemetry `1.15.x`, Microsoft.Extensions.Http.Resilience `10.3.0`, ServiceDiscovery `10.3.0`.
- Testing: xUnit `2.9.3`, xUnit runner `3.1.5`, Shouldly `4.3.0`, NSubstitute `5.3.0`, bUnit `2.7.2`, Testcontainers `4.10.0`, coverlet `6.0.4`.

## Critical Implementation Rules

### Language-Specific Rules

- Use C# file-scoped namespaces and keep `using` directives outside namespaces; sort `System.*` directives first.
- Preserve nullable correctness. Do not suppress nullable warnings casually because warnings are treated as errors.
- Prefer `sealed record` for contract commands/events/value payloads and `sealed class` for services unless extensibility is intentionally required.
- Interface names must start with `I`; private fields must use `_camelCase`; async methods must end in `Async`.
- Keep Allman brace style, 4-space indentation, CRLF line endings, UTF-8, trimmed trailing whitespace, and final newline per `.editorconfig`.
- Do not add package versions to individual `.csproj` files; use `Directory.Packages.props` for central version management.
- Contracts/events must remain additive and forward-compatible. Avoid changing existing public contract shapes unless a migration is explicitly part of the task.
- `PartyState.Apply(...)` has a non-obvious ordering rule: no-op `IRejectionEvent` Apply overloads must remain before success-event Apply overloads because EventStore rehydration resolves event types by short-name suffix matching.
- Rejection event Apply methods are intentionally no-op instance methods. Do not remove them or convert them to static helpers; EventStore rehydration requires concrete Apply overloads.

### Framework-Specific Rules

- Hexalith.Parties validates the Hexalith.EventStore platform. If an EventStore abstraction does not fit, prefer fixing/adapting EventStore rather than adding Parties-side workarounds.
- Domain behavior belongs in EventStore aggregate conventions: pure static `Handle(Command, PartyState?)` methods emit events; `Apply(Event)` mutates `PartyState`.
- The main `src/Hexalith.Parties` project is an actor host. Do not add REST controllers, public minimal APIs, OpenAPI/Swagger endpoints, or in-process MCP tools there.
- EventStore-owned gateways handle public request authorization. Do not implement or wire `ITenantValidator`, `IRbacValidator`, or retired request-path tenant denial translators in Parties.
- Parties still owns projection-side tenant/access behavior. Keep tenant isolation fail-closed for reads, projections, search, admin views, and tenant event consumption.
- Dapr sidecar-internal endpoints such as `/dapr/subscribe` and `/tenants/events` must remain explicitly documented as internal pub/sub plumbing, not client APIs.
- Projection architecture uses actor-managed read models: party detail projections per party and party index projections per tenant, with partition strategy abstractions for scale.
- Composite commands such as `CreatePartyComposite` and `UpdatePartyComposite` are intentional: they keep MCP-style multi-operation use cases atomic within one aggregate turn.
- Admin portal code uses Blazor/Razor and Fluent UI Blazor, hosted through FrontComposer contracts. Keep UI code in AdminPortal/Picker projects, not in the actor host.
- Aspire AppHost must keep the `parties` app id and dedicated Dapr access-control component; do not loosen access control with wildcard app ids or wildcard operation paths.

### Testing Rules

- Use xUnit with Shouldly assertions by default; use NSubstitute for mocks/stubs when needed.
- Keep test projects aligned with production project boundaries: Contracts, Client, Server, Projections, Security, AdminPortal, Picker, MCP, Integration, DeployValidation.
- Add or update architectural fitness tests when changing boundaries that are intentionally guarded, especially REST/MCP exposure, tenant authorization ownership, Dapr ACLs, contract dependencies, or projection isolation.
- Unit tests should cover pure aggregate `Handle` behavior and `PartyState.Apply` behavior without requiring Dapr/Aspire infrastructure.
- Integration tests belong in the integration/deploy validation projects when behavior depends on Dapr, Aspire topology, access-control components, pub/sub, or cross-module wiring.
- Projection tests must verify tenant isolation and event ordering assumptions; read-side code should fail closed on stale or missing tenant access state.
- For composite commands, test applied/skipped/rejected outcomes, duplicate operation handling, sub-operation limits, no-op idempotency, erasure/restriction guards, and emitted event order.
- Do not remove or weaken the `PartyState` rejection-event fitness tests; they guard EventStore suffix-based Apply resolution.
- Existing `xUnit1051` suppression in `Hexalith.Parties.Tests` is documented deferred work. Do not spread that suppression to new projects unless deliberately accepted.

### Code Quality & Style Rules

- Keep project structure under `src/`, `tests/`, and `samples/` aligned with `Hexalith.Parties.*` package boundaries.
- Prefer small, explicit domain types in `Hexalith.Parties.Contracts`; avoid adding infrastructure dependencies to contracts.
- Keep public contracts stable and additive. Rejections and events are part of the observable event stream, not internal exceptions.
- Use FluentValidation for command/input validation where the project already uses validators; keep domain invariant enforcement in aggregate `Handle` methods.
- Do not hide personal data handling behind generic helpers that obscure `[PersonalData]` inventory or crypto-shredding behavior.
- Keep comments sparse, but preserve comments that explain architectural exceptions, Dapr-internal routes, rejection-event Apply ordering, or documented deferred work.
- Avoid broad refactors while implementing stories; the repo relies on fitness tests and explicit architecture decisions to protect boundaries.
- When changing package references, update central versions and validate packability/publishability defaults instead of scattering project-local metadata.
- Generated or helper test data should live in testing/helper projects when reusable across test assemblies.

### Development Workflow Rules

- Use `Hexalith.Parties.slnx` as the solution entry point; do not introduce legacy `.sln` files unless explicitly requested.
- For local full-topology runs, use Aspire through `src/Hexalith.Parties.AppHost`.
- For repositories with submodules, initialize/update only root-level submodules by default. Never run `git submodule update --init --recursive` or initialize nested submodules unless the user explicitly requests nested submodules.
- Treat sibling directories `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, `Hexalith.FrontComposer`, and `Hexalith.AI.Tools` as root-level submodules/dependencies; avoid editing them unless the task explicitly crosses that boundary.
- Before changing EventStore-owned behavior, check whether the correct fix belongs in the EventStore submodule rather than Parties.
- Run focused `dotnet test` commands for the changed project/test project pair before broader suites; use integration/deploy validation tests when changing Dapr/Aspire wiring.
- Do not revert or clean generated/test output folders as part of unrelated work.
- Package releases are MinVer/tag-driven with `v` tag prefix; do not hard-code package versions in project files.

### Critical Don't-Miss Rules

- Do not reintroduce public REST, Swagger/OpenAPI, or MCP hosting into `src/Hexalith.Parties`; that project is guarded as an actor host.
- Do not move tenant/RBAC gateway authorization into Parties. EventStore owns the public request path; Parties owns domain behavior and projection-side access checks.
- Do not delete rejection events from streams or model them as exceptions only. Rejection events are persisted and replayed.
- Do not reorder `PartyState.Apply` methods blindly. Rejection Apply overloads must come before success Apply overloads to avoid suffix-match misrouting.
- Do not treat Dapr pub/sub subscription routes as public API surface. Keep comments and tests documenting sidecar-internal exceptions.
- Do not loosen Dapr access-control YAML with wildcard app ids or wildcard paths.
- Do not bypass composite command guards: max sub-operation count, duplicate detection, erasure status, processing restriction, and idempotent no-op behavior are intentional.
- Do not leak personal data in logs, telemetry, search metadata, admin UI state, or exceptions. When classification is ambiguous, preserve explicit `[PersonalData]` handling and crypto-shredding intent.
- Do not let read models or search return cross-tenant data, even during stale projection or missing tenant state scenarios; fail closed.
- Do not make Contracts depend on hosting, Dapr, MediatR, FluentValidation, UI, or infrastructure packages unless an architectural decision explicitly changes the package boundary.
- Do not recursively initialize nested submodules. Root-level submodules are enough unless the user explicitly asks for nested submodules.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code.
- Follow all rules exactly as documented.
- When in doubt, prefer the more restrictive option.
- Update this file if new project-specific patterns emerge.

**For Humans:**

- Keep this file lean and focused on agent needs.
- Update when technology stack, package boundaries, or architecture guardrails change.
- Review periodically for outdated rules.
- Remove rules that become obvious over time.

Last Updated: 2026-05-10
