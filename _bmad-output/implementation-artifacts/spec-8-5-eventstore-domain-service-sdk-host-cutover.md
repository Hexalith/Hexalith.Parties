---
title: '8.5 EventStore domain-service SDK host cutover'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
status: 'done'
review_loop_iteration: 1
followup_review_recommended: false
baseline_commit: '2b209ada295246a8f590928f426ffcd28d764bdc'
baseline_revision: '2b209ada295246a8f590928f426ffcd28d764bdc'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-4-leaf-project-retirement.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings:
  - oversized
---

<intent-contract>

## Intent

**Problem:** The Parties host still hand-maps the EventStore domain-service `/process` endpoint and registers EventStore server-side routing infrastructure even though the EventStore DomainService SDK now owns the canonical host surface. Keeping this path makes Parties look like a platform host and blocks later Epic 8 cleanup.

**Approach:** Move `src/Hexalith.Parties/Program.cs` to `AddEventStoreDomainService` / `UseEventStoreDomainService`, replace the production `PartyDomainServiceInvoker` registration with an SDK-keyed Parties domain processor that preserves Parties-only validation, payload-protection/redaction, and GDPR erasure-status behavior, and record the EventStore submodule-pin proof before consuming the SDK host row.

## Boundaries & Constraints

**Always:** Keep public command/query ingress through the EventStore gateway; preserve `Domain="party"`, `EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName=process`, DAPR ACL deny-by-default, MVP compliance warning middleware, Parties degraded-response middleware, local DAPR health checks, Tenants event subscription mapping, actors needed by existing projection/query/security code, command validation rejection shape, protected current-state unprotection/redaction, erasure retry/status side effects, and no-PII diagnostics.

**Block If:** The SDK cannot route `/process` through a Parties-compatible keyed processor without losing protected-state transformation or erasure-status persistence; adopting SDK service defaults removes `/health`, `/alive`, `/ready`, or `Hexalith.Parties` telemetry without an equivalent local hook; the EventStore submodule pin proof cannot be recorded in the Story 8.3 matrix.

**Never:** Do not migrate projection/query actors, read-model stores, cursor codecs, DataProtection, AppHost publish helpers, MCP/client/UI surfaces, payload-protection engine, or `Hexalith.Parties.Authentication`; do not widen Parties DAPR service-invocation ACLs beyond `eventstore -> POST /process`; do not keep a second hand-mapped `MapPost("/process")` beside the SDK route.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| SDK process route | `POST /process` with a valid `CreatePartyComposite` `Domain="party"` request | SDK endpoint returns `200 OK` with `DomainServiceWireResult`, emits `PartyCreated`, preserves MVP warning header behavior, and routes through the Parties-compatible processor | Unknown domains fail through SDK keyed-processor resolution; no public controller is added |
| Invalid payload | Known Parties command with empty, malformed, or validator-rejected payload | Returns a rejection wire result carrying `PartyCommandValidationRejected` with support-safe `PropertyName` and `ErrorCode` values | Deserialization exception messages and payload fragments are not surfaced |
| Protected state replay | Current state contains protected event payloads or a deleted-key redaction case | Processor unprotects readable payloads, redacts unreadable deleted-key payloads, and rehydrates `PartyState` before aggregate handling | Logs only event type and exception type, never payload or party data |
| Erasure retry/status | `RetryErasureVerification` or erasure lifecycle result events occur | Existing verification retry behavior and `IPartyErasureRecordStore` status updates remain intact | Missing verifier/certificate returns existing rejection/no-op semantics |

</intent-contract>

## Code Map

- `src/Hexalith.Parties/Program.cs` -- host cutover surface; remove manual `/process` map and call SDK host methods while keeping local middleware and subscription mappings.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` -- remove `AddEventStoreServer` and the retired Parties invoker registration, add the Parties SDK processor override/registration, and retain only the EventStore Server projection/rebuild compatibility services still required by local actors until Story 8.6.
- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- new Parties-owned `IDomainProcessor` / `IAggregateReplay` compatibility processor preserving validation, payload protection, erasure side effects, and SDK replay support.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` -- retired server-invoker implementation after equivalent processor behavior exists.
- `src/Hexalith.Parties/Domain/PartyAggregate.cs` -- pin EventStore discovery to the `party` domain explicitly if needed for SDK keyed routing.
- `src/Hexalith.Parties/Hexalith.Parties.csproj` -- remove host-only EventStore server dependency only if no remaining production code in this project needs it after the processor cutover; keep projection/query/security dependencies that belong to later stories.
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml` and `deploy/dapr/accesscontrol-parties.yaml` -- keep ACLs narrow and update comments only if SDK route wording changes.
- `tests/Hexalith.Parties.Tests/Gateway/PartiesProcessEndpointTests.cs` -- update `/process` tests to exercise SDK routing through keyed domain processing rather than fake `IDomainServiceInvoker`.
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` -- update direct test router helpers away from `PartyDomainServiceInvoker`.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` and `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- update host-shape guards and allow only Story 8.5 SDK-host diff shapes.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `_bmad-output/implementation-artifacts/tests/test-summary.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record submodule-pin proof, validation evidence, residual blocked rows, and Story 8.5 status.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- add Story 8.5 EventStore domain-service host proof using `references/Hexalith.EventStore` pin `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`, rollback notes, and planned validation -- satisfies the prerequisite row before source migration.
- [x] `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` and `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` -- move the current invoker behavior into `PartyDomainProcessor : IDomainProcessor`, update constructor/logging names, and delete the retired server-invoker file after callers are moved -- lets the SDK `/process` route call Parties-specific logic without production `IDomainServiceInvoker` registration.
- [x] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` -- remove `AddEventStoreServer` and the retired `PartyDomainServiceInvoker` registration, register the Parties processor for every casing variant of keyed domain `party`, and keep local degraded-response/DAPR health/auth/tenant/crypto/projection-rebuild compatibility services -- prevents server-host reintroduction while retaining unresolved local behavior.
- [x] `src/Hexalith.Parties/Program.cs` -- call `builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly)` and `app.UseEventStoreDomainService()`, remove manual `MapPost("/process")`, avoid duplicate health endpoint mapping, and keep middleware/order plus `MapSubscribeHandler`, `MapEventStoreDomainEvents`, and actor mappings -- completes the host cutover without changing public ingress.
- [x] `tests/Hexalith.Parties.Tests/Gateway/PartiesProcessEndpointTests.cs`, `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`, `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs`, `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`, and `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs` -- update host/process/ACL/platform-guard tests and add parity checks for invalid payloads, protected-state redaction, and erasure status through the SDK path -- proves behavioral equivalence.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record exact commands/results and move Story 8.5 only after validation passes -- keeps BMAD continuity accurate.

**Acceptance Criteria:**
- Given the Parties host source, when inspected after implementation, then it contains `AddEventStoreDomainService` and `UseEventStoreDomainService`, contains no hand-written `MapPost("/process")`, and still documents DAPR-internal subscription routes and the `eventstore -> POST /process` ACL.
- Given a valid Parties command is posted to `/process`, when the SDK route dispatches it, then the response is a successful `DomainServiceWireResult` with the same event/result-payload semantics as before the cutover.
- Given malformed, unresolved, or validator-rejected Parties command payloads, when processed through the SDK route, then `PartyCommandValidationRejected` is returned without exposing payload fragments, stack traces, tenant IDs, user IDs, or party data.
- Given protected current state and erasure lifecycle commands, when processed through the SDK route, then payload unprotection/redaction and erasure record-store updates match the previous invoker behavior.
- Given DAPR access-control YAML in AppHost and deploy manifests, when validated, then Parties remains deny-by-default with only `eventstore` allowed to `POST /process`; SDK `/query`, `/project`, `/replay-state`, and metadata endpoints are not exposed through service invocation in Story 8.5.
- Given Story 8.3 rows for degraded response/DAPR health checks and Aspire publish helpers, when Story 8.5 completes, then those rows remain unresolved/local unless owner parity proof exists; no projection/query/AppHost helper migration is hidden in this story.

## Spec Change Log

## Review Triage Log

- `patch` -- Review found `/replay-state` resolved the keyed Parties processor but failed because the processor did not implement `IAggregateReplay`. `PartyDomainProcessor` now delegates replay ownership to `PartyAggregate`, and `PartiesProcessEndpointTests` covers the SDK replay endpoint.
- `patch` -- Review found the SDK keyed lookup was exact-match while the retired invoker accepted case-insensitive `party` domains. The service registration now covers every casing variant of `party`, and endpoint tests include title, upper, and mixed casing.
- `patch` -- Review found removing `AddEventStoreServer` also removed projection/rebuild runtime dependencies still used before Story 8.6. `AddEventStoreProjectionRuntimeCompatibility` restores the narrow EventStore services, hosted projection discovery/cleanup/poller services, and `AggregateActor` registration required by the retained local projection/rebuild path without reintroducing the old hand-mapped `/process` route or `AddEventStoreServer`.
- `accepted` -- SDK `/query`, `/project`, `/replay-state`, and metadata endpoints are mapped in-process by `UseEventStoreDomainService`, but DAPR service invocation remains ACL-limited to `eventstore -> POST /process`; projection/query exposure remains deferred to Story 8.6.
- `accepted` -- The SDK processor contract does not pass a cancellation token into `IDomainProcessor.ProcessAsync`; the compatibility processor reads the active ASP.NET request token through `IHttpContextAccessor` until EventStore exposes an additive cancellation-aware hook.

## Design Notes

`UseEventStoreDomainService()` maps more endpoints than Story 8.5 exposes through DAPR service invocation. The endpoints can exist in-process, but the ACL remains `/process`-only until Story 8.6 proves query/projection parity.

The current `PartyDomainServiceInvoker` is not pure platform boilerplate. The cutover should remove the old server-invoker surface, not delete its Parties-only behavior. A narrow keyed `IDomainProcessor` compatibility layer is acceptable only as a transitional Story 8.5 bridge; later stories must remove it when EventStore provides equivalent hooks or the behavior moves into approved domain services.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: build passes with 0 warnings.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesProcessEndpointTests` -- expected: SDK `/process`, domain casing, and `/replay-state` parity tests pass.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests` -- expected: processor validation/protection/erasure tests pass.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests` -- expected: EventStore gateway routing remains green.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.ArchitecturalFitnessTests` -- expected: host-shape and ACL guard tests pass.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: prerequisite matrix and Story 8.5 diff guard pass.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- expected: retired leaf project guard tests pass.
- `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: build passes with 0 warnings.
- `dotnet ./tests/Hexalith.Parties.DeployValidation.Tests/bin/Debug/net10.0/Hexalith.Parties.DeployValidation.Tests.dll -class Hexalith.Parties.DeployValidation.Tests.DaprAccessControlFitnessTests` -- expected: ACL remains `/process`-only.
- `git diff --check` -- expected: no whitespace or conflict-marker issues.
- `git -C references/Hexalith.EventStore rev-parse HEAD` -- expected: `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`.

**Result (2026-07-07):** All expected validation commands passed. Final focused results: root test build 0 warnings/0 errors; Parties process endpoint suite 8 passed; domain processor suite 13 passed; EventStore gateway routing 52 passed; architectural fitness 21 passed; platform API prerequisites 10 passed; retired leaf fitness 4 passed; deploy-validation build 0 warnings/0 errors; DAPR ACL fitness 5 passed; `git diff --check` clean. Review fixes added SDK replay support, exact-match domain casing coverage, and the narrow EventStore projection/rebuild runtime compatibility services still required until Story 8.6.
