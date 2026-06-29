# Epic 7 Final Readiness Evidence - 2026-06-29

## Decision

Epic 7 implementation evidence is complete, but the repository is not release-ready from this workspace. Release remains blocked until the full solution build, package compatibility lanes, UI accessibility lane, and deploy validation test assembly are either fixed or revalidated in an environment with the required dependency graph and network access.

No source cleanup was performed in Story 7.8. Projection and crypto rollback paths remain intact because the evidence does not prove deletion-safe parity for every rollback and GDPR behavior listed in the Epic 7 architecture.

Epic 7 remains post-MVP platform maintenance. It adds no PRD functional requirement coverage.

## Baseline

- Root baseline commit: `34f7a941997712955dab2d015cf57cdcfda46252` (`34f7a94`).
- Story 7.8 started from sprint status `ready-for-dev`, then moved to `in-progress`.
- Stories 7.1 through 7.7 are `done` in `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Root submodules were inspected with `git submodule status`; no recursive submodule update was run.
- Submodule internal working trees reported no local file changes.

## Story Evidence

| Story | Result | Touched surfaces | Validation evidence | Rollback and residual items |
| --- | --- | --- | --- | --- |
| 7.1 | Done | ADRs, release plan, sprint evidence | Documentation validation complete; source unchanged | Rollback plan established. Broader build was blocked by unrelated FrontComposer target-framework evaluation. |
| 7.2 | Done | Commons service defaults, correlation, ProblemDetails, paging facades | Focused Parties and Commons lanes passed; broad build blocked by referenced repo drift | Restore local wrappers/DI or Commons pointer. |
| 7.3 | Done | Commons `StringHelper` search normalization and similarity helpers | Search tests passed; production search providers consume Commons through Parties shims | UI fixture private `StripDiacritics` remains deferred because routing it to Commons changes UI build surface. |
| 7.4 | Done | Projection adapter boundary and EventStore projection adapter | Projection adapter tests passed after review fix | Local projection infrastructure retained. No EventStore source changes. |
| 7.5 | Done | EventStore rebuild checkpoints, projection freshness mapping, replay behavior | Security/projection lanes passed; full `.slnx` blocked by Tenants/EventStore drift | `LocalPartyProjectionPlatformAdapter`, `PartyProjectionPlatformAdapterMode.Local`, actor companion sequence keys, and `ProjectionRebuildService` retained. |
| 7.6 | Done | Crypto/key-management split ADR and compatibility harness | Full `Hexalith.Parties.Security.Tests` assembly passed 154 tests after review fixes | Production KMS remains prerequisite; no production provider migration. |
| 7.7 | Done | Active EventStore payload protection adapter over Parties AES-GCM service | Full `Hexalith.Parties.Security.Tests` assembly passed 165 tests after review fix | `PartyPayloadProtectionService` retained as AES-GCM implementation and rollback provider. |
| 7.8 | Done | Final readiness artifact, sprint/story records | See validation matrix below | Source cleanup deferred where deletion proof is incomplete. |

## Repository And Package State

| Path | Recorded in root HEAD | Current working tree | Status | Release decision |
| --- | --- | --- | --- | --- |
| `references/Hexalith.AI.Tools` | `993169659a7aa8f1b1dc8444a49d876bbb7175f7` | `993169659a7aa8f1b1dc8444a49d876bbb7175f7` (`9931696`) | Clean | Accept current pointer. |
| `references/Hexalith.Builds` | `f0750ca703cc3ada6eb25050cb6b287e83ce3938` | `cd0a46f979bb77e71c9e12130b3b4bb4a57266fa` (`v4.13.4-19-gcd0a46f`) | Gitlink drift, internal tree clean | Pre-release hygiene: validate owner lane or reset pointer before release. |
| `references/Hexalith.Commons` | `e0819e3b8cab4f55408b7e8c1974d3a84b6eac6b` | `e0819e3b8cab4f55408b7e8c1974d3a84b6eac6b` (`v2.23.0-2-ge0819e3`) | Clean | Accept current pointer. |
| `references/Hexalith.EventStore` | `87c1ec602bbe45770890829c02f17e549cf4c0df` | `9fcc0687d7fad922f7e4901d0989f992cda14f48` (`v3.20.0-1-g9fcc0687`) | Gitlink drift, internal tree clean | Release blocker: full solution build fails in EventStore/Tenants graph. |
| `references/Hexalith.FrontComposer` | `99fab2d260dc6956aac1bd7afad9139f99c51647` | `e2ac85aac67dd515ce7bcad0ea619b8213710898` (`e2ac85aa`) | Gitlink drift, internal tree clean | Release blocker or reset candidate: direct UI test fails against current surface. |
| `references/Hexalith.Memories` | `a6f1bb98c8b2ff16192da226cc667e07842f5ed5` | `24757db93c90427b83195deb5e5a54b092affc2b` (`v1.32.0-1-g24757db`) | Gitlink drift, internal tree clean | Pre-release hygiene: validate owner lane or reset pointer before release. |
| `references/Hexalith.PolymorphicSerializations` | `3f7ca70808300b29e0697a76c8198e67a57a6806` | `1a31416129a5042e89ff13114bab84eb78e70526` (`v1.15.2`) | Gitlink drift, internal tree clean | Pre-release hygiene: validate owner lane or reset pointer before release. |
| `references/Hexalith.Tenants` | `a9cb7f76bee393267c52f3ec5a96c548905027b1` | `671c282d183bc1111a16c88d9ee1761d361e4ce7` (`v2.1.2-33-g671c282`) | Gitlink drift, internal tree clean | Release blocker: full solution build fails in Tenants/EventStore graph. |

Central Package Management remains intact:

- No `.csproj` files contain `PackageReference Version=` or direct `Version=` package attributes.
- Package versions remain centralized in `Directory.Packages.props`.
- No classic `.sln` file was introduced; the repository continues to use `Hexalith.Parties.slnx`.
- No package version changes were made by Story 7.8.

Notable pinned package versions include Dapr `1.18.4`, Aspire `13.4.6`, Microsoft identity packages `10.0.9`, Microsoft.IdentityModel `8.19.1`, FluentValidation `12.1.1`, MediatR `14.1.0`, FluentUI Blazor `5.0.0-rc.3.25302.2`, ModelContextProtocol `1.4.0`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.

## Validation Matrix

| Command | Result | Notes |
| --- | --- | --- |
| `git diff --check` | Pass | No whitespace errors. |
| `bash scripts/check-no-warning-override.sh` | Pass | `OK: no warning-override or nested-submodule regressions detected in active CI/build scripts.` |
| `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/` | Pass | `0 findings (0 blocking, 0 warnings) - PASS`. |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false` | Fail | 115 errors in referenced EventStore/Tenants graph, including missing `Hexalith.Tenants.*` and EventStore contract symbols. Scope: current submodule drift. Release decision: blocker. |
| `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Pass | Focused security project builds cleanly. |
| `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Pass | Sequential rerun passed with 0 warnings and 0 errors. |
| `dotnet tests/Hexalith.Parties.Security.Tests/bin/Release/net10.0/Hexalith.Parties.Security.Tests.dll` | Pass | 165 tests, 0 failed, 0 skipped. |
| `dotnet build src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Focused projection project builds cleanly. |
| `dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Projection test project builds cleanly. |
| `dotnet tests/Hexalith.Parties.Projections.Tests/bin/Release/net10.0/Hexalith.Parties.Projections.Tests.dll` | Pass | 139 tests, 0 failed, 0 skipped. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll` | Pass | 532 tests, 0 failed, 0 skipped. Expected sandbox DAPR/Tenants permission-denied logs were emitted by passing tests. |
| `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Deploy validation test assembly builds cleanly. |
| `dotnet tests/Hexalith.Parties.DeployValidation.Tests/bin/Release/net10.0/Hexalith.Parties.DeployValidation.Tests.dll` | Blocked | Live cluster tests skipped because `KUBECONFIG_TEST_PATH` is not set, then the assembly did not produce a final summary and was interrupted after more than 2 minutes. Exit 130. Release decision: blocker until rerun completes. |
| `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false --no-dependencies` | Pass | UI project builds when isolated from referenced graph drift. |
| `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false --no-dependencies` | Pass | UI tests build when isolated from referenced graph drift. |
| `dotnet tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests.dll` | Fail | 324 tests, 1 failed. `MainLayout_exposes_named_navigation_and_content_landmarks` expected `[data-testid='fc-navigation-full']` or `[data-testid='fc-collapsed-rail']` under navigation. Release decision: blocker or FrontComposer pointer reset/owner validation required. |
| `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false --no-dependencies` | Pass | Client tests build when isolated from referenced graph drift. |
| `dotnet tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests.dll` | Fail | 127 tests, 2 failed. Both failures occur while package tests try to reach NuGet repository signature metadata at `api.nuget.org:443`; network permission denied. Release decision: blocked in sandbox, rerun in network-enabled package validation environment. |
| `dotnet build tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false --no-dependencies` | Pass | Contracts tests build when isolated from referenced graph drift. |
| `dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll` | Fail | 135 tests, 5 failed. Failures are package fixture failures caused by the same NuGet repository signature network denial. Release decision: blocked in sandbox, rerun in network-enabled package validation environment. |
| `dotnet build references/Hexalith.Commons/src/libraries/Hexalith.Commons/Hexalith.Commons.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Commons core builds and packs cleanly. |
| `dotnet build references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/Hexalith.Commons.Http.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Commons HTTP builds cleanly. |
| `dotnet build references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/Hexalith.Commons.ServiceDefaults.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false` | Pass | Commons service defaults build cleanly. |
| `git -C references/Hexalith.Commons diff --check` | Pass | No whitespace errors in Commons. |

## Public Compatibility Notes

- Command/query contracts, public package surfaces, `PagedResult<T>`, projection freshness records, self-scope authorization behavior, DAPR `/process` assumptions, GDPR legal semantics, and UI behavior were not intentionally changed in Story 7.8.
- Focused Parties security, projection, and main test assemblies pass.
- Package compatibility lanes are blocked in this sandbox by NuGet signature metadata network denial, not by a source assertion failure.
- UI RCL compatibility is not fully green because one direct UI accessibility test fails against the current FrontComposer-related navigation test-id expectation.
- Deployment static validation passes, but deploy validation test assembly completion remains blocked by the hanging direct assembly run.

## Cleanup Decisions

| Owner | File or surface | Decision | Reason | Required proof before deletion | Follow-up |
| --- | --- | --- | --- | --- | --- |
| Parties UI / Commons | `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` private `StripDiacritics` | Deferred | Story 7.3 proved production search helper adoption but left UI fixture routing to Commons as a build-surface risk. | UI project build plus relevant bUnit/e2e evidence with the shared helper. | Create UI cleanup story after FrontComposer pointer is validated. |
| Parties / EventStore | `LocalPartyProjectionPlatformAdapter` and `PartyProjectionPlatformAdapterMode.Local` | Preserved | They remain the explicit rollback path through `Parties:Projections:PlatformAdapterMode=Local`. | EventStore-mode proof for replay-from-zero idempotency, rebuild resume/cancel, GDPR processing-record reads, and config rollback replacement. | Create projection rollback retirement story only after owner EventStore orchestration proof. |
| Parties projections | Actor companion `last-sequence` keys | Preserved | They remain active replay-from-zero duplicate/out-of-order and erased/recreated safety guards. | Tests proving replay safety without stale high-water marks across duplicate, out-of-order, erased-party, and recreated-party sequences. | Include in projection rollback retirement story. |
| Parties projections / EventStore | `ProjectionRebuildService` | Preserved | It still owns DAPR actor-state replay mechanics, trusted party-id enumeration, and GDPR processing-record behavior. | EventStore `IProjectionRebuildOrchestrator` replacement preserving local replay, enumeration, GDPR reads, resume/cancel, and rollback. | Create EventStore orchestrator adoption story if desired. |
| Parties security / EventStore | `PartyPayloadProtectionService`, key management, erasure orchestration, certificate handling, and redaction helpers | Preserved | `EventStorePartyPayloadProtectionAdapter` still uses the Parties AES-GCM implementation and rollback provider. | Approved shared provider with compatibility proof for `json+pdenc-v1`, `json-redacted`, legacy unprotected reads, key zeroing, typed unreadable outcomes, no-leak diagnostics, exports, and processing-record redaction. | Create shared crypto provider migration story only after provider approval. |
| Release manager / submodule owners | Builds, EventStore, FrontComposer, Memories, PolymorphicSerializations, Tenants gitlinks | Deferred | Current gitlink drift is not validated as part of Story 7.8 and causes full solution/UI blockers. | Owner validation for each drifted pointer or reset to recorded root baseline. | Resolve before release candidate tagging. |

## Rollback Set

- Utility rollback: restore local wrappers/DI or revert the Commons reference/pointer while preserving public `PagedResult<T>`, correlation, ProblemDetails, and health behavior.
- Search rollback: restore local normalization and Jaro-Winkler shim bodies or revert Commons helper usage; Memories remains optional.
- Projection rollback: set `Parties:Projections:PlatformAdapterMode=Local` while retained, or roll back the EventStore projection adapter commit/pointer. Do not delete local projection paths until deletion proof exists.
- Crypto rollback: restore `IEventPayloadProtectionService` registration to `PartyPayloadProtectionService`, or roll back an approved provider/pointer change. Existing protected payloads must remain readable or safely unreadable.
- Release rollback: return to root baseline `34f7a94` plus recorded submodule/package pointers. If final validation gates fail, keep rollback code and record the blocker instead of deleting it.

## KMS And Data Protection

Epic 7 did not make `LocalDevKeyStorageBackend` production-safe. Regulated EU personal data remains unauthorized without a real KMS or secret-store-backed key provider and the required deployment controls. The Story 7.6 and 7.7 crypto work preserved typed unreadable outcomes and no-leak diagnostics but did not replace production key management.

## Release Blockers

1. Full solution build fails in current referenced EventStore/Tenants graph.
2. Direct UI tests have one failing accessibility/navigation assertion against current UI/FrontComposer surface.
3. Client and contracts package compatibility tests cannot complete in this sandbox because NuGet repository signature metadata access is denied.
4. Deploy validation test assembly builds, but direct execution did not produce a final summary and was interrupted after more than 2 minutes.
5. Drifted root gitlinks must be owner-validated or reset before a release candidate is tagged.
