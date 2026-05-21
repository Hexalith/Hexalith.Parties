# Story 3.6: Enable One-Command Local Run

Status: done

## Story

As a developer trying Parties locally,
I want to start the full local system with one documented command,
so that I can evaluate and develop against the service without hand-wiring infrastructure.

## Acceptance Criteria

1. Given documented prerequisites, running the documented Aspire/AppHost command from the repo root starts the Parties service, required DAPR sidecar config, state/pubsub backing services, and health/readiness endpoints.
2. After startup, Aspire dashboard/diagnostics show Parties resources, DAPR sidecar status, backing service health, and the REST/API endpoint for first commands.
3. If not ready, readiness remains false until required infrastructure and Parties can accept requests; it becomes true within the documented cold-start target.
4. Missing or misconfigured local dependencies fail with actionable guidance and no silent partial configuration is treated as production-ready.
5. Local-run validation tests or scripted smoke checks start AppHost in supported local mode, verify health/readiness and at least one authenticated/documented dev-mode request path, and do not require recursive submodule initialization.

## Tasks / Subtasks

- [x] Verify the AppHost local topology. (AC: 1, 2)
  - [x] Confirm stable Aspire resources exist for `eventstore`, `eventstore-admin`, `parties`, `tenants`, `redis`, optional `keycloak`, explicit-start `eventstore-admin-ui`/`parties-mcp`, and opt-in `memories`.
  - [x] Confirm Parties and Tenants use DAPR sidecars and shared state/pubsub backing services; Memories joins the same backing services only when rich search is enabled.
  - [x] Confirm local health/readiness endpoints are mapped by service defaults.
- [x] Document the default local run path. (AC: 1, 2, 4, 5)
  - [x] Document root-level submodule prerequisites only.
  - [x] Document the single AppHost run command from the repository root.
  - [x] Explicitly reject recursive/nested submodule initialization for the default local run.
- [x] Make missing local dependencies actionable. (AC: 4)
  - [x] Validate required root-level sibling submodule paths before AppHost project references are resolved.
  - [x] Keep `Hexalith.Memories` opt-in for local rich search so its nested dependencies do not block the default local Parties run.
  - [x] Emit setup guidance that points to the supported root-level submodule command.
- [x] Add guardrail tests. (AC: 1-5)
  - [x] Assert the local topology resources and backing services are present in AppHost composition.
  - [x] Assert README and getting-started docs use the one-command AppHost path and root-level submodule setup only.
  - [x] Assert AppHost missing-dependency errors do not instruct recursive or nested submodule setup.

## Dev Notes

Story 3.6 is scoped to the local Aspire/AppHost developer experience. The AppHost remains the single source of truth for the local topology; do not introduce a separate local orchestration script unless a later story explicitly needs it.

The default setup path must respect the repository rule that nested submodules are not initialized unless explicitly requested. Root-level sibling submodules `Hexalith.EventStore` and `Hexalith.Tenants` are enough for the default AppHost local run. `Hexalith.Memories` is opt-in behind `EnableMemoriesSearch=true` because its upstream build currently enforces its own nested submodule prerequisites.

Relevant existing surfaces:

- `src/Hexalith.Parties.AppHost/Program.cs` composes EventStore, EventStore Admin, Parties, Parties MCP, Tenants, Memories, Redis, and optional Keycloak.
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` maps `/health`, `/alive`, and `/ready`.
- `README.md` and `docs/getting-started.md` contain the developer-facing local run path.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` contains static topology and local-run guardrails.

## References

- Story source: `_bmad-output/planning-artifacts/epics.md#Story 3.6: Enable One-Command Local Run`
- PRD: `_bmad-output/planning-artifacts/prd.md` FR31, FR60, FR71
- Architecture: `_bmad-output/planning-artifacts/architecture.md` developer integration, Aspire/AppHost, and resilience guidance
- Prior story: `_bmad-output/implementation-artifacts/3-5-generate-openapi-and-error-catalog.md`

## Dev Agent Record

### Agent Model Used

Parent-managed Codex recovery flow after nested Codex story sessions were blocked from writing and test execution by read-only/no-approval child policy.

### Debug Log References

- Story automator resumed from `_bmad-output/story-automator/orchestration-1-20260521-062818.md` after user requested continuation.
- Local inspection found an obsolete nested `Hexalith.Memories` submodule instruction in `docs/getting-started.md`; the default path was corrected to root-level submodules only.

### Completion Notes List

- Default AppHost build no longer evaluates `Hexalith.Memories`, avoiding its nested `Hexalith.Commons` prerequisite for the baseline local Parties run.
- README and getting-started docs now document root-level submodule setup only and the single `dotnet aspire run --project src/Hexalith.Parties.AppHost` command.
- Missing required root-level EventStore/Tenants submodules fail during AppHost project evaluation with actionable setup guidance.
- `EnableMemoriesSearch=true` remains available as an opt-in path with an explicit Memories project resolver and local DAPR ACL/component scopes.
- `eventstore-admin-ui` and `parties-mcp` are explicit-start dashboard resources so the default one-command topology starts cleanly while preserving auxiliary access.
- Validation passed: focused AppHost topology tests, AppHost build, Aspire doctor, and AppHost startup smoke.
- Aspire doctor warning remains environmental: multiple/older HTTPS development certificates are present; Docker and .NET SDK checks pass.

### File List

- `_bmad-output/implementation-artifacts/3-6-enable-one-command-local-run.md`
- `README.md`
- `docs/getting-started.md`
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.memories.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`

## Change Log

| Date | Author | Change |
|------|--------|--------|
| 2026-05-21 | bmad-story-automator (AI) | Created story artifact and implemented local-run documentation/AppHost guardrails. |
| 2026-05-21 | bmad-story-automator (AI) | Completed validation and marked story done. |
