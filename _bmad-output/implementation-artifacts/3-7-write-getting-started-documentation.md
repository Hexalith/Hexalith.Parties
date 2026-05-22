# Story 3.7: Write Getting Started Documentation

Status: done

## Story

As a developer new to Parties,
I want a tested getting-started guide,
so that I can deploy locally and send my first command as a self-service experience.

## Acceptance Criteria

1. Given a developer opens the getting-started guide, when they read the prerequisites and local setup section, then they can identify required .NET, Docker/container tooling, DAPR/Aspire expectations, and supported development mode, and the guide does not require recursive submodule initialization.
2. Given a developer follows the guide on a clean machine with documented prerequisites, when they start the local system, then they can reach a healthy/readiness-confirmed Parties instance within the documented deployment target, and troubleshooting steps explain common startup failures.
3. Given the local system is running, when the developer follows the first-command walkthrough, then they can send a successful `CreateParty` request through REST and understand the command -> event -> projection flow at a high level.
4. Given the developer continues the walkthrough, when they perform the first query, then they can retrieve or search for the created party, and the guide explains eventual consistency and freshness indicators.
5. Given the developer wants to integrate from .NET, when they follow the client package section, then they can register `AddPartiesClient()` and send a typed command/query, and the guide explains required configuration without exposing service internals.
6. Given the guide includes MVP compliance positioning, when the developer reads the warning, then it clearly states that MVP is not for regulated EU personal data until v1.1 GDPR features are active and links to operational handling guidance where available.
7. Given documentation validation is performed, when a non-author or scripted doc check follows the guide, then broken commands, missing prerequisites, stale endpoint names, or unclear first-command steps are caught.

## Tasks / Subtasks

- [x] Verify and tighten the getting-started guide prerequisites and local setup path. (AC: 1, 2)
  - [x] Document .NET, Docker, Git, Aspire command expectations, and DAPR CLI scope.
  - [x] Preserve root-level submodule initialization only for the default local path.
  - [x] Document dashboard and `/ready` readiness confirmation.
- [x] Verify and tighten the command/query walkthrough. (AC: 3, 4)
  - [x] Keep REST examples on EventStore `POST /api/v1/commands` and `POST /api/v1/queries`.
  - [x] Explain command -> event -> projection flow and correlation-id use.
  - [x] Explain eventual consistency and freshness handling for first queries.
- [x] Verify and tighten the .NET integration section. (AC: 5)
  - [x] Keep `AddPartiesClient()` as the integration path.
  - [x] Document `Parties:BaseUrl` as the EventStore gateway base URL and `Parties:Tenant` as required tenant configuration.
  - [x] Avoid advertising internal actor/projection/service internals.
- [x] Verify and tighten MVP compliance guidance. (AC: 6)
  - [x] State that MVP is not for regulated EU personal data until v1.1 GDPR features are active.
  - [x] Keep sample data synthetic and non-sensitive.
- [x] Add scripted documentation guardrails. (AC: 7)
  - [x] Assert getting-started prerequisites, supported local mode, and readiness guidance.
  - [x] Assert first-command/query/client snippets use the accepted EventStore-fronted contract.
  - [x] Assert recursive submodule initialization and retired/public-internal surfaces are not advertised.

## Dev Notes

Story 3.7 is documentation and guardrail-test work. It builds on Story 3.6's one-command local-run baseline and should not change runtime composition unless a documented command is proven stale. The public boundary remains EventStore-fronted REST plus the typed Parties client; the `parties` resource remains the actor host behind EventStore and must not be documented as an adopter-facing REST/MCP endpoint.

Relevant existing surfaces:

- `docs/getting-started.md` contains the self-service onboarding walkthrough.
- `README.md` points developers to the guide and summarizes the local run.
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs` contains static onboarding documentation and sample guardrails.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` and `HttpPartiesQueryClient.cs` define the accepted gateway request shapes used by the typed client.

## References

- Story source: `_bmad-output/planning-artifacts/epics.md#Story 3.7: Write Getting Started Documentation`
- Prior story: `_bmad-output/implementation-artifacts/3-6-enable-one-command-local-run.md`
- PRD: `_bmad-output/planning-artifacts/prd.md` FR31, FR32, FR56, FR60, FR62
- Architecture: `_bmad-output/planning-artifacts/architecture.md` developer integration, gateway, and deployment guidance

## Dev Agent Record

### Agent Model Used

Parent-managed Codex recovery flow after nested Codex story sessions were previously blocked from writing and test execution by read-only/no-approval child policy.

### Debug Log References

- Story automator resumed from `_bmad-output/story-automator/orchestration-1-20260521-062818.md` after user requested continuation.
- Existing `docs/getting-started.md` already contained most EventStore-fronted walkthrough content from prior stories; this story tightened missing readiness, DAPR/Aspire, freshness, compliance, and scripted validation details.

### Completion Notes List

- Tightened prerequisites and local setup to call out Aspire mode, DAPR CLI scope, Docker, root-level submodules only, and readiness confirmation via dashboard plus service-default endpoints.
- Added a command -> event -> projection explanation after the first REST command and a freshness/eventual-consistency section after first queries.
- Expanded the typed .NET client section with the required namespaces, `AddPartiesClient(builder.Configuration)`, and explicit `Parties:BaseUrl` / `Parties:Tenant` configuration boundaries.
- Added troubleshooting coverage for Docker/submodule startup failures and an explicit MVP compliance boundary for regulated EU personal data.
- Extended onboarding guardrail tests so stale local-run, REST gateway, client configuration, freshness, and compliance wording fails scripted validation.
- Validation passed: `dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~SampleOnboardingGuardrailTests"` (6/6).

### File List

- `_bmad-output/implementation-artifacts/3-7-write-getting-started-documentation.md`
- `docs/getting-started.md`
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs`

## Change Log

| Date | Author | Change |
|------|--------|--------|
| 2026-05-21 | bmad-story-automator (AI) | Created story artifact and tightened getting-started documentation. |
| 2026-05-21 | bmad-story-automator (AI) | Completed validation and marked story done. |

## Senior Developer Review (AI)

**Reviewer:** bmad-story-automator parent-managed review
**Date:** 2026-05-21
**Outcome:** Approve

### Summary

Story 3.7 is limited to developer onboarding documentation and guardrail tests. The guide now covers prerequisites, supported Aspire mode, root-level submodule setup, readiness checks, first command/query flow, typed client configuration, freshness expectations, and MVP compliance positioning. No runtime code was changed.

### AC Coverage

- **AC1 (Prerequisites, local setup, no recursive submodules)** — Pass. The guide names .NET, Docker, Git, Aspire, DAPR CLI scope, and the root-level `Hexalith.EventStore` / `Hexalith.Tenants` submodule command while rejecting recursive setup.
- **AC2 (Healthy/readiness-confirmed local instance and startup troubleshooting)** — Pass. The guide points to Aspire dashboard health plus `/ready`, `/health`, and `/alive`, and troubleshooting covers Docker/submodule startup failures.
- **AC3 (First REST CreateParty command and flow explanation)** — Pass. The EventStore `POST /api/v1/commands` example remains intact and the command -> event -> projection flow is documented.
- **AC4 (First query, search, eventual consistency, freshness)** — Pass. `PartyDetail`, `PartySearch`, and `PartyIndex` examples remain intact, with a dedicated freshness/eventual-consistency explanation.
- **AC5 (.NET client registration/configuration boundary)** — Pass. The typed client section shows `AddPartiesClient(builder.Configuration)`, required namespaces, and `Parties:BaseUrl` / `Parties:Tenant` configuration without exposing service internals.
- **AC6 (MVP compliance positioning)** — Pass. The guide states the MVP is not approved for regulated EU personal data until v1.1 GDPR features are active and keeps examples synthetic.
- **AC7 (Scripted documentation validation)** — Pass. `SampleOnboardingGuardrailTests` now locks the accepted local-run, gateway, query, client, freshness, compliance, and no-recursive-submodule contract.

### Validation

- `dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj --filter "FullyQualifiedName~SampleOnboardingGuardrailTests"` — Passed 6, Failed 0, Skipped 0.
