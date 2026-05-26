# Kubernetes Deployment Architecture

This document describes the final structure of the Hexalith.Parties deployment on Kubernetes. It captures what runs where, how the pieces connect, how an operator drives a release, and where each piece of configuration lives.

## 1. Overview

Hexalith.Parties is deployed as a 10-workload topology inside a single Kubernetes namespace (`hexalith-parties`). The platform sits on a vanilla Kubernetes cluster (no managed-service dependencies) with a Dapr control plane handling state, pub/sub, and service-invocation. Container images live in a self-hosted Zot OCI registry. A single PowerShell script (`publish.ps1`) takes the operator from a clean checkout to a healthy cluster in one command.

## 2. Operator Workflow

```
One-time per workstation:
  $ docker login -u parties-publisher registry.hexalith.com

Each release:
  $ git tag v0.2.0
  $ pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local

Result (≈5-10 minutes later):
  10 pods running in the hexalith-parties namespace at tag v0.2.0
```

No separate build / push / generate-manifest / apply ceremony. No `:latest` ambiguity. The MinVer-resolved version stamps every image; the same commit produces the same tag every time.

## 3. Cluster Topology

```
namespace: hexalith-parties

┌──────────────────────────────────────────────────────────────────────┐
│                                                                        │
│  ┌──────────────┐                          ┌──────────────────────┐  │
│  │   keycloak   │                          │ eventstore-admin-ui  │  │
│  │   1/1 Pod    │                          │       1/1 Pod        │  │
│  │ (vendor img) │                          │   (no Dapr sidecar)  │  │
│  └──────┬───────┘                          └─────────┬────────────┘  │
│         │ OIDC                                       │ HTTP UI       │
│         │                                            │                │
│  ┌──────▼────────────────────────────────────────────▼────────────┐  │
│  │   Hexalith Core Services (5 pods, each 2/2 — app + daprd)       │  │
│  │   ────────────────────────────────────────                      │  │
│  │   • eventstore         (event store + projections)               │  │
│  │   • eventstore-admin   (admin commands)                          │  │
│  │   • parties        ★   (party aggregate + REST API)              │  │
│  │   • tenants            (tenant management)                       │  │
│  │   • memories           (vector search backend)                   │  │
│  │                                                                  │  │
│  │   All images: registry.hexalith.com/<service>:vX.Y.Z             │  │
│  │   Communication: daprd ↔ daprd via service invocation            │  │
│  └─────────────────────────┬────────────────────────────────────────┘  │
│                            │ Dapr state + pubsub                       │
│                            ▼                                          │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │   redis (1/1, vendor image)                                    │    │
│  │   ────────────────────────                                     │    │
│  │   • Port: 6379                                                 │    │
│  │   • Storage: emptyDir (MVP — no PVC)                          │    │
│  │   • Backing store for Dapr state + pubsub Components          │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │   parties-mcp (1/1, no Dapr sidecar)                          │    │
│  │   ──────────────                                              │    │
│  │   • MCP protocol surface for AI agents                        │    │
│  │   • Calls parties via in-cluster HTTP                         │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │   falkordb (1/1, vendor image)                                 │    │
│  │   ─────────────────────────                                    │    │
│  │   • Port: 6379                                                 │    │
│  │   • Storage: emptyDir (MVP — no PVC)                          │    │
│  │   • Graph backing store for Memories.Server                   │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                        │
└──────────────────────────────────────────────────────────────────────┘
```

### 3.1 Workloads at a glance

| Pod | Containers | Image source | Dapr sidecar | Role |
|---|---|---|---|---|
| `keycloak` | 1 | `quay.io/keycloak/keycloak` (vendor) | — | Identity provider + OIDC issuer |
| `eventstore` | 2 (app + daprd) | `registry.hexalith.com/eventstore` | Yes | Event store + projection host |
| `eventstore-admin` | 2 (app + daprd) | `registry.hexalith.com/eventstore-admin` | Yes | Administrative commands on the event store |
| `eventstore-admin-ui` | 1 | `registry.hexalith.com/eventstore-admin-ui` | — | Browser UI shell (HTTP only) |
| `parties` | 2 (app + daprd) | `registry.hexalith.com/parties` | Yes | Party aggregate, REST API, primary service |
| `parties-mcp` | 1 | `registry.hexalith.com/parties-mcp` | — | MCP gateway for AI agents |
| `tenants` | 2 (app + daprd) | `registry.hexalith.com/tenants` | Yes | Tenant management surface |
| `memories` | 2 (app + daprd) | `registry.hexalith.com/memories` | Yes | Vector search backend |
| `redis` | 1 | `redis` (vendor) | — | State + pub/sub backing store |
| `falkordb` | 1 | `falkordb/falkordb` (vendor) | — | Graph backing store for Memories |

## 4. Dapr Control Plane

Dapr runs cluster-wide in the `dapr-system` namespace (installed once via `dapr init -k`). Each Hexalith service that needs state or pubsub carries a daprd sidecar in its pod.

### 4.1 Components

| Component | Type | Target | Purpose |
|---|---|---|---|
| `statestore` | `state.redis` | `redis:6379` | Actor state + snapshots + command status |
| `pubsub` | `pubsub.redis` | `redis:6379` (Redis Streams) | Domain-event distribution |
| `resiliency` | resiliency policy | (cluster-wide) | Timeout + retry + circuit-breaker defaults |

### 4.2 Access Control configurations

Each daprd-equipped service has its own access-control configuration scoping allowed callers and verbs:

- `accesscontrol` (eventstore)
- `accesscontrol-eventstore-admin`
- `accesscontrol-parties`
- `accesscontrol-tenants`
- `accesscontrol-memories`

These configurations prevent arbitrary cross-service invocation; only the topology-prescribed call paths are allowed (e.g., `parties` → `tenants`, `parties` → `eventstore`, but never `tenants` → `parties`).

### 4.3 Declarative subscriptions

Two subscription manifests wire Redis Streams topics to consumer endpoints:

- `parties-events-sample-tenant` (sample subscriber for `party.*` events)
- `hexalith-parties-tenants-events-parties` (parties consumes tenant lifecycle events)

## 5. Image Registry — Zot

Zot is deployed on the same cluster, in the `zot` namespace, fronted by an nginx Ingress at `registry.hexalith.com` (HTTPS). It serves the OCI Distribution v2 protocol.

### 5.1 Access control

```
htpasswd file mounted from Secret zot-auth-secret
accessControl.groups:
  admins:    [jpiquot, qdassivignon]                       ← push + pull + delete
  builders:  [kaniko, github-ci, parties-publisher]        ← push + pull
```

The `parties-publisher` account is a dedicated build identity. Human operators (`jpiquot`, `qdassivignon`) keep separate admin credentials but do NOT use them for `publish.ps1` — `publish.ps1` reads the `parties-publisher` token from `~/.docker/config.json` exclusively.

### 5.2 Tagging policy

| Tag shape | Form | Source | Meaning |
|---|---|---|---|
| `vMAJOR.MINOR.PATCH` | git tag | tag on `main` (e.g. `v0.2.0`) | Stable release marker. The `v` prefix is MinVer's tag-recognition convention only — image tags drop the `v`. |
| `MAJOR.MINOR.PATCH` | image tag | MinVer-resolved version (e.g. `0.2.0`) | Stable release image tag pushed to `registry.hexalith.com/<app-id>:0.2.0`. |
| `MAJOR.MINOR.PATCH-preview.0.N` | image tag | MinVer auto-bump | Preview commits past the last tag. N = `git rev-list --count v<last>..HEAD`. |
| `MAJOR.MINOR.PATCH-preview.0.N+dirty` | image tag | MinVer with uncommitted changes | `publish.ps1` warns and proceeds (operator opt-in); `validate-deployment.ps1` (Story 9.6) rejects as a blocking lint failure for any tag destined to ship to a real cluster. |

Mutable tags (`latest`, `staging-latest`, empty) are explicitly forbidden for any `registry.hexalith.com/*` image consumed by `deploy/k8s/`; `validate-deployment.ps1` (Story 9.6) treats them as blocking lint failures.

Each image is built once per commit, immutable thereafter (no re-tag, no overwrite). Same commit + same MinVer + same aspirate version → byte-identical image manifest. Re-pushing the same tag to Zot is benign — Zot stores the same digest under the same tag without duplication.

### 5.3 Pull credentials in the cluster

A Secret `zot-pull-secret` (type `kubernetes.io/dockerconfigjson`) is bootstrapped by `publish.ps1` from the operator's `~/.docker/config.json`. Every Deployment whose container image starts with `registry.hexalith.com/` carries an `imagePullSecrets: [{ name: zot-pull-secret }]` reference. Vendor-image carve-outs (`keycloak`, `redis`, `falkordb`) do not need it.

## 6. Operator-Managed Secrets

Three Secrets sit outside the kustomization (imperatively bootstrapped by `publish.ps1` and torn down by `teardown.ps1`):

| Secret | Type | Created from | Used by |
|---|---|---|---|
| `hexalith-jwt-signing` | `Opaque` | Random 32 bytes on first publish | All daprd-equipped services (JWT validation) |
| `hexalith-keycloak-admin` | `Opaque` | Random 24 bytes on first publish | Keycloak bootstrap |
| `zot-pull-secret` | `dockerconfigjson` | `~/.docker/config.json` (parties-publisher entry) | Every consumer Deployment |

All three are **idempotent**: re-running `publish.ps1` does not regenerate them if they already exist. The bootstrap never echoes secret values to stdout, stderr, manifest YAML, or any other observable surface.

## 7. Configuration Sources

There are exactly three sources of truth for the deployed topology. Each one owns a distinct slice.

```
┌─────────────────────────────────────────────────────────────────────┐
│  Source 1: src/Hexalith.Parties.AppHost/Program.cs                  │
│  ──────────────────────────────────────────────                     │
│  Defines the Aspire resource graph (services, containers, projects, │
│  Dapr sidecars, references). Aspirate consumes this to emit the     │
│  Kubernetes Deployment + Service + ConfigMap manifests under        │
│  deploy/k8s/<service>/.                                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│  Source 2: deploy/dapr/*.yaml (hand-authored)                        │
│  ───────────────────────────────────                                 │
│  Dapr Components + Configurations + Subscriptions. Lives outside    │
│  the Aspire graph because Dapr CR shape is opinionated and          │
│  hand-curated. publish.ps1 applies these via kubectl apply after    │
│  validating with --dry-run=server.                                  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│  Source 3: deploy/k8s/{redis,keycloak,falkordb}/ (hand-authored carve-outs) │
│  ───────────────────────────────────────────────                    │
│  Workloads aspirate either cannot emit cleanly or whose shape must  │
│  diverge from Aspire's defaults (Redis MVP scope: no AUTH / no PVC; │
│  Keycloak randomized admin password; FalkorDB graph backing store). │
│  publish.ps1 preserves these                                        │
│  across regenerations.                                               │
└─────────────────────────────────────────────────────────────────────┘
```

## 8. Build & Deploy Flow

`publish.ps1` executes 16 phases in order. Each phase is bounded — failures surface a clear error and a specific exit code without partial state.

| Step | Action | Failure mode |
|---|---|---|
| 0 | `-ConfirmContext` gate against `kubectl current-context` | Exit 2 on mismatch |
| 1 | Resolve MinVer version via `dotnet msbuild` | Exit 5 on empty / non-SemVer |
| 2 | Clean `deploy/k8s/` (preserves carve-outs, scripts, README) | — |
| 3 | `dotnet aspirate generate` builds + pushes 7 container images to Zot | Propagates aspirate exit code |
| 4 | Strip aspirate placeholder files | — |
| 5 | Patch Dapr annotations (`app-id`, `app-port`, per-service config) | — |
| 6 | Patch JWT `secretKeyRef` (`hexalith-jwt-signing`) into 5 consumer Deployments | — |
| 7 | Patch `/health` readiness/liveness probes into generated app Deployments | Exit 1 on patch failure |
| 8 | Inject `imagePullSecrets: [{name: zot-pull-secret}]` into Hexalith Deployments | — |
| 9 | Verify all expected per-service folders were emitted | Exit 4 on missing folders |
| 10 | Verify all 7 MinVer image manifests exist in Zot | Exit 6 on missing/unauthorized manifests |
| 11 | Run `deploy/validate-deployment.ps1` against the patched tree | Exit 1 on blocking findings |
| 12 | Run `dapr status -k`, or `dapr init -k` if no healthy control plane exists | Exit 3 on dapr CLI missing |
| 13 | Ensure namespace + server dry-run of `deploy/dapr/resiliency.yaml` | Exit 1 on dry-run failure |
| 14 | Bootstrap or patch operator-managed Secrets (idempotent) | Exit 6 on missing credentials |
| 15 | Apply Dapr CRs from `deploy/dapr/` (skipping alternative-backend templates) | — |
| 16 | `kubectl apply -k deploy/k8s/` | — |

Two minutes after step 16, the 10 pods reach `Ready`.

## 9. Network & Data Flow (Example: "Create Party")

1. **External client** issues `POST /api/parties/v1/parties` (typically via `kubectl port-forward` or an external Ingress not provisioned in MVP scope).
2. The request hits the `parties` Service (`ClusterIP`) → `parties` pod port 8080.
3. The `parties` app validates the request, then invokes its daprd sidecar on `localhost:3500`.
4. The daprd sidecar:
   - Validates the call against `accesscontrol-parties`.
   - Routes service-invocations: `parties` → `tenants` (tenant lookup) and `parties` → `eventstore` (event append). Memories search-index updates use the in-cluster Memories Service URL when `EnableMemoriesSearch=true`; `accesscontrol-memories` intentionally denies peer Dapr invocation until a concrete route contract is introduced.
   - Persists actor state via the `statestore` Component to Redis.
   - Publishes domain events via the `pubsub` Component to Redis Streams.
5. Subscribers (declarative subscriptions on Redis Streams) wake up and process the events asynchronously.
6. The HTTP response returns to the client with the new party's id.

All inter-service traffic stays inside the namespace via cluster DNS (`<service>.hexalith-parties.svc.cluster.local`). No external service is on the data path.

## 10. Teardown

A single command unwinds everything:

```
$ pwsh deploy/k8s/teardown.ps1 -ConfirmContext kubernetes-admin@cluster.local
```

This removes:
- All 10 workloads via `kubectl delete -k`.
- All Dapr Components, Configurations, Subscriptions, Resiliency CRs.
- The 3 operator-managed Secrets.
- The namespace itself (optional `-PurgeNamespace` switch).

The Dapr control plane in `dapr-system` remains untouched unless `-PurgeDapr` is explicitly passed (it is cluster-wide and may be shared by other projects).

After teardown, the residual-state probe asserts that the `hexalith-parties` namespace contains zero owned resources — any leftover indicates a manual intervention that must be cleaned up before the next publish.

## 11. Reproducibility Guarantees

For a given commit on the `main` branch:

1. **Image tags are deterministic** — the MinVer-resolved version is the same for everyone who runs `publish.ps1` on that commit. Same tag → same image content (subject to identical build chain).
2. **Manifest YAMLs are byte-stable** — for every line except the image-tag line, `kustomize build deploy/k8s/` produces the same bytes across runs at the same commit on the same machine (cross-platform byte-stability is best-effort).
3. **Hand-authored carve-outs survive regeneration** — `redis/`, `keycloak/`, and `falkordb/` Deployments + Services are preserved across `publish.ps1` runs; only the Aspire-composed services are regenerated.
4. **Idempotent re-publish** — re-running `publish.ps1` on an unchanged commit produces zero diff in the cluster.

## 12. Boundaries (Not in the Current Architecture)

These are intentionally outside the current platform shape:

- **Production-grade storage**: Redis is `emptyDir`-backed. State does not survive a Redis pod restart. PVC + StatefulSet + replication are out of scope.
- **External Ingress**: No public Ingress is provisioned for the Hexalith services. Access is via `kubectl port-forward` or a network you add yourself. Only Zot (the registry) and Keycloak (when an Ingress is configured) have external endpoints.
- **TLS termination on Hexalith services**: Services accept HTTP on port 8080. TLS is terminated at the cluster edge by whatever Ingress controller you bring.
- **Resource limits & autoscaling**: Pods run with default resource requests / limits. HorizontalPodAutoscaler, PodDisruptionBudget, and per-service envelope sizing are deferred to a hardening pass.
- **Observability stack**: OpenTelemetry is wired into the services but no collector (Prometheus, Loki, Tempo, Grafana) is deployed.
- **Image signing & SBOM**: `cosign` signing, SBOM emission, and registry vulnerability scanning are deferred.
- **Multi-cluster / multi-region**: The platform is a single-cluster deploy.
- **Tenant isolation at the infrastructure layer**: All tenants share the same namespace, Redis instance, and EventStore. Per-tenant namespacing is a future concern.

## 13. Quick Reference

```
Namespace:              hexalith-parties
Cluster context:        kubernetes-admin@cluster.local (configurable)
Registry:               registry.hexalith.com (Zot)
Dapr control plane:     namespace dapr-system, version 1.14.4
Aspire AppHost SDK:     13.3.3
Aspirate tool:          9.1.0 (pinned in .config/dotnet-tools.json)
MinVer tag prefix:      v
Pre-release default:    preview.0

One-command publish:
  pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>

One-command teardown:
  pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>

Validate the committed tree:
  pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
```
