---
title: Restore localized UI startup in release containers
status: done
route: dispatch
baseline_commit: cdd72b1d4a73013b04e909b5e3d82b59e61386d1
review_loop_iteration: 0
---

## User intent and frozen scope

The user authorized fixing CI/CD failures and running Release with human validation bypassed. Release 37607197353 successfully authenticated through NuGet Trusted Publishing, pushed all nine 1.2.0 packages, and published the three Parties OCI indexes. The UI startup smoke then failed, preventing the GitHub release. Repair the actual container startup failure and run a fresh patch Release. Existing published 1.2.0 packages, images, and tag must remain immutable. No upstream SDK publication, dependency version change, production policy change, or validation suppression is authorized by this followup.

## Evidence

`parties-ui` startup on both architectures throws `CultureNotFoundException` because `en` is unavailable in invariant globalization mode. amd64 exits 139; arm64 times out at the unchanged 180-second liveness limit. The domain and MCP images pass both platform smoke checks. The root `Directory.Build.targets` unconditionally selects `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`. The UI registers FrontComposer localization and runs `UseRequestLocalization`, which constructs supported cultures. Official .NET image documentation supplies `alpine-extra` variants with ICU; the current image index has amd64 and arm64 manifests. This is an application image requirement, not a false publisher exit.

## Implementation and acceptance

Make the root container base image and family conditional defaults, so an explicitly selected project image survives the post-project import. Configure only the UI project for `InvariantGlobalization=false`, `aspnet:10.0-alpine-extra`, and `ContainerFamily=alpine-extra`. Preserve musl runtime identifiers, provenance validation, all three image names, and standard Alpine defaults for the domain and MCP hosts.

Verify effective MSBuild properties for the UI and the two other hosts, build/publish the UI in serialized package-mode Release with isolated artifacts, and inspect the generated runtime configuration for non-invariant globalization. Run existing focused release workflow tests. Review the exact followup diff with blind, edge-case, and verification-gap lenses, acknowledging reused review thread context. Validate the exact Conventional Commit, commit only owned files, and push. Wait for exact-source Commitlint proof, dispatch Release with bypass-validation=true, and approve production under the standing user authorization. The fresh version must be 1.2.1 with all nine NuGet packages independently downloaded and matched to its new exact source. All three container indexes must pass the unchanged OCI validator and amd64/arm64 liveness checks; GitHub Release and downstream exact-source verification must succeed. Record full CI separately: the upstream Tenants source still requires administrator SDK contracts absent from published EventStore 3.115.0.

## Review triage

## Verification

## Remote evidence

## Local verification — 2026-10-07

Effective Release package-mode properties select UI `aspnet:10.0-alpine-extra` / `alpine-extra` / non-invariant globalization, with domain and MCP retaining the standard Alpine image/family. Both musl RID evaluations preserve these choices. The isolated UI Release build passes with zero warnings/errors (4.88 s), and ordinary directory publication passes. Its runtimeconfig explicitly records `System.Globalization.Invariant=false`.

The published UI output ran as the `app` user on the official Alpine extra amd64 image with the same Development liveness posture as the release smoke. `/alive` returned 200 `Healthy`; the task-owned container was removed successfully. The focused release workflow class passes 41/41 with zero skips against the updated root configuration. Receipt: `tests/nuget-trusted-publishing-2026-10-07/ui-globalization-local-verification.json`. Local logs live in `/tmp/parties-ui-globalization-{build,publish,ci-tests,local-startup}.log`.

Blind and verification-gap reviews report no findings. Reused review threads limit independence; the edge lens is performed by the implementation thread and must be described as self-review. The exact full candidate passes pinned commitlint with zero problems/warnings (`/tmp/parties-ui-globalization-commitlint.log`). Actual published OCI identity and both-platform startup remain acceptance evidence for the next protected Release, not a claim based solely on configuration.

Edge-case self-review also reports zero findings after tracing post-project property imports, both musl RID evaluations, explicit base/family precedence, .NET 10 environment precedence, and the official image's environment. All three prescribed lenses are collected; no findings require a patch.

## Verified completion — 2026-10-07

Protected Release [37609885094](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37609885094) succeeded with the user-authorized validation bypass at exact source `34f57baccdf3e9fc2ec615f07499c6d274105851`. Caller-owned NuGet login, preparation, root gitlink validation, publication and downstream exact-source verification all pass. [GitHub Release v1.2.1](https://github.com/Hexalith/Hexalith.Parties/releases/tag/v1.2.1) was published at 2026-10-07T10:59:03Z.

Independent NuGet index requests and package downloads verified all nine 1.2.1 identities/versions, exact repository commit and EventStore 3.115.0 dependencies. The three container indexes pass OCI provenance validation; all six digest-pinned platform startup/liveness checks and their cleanup pass. Receipts: `tests/nuget-trusted-publishing-2026-10-07/registry-publication-1.2.1.json` and `release-1.2.1-summary.json`. Existing 1.2.0 packages/images/tag remain untouched.

Commitlint 37609806697 and CodeQL 37609806812 pass. CI 37609806709 passes Release build, isolated package consumers and 2,713/2,713 build-and-test cases with zero skips. Its required Aspire job remains failed: 35 passed, six existing health skips, one gateway timeout. The launched upstream Tenants source still reports CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`, absent from published EventStore Contracts/DomainService 3.115.0. That unresolved upstream publication boundary belongs to the original full-CI spec, which stays in-review. These scoped trusted-publishing/UI-container acceptance criteria are complete; no claim of green full CI or refreshed platform parity is made.
