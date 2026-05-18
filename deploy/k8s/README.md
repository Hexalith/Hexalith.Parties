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
| `keycloak` | `Aspire.Hosting.Keycloak` | — | Conditional on `EnableKeycloak` (default `true` for `dotnet aspire run`; `regen.ps1` defaults to `false` to keep the manifest set hermetic — pass `-EnableKeycloak true` to include it). |

Hexalith.Memories, Hexalith.FrontComposer, and Hexalith.AI.Tools are **not** wired into `Program.cs` and therefore are not in this manifest set. When a future story wires them, regenerate this directory and the new resources will appear automatically.

## Layout

```
deploy/k8s/
├── README.md
├── regen.ps1                 # Calls `dotnet aspirate generate` with the pinned flags
├── deploy-local.ps1          # One-command local-cluster deploy
├── teardown-local.ps1        # Clean teardown + residual probe
├── kustomization.yaml        # Top-level kustomize entry (aspirate-generated)
├── namespace.yaml            # `hexalith-parties` namespace
├── dapr/                     # DAPR component CRs aspirate emits from `WithReference(...)`
│   ├── statestore.yaml
│   └── pubsub.yaml
├── eventstore/               # Per-app workload (Deployment + Service + per-app kustomization + ConfigMap generator)
├── eventstore-admin/
├── eventstore-admin-ui/
├── parties/
├── parties-mcp/
└── tenants/
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
6. **Keycloak K8s manifests are not generated.** `regen.ps1` defaults `EnableKeycloak=false` even though the AppHost defaults to keycloak enabled, because: (a) aspirate captures Keycloak's randomized admin bootstrap password into the ConfigMap each run, breaking AC1 byte-determinism; (b) shipping a randomly-generated credential value in the committed tree violates the deploy script's no-secret-values contract. Proper Keycloak deployment (pinned admin credential mounted from a `Secret`, not a `ConfigMap`) is deferred to a follow-up story. `dotnet aspire run` (Aspire local orchestration) is unaffected — it still spins keycloak with a per-process random password as Aspire's local-dev default.

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
