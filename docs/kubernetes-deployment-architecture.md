# Kubernetes Deployment Architecture

This document describes the final structure of the Hexalith.Parties deployment on Kubernetes. It captures what runs where, how the pieces connect, how an operator drives a release, and where each piece of configuration lives.

## 1. Overview

Hexalith.Parties is deployed as an 11-workload topology inside a single Kubernetes namespace (`hexalith-parties`). Authentication uses the platform Keycloak service in namespace `keycloak`, realm `tache`. The platform sits on a vanilla Kubernetes cluster with a Dapr control plane handling state, pub/sub, and service-invocation. Container images live in a self-hosted Zot OCI registry. A single PowerShell script (`publish.ps1`) takes the operator from a clean checkout to a healthy cluster in one command.

## 2. Operator Workflow

```
One-time per workstation:
  $ docker login -u parties-publisher registry.hexalith.com

Each release:
  $ git tag v0.2.0
  $ pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local

Result (≈5-10 minutes later):
  11 Parties-owned pods running in the hexalith-parties namespace at tag v0.2.0
```

No separate build / push / generate-manifest / apply ceremony. No `:latest` ambiguity. The MinVer-resolved version stamps every image; the same commit produces the same tag every time.

## 3. Cluster Topology

```
namespace: hexalith-parties

┌──────────────────────────────────────────────────────────────────────┐
│                                                                        │
│  ┌──────────────┐                          ┌──────────────────────┐  │
│  │ external Keycloak: keycloak/keycloak realm tache  │ eventstore-admin-ui  │
│  │ issuer http://auth.tache.ai:8080/realms/tache     │       2/2 Pod        │
│  │ reached through publish-time hostAliases          │ Dapr client-only     │
│  └──────────────────────────────────────────┬─────────┴────────────┘  │
│                                             │ HTTP UI / OIDC          │
│         │                                            │                │
│  ┌──────▼────────────────────────────────────────────▼────────────┐  │
│  │   Hexalith Dapr Services (7 pods, each 2/2 — app + daprd)       │  │
│  │   ────────────────────────────────────────                      │  │
│  │   • eventstore         (event store + projections)               │  │
│  │   • eventstore-admin   (admin commands)                          │  │
│  │   • sample             (EventStore counter sample service)        │  │
│  │   • sample-blazor-ui   (sample browser UI, client-only daprd)     │  │
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
| `eventstore` | 2 (app + daprd) | `registry.hexalith.com/eventstore` | Yes | Event store + projection host |
| `eventstore-admin` | 2 (app + daprd) | `registry.hexalith.com/eventstore-admin` | Yes | Administrative commands on the event store |
| `eventstore-admin-ui` | 2 (app + daprd) | `registry.hexalith.com/eventstore-admin-ui` | Client-only | Browser UI shell; invokes Admin Server through Dapr |
| `sample` | 2 (app + daprd) | `registry.hexalith.com/sample` | Yes | EventStore counter sample domain service |
| `sample-blazor-ui` | 2 (app + daprd) | `registry.hexalith.com/sample-blazor-ui` | Client-only | Browser UI shell for the EventStore sample |
| `parties` | 2 (app + daprd) | `registry.hexalith.com/parties` | Yes | Party aggregate, REST API, primary service |
| `parties-mcp` | 1 | `registry.hexalith.com/parties-mcp` | — | MCP gateway for AI agents |
| `tenants` | 2 (app + daprd) | `registry.hexalith.com/tenants` | Yes | Tenant management surface |
| `memories` | 2 (app + daprd) | `registry.hexalith.com/memories` | Yes | Vector search backend |
| `redis` | 1 | `redis` (vendor) | — | State + pub/sub backing store |
| `falkordb` | 1 | `falkordb/falkordb` (vendor) | — | Graph backing store for Memories |

## 4. Dapr Control Plane

Dapr runs cluster-wide in the `dapr-system` namespace (installed once via `dapr init -k`). Each Hexalith service that needs state, pub/sub, actors, or Dapr service invocation carries a daprd sidecar in its pod. `eventstore-admin-ui` and `sample-blazor-ui` use client-only sidecars for UI -> backend invocation and do not reference state or pub/sub components directly.

### 4.1 Components

| Component | Type | Target | Purpose |
|---|---|---|---|
| `statestore` | `state.redis` | `redis:6379` | Actor state + snapshots + command status |
| `pubsub` | `pubsub.redis` | `redis:6379` (Redis Streams) | Domain-event distribution |
| `resiliency` | resiliency policy | (cluster-wide) | Timeout + retry + circuit-breaker defaults |

### 4.2 Access Control configurations

Each full daprd-equipped service has its own access-control configuration scoping allowed callers and verbs:

- `accesscontrol` (eventstore)
- `accesscontrol-eventstore-admin`
- `accesscontrol-sample`
- `accesscontrol-parties`
- `accesscontrol-tenants`
- `accesscontrol-memories`

These configurations prevent arbitrary cross-service invocation; only the topology-prescribed call paths are allowed (e.g., `parties` -> `tenants`, `parties` -> `eventstore`, but never `tenants` -> `parties`). `accesscontrol-eventstore-admin` also allows `eventstore-admin-ui` to invoke the Admin Server API through Dapr.

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

A Secret `zot-pull-secret` (type `kubernetes.io/dockerconfigjson`) is bootstrapped by `publish.ps1` from the operator's `~/.docker/config.json`. Every Deployment whose container image starts with `registry.hexalith.com/` carries an `imagePullSecrets: [{ name: zot-pull-secret }]` reference. Vendor-image carve-outs (`redis`, `falkordb`) do not need it.

## 6. Operator-Managed Secrets

Two Secrets sit outside the kustomization:

| Secret | Type | Created from | Used by |
|---|---|---|---|
| `hexalith-tache-ui-credentials` | `Opaque` | Operator pre-created, keys `username` and `password` | `eventstore-admin-ui`, `sample-blazor-ui` Keycloak token acquisition |
| `zot-pull-secret` | `dockerconfigjson` | `~/.docker/config.json` (parties-publisher entry) | Every consumer Deployment |

`publish.ps1` validates `hexalith-tache-ui-credentials` by key name only and creates/updates `zot-pull-secret`. It never echoes secret values to stdout, stderr, manifest YAML, or any other observable surface.

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
│  Source 3: deploy/k8s/{redis,falkordb}/ (hand-authored carve-outs)     │
│  ───────────────────────────────────────────────                    │
│  Workloads aspirate either cannot emit cleanly or whose shape must  │
│  diverge from Aspire's defaults (Redis MVP scope: no AUTH / no PVC; │
│  FalkorDB graph backing store).                                     │
│  publish.ps1 preserves these                                        │
│  across regenerations.                                               │
└─────────────────────────────────────────────────────────────────────┘
```

## 8. Build & Deploy Flow

`publish.ps1` executes 22 phases in order. Each phase is bounded — failures surface a clear error and a specific exit code without partial state.

| Step | Action | Failure mode |
|---|---|---|
| 0 | `-ConfirmContext` gate against `kubectl current-context` | Exit 2 on mismatch |
| 1 | Ensure namespace and preflight external Keycloak auth: UI credential Secret keys, service ClusterIP, OIDC discovery, JWKS, token issuer/audience/claims | Exit 1 on missing or invalid external auth contract |
| 2 | Resolve MinVer version via `dotnet msbuild` | Exit 5 on empty / non-SemVer |
| 3 | Clean `deploy/k8s/` (preserves carve-outs, scripts, README) | — |
| 4 | `dotnet aspirate generate` builds + pushes 9 container images to Zot | Propagates aspirate exit code |
| 5 | Strip aspirate placeholder files | — |
| 6 | Patch Dapr annotations: full services get `app-id`, `app-port`, and per-service config; UI services get client-only `enabled` + `app-id` | — |
| 7 | Patch `auth.tache.ai` host aliases to the `keycloak/keycloak` service ClusterIP | Exit 1 on missing service |
| 8 | Patch UI credential `secretKeyRef` entries from `hexalith-tache-ui-credentials` and assert symmetric signing-key refs are absent | Exit 1 on patch failure |
| 9 | Patch `/health` readiness/liveness probes into generated app Deployments | Exit 1 on patch failure |
| 10 | Inject `imagePullSecrets: [{name: zot-pull-secret}]` into Hexalith Deployments | — |
| 11 | Verify all expected per-service folders were emitted | Exit 4 on missing folders |
| 12 | Verify all 9 MinVer image manifests exist in Zot | Exit 6 on missing/unauthorized manifests |
| 13 | Run `deploy/validate-deployment.ps1` against the patched tree | Exit 1 on blocking findings |
| 14 | Run `dapr status -k`, or `dapr init -k` if no healthy control plane exists | Exit 3 on dapr CLI missing |
| 15 | Server dry-run of `deploy/dapr/resiliency.yaml` | Exit 1 on dry-run failure |
| 16 | Create/update `zot-pull-secret` | Exit 6 on Zot credentials |
| 17 | Apply Dapr CRs from `deploy/dapr/` (skipping alternative-backend templates) | — |
| 18 | Reconcile legacy local Keycloak resources from the Parties namespace | Exit 1 on cleanup failure |
| 19 | `kubectl apply -k deploy/k8s/` | — |
| 20 | Restart generated deployments | Exit 1 on rollout restart failure |
| 21 | Wait for all Parties-owned workloads to become Ready | Exit 1 on timeout |

Two minutes after step 21, the 11 Parties-owned pods reach `Ready`.

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
- All 11 Parties-owned workloads via per-folder `kubectl delete -k`.
- The UI Ingress `hexalith-pages-ingress` via an explicit `kubectl delete -f deploy/k8s/ingress.yaml`.
- All Dapr Components, Configurations, Subscriptions, Resiliency CRs.
- The Parties-owned operator-managed Secrets.
- The namespace itself (optional `-PurgeNamespace` switch).

The Dapr control plane in `dapr-system` remains untouched unless `-PurgeDapr` is explicitly passed (it is cluster-wide and may be shared by other projects).

After teardown, the residual-state probe asserts that the `hexalith-parties` namespace contains zero owned resources — any leftover indicates a manual intervention that must be cleaned up before the next publish.

## 11. Reproducibility Guarantees

For a given commit on the `main` branch:

1. **Image tags are deterministic** — the MinVer-resolved version is the same for everyone who runs `publish.ps1` on that commit. Same tag → same image content (subject to identical build chain).
2. **Manifest YAMLs are byte-stable** — for every line except the image-tag line, `kustomize build deploy/k8s/` produces the same bytes across runs at the same commit on the same machine (cross-platform byte-stability is best-effort).
3. **Hand-authored carve-outs survive regeneration** — `redis/` and `falkordb/` Deployments + Services are preserved across `publish.ps1` runs; only the Aspire-composed services are regenerated.
4. **Idempotent re-publish** — re-running `publish.ps1` on an unchanged commit produces zero diff in the cluster.

## 12. Boundaries (Not in the Current Architecture)

These are intentionally outside the current platform shape:

- **Production-grade storage**: Redis is `emptyDir`-backed. State does not survive a Redis pod restart. PVC + StatefulSet + replication are out of scope.
- **External Ingress**: Only browser UI ingress is provisioned: `eventstore.hexalith.com` -> `eventstore-admin-ui` and `sample.hexalith.com` -> `sample-blazor-ui`. API/data-path services remain cluster-internal unless an operator adds a separate gateway.
- **TLS termination on Hexalith services**: Services accept HTTP on port 8080. TLS is terminated at the cluster edge by whatever Ingress controller you bring.
- **Resource limits & autoscaling**: Pods run with default resource requests / limits. HorizontalPodAutoscaler, PodDisruptionBudget, and per-service envelope sizing are deferred to a hardening pass.
- **Observability stack**: OpenTelemetry is wired into the services but no collector (Prometheus, Loki, Tempo, Grafana) is deployed.
- **Image signing & SBOM**: `cosign` signing, SBOM emission, and registry vulnerability scanning are deferred.
- **Multi-cluster / multi-region**: The platform is a single-cluster deploy.
- **Tenant isolation at the infrastructure layer**: All tenants share the same namespace, Redis instance, and EventStore. Per-tenant namespacing is a future concern.

## 13. Public UI Routing

`deploy/k8s/ingress.yaml` is the durable source of truth for browser access:

| Host | Kubernetes backend | TLS |
|---|---|---|
| `eventstore.hexalith.com` | `service/eventstore-admin-ui:8080` | `hexalith-pages-tls` |
| `sample.hexalith.com` | `service/sample-blazor-ui:8080` | `hexalith-pages-tls` |

No backend service is public-routeable through this Ingress. `eventstore`, `eventstore-admin`, `parties`, `tenants`, `sample`, Dapr sidecars, Redis, FalkorDB, and Memories stay internal. The sample UI uses `EventStore__SignalR__HubUrl=http://eventstore:8080/hubs/projection-changes`; that is in-cluster service traffic and is not governed by Dapr ACLs or exposed through public Ingress.

DNS must point both hosts to the nginx ingress endpoint, and Secret `hexalith-pages-tls` must exist in namespace `hexalith-parties` before HTTPS validation. If no in-cluster ingress controller is installed yet, a host-level nginx bridge is allowed only as a temporary operator-owned bridge to `eventstore-admin-ui.hexalith-parties.svc.cluster.local:8080` and `sample-blazor-ui.hexalith-parties.svc.cluster.local:8080`. The exit condition is installing the nginx ingress controller and serving this committed Ingress unchanged.

Routing validation proves only the page shell and host mapping. Authenticated backend workflows remain dependent on Story 9.13 external Keycloak `tache` realm wiring and the `hexalith-tache-ui-credentials` Secret.

## 14. Quick Reference

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
