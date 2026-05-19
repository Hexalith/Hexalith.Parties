# Kubernetes Manifests for Hexalith.Parties

These manifests deploy the **Parties + sibling-submodule topology** to a *local* Kubernetes cluster. They are generated from the Aspire AppHost (`src/Hexalith.Parties.AppHost`) by [aspirate (aspir8)](https://github.com/prom3theu5/aspirational-manifests) and laid out under this directory.

> **Authoritative source:** the Aspire AppHost is the single source of truth for what gets deployed. To add or remove a resource, change `Program.cs` in the AppHost and re-run `regen.ps1`. **Do not hand-edit the YAML files in this directory** — your changes will be erased on the next regeneration and they break the determinism contract (AC1 of story 9-1).

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | `10.0.300` (pinned in `global.json`, `rollForward: latestFeature`) | Required for `dotnet aspirate generate` and `dotnet build`. |
| aspirate | `9.1.0` (pinned in `.config/dotnet-tools.json`) | Installed via `dotnet tool restore`. **Do not install globally.** Targets `net9.0`; runs on .NET 10 via `rollForward: true`. |
| Local Kubernetes cluster | any of: kind, k3d, minikube, Docker Desktop | Managed clusters (AKS / EKS / GKE) are explicitly **rejected** by the deploy/teardown scripts. |
| `kubectl` | recent | Active context must match the local-cluster allowlist below. |
| DAPR CLI | recent | `deploy-local.ps1` installs the DAPR control plane with `dapr init -k` if it is not already present (skip with `-SkipDaprInit`). |
| PowerShell (`pwsh`) | 7+ | All scripts in this directory use `pwsh` cross-platform. |

Verify your toolchain:

```bash
dotnet --version
kubectl version --client
dapr --version
pwsh --version
```

## AppHost-current resources

The set of resources emitted by `regen.ps1` is exactly what `src/Hexalith.Parties.AppHost/Program.cs` composes today:

| App id | Source project | DAPR sidecar | Notes |
|---|---|---|---|
| `eventstore` | `Hexalith.EventStore/src/Hexalith.EventStore` | indirect (via siblings) | Public command/query gateway. |
| `eventstore-admin` | `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host` | — | Admin API behind the Admin UI. |
| `eventstore-admin-ui` | `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI` | — | Generic stream/event browser. |
| `parties` | `src/Hexalith.Parties` | yes (`app-id=parties`, ACL `accesscontrol.parties.yaml`) | Domain actor host. |
| `parties-mcp` | `src/Hexalith.Parties.Mcp` | — | Separate MCP host. |
| `tenants` | `Hexalith.Tenants/src/Hexalith.Tenants` | yes (`app-id=tenants`, ACL `accesscontrol.tenants.yaml`) | Tenant lifecycle authority. |
| `memories` | `Hexalith.Memories/src/Hexalith.Memories.Server` | yes (`app-id=memories`, ACL `accesscontrol.memories.yaml`) | Memories service host. Composed in-cluster as the single endpoint (story 9.3 AC2 / ADR 9.3-2 — the configurable external HTTP `MemoriesEndpoint` escape hatch was removed). |
| `keycloak` | hand-authored `deploy/k8s/keycloak/` (story 9.3 AC4 / ADR 9.3-3, path b) | — | Aspirate cannot emit Keycloak with a stable `secretKeyRef` admin password + realm import; subfolder is preserved by `regen.ps1` `$PreservedNames`. Admin password sourced from `hexalith-keycloak-admin` Secret bootstrapped by `deploy-local.ps1`. |
| `redis` | hand-authored `deploy/k8s/redis/` (story 9.3 AC5) | — | Backing store for state + pubsub Dapr Components (`redis:6379`). MVP scope is `emptyDir`-backed — no PVC / AUTH / TLS. |

Hexalith.FrontComposer and Hexalith.AI.Tools are **not** wired into `Program.cs` and therefore are not in this manifest set. FrontComposer was inspected by Story 9.3 Task 4 — none of `Cli` (dotnet tool), `Mcp` (library), or `Shell` (Razor library) is a deployable service host today; the topology slot is carved out to Story 9.4 `9-4-frontcomposer-deployable-host`. When AI.Tools or FrontComposer ship deployable hosts, regenerate this directory and the new resources will appear automatically.

## Layout

```
deploy/k8s/
├── README.md
├── regen.ps1                 # Calls `dotnet aspirate generate` with the pinned flags
├── deploy-local.ps1          # One-command local-cluster deploy
├── teardown-local.ps1        # Clean teardown + residual probe
├── kustomization.yaml        # Top-level kustomize entry (aspirate-generated)
├── namespace.yaml            # `hexalith-parties` namespace
├── eventstore/               # Per-app workload (Deployment + Service + per-app kustomization + ConfigMap generator)
├── eventstore-admin/
├── eventstore-admin-ui/
├── parties/
├── parties-mcp/
├── tenants/
├── memories/                 # Story 9.3 AC2 — Memories.Server (Dapr-enabled, ACL accesscontrol-memories)
├── keycloak/                 # Story 9.3 AC4 — hand-authored carve-out, preserved by regen.ps1 (path b)
└── redis/                    # Story 9.3 AC5 — hand-authored backing store (emptyDir, redis:6379)
```

Aspirate's emitted layout is **per-app folders at the top level**, not the `deployments/` subfolder originally envisioned by the story spec. This is aspirate's canonical kustomize layout and the deploy script applies it via `kubectl apply -k deploy/k8s/`.

## Regeneration

```bash
# From the repository root:
dotnet tool restore                    # restores aspirate at the pinned version
pwsh deploy/k8s/regen.ps1              # regenerates deploy/k8s/ via aspirate
```

Two consecutive regenerations against the same AppHost commit and pinned aspirate version produce **byte-identical** output (`git diff deploy/k8s/` is empty). If they diverge, the AppHost composition or aspirate's resolved version has changed — investigate before committing.

## Deploying to a local cluster

```bash
# Switch to a local-cluster context first (e.g. `kind-test`, `minikube`, `k3d-local`, `docker-desktop`)
kubectl config use-context kind-test

# Then:
pwsh deploy/k8s/deploy-local.ps1
```

The script:

1. **Refuses to run** if the active `kubectl` context does not match the local-cluster allowlist: `^kind-.*$`, `^k3d-.*$`, `^minikube$`, `^docker-desktop$`. Exit code `2`.
2. Installs the DAPR control plane (`dapr init -k`) if it is missing — skip with `-SkipDaprInit`. The `dapr` CLI is a hard prerequisite when `-SkipDaprInit` is not set; the script exits with code `3` if `dapr` is not on `PATH`.
3. Applies the authoritative DAPR component CRs from `deploy/dapr/` (statestore, pubsub, access control, subscriptions, resiliency, topology). Skips alternative-backend templates (`statestore-*.yaml`, `pubsub-{kafka,rabbitmq,servicebus}.yaml`).
4. Applies the aspirate-generated set via `kubectl apply -k deploy/k8s/` (workloads, services, ConfigMaps, namespace). Aspirate's broken DAPR placeholders are stripped by `regen.ps1` (see "Known aspirate limitations" below) so the kustomize apply only carries workload manifests.
5. Prints a bounded summary (kind + count, no Secret / ConfigMap data values).

## Tearing down

```bash
pwsh deploy/k8s/teardown-local.ps1
# Add -PurgeDapr to also uninstall the DAPR control plane (interactive
# confirmation required; -Force bypasses the prompt for CI).
```

The teardown script enforces the same local-cluster allowlist, deletes via kustomize, removes the authoritative DAPR component CRs, and probes for residual resources in the `hexalith-parties` namespace (Deployments, Services, ConfigMaps, Secrets, DAPR Components / Subscriptions / Configurations / Resiliencies). Platform-owned ConfigMaps (`kube-root-ca.crt`, `default-token-*`) are excluded from the residual list.

**About `-PurgeDapr`:** DAPR is cluster-wide shared state. `dapr uninstall -k --all` removes the `dapr-system` namespace, control-plane Deployments, and CRDs — which breaks ANY other DAPR project running on the same cluster. The teardown script prompts for an explicit `PURGE` confirmation before running it (pass `-Force` to bypass for CI).

## DAPR component parity with `deploy/dapr/`

`deploy/dapr/*.yaml` remains the **authoritative DAPR component source** for Hexalith.Parties. Aspirate emits placeholder CRs from the AppHost's `WithReference(...)` graph but does not translate file-based DAPR configurations (access control, subscriptions, resiliency, real Redis/Kafka backing) into Kubernetes-equivalent CRs. The deploy script therefore applies `deploy/dapr/` directly before the aspirate-generated set.

| `deploy/dapr/` template | `deploy/k8s/dapr/` analog | Component type | Notes |
|---|---|---|---|
| `statestore.yaml` | (stripped by `regen.ps1`) | `state.redis` | **Authoritative local-cluster default.** Targets the Redis StatefulSet that `dapr init -k` provisions in `dapr-system`. Carries `actorStateStore: true` (required for actor state) and `keyPrefix: none` (validated by Story 8.1). Operators on a managed backend overwrite this file with a copy of one of the `statestore-*.yaml` templates below. |
| `pubsub.yaml` | (stripped by `regen.ps1`) | `pubsub.redis` | **Authoritative local-cluster default.** Same Redis backend as the state store. Carries `enableDeadLetter: true` (validated by Story 8.1). Operators on a managed broker overwrite with a copy of one of the `pubsub-*.yaml` templates below. |
| `statestore-cosmosdb.yaml` | — | `state.azure.cosmosdb` | Operator opt-in alternative backend; deploy script skips files matching `statestore-*` (operator copies / renames the chosen backend into `statestore.yaml` before deploy). |
| `statestore-postgresql.yaml` | — | `state.postgresql` | Operator opt-in alternative backend; deploy script skips by default. |
| `pubsub-kafka.yaml` | — | `pubsub.kafka` | Operator opt-in alternative backend. |
| `pubsub-rabbitmq.yaml` | — | `pubsub.rabbitmq` | Operator opt-in alternative backend. |
| `pubsub-servicebus.yaml` | — | `pubsub.azure.servicebus` | Operator opt-in alternative backend. |
| `accesscontrol.yaml` | — | `Configuration` (deny by default, `eventstore-admin` / `tenants` / `parties` allowed) | Authoritative only; aspirate does not emit `Configuration` CRs. Applied directly by `deploy-local.ps1`. |
| `accesscontrol.eventstore-admin.yaml` | — | `Configuration` (locked down, `policies: []`) | Authoritative only. |
| `accesscontrol.parties.yaml` | — | `Configuration` (eventstore → `/process` POST only) | Authoritative only. |
| `accesscontrol.tenants.yaml` | — | `Configuration` (cross-service ACL) | Authoritative only. |
| `subscription-parties.yaml` | — | `Subscription` (parties subscriber routes) | Authoritative only. |
| `subscription-tenants.yaml` | — | `Subscription` (tenants subscriber routes) | Authoritative only. |
| `resiliency.yaml` | — | `Resiliency` (shared retry / circuit breaker policies) | Authoritative only. |
| `topology.yaml` | — | `PartiesTopology` (`hexalith.io/v1` custom CRD) | **Skipped by `deploy-local.ps1` for story 9.1.** The Hexalith CRD is not shipped in this MVP (deferred to Story 9.3+). Apply manually after installing the CRD out-of-band. |
| `tenants-integration.yaml` | — | `TenantsIntegration` (`hexalith.io/v1` custom CRD) | **Skipped by `deploy-local.ps1` for story 9.1.** Same Hexalith CRD gap as above. |

**Drift summary.** `deploy/dapr/` is the single authoritative source for every DAPR Component, Configuration, Subscription, Resiliency, and Topology CR. Aspirate's broken DAPR placeholders for statestore / pubsub are stripped by `regen.ps1` (see "Known aspirate limitations") so the deploy carries exactly one copy of each Component, with the correct backing-store metadata. Operators switching to a managed backend overwrite `deploy/dapr/statestore.yaml` or `deploy/dapr/pubsub.yaml` with a copy of the matching variant template.

Automated lint of this parity table is **Story 9.2's responsibility**; this story's contract is the manual table above plus the file inspection covered by `K8sManifestGenerationTests`.

## Known aspirate limitations (recorded for this story)

These are accepted behaviors of aspirate 9.1.0 and not regressions in the Parties code base.

1. **Aspire manifest side effect.** `dotnet aspirate generate` writes an intermediate `src/Hexalith.Parties.AppHost/manifest.json` as a build artifact. `regen.ps1` cleans it after a successful run, and `.gitignore` covers it as defense-in-depth.
2. **DAPR component CRs are broken placeholders.** Aspirate emits `dapr/statestore.yaml` with `metadata: []` (no `redisHost`, no `actorStateStore: true`) and `dapr/pubsub.yaml` with `spec.type: pubsub` (invalid -- DAPR requires `pubsub.<backend>`). If applied, these would override the authoritative Redis Components in `deploy/dapr/`. `regen.ps1` strips them after aspirate emits, and the kustomization references are removed in the same step. The authoritative `deploy/dapr/statestore.yaml` and `deploy/dapr/pubsub.yaml` carry the correct `redisHost`, `actorStateStore: true`, and `enableDeadLetter: true` metadata; `deploy-local.ps1` applies them directly. Story 9.2 may add a determinism fitness assertion that `deploy/k8s/dapr/` does not exist post-regen.
3. **DAPR annotations are patched post-aspirate.** Aspirate 9.1.0 emits `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/config: tracing` (its hardcoded default), and `dapr.io/enable-api-logging`. It does **not** emit `dapr.io/app-port` and it does **not** translate the AppHost's `DaprSidecarOptions.Config = <accesscontrol path>` into a per-app `dapr.io/config: <Configuration-name>` annotation. `regen.ps1` patches both annotations into every DAPR-enabled Deployment (`eventstore`, `eventstore-admin`, `parties`, `tenants`) so the sidecar callbacks reach port 8080 and the per-app access-control `Configuration` CRs are actually consulted. The patch is deterministic and idempotent.
4. **No `Subscription`, `Resiliency`, or `Configuration` CRs are emitted.** Those come from `deploy/dapr/` as described above.
5. **Hexalith custom CRDs (`topology.yaml`, `tenants-integration.yaml`) are skipped at apply time.** They use `apiVersion: hexalith.io/v1`, and the CRDs themselves are not shipped in story 9.1 (deferred to Story 9.3+). `deploy-local.ps1` and `teardown-local.ps1` skip these files; the deploy proceeds without the custom CRs on a vanilla local cluster. Apply manually after installing the CRDs out-of-band if you need them.
6. **Keycloak K8s manifests are hand-authored, not generated (Story 9.3 AC4 / ADR 9.3-3, path b).** `regen.ps1` defaults `EnableKeycloak=false` for aspirate emission because aspirate `9.1.0` cannot express (a) Keycloak admin credential via stable `secretKeyRef` (it captures a randomized value into the emitted ConfigMap each regen — breaks byte-determinism + violates the no-secret-values contract), (b) `WithRealmImport` realm-bootstrap ConfigMap, (c) consumer-side JWT `secretKeyRef` envFrom. Story 9.3 closed the gap with a hand-authored `deploy/k8s/keycloak/` set preserved by `regen.ps1`'s `$PreservedNames` and excluded from the byte-determinism contract on that subfolder only. The admin password is sourced from a `hexalith-keycloak-admin` Secret bootstrapped by `deploy-local.ps1` (random per-deploy, idempotent, never committed). JWT signing keys are sourced via `secretKeyRef` patched into consumer Deployments by a new `regen.ps1` post-aspirate step (mirroring the existing Dapr-annotation patching block). `dotnet aspire run` (Aspire local orchestration) is unaffected — it still spins keycloak with a per-process random password as Aspire's local-dev default. This carve-out is documented as architectural debt for Epic 10 tool-choice review.

## JWT signing key bootstrap (Story 9.3 AC4)

`deploy-local.ps1` bootstraps two operator-managed Secrets in the `hexalith-parties` namespace immediately after `dapr init -k`:

- `hexalith-jwt-signing` — 32-byte cryptographically random base64 key under the `Authentication__JwtBearer__SigningKey` data key. Consumer Deployments (`eventstore`, `eventstore-admin`, `parties`, `parties-mcp`, `tenants`) source their JWT signing key from this Secret via `valueFrom.secretKeyRef`. The Secret name is fixed; rotating it requires `kubectl rollout restart` on the consumer Deployments and is operator-facing (depth of detail beyond a one-liner is post-MVP).
- `hexalith-keycloak-admin` — 24-byte cryptographically random base64 password under the `KEYCLOAK_ADMIN_PASSWORD` data key. The Keycloak Deployment sources its admin password from this Secret via `valueFrom.secretKeyRef`.

Both Secrets are applied via `kubectl apply --dry-run=client -o yaml | kubectl apply -f -` (idempotent: first invocation creates, subsequent invocations leave existing Secrets in place). Random material is generated in-process via `System.Security.Cryptography.RandomNumberGenerator`; never echoed to stdout, stderr, or any log.

## Dapr resiliency CRD schema drift (Story 9.3 AC6)

`deploy/dapr/resiliency.yaml` was rewritten against the Dapr 1.14.4 `resiliencies.dapr.io` CRD. Two field-shape fixes vs the legacy form:

1. `spec.policies.timeouts` is a flat `name: Duration` map (legacy nested `timeouts.daprSidecar.general: 5s` is rejected as `unknown field`).
2. `spec.targets.components.<name>.{retry,timeout,circuitBreaker}` must be split into `outbound`/`inbound` for component targets that have both directions (statestore reads/writes, pubsub publish/subscribe).

`deploy-local.ps1` runs `kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml` immediately after `dapr init -k` so any future Dapr CRD upgrade that drops or renames a field surfaces here (exit 1) before any namespaced resource is applied. The matching static lint `K8sDapr-ResiliencyCrdSchemaDrift` in `validate-deployment.ps1` catches drift in the committed file without requiring a live cluster.

## K8s manifest lint

Story 9.2 extends `deploy/validate-deployment.ps1` with a Kubernetes-manifest lint that runs alongside the existing DAPR config checks. It is **read-only** (no `kubectl`, no DAPR install) and inspects the aspirate-generated tree under `deploy/k8s/` plus the authoritative DAPR templates under `deploy/dapr/`.

```pwsh
pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
pwsh deploy/validate-deployment.ps1 -K8sPath deploy/k8s/ --output json
```

The lint emits one finding per category code. Findings are sorted ascending by `(category, code, target)` so two consecutive runs against the unchanged tree produce byte-identical JSON modulo the `timestamp` field.

| Category code | Severity | What fires |
|---|---|---|
| `K8sWorkload-MissingImage` | fail | Deployment container image is missing, empty, `[]`, or `null`. |
| `K8sWorkload-MissingDaprAnnotation` | fail | DAPR-enabled app (`eventstore`, `eventstore-admin`, `parties`, `tenants`) missing one of `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, `dapr.io/config`. |
| `K8sWorkload-UnresolvedConfigMapRef` | fail | `envFrom.configMapRef.name` does not match any `configMapGenerator.name` in the same app folder. |
| `K8sWorkload-UnresolvedKustomizationResource` | fail | Top-level `kustomization.yaml` `resources:` entry that does not resolve under `deploy/k8s/`. |
| `K8sWorkload-MissingProbes` | warn | Deployment missing both `readinessProbe` and `livenessProbe`. Hardening deferred to Story 9.3. |
| `K8sWorkload-MissingResources` | warn | Container missing `resources.requests`/`resources.limits` for `cpu` + `memory`. Hardening deferred to Story 9.3. |
| `K8sWorkload-LatestImageTag` | warn | Container image uses `:latest`. Pin to a semver, build-id, or `@sha256:` digest. Hardening deferred to Story 9.3. |
| `DAPR-ACL-DefaultActionNotDeny` | fail | An `accesscontrol*.yaml` Configuration CR has `defaultAction` other than `deny`. |
| `DAPR-ACL-WildcardAppId` | fail | An ACL policy uses a wildcard `appId: '*'` / `'**'`. |
| `DAPR-Subscription-MissingDeadLetter` | fail | A Subscription CR is missing `deadLetterTopic`. |
| `DAPR-Subscription-WrongPubsubName` | fail | A Subscription CR uses `pubsubname` other than `pubsub`. |
| `DAPR-Regen-PlaceholderNotStripped` | fail | `deploy/k8s/dapr/statestore.yaml` or `deploy/k8s/dapr/pubsub.yaml` re-appeared (regen invariant violated). |
| `K8sSecret-PlaintextCredential` | fail | A `password=`/`secret=`/`token=`/`api_key=` shape with a non-placeholder value. |
| `K8sSecret-UrlEmbeddedCred` | fail | URL with embedded user:password (`postgres://user:pw@host`). |
| `K8sSecret-JwtTokenLiteral` | fail | JWT literal (`eyJ...` three-segment base64). |
| `K8sSecret-AwsAccessKey` | fail | AWS access key (`AKIA[0-9A-Z]{16}`). |
| `K8sSecret-AzureConnString` | fail | Azure connection string (`DefaultEndpointsProtocol=https;...`). |
| `K8sSecret-PrivateKey` | fail | PEM private-key header. |
| `K8sSecret-StaticTenantId` | fail | `Tenants__TenantId`/`TENANT_ID`/`*__TenantId` with a literal value (must use `{env:VAR}` or `valueFrom`). |
| `K8sSecret-CommittedSecretValue` | fail | A `kind: Secret` whose `stringData`/`data` carries a non-placeholder value. |
| `K8s-NonLocalClusterCapability` | fail (warn with `-AllowCloudCapabilities`) | Cloud-only `StorageClass`/`IngressClass`/`Service.type: LoadBalancer`. |
| `K8s-YamlParseError` | fail | A YAML document failed to parse; the offending content is **not** echoed. |
| `K8s-PathTraversal` | fail | A symbolic link under `deploy/k8s/` resolves outside the repo. |

The lint runs in `console` and `--output json` modes (same `--output` switch as the Story 8.1 DAPR validator). The exit-code contract is unchanged: `0` = pass (warnings OK), `1` = at least one blocking failure, `2` = invalid arguments / path not found. Pass `-AllowCloudCapabilities` (or `--allow-cloud-capabilities`) to demote `K8s-NonLocalClusterCapability` findings to warn (post-MVP).

**Known gap (the as-committed tree intentionally has these warns).** The aspirate-emitted Deployments today carry no probes, no resource requests/limits, and a `:latest` image tag. They surface as `K8sWorkload-MissingProbes`, `K8sWorkload-MissingResources`, and `K8sWorkload-LatestImageTag` warnings — they document the hardening gap but do not fail the lint. Patching the manifests to clear these warns is deferred to a follow-up "Aspirate output hardening" story (Story 9.3 candidate) that owns the per-service profiling and immutable-tag publishing decisions.

The validator output never echoes raw env-var values, `Secret.data`/`Secret.stringData` contents, ConfigMap literal values, tenant identifiers, bearer/JWT tokens, or DAPR access-control policy bodies. Offending values are reported as `<redacted:N chars at <file>:<line>>`. Control characters in file paths are sanitized to `?`.

## Out of MVP scope

The following are **out of scope** for story 9-1 and the local-cluster MVP. Future stories own them.

- Managed-cloud deploy (AKS / EKS / GKE) and any cloud-specific storage class, registry secret, image pull secret, or ingress controller integration.
- TLS termination beyond `kubectl port-forward`.
- Ingress controllers (NGINX / Traefik / Istio gateway), TLS issuer CRDs, or DNS wiring.
- Automated DAPR component parity lint — Story 9.2.
- CI step that runs `regen.ps1` and diffs against committed manifests — explicitly deferred per `sprint-change-proposal-2026-05-18.md`.
- Custom container registries / private pull secrets — `regen.ps1` defaults to `registry.hexalith.com`; override via `-ContainerRegistry`.

## Troubleshooting

- **`regen.ps1` errors with `aspirate is not a tool`.** Run `dotnet tool restore` from the repository root.
- **`deploy-local.ps1` exits with code `2`.** Active `kubectl` context is not in the allowlist. Switch with `kubectl config use-context <local-context-name>`.
- **`kubectl apply -k` fails with namespace conflict.** Run `pwsh deploy/k8s/teardown-local.ps1` first to remove a previous deployment.
- **DAPR sidecars not starting.** Verify `dapr status -k` shows the control plane is `Running`. Re-run `deploy-local.ps1` without `-SkipDaprInit`.
- **`regen.ps1` produces unexpected diff.** Confirm the pinned aspirate version (`.config/dotnet-tools.json`) matches what the AppHost expects. Diff is almost always a sign that `Program.cs` changed.
