# Story 9.6: validate-deployment.ps1 Lint Tooling

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story Boundary

Story 9.6 delivers the static deployment lint entry point. It must read the committed or candidate deployment tree, report deterministic findings, and never require access to a Kubernetes cluster. It must not mutate deployment artifacts, install Dapr, apply Kubernetes resources, bootstrap Secrets, or replace the Story 9.7 fitness-test suite.

Ownership split:

| Story | Owns | Must not claim |
|---|---|---|
| 9.2 | Aspire AppHost composition and the seven Aspirate-emitted app folders | Static lint implementation |
| 9.3 | `deploy/k8s/redis/`, `deploy/k8s/keycloak/`, top-level Kustomize carve-out wiring | Changing carve-out MVP scope |
| 9.4 | `deploy/dapr/*.yaml` exact production CR file set | Changing ACL/subscription topology while linting it |
| 9.5 | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1`, operator mutation path | Adding `-ConfirmContext` to validation |
| 9.6 | `deploy/validate-deployment.ps1`, lint output contract, JSON schema, poison-sweep self-check, focused lint tests, docs forward-reference cleanup | Live-cluster tests or full fixture matrix |
| 9.7 | Complete deploy fitness suite, curated fixture trees, LiveCluster opt-in tests | Reworking validate-deployment implementation |

## Story

As an operator preparing a deployment for review,
I want `deploy/validate-deployment.ps1` to lint the committed `deploy/dapr/` and `deploy/k8s/` tree, or a generated candidate tree at a supplied path, and report blocking violations across eight well-defined categories,
so that unsafe or drifted artifacts are caught before they reach a cluster and the lint output is safe to attach to a PR or CI log.

## Acceptance Criteria

### AC1 - Invocation is context-free, non-mutating, and matches the documented command

Given the invocation contract in `_bmad-output/planning-artifacts/epics.md` Story 9.6 and `docs/kubernetes-deployment-architecture.md` section 13,
when the operator runs:

```pwsh
pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
```

then the script accepts the command exactly as written, accepts idiomatic PowerShell equivalents (`-ConfigPath`, `-K8sPath`, `-Format json`), and resolves all paths relative to the repository root unless absolute paths are supplied.

And the script exits with:

| Exit code | Meaning |
|---:|---|
| 0 | Pass, no BLOCKING findings |
| 1 | Fail, at least one BLOCKING finding |
| 2 | Invalid arguments, unsupported format, parse failure, or internal self-check failure |
| 3 | `ConfigPath` or `K8sPath` does not exist |

Exit-code precedence is part of the contract: invalid arguments and self-check failures return `2` before path validation; missing paths return `3` before lint scanning; BLOCKING findings return `1` only after arguments, self-checks, and path validation succeed; pass returns `0` only when scanning completes with no BLOCKING findings.

And the script must not call `kubectl config current-context`, must not require or import `deploy/k8s/_lib/Confirm-KubeContext.ps1`, and must not require a live Kubernetes cluster.

And the script must not write to `deploy/dapr/`, `deploy/k8s/`, candidate paths, docs, or tests. It is a read-only validator.

Developer guardrail: standard PowerShell parameter binding does not bind `--config-path` to `-ConfigPath`. Implement explicit argument normalization or a small pre-parser so the documented GNU-style flag works. Also support `-Output json` as a compatibility alias for existing docs, but emit/help-document `-Format json` as the canonical option.

### AC2 - The eight BLOCKING lint categories are implemented exactly

Given the script runs against a target tree,
then it evaluates these categories and emits one finding per concrete violation:

The category strings below are output API. Human output, JSON output, tests, and documentation examples must use them exactly.

| Category | Severity | Required detection |
|---|---|---|
| `K8sWorkload-MissingImagePullSecret` | `BLOCKING` | Any Deployment with a primary or sidecar container image starting with `registry.hexalith.com/` lacks `spec.template.spec.imagePullSecrets[*].name == "zot-pull-secret"` |
| `K8sWorkload-MissingDaprAnnotations` | `BLOCKING` | Any Deployment for Dapr-equipped app ids `eventstore`, `eventstore-admin`, `parties`, `tenants`, or `memories` lacks one of `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, or `dapr.io/config` on the pod template |
| `K8sWorkload-MissingProbes` | `BLOCKING` | Any Deployment primary application container lacks `readinessProbe` or `livenessProbe`; vendor carve-outs `redis` and `keycloak` are not exempt unless the story implementer records an explicit architecture exception before dev completion |
| `K8sWorkload-NonSemVerTag` | `BLOCKING` | Any consumer image starts with `registry.hexalith.com/` and has an empty, missing, `latest`, `staging-latest`, or non-matching tag for `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$` |
| `K8sWorkload-DirtyTagOnConsumerImage` | `BLOCKING` | Any consumer image starts with `registry.hexalith.com/` and its tag contains `+dirty` build metadata |
| `DaprACL-WildcardAppId` | `BLOCKING` | Any Dapr `Configuration` access-control policy in the supplied `ConfigPath` has `appId: "*"`, an empty app id, or a missing app id under an allow policy |
| `DaprACL-WildcardOperation` | `BLOCKING` | Any Dapr `Configuration` access-control operation in the supplied `ConfigPath` has operation name `"*"`, `"/**"`, or a loose trailing single-star segment such as `"/api/*"`; documented EventStore prefix route-map entries such as `"/api/v1/commands/**"` remain allowed |
| `Secret-Plaintext` | `BLOCKING` | Any Deployment, ConfigMap, Secret, or Dapr Component contains a `value`, `stringData`, literal env value, or component metadata value matching credential-shaped content |

The current production tree should pass after Story 9.5: seven registry-backed application Deployments must have `zot-pull-secret`; exactly five Dapr-equipped Deployments must have Dapr annotations; Redis and Keycloak must not get Dapr annotations or Zot pull secrets.

Scope clarifications:

- A "consumer image" is any image reference beginning with `registry.hexalith.com/` in the supplied `K8sPath`. Vendor images such as Redis and Keycloak are outside the SemVer/Zot pull-secret rules unless they are accidentally changed to the registry prefix.
- Dapr ACL categories scan the raw Dapr `Configuration` manifests under the supplied `ConfigPath`; they do not infer ACLs from rendered Kubernetes annotations.
- The implementation may report multiple findings for the same file when distinct categories are violated.

### AC3 - Findings are bounded, stable, and safe

Given findings are reported in the default human-readable format,
then each finding line is at most 200 characters and uses this shape:

```text
[<severity>] <category> at <file>:<jsonpath> - <reason>
```

And every finding includes a normalized repo-relative file path, a JSONPath-like location, a short reason, and no offending secret value.

And the default output ends with:

```text
[validate] <N> findings (<B> blocking, <W> warnings) - <PASS|FAIL>
```

And warnings are supported by the output schema, but Story 9.6 does not need to introduce any warning category. The expected MVP warning count is usually `0`.

### AC4 - JSON output is CI-stable

Given `-Format json` is supplied,
then stdout is valid JSON with exactly this schema version:

```json
{
  "version": "1",
  "findings": [
    {
      "severity": "BLOCKING",
      "category": "K8sWorkload-NonSemVerTag",
      "file": "deploy/k8s/parties/deployment.yaml",
      "jsonpath": "$.spec.template.spec.containers[0].image",
      "reason": "registry image tag is mutable or not SemVer"
    }
  ],
  "summary": {
    "findings": 1,
    "blocking": 1,
    "warnings": 0,
    "status": "FAIL"
  }
}
```

And no non-JSON banner, progress line, PowerShell warning stream, or debug text is written to stdout in JSON mode.

And schema-breaking changes require bumping `version` away from `"1"`.

### AC5 - Secret detection is fail-closed without leaking values

Given the lint inspects suspected credential-shaped strings,
then `Secret-Plaintext` reports only a shape descriptor in the reason, never the captured literal value.

Required shape descriptors:

| Descriptor | Minimum trigger |
|---|---|
| `jwt-shaped` | Value begins with `eyJ` and has token-like Base64URL segments |
| `password-prefixed` | Key or value contains obvious password/token/secret naming plus a literal non-placeholder value |
| `base64-shaped` | High-entropy Base64-like value long enough to plausibly be a credential |
| `docker-auth-shaped` | Docker config or `.dockerconfigjson` style auth payload in a manifest, ConfigMap, or script-generated YAML |

And `deploy/validate-deployment.ps1` contains a startup self-check that feeds a sentinel value such as `DO-NOT-PRINT-THIS-SECRET-eyJ...` through the formatting path and fails with exit 2 if the sentinel appears in rendered human or JSON output.

And test diagnostics must follow the same rule: fail messages may include category, file, path, and shape descriptor, but not the original suspicious value.

The redaction boundary applies to all output paths: human findings, JSON findings, startup self-check failures, parser errors, invalid-argument diagnostics, and test assertion messages.

### AC6 - Alternative backend templates are skipped

Given `deploy/dapr-alternatives/` exists now or later,
when lint runs with `-ConfigPath deploy/dapr`,
then only files under the supplied config path are scanned.

And `deploy/dapr-alternatives/`, sibling example folders, disabled manifests, overlays, and historical proposal folders are not scanned implicitly, are not treated as part of the production CR set, and cannot produce false positives in a default run.

And if an operator explicitly supplies an alternative path as `-ConfigPath`, that path is scanned as the target; the validator never walks sibling directories by convention or name.

### AC7 - Static lint implementation is robust enough for generated YAML without new production dependencies

Given PowerShell does not ship a native YAML cmdlet in current 7.x releases,
then the implementation must choose one of these approaches and document it in completion notes:

1. Use a small script-local YAML/object scanner tailored to the exact Kubernetes and Dapr manifest shapes in this repo.
2. Use `kubectl kustomize` only as a local renderer when present, without calling current-context or the API server.
3. Add a narrowly justified parser dependency only if it is already present in repo tooling or approved by existing project patterns.

Do not add a project-local NuGet/package dependency solely for the PowerShell script unless the implementation explains why text/object scanning cannot cover the eight bounded categories.

Preferred implementation for Story 9.6 is a script-local scanner tailored to the current repo manifests. If that path is chosen, tests must cover at least: single-resource YAML, multi-document YAML, comments, quoted scalar values, literal env values, `stringData`, component metadata arrays, and indentation variance found in generated Kubernetes manifests.

If `kubectl kustomize` is used, it must be optional local rendering only and tests must prove the script does not call `kubectl config current-context`, `kubectl apply`, `kubectl delete`, `kubectl get` against the API server, or any mutation command. Do not make a live cluster or kubeconfig a prerequisite for the default validator run.

If regex scanning is used, use case-sensitive operators (`-cmatch`, `-creplace`, or explicit .NET regex options) where category names, YAML keys, app ids, and secret paths are case-sensitive. PowerShell regex operators are case-insensitive by default.

### AC8 - Documentation forward references are updated to delivered behavior

Given Story 9.6 is implemented,
then update docs that currently describe the validator as a forward reference:

- `docs/deployment-guide.md`: replace Story 9.6 forward-reference blocks with delivered usage, remove obsolete `-AllowCloudCapabilities` and v1-era category descriptions unless explicitly marked historical.
- `deploy/k8s/README.md`: add `deploy/validate-deployment.ps1` to the roadmap as delivered and include the canonical validation command.
- `docs/getting-started.md`: update only if it contains stale forward-reference or old validate command text.

And docs must use the delivered flags (`-ConfigPath`, `-K8sPath`, `-Format json`) while noting that the script also accepts the epic's `--config-path` compatibility spelling.

### AC9 - Focused tests prove behavior before Story 9.7

Given Story 9.7 owns the full curated fixture suite,
when Story 9.6 is implemented,
then add focused tests under `tests/Hexalith.Parties.DeployValidation.Tests/` that run in the default test pass and do not require a live cluster.

Minimum tests:

- Process-level invocation of `pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` works exactly.
- Valid committed tree returns exit 0 in human format from the repository root and from a non-root working directory.
- Valid committed tree returns JSON schema `version: "1"` with `status: "PASS"`, repo-relative forward-slash file paths, and no absolute paths.
- Each of the eight categories can be triggered from a temp copied or synthetic mini tree and returns exit 1.
- Each category has at least one near-miss fixture that must not trigger the category, for example a literal asterisk in unrelated text, a valid `1.2.3` tag, or a vendor image outside `registry.hexalith.com/`.
- Missing `ConfigPath` or `K8sPath` returns exit 3.
- Invalid arguments or unsupported `-Format` returns exit 2.
- JSON mode writes parseable JSON to stdout with no banner/progress text.
- JSON contract tests assert exact category strings, severity, `file`, `jsonpath`, `reason`, summary counts, and schema version.
- Secret poison values do not appear in human output, JSON output, stderr, parser-error output, or test failure messages.
- The script does not call `kubectl config current-context`, `kubectl apply`, `kubectl delete`, `helm install`, `dapr init`, or any mutation command; if command shims are used, prove only local reads/rendering happen.
- `deploy/dapr-alternatives/` is skipped by default.
- The script source does not import `_lib/Confirm-KubeContext.ps1` and does not contain `-ConfirmContext`.

Use temp directories and command shims where process-level tests are useful. Do not mutate the committed `deploy/` tree from tests.

## Tasks / Subtasks

- [x] Task 1 - Add validator script shell (AC: 1, 3, 4, 7)
  - [x] Create `deploy/validate-deployment.ps1` with `#Requires -Version 7`, strict mode, stable exit codes, repo-root-relative path resolution, and read-only behavior.
  - [x] Implement argument normalization for `--config-path`, `-ConfigPath`, `-K8sPath`, `-Format json`, compatibility alias `-Output json`, missing values, duplicate args, and unknown args.
  - [x] Enforce exit-code precedence: `2` invalid args/self-check, then `3` missing paths, then `1` findings, then `0` pass.
  - [x] Ensure JSON mode emits only JSON to stdout.

- [x] Task 2 - Implement Kubernetes workload lint categories (AC: 2, 3)
  - [x] Detect registry image pull-secret violations at `spec.template.spec.imagePullSecrets`.
  - [x] Detect missing Dapr annotations only for `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories`.
  - [x] Detect missing readiness/liveness probes on primary containers.
  - [x] Detect non-SemVer, missing, mutable, and dirty registry image tags.

- [x] Task 3 - Implement Dapr and secret lint categories (AC: 2, 5, 6)
  - [x] Detect wildcard or missing app ids in Dapr access-control policies.
  - [x] Detect wildcard operations including `"*"`, `"/**"`, and path trailing-wildcard entries.
  - [x] Detect plaintext credential-shaped values without reporting the value.
  - [x] Add the startup poison-sweep self-check across human and JSON renderers.
  - [x] Ensure only the supplied `ConfigPath` is scanned; skip `deploy/dapr-alternatives/` unless explicitly supplied.

- [x] Task 4 - Add focused tests (AC: 1, 4, 5, 6, 9)
  - [x] Add `ValidateDeploymentLintToolingTests.cs` or equivalent under `tests/Hexalith.Parties.DeployValidation.Tests/`.
  - [x] Use temp fixture trees or copied manifests to trigger each category and one near-miss per category.
  - [x] Assert exit-code precedence, output shape, JSON schema, exact category strings, poison redaction, non-root working-directory behavior, and no cluster-context or mutation-command dependency.

- [x] Task 5 - Update operator documentation (AC: 8)
  - [x] Update `docs/deployment-guide.md` forward-reference sections to delivered Story 9.6 behavior.
  - [x] Add validator usage to `deploy/k8s/README.md`.
  - [x] Update `docs/getting-started.md` only if stale validator text exists.

- [x] Task 6 - Verify completion (AC: 1-9)
  - [x] Run `pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/`.
  - [x] Run `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/ -Format json`.
  - [x] Run `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release`.
  - [x] Run `git diff --check`.

### Review Findings

- [x] [Review][Patch] **Align Dapr ACL wildcard contract with documented prefix-wildcard baseline** — Decision resolved 2026-05-22: allow documented `/api/v1/.../**` prefix wildcards while still rejecting bare/global wildcard operations such as `*`, `/**`, and loose trailing `/*`; patched story/tests/validator so AC2 no longer contradicts `deploy/dapr/accesscontrol.yaml`. [deploy/validate-deployment.ps1:351; deploy/dapr/accesscontrol.yaml:22]
- [x] [Review][Patch] **Dapr annotation validation is file-wide instead of pod-template scoped** [deploy/validate-deployment.ps1:254]
- [x] [Review][Patch] **Probe validation is file-wide instead of primary-container scoped** [deploy/validate-deployment.ps1:312]
- [x] [Review][Patch] **Missing Dapr ACL `appId` under an allow policy is not detected** [deploy/validate-deployment.ps1:344]
- [x] [Review][Patch] **Zot pull-secret validation is file-wide instead of Deployment/pod-template scoped** [deploy/validate-deployment.ps1:248]
- [x] [Review][Patch] **Secret scanner misses credential-shaped env/component names with literal values** [deploy/validate-deployment.ps1:389]
- [x] [Review][Patch] **Parser diagnostics can echo base64-shaped secret arguments** [deploy/validate-deployment.ps1:62]
- [x] [Review][Patch] **Redaction self-check does not pass the sentinel through rendered output** [deploy/validate-deployment.ps1:140]
- [x] [Review][Patch] **Case-sensitive scanner guardrail is not consistently followed** [deploy/validate-deployment.ps1:275]
- [x] [Review][Patch] **Focused tests miss required scanner shapes and near-miss coverage** [tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs:109]
- [x] [Review][Patch] **Keycloak liveness probe can restart the pod during realm import/startup** [deploy/k8s/keycloak/deployment.yaml:24]

## Dev Notes

### Current State And Files To Touch

New file expected:

- `deploy/validate-deployment.ps1`: this file was wiped by the Epic 9 v2 greenfield reset and does not exist on `main` at story creation time.

Likely test file:

- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`: new focused process/fixture tests for Story 9.6.

Existing files likely updated:

- `docs/deployment-guide.md`: currently contains multiple "Status (forward reference - Story 9.6)" blocks, old `--output json` examples, an obsolete `-AllowCloudCapabilities` example, and v1-era category wording. Replace with delivered behavior.
- `deploy/k8s/README.md`: currently stops the roadmap at Story 9.5 and does not describe `deploy/validate-deployment.ps1`.
- `docs/getting-started.md`: only update if it contains stale validation wording.

Existing files to read and preserve:

- `deploy/k8s/publish.ps1`: Story 9.5 mutation pipeline; do not move validation into publish unless the user asks later.
- `deploy/k8s/teardown.ps1`: Story 9.5 teardown pipeline; no validator dependency.
- `deploy/k8s/_lib/Confirm-KubeContext.ps1`: intentionally not used by this story.
- `deploy/dapr/*.yaml`: Story 9.4 production CR set to lint, not redesign.
- `deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/deployment.yaml`: registry-backed workload manifests to lint.
- `deploy/k8s/{redis,keycloak}/deployment.yaml`: vendor carve-outs; lint for probes/secrets as specified, but do not add Zot or Dapr expectations.

### Current Deployment Tree Observations

- `deploy/dapr/` currently contains exactly ten production Dapr CR files: `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml`, five `accesscontrol*.yaml`, and two `subscription-*.yaml`.
- `deploy/k8s/` currently contains seven generated application folders, Redis and Keycloak carve-outs, top-level `namespace.yaml`, `kustomization.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`, and `_lib/Confirm-KubeContext.ps1`.
- Story 9.5 tests already exercise operator scripts in `OperatorScriptValidationTests.cs`; keep lint tests separate so failures point to the validator surface.
- Existing deploy-validation tests use xUnit v3, Shouldly, and YamlDotNet in C#. That is acceptable for tests. Do not assume YamlDotNet is available inside PowerShell unless explicitly loaded and justified.

### Architecture Compliance

- Canonical architecture: `docs/kubernetes-deployment-architecture.md` sections 5, 6, 8, 11, and 13.
- ADRs: `_bmad-output/planning-artifacts/architecture.md` D-K8s-2, D-K8s-3, and D-K8s-4.
- PRD: `_bmad-output/planning-artifacts/prd.md` FR31a and FR61.
- Epic source: `_bmad-output/planning-artifacts/epics.md` Story 9.6.
- Sprint-change source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md`.

Do not reintroduce v1 script names as live paths. `regen.ps1`, `deploy-local.ps1`, and `teardown-local.ps1` are superseded history.

Do not use `-ConfirmContext` here. `validate-deployment.ps1` is intentionally static and context-free.

Do not emit real Secret values, Docker config bodies, bearer tokens, cluster URLs, certificate authorities, or JWT-shaped values in findings, JSON, docs, or tests.

### Implementation Guidance

- Prefer small helper functions that return finding objects with fields `Severity`, `Category`, `File`, `JsonPath`, and `Reason`; render from those objects for human and JSON formats.
- Keep category constants in one place so tests can assert the exact eight-category set.
- Path normalization should produce forward-slash repo-relative paths in findings, even on Windows.
- Use stable ordering: sort findings by file, category, and jsonpath before rendering. CI diffs become readable.
- If scanning YAML text directly, limit assumptions to the current manifest shapes and back them with tests. Avoid broad ad hoc parsing for arbitrary Kubernetes YAML.
- If using `kubectl kustomize`, treat it as optional local rendering only; do not require a kubeconfig, current context, or API server. The Kubernetes docs describe `kubectl kustomize` as building resources from a local `kustomization.yaml` directory.
- PowerShell regex operators are case-insensitive by default. Use case-sensitive forms for app ids, category names, and YAML keys where case matters.

### Previous Story Intelligence

Story 9.5 completed in commit `9eab497` and delivered:

- `deploy/k8s/publish.ps1`: 13-step publish pipeline with MinVer resolution, safe cleanup, Aspirate generation, Dapr/JWT/imagePullSecrets patches, Dapr apply ordering, and Secret bootstrap.
- `deploy/k8s/teardown.ps1`: context-gated teardown with residual-state probe and guarded namespace/Dapr purge switches.
- `deploy/k8s/_lib/Confirm-KubeContext.ps1`: shared context gate for mutation scripts only.
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`: focused script tests, currently 22/22 green after review patches.

### Party-Mode Review Patches Applied

Roundtable review on 2026-05-22 tightened this story before dev:

- Winston: define exit-code precedence; narrow Dapr ACL scope to raw `ConfigPath`; clarify "consumer image" as `registry.hexalith.com/*`; keep the validator deterministic and repo-local.
- Amelia: treat category strings and JSON schema as API; require process-level tests for `--config-path`; assert repo-relative file paths and no absolute paths.
- Murat: add adversarial poison tests, near-miss fixtures per category, non-root working-directory tests, and static safety checks against cluster-context or mutation commands.
- Paige: keep canonical docs teaching idiomatic PowerShell flags while documenting `--config-path` as compatibility; state early that validation reads manifests and changes nothing.

Actionable lessons from Story 9.5:

- Tests must execute behavior, not only inspect source text.
- YAML location matters. `imagePullSecrets` must be checked at pod-template scope, not just anywhere in the file.
- Secret handling must be no-decode/no-echo by default; value-shaped diagnostics need explicit redaction tests.
- Exit codes are part of the operator contract and must be asserted.
- PowerShell scripts must resolve paths from the repository root, not the caller's shell directory.

Recent git history:

- `9eab497 feat(deploy): add story 9-5 operator scripts`
- `2da6141 feat(deploy): add story 9-4 dapr manifests`
- `4273b59 feat(deploy): story 9-3 add redis and keycloak carve-outs`
- `6dbcb6e feat(deploy): story 9-2 aspirate apphost composition`
- `12a0f47 feat(deploy): story 9-1 zot registry documentation`

### Latest Technical Information

- Microsoft Learn currently lists PowerShell 7.5.7 as the stable release and 7.6.2 as the current LTS release. This story should keep the repo's existing `#Requires -Version 7` floor and avoid relying on preview features. [Source: https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.5]
- Microsoft's cmdlet history page for PowerShell 7.6 contains no native `ConvertFrom-Yaml` cmdlet entry; do not assume YAML parsing is built into PowerShell. [Source: https://learn.microsoft.com/en-us/powershell/scripting/whats-new/cmdlet-versions?view=powershell-7.6]
- Kubernetes docs describe `kubectl kustomize DIR` as building resources from a directory containing `kustomization.yaml`; this can be used as a local renderer if the implementation chooses that path, but it is not required for all checks. [Source: https://kubernetes.io/docs/reference/kubectl/generated/kubectl_kustomize/]
- PowerShell regex operators use the .NET regex engine and are case-insensitive by default; use case-sensitive variants for lint rules where exact YAML keys and app ids matter. [Source: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_regular_expressions?view=powershell-7.5]
- Kubernetes JSONPath support does not include regular-expression matching, so if a future implementation shells to `kubectl -o jsonpath`, do not rely on JSONPath regex filters. [Source: https://kubernetes.io/docs/reference/kubectl/jsonpath/]

### Common Mistakes To Avoid

- Do not ship a script that ignores the documented `--config-path` invocation.
- Do not add `-ConfirmContext` or call `kubectl config current-context`.
- Do not mutate manifests while validating them.
- Do not print captured credential values in human output, JSON output, exception text, or test assertion messages.
- Do not lint `deploy/dapr-alternatives/` during the default `deploy/dapr` run.
- Do not duplicate Story 9.7's full fixture matrix; add enough focused tests to prove the implementation.
- Do not make C# test dependencies part of the PowerShell runtime path unless explicitly justified.
- Do not treat Redis/Keycloak as registry-backed workloads.
- Do not weaken Story 9.4 Dapr ACLs or Story 9.5 operator scripts while adding validation.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` Story 9.6]
- [Source: `docs/kubernetes-deployment-architecture.md` sections 5, 6, 8, 11, 13]
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-2, D-K8s-3, D-K8s-4]
- [Source: `_bmad-output/planning-artifacts/prd.md` FR31a, FR61]
- [Source: `_bmad-output/implementation-artifacts/9-5-operator-scripts-publish-and-teardown.md` Dev Notes and Dev Agent Record]
- [Source: `deploy/k8s/publish.ps1` current implementation]
- [Source: `deploy/k8s/teardown.ps1` current implementation]
- [Source: `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs`]
- [Source: `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`]
- [Source: `docs/deployment-guide.md` current Story 9.6 forward references]
- [Source: PowerShell Support Lifecycle, https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.5]
- [Source: PowerShell cmdlet history, https://learn.microsoft.com/en-us/powershell/scripting/whats-new/cmdlet-versions?view=powershell-7.6]
- [Source: Kubernetes kubectl kustomize reference, https://kubernetes.io/docs/reference/kubectl/generated/kubectl_kustomize/]
- [Source: PowerShell regular expressions, https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_regular_expressions?view=powershell-7.5]
- [Source: Kubernetes JSONPath reference, https://kubernetes.io/docs/reference/kubectl/jsonpath/]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Red phase: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --no-restore --filter ValidateDeploymentLintToolingTests` failed because `deploy/validate-deployment.ps1` did not exist.
- Focused validator test class: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --no-restore --filter ValidateDeploymentLintToolingTests` passed, 10/10 after code-review patches.
- Required validator human run: `pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` passed with 0 findings.
- Required validator JSON run: `pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/ -Format json` passed with schema version 1 and PASS summary.
- Full deploy-validation suite: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --no-restore` passed, 32/32 after code-review patches.
- Broader solution regression command: `/home/quentindv/.dotnet/dotnet test Hexalith.Parties.slnx -c Release --no-restore` failed only in previously recorded unrelated areas: sample docs Linux path separators (`docs\event-handler-patterns.md`, `docs\event-subscribing.md`), two existing client tests, and three search benchmark threshold tests. Story 9.6 deploy-validation tests passed in that run.
- Whitespace validation: `git diff --check` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented `deploy/validate-deployment.ps1` as a context-free, read-only PowerShell 7 validator with explicit argument normalization, deterministic exit-code precedence, human and JSON output, stable finding ordering, and startup redaction self-check.
- Added the exact eight BLOCKING lint categories for registry-backed Kubernetes workloads, Dapr-equipped workloads, probe coverage, registry tag policy, Dapr access-control wildcards, and credential-shaped plaintext detection.
- Used a small script-local YAML/text scanner tailored to the repository's current Kubernetes and Dapr manifest shapes. No production dependency was added and the script does not call `kubectl`, `helm`, `dapr`, kubeconfig, or the Story 9.5 context helper.
- Updated the committed deployment baseline with pull-secret references, Dapr app-port annotations, and readiness/liveness probes so the required default validator run passes against the checked-in tree.
- Added focused process-level deploy-validation tests for documented invocation, non-root invocation, JSON schema, exit-code precedence, all eight categories, near-misses, poison redaction, alternative-folder scope, and no cluster/mutation-command dependency.
- Replaced Story 9.6 forward-reference documentation with delivered validator usage in the deployment guide and Kubernetes operator README.
- Applied code-review patches: scoped workload checks to YAML documents, pod templates, pod specs, and primary containers; aligned Dapr ACL wildcard rules with the documented prefix-wildcard baseline; detected missing ACL app IDs; hardened secret redaction and parser diagnostics; expanded focused scanner tests; and added Keycloak startup/liveness probe tolerances.
- Broader solution-suite failures remain outside Story 9.6 scope and match previously recorded unrelated failures from recent Epic 9 runs; no validator, deploy manifest, or docs-forward-reference failures remain.

### File List

- `_bmad-output/implementation-artifacts/9-6-validate-deployment-lint-tooling.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/validate-deployment.ps1`
- `deploy/k8s/README.md`
- `deploy/k8s/eventstore/deployment.yaml`
- `deploy/k8s/eventstore-admin/deployment.yaml`
- `deploy/k8s/eventstore-admin-ui/deployment.yaml`
- `deploy/k8s/keycloak/deployment.yaml`
- `deploy/k8s/memories/deployment.yaml`
- `deploy/k8s/parties/deployment.yaml`
- `deploy/k8s/parties-mcp/deployment.yaml`
- `deploy/k8s/redis/deployment.yaml`
- `deploy/k8s/tenants/deployment.yaml`
- `docs/deployment-guide.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`

## Change Log

| Date | Author | Changes |
|---|---|---|
| 2026-05-22 | create-story agent | Created ready-for-dev story context for Story 9.6 validate-deployment lint tooling. |
| 2026-05-22 | party-mode review | Hardened Story 9.6 around exit-code precedence, parser scope, Dapr/image rule scope, adversarial output safety tests, and documentation guidance. |
| 2026-05-22 | dev-story agent | Implemented Story 9.6 validate-deployment lint tooling, focused tests, committed-tree baseline fixes, and delivered documentation updates. |
| 2026-05-22 | code-review agent | Applied 11 review patches, verified validator human/JSON runs, full DeployValidation 32/32, and git diff --check; moved story to done. |
