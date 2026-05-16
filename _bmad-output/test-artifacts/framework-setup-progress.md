---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-05-16'
---

# Test Framework Setup Progress

## Step 1: Preflight Checks

- Detected stack: `backend`.
- Detected language/runtime: C#/.NET, SDK `10.0.103`, target framework `net10.0`.
- Existing framework: xUnit v3 test projects already exist under `tests/`, with Shouldly, NSubstitute, bUnit, Testcontainers, coverlet collector, and Aspire.Hosting.Testing centrally versioned in `Directory.Packages.props`.
- Frontend/browser E2E framework at repo root: none detected.
- Architecture context found: `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/project-context.md`, root `README.md`, and existing `tests/Directory.Build.props`.
- Preflight decision: create mode may proceed, but as a conservative standardization pass around the existing test estate, not a greenfield replacement.

## Step 2: Framework Selection

Selected framework: xUnit v3 for .NET backend testing.

Rationale:

- The repository already uses xUnit v3 package IDs (`xunit.v3`, `xunit.runner.visualstudio`) across its test projects.
- The product is a Dapr/Aspire actor-hosted backend service, so lower-level .NET tests give the highest signal-to-cost ratio.
- Existing project boundaries map cleanly to Tier 1 unit/component tests, Tier 2 WebApplicationFactory/service integration tests, Tier 3 Aspire/Dapr topology tests, and deployment validation tests.
- No Playwright or Cypress scaffold was created at the repo root because there is no root frontend application or browser E2E contract to exercise here.

Official documentation cross-check:

- xUnit.net v3 documentation confirms .NET SDK usage, xUnit v3 project shape, common `Using Include="Xunit"` usage, and `dotnet test` support.
- Microsoft `dotnet test` documentation confirms solution/project execution through the .NET CLI.
- coverlet documentation confirms `dotnet test --collect:"XPlat Code Coverage"` for coverage collection.

## Step 3: Scaffold Framework

Created or standardized:

- `tests/README.md` documents the current framework architecture, lanes, commands, helper placement, and quality rules.
- `scripts/test.ps1` provides repeatable lane execution for `unit`, `integration`, `topology`, `deploy`, `all`, and `coverage`.
- Existing helper/factory location preserved: `src/Hexalith.Parties.Testing`.

No package references were added because the required framework dependencies are already centrally managed. No test projects were created because the repo already has package-aligned projects for the meaningful boundaries.

Knowledge fragments applied:

- `fixture-architecture`: helpers remain pure and shared helpers belong in `src/Hexalith.Parties.Testing` before adding lifecycle fixtures.
- `data-factories`: shared test data should use override-friendly factories/builders and avoid repeated hardcoded payloads.
- `test-quality`: deterministic, isolated, explicit tests with no sleeps or hidden assertions.
- `test-levels-framework`: prefer unit/component tests for pure domain behavior, service integration tests for boundaries, and topology tests only for Dapr/Aspire behavior.
- `test-priorities-matrix`: security, tenant isolation, GDPR/erasure, command integrity, and deployment access control stay highest-priority coverage.

## Step 4: Documentation And Scripts

Documentation:

- Added `tests/README.md`.
- Included setup/run commands, lane architecture, best practices, quality gates, and official documentation references.

Scripts:

- Added `scripts/test.ps1`.
- The script intentionally keeps the lane list explicit so project ownership remains visible and changes to test topology are reviewed deliberately.

## Step 5: Validation And Summary

Validation result:

- PASS: backend stack identified.
- PASS: selected framework matches existing repo and official xUnit v3 guidance.
- PASS: no conflicting root Playwright/Cypress scaffold introduced.
- PASS: documentation and repeatable scripts created.
- PASS: central package management preserved.
- PASS: `.\scripts\test.ps1 -Lane unit` passed 651 tests across the fast unit/component projects.
- WARN: the framework checklist contains frontend-specific Playwright/Cypress items that do not apply to this backend-only root.
- WARN: no sample test was added because the suite already contains representative tests across domain, projections, security, gateway, admin, sample, integration, and deploy-validation projects. Adding a synthetic example would lower signal.

Artifacts created:

- `tests/README.md`
- `scripts/test.ps1`
- `_bmad-output/test-artifacts/framework-setup-progress.md`

Recommended next workflow:

- Run `RV` to review the existing test estate against these lanes.
- Run `TD` for risk-based coverage planning if a specific epic or feature is next.
