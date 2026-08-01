---
title: 'Story 8.8 G8-A EventStore Aspire JWT composition surface'
type: 'feature'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 0
baseline_commit: '8c7b4e6ddb2bce9a0d3041cc2e8f3d6153e45c3c'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g8-aspire-publish-helper-routing.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Hexalith.EventStore.Aspire.WithJwtBearerSecurity` requires local Keycloak and cannot express explicit external publish authority/issuer or ordered multi-resource audiences. Consumers therefore duplicate mode selection and cannot identify one owner-approved composition contract.

**Approach:** Add a source-compatible EventStore.Aspire JWT helper that selects local or external validation settings from Aspire execution mode, emits deterministic audience configuration, and accepts no credential material. Prove the application model, commit it locally on EventStore `main`, and record the exact owner identity without adopting it in Parties.

## Boundaries & Constraints

**Always:** Preserve `WithJwtBearerSecurity`; use the Keycloak realm and dependency only in run mode; require explicit HTTPS external authority and issuer in publish mode; clear `Authentication__JwtBearer__SigningKey`; place the primary audience first and de-duplicate valid audiences deterministically; validate before adding annotations; commit only the verified EventStore owner change with a Conventional Commit; record Administrator as approving owner and the resulting SHA.

**Ask First:** Halt if EventStore `main` is not clean at `9b9c776791c149cab26c795a476d23d3d11f7796`, or if work requires a breaking API, push, release, dependency update, Parties gitlink change, or consumer adoption.

**Never:** Do not initialize nested submodules; accept passwords, secrets, signing keys, or tokens in the new options; modify EventStore runtime token validation or any AppHost; modify Parties production code; widen ACLs; mark G8 available/done; implement the deferred client, topology, operations, runtime-validation, or publish-hardening goals.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Local run | Keycloak security, primary and cross-service audiences | Realm URL is authority and issuer; Keycloak reference/wait exists; HTTPS metadata follows local security; signing key is empty | Missing security fails before annotations |
| External publish | Explicit HTTPS authority/issuer and audiences | No Keycloak dependency; HTTPS metadata is true; validation-only environment entries are emitted | Missing, blank, relative, non-HTTPS, or user-info URI fails closed |
| Audience set | Primary plus duplicate/blank values | Primary is index zero; unique values retain first-seen order | Blank values are rejected |
| Compatibility | Existing helper consumer | Existing signature and local behavior remain unchanged | No implicit migration |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs` -- new public validation-only configuration.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs` -- mode-aware composition and compatibility surface.
- `references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs` -- application-model and input-validation proof.
- `references/Hexalith.EventStore/docs/reference/nuget-packages.md` -- public API, operator inputs, limitations, and rollback.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- owner approval, exact commit, producer evidence, deferred proof, and rollback.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs` -- define primary/valid audiences and explicit external publish authority/issuer without credential fields.
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs` -- add execution-mode composition while retaining the legacy helper unchanged.
- [x] `references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs` -- verify all matrix cases, annotation ordering, dependency presence/absence, and prohibited secret fields.
- [x] `references/Hexalith.EventStore/docs/reference/nuget-packages.md` -- document usage, security boundary, deferred runtime/host proof, and rollback.
- [x] `references/Hexalith.EventStore` -- verify and Conventional-Commit on local `main`; push only after separate authorization.
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- record Administrator approval, owner SHA, commands/results/counts, API inventory, rollback pin, and remaining G8-A gaps while retaining `needs-additive-api`.

**Acceptance Criteria:**
- Given run and publish application models, when the helper configures the G8-A audience shapes, then authority, issuer, audience entries, HTTPS metadata, Keycloak dependency, and signing-key clearing match the matrix.
- Given invalid external endpoints or audience entries, when composition is attempted, then it fails before mutating the resource model.
- Given existing EventStore.Aspire consumers, when they compile unchanged, then the legacy helper retains its signature and local behavior.
- Given verified producer evidence, when the proof is recorded, then it names the exact owner commit and approval while Parties remains pinned to `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` and G8 remains blocked on deferred proof.

## Spec Change Log

## Design Notes

Aspire 13.4.6 exposes `resource.ApplicationBuilder.ExecutionContext`, allowing one additive helper to choose mode without duplicating branches in every AppHost. Runtime consumption of emitted valid audiences and actual owner-AppHost publish hardening are explicitly deferred; this slice proves only the reusable composition surface.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreJwtAuthenticationTests` -- expected: all focused tests pass.
- `dotnet build Hexalith.EventStore.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` -- expected: broad owner build passes.
- `git diff --check` and EventStore commitlint checks -- expected: clean diff and valid local owner commit; no push.

**Results:**
- Focused source-mode AppHost test project build: passed with 0 warnings and 0 errors.
- Broad source-mode `Hexalith.EventStore.slnx` build: passed with 0 warnings and 0 errors.
- Focused JWT application-model tests: passed 26/26 from a clean detached exact-commit worktree with
  0 errors, failures, skips, or not-run tests.
- Diff checks and commitlint: passed before and after local owner commit
  `e7cf91fa714b780d60eb129722f4ab82fc7b0b26`; the implementation workflow did not push.
- Clean detached package-dependency AppHost test project build at the owner commit: passed with
  0 warnings and 0 errors; the temporary worktree was removed after validation.
- After separate Administrator authorization, a non-force push integrated concurrent remote `main`
  through `a7af77ecf141278d891b3ef64b03c0da7d9dab74`; the clean integration build passed
  with 0 warnings/errors and all 26 focused tests passed. GitHub reported a direct-main bypass of
  its pull-request rule and seven expected status checks. No release, root gitlink update, or
  consumer adoption was performed.
- Parties rollback pin remains
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; no consumer or gitlink adoption was performed.

## Suggested Review Order

**Mode-aware trust composition**

- Centralizes run-mode Keycloak and publish-mode external authority selection.
  [`HexalithEventStoreSecurityExtensions.cs:167`](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs#L167)

- Rejects repeated application before stale indexed audiences can retain trust.
  [`HexalithEventStoreSecurityExtensions.cs:199`](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs#L199)

- Produces primary-first, trimmed, stable, unique audience configuration.
  [`HexalithEventStoreSecurityExtensions.cs:374`](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs#L374)

- Fails closed for unsafe external authority and issuer URI shapes.
  [`HexalithEventStoreSecurityExtensions.cs:401`](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs#L401)

**Identity, gating, and rollback**

- Records the exact owner commit without claiming root-pin consumption.
  [`story-8-3-platform-api-prerequisite-matrix.md:113`](story-8-3-platform-api-prerequisite-matrix.md#L113)

- Separates clean producer proof from shared-workspace source compatibility.
  [`story-8-3-platform-api-prerequisite-matrix.md:144`](story-8-3-platform-api-prerequisite-matrix.md#L144)

- Preserves deferred consumer, topology, publish, operations, and rollback gates.
  [`story-8-3-platform-api-prerequisite-matrix.md:156`](story-8-3-platform-api-prerequisite-matrix.md#L156)

**Behavioral proof**

- Verifies local realm wiring, dependency ordering, audiences, and signing-key clearing.
  [`HexalithEventStoreJwtAuthenticationTests.cs:18`](../../references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs#L18)

- Proves publish ignores supplied local security and retains external-only dependencies.
  [`HexalithEventStoreJwtAuthenticationTests.cs:59`](../../references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs#L59)

- Exercises endpoint rejection and repeat-application non-mutation boundaries.
  [`HexalithEventStoreJwtAuthenticationTests.cs:144`](../../references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs#L144)

- Guards credential-free options and unchanged legacy-helper behavior.
  [`HexalithEventStoreJwtAuthenticationTests.cs:227`](../../references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs#L227)

**Supporting contract and operations guidance**

- Keeps the public options surface validation-only and credential-free.
  [`HexalithEventStoreJwtAuthenticationOptions.cs:6`](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs#L6)

- Documents usage, security boundary, limitations, and rollback.
  [`nuget-packages.md:266`](../../references/Hexalith.EventStore/docs/reference/nuget-packages.md#L266)

- Defers automated receipt-integrity enforcement to an independently shippable slice.
  [`deferred-work.md:56`](deferred-work.md#L56)
