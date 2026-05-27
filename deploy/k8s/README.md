# Hexalith.Parties — Kubernetes Deployment

> **Canonical reference:** For the full cluster topology, configuration sources, operator
> workflow, reproducibility guarantees, and MVP boundaries, see
> [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).

This folder is the operator entry-point for deploying Hexalith.Parties to a Kubernetes
cluster. Today on `main`, it contains the Story 9.2 Aspirate-emitted application service
folders, the Story 9.3 hand-authored Redis carve-out, Story 9.8's FalkorDB carve-out,
Story 9.13's external Keycloak `tache` realm wiring, Story 9.4's Dapr CR
set under `../dapr/`, top-level namespace and Kustomize wiring, the Story 9.5
operator scripts plus shared context helper, and the Story 9.6 static validator at
`../validate-deployment.ps1`.

The image substrate (Zot OCI registry serving `registry.hexalith.com`) is delivered by
**Story 9.1** — see [`../zot/README.md`](../zot/README.md).

---

## Zot credentials

The publish pipeline (Story 9.5 `publish.ps1`) and any developer running `docker pull` from
`registry.hexalith.com` authenticate with the dedicated **`parties-publisher`** account in
the Zot `builders` group. Human admin credentials (`jpiquot`, `qdassivignon`) are reserved
for out-of-band emergency operations (repository deletion, dispute resolution) and are NOT
used by `publish.ps1`, by CI runners, or by any developer in the normal flow.

One-time login (writes the credential to `~/.docker/config.json` under the
`auths["registry.hexalith.com"]` entry):

```bash
docker login -u parties-publisher registry.hexalith.com
# Password: <ask the infra team — cluster-side htpasswd entry is infra-managed>
```

Verify the credential landed in the expected shape (Path B emission — see ADR D-K8s-2):

```bash
jq '.auths["registry.hexalith.com"]' ~/.docker/config.json
# Expected: { "auth": "<base64 user:password>" }
# Forbidden: an entry that delegates to "credsStore" or "credHelpers" — publish.ps1 exits 6.
```

The cluster-side htpasswd file (adding new builders or rotating an admin password) is owned
by the infra team — see [`../zot/README.md`](../zot/README.md) "Out-of-band Secret creation".

---

## Publish + teardown (one-command flow)

`publish.ps1` confirms the active kubectl context, resolves the MinVer image tag, regenerates
the nine Aspirate-owned service folders, preserves Redis/FalkorDB carve-outs, patches Dapr
annotations, Keycloak host aliases, UI credential Secret refs, health probes, and image pull
secrets, verifies Zot image manifests, runs the static deployment validator, initializes Dapr
when needed, validates operator-managed Secrets, applies `deploy/dapr/` in dependency order, then applies this
Kustomize tree.

```pwsh
pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>
```

`teardown.ps1` uses the same shared `-ConfirmContext` gate, deletes the Kustomize workload
set, removes Dapr CRs, deletes operator-managed Secrets, then probes for residual owned
state. Add `-PurgeNamespace` only when the namespace contains no non-story resources. Add
`-PurgeDapr` only when the cluster-wide Dapr control plane should be uninstalled.

```pwsh
pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>
pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name> -PurgeNamespace
pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name> -PurgeDapr
```

The shared `deploy/k8s/_lib/Confirm-KubeContext.ps1` helper compares the active
`kubectl config current-context` against `<name>` and exits 2 on mismatch before cleanup,
publish, delete, Secret, or Dapr mutation. The active context is echoed once at the start of
every run for auditability (per ADR D-K8s-3).

---

> **🗺️ Roadmap — this folder fills in across Stories 9.2 → 9.8**
>
> | Path | Owning story | Purpose |
> |---|---|---|
> | `../zot/` | Story 9.1 | Delivered: Zot OCI registry, credentials, and deployment documentation |
> | `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `sample/`, `sample-blazor-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/` | Story 9.2 + 2026-05-27 correction | Delivered: Aspirate-emitted per-service manifests |
> | `namespace.yaml`, `kustomization.yaml`, `ingress.yaml` | Story 9.2 + 2026-05-27 correction | Delivered: top-level namespace, Kustomize wiring, and UI host ingress |
> | `redis/` | Story 9.3 | Delivered: hand-authored Redis carve-out outside Aspirate regeneration |
> | external `keycloak/keycloak` realm `tache` | Story 9.13 | Delivered: platform Keycloak reused outside the Parties namespace |
> | `falkordb/` | Story 9.8 | Delivered: Memories graph backing-service carve-out outside Aspirate regeneration |
> | `deploy/dapr/` control-plane CRs | Story 9.4 | Delivered: Dapr Components, ACL, Subscriptions, and Resiliency |
> | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1` | Story 9.5 | Delivered: operator scripts + shared context-gate helper |
> | `../validate-deployment.ps1` | Story 9.6 | Delivered: context-free lint for the Dapr and Kubernetes manifest tree |
>
> Track progress in `_bmad-output/implementation-artifacts/sprint-status.yaml`.

---

## Static validation

Run the validator before publishing or attaching manifests to a review:

```pwsh
pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/
pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/ -Format json
```

The validator is read-only and does not use `kubectl`, `helm`, `dapr`, kubeconfig, or
`-ConfirmContext`. It also accepts `--config-path deploy/dapr` for compatibility with the
epic text. Findings use exact category strings and redact credential-shaped values.

---

## Dapr control-plane CRs

`deploy/dapr/` is Source 2 from the canonical
[Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).
Story 9.4 delivers the committed production CR set only; `publish.ps1` applies it in this order:
Components -> Resiliency -> Configurations -> Subscriptions. Do not add the Dapr CRs to
`deploy/k8s/kustomization.yaml`.

`publish.ps1` also owns the control-plane install and runtime activation steps:

- Run `dapr init -k` against the active `kubectl` context unless `-SkipDaprInit` is passed.
- Keep the Dapr control plane in `dapr-system`, never in `hexalith-parties`.
- Treat the canonical Dapr runtime baseline as `1.14.4`; if an existing cluster has a
  different cluster-wide version, warn rather than block.
- Run `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server`, then apply
  `deploy/dapr/resiliency.yaml` before Configurations and Subscriptions; fail publish with
  exit code 1 on validation errors using bounded output.
- Patch generated Deployment annotations from `dapr.io/config: tracing` to the per-service
  configuration names: `eventstore` -> `accesscontrol`, `eventstore-admin` ->
  `accesscontrol-eventstore-admin`, `sample` -> `accesscontrol-sample`, `parties` -> `accesscontrol-parties`, `tenants` ->
  `accesscontrol-tenants`, and `memories` -> `accesscontrol-memories`.
- Preserve `eventstore-admin-ui` and `sample-blazor-ui` as Dapr client-only: each carries
  `dapr.io/enabled: "true"` and `dapr.io/app-id`, but no `dapr.io/app-port` and no
  `dapr.io/config`. `parties-mcp`, `redis`, and `falkordb` remain true
  non-Dapr workloads.

Runtime smoke tests for Story 9.5:

- Unauthorized app id is denied.
- Wrong method or path is denied.
- `eventstore -> parties POST /process` is allowed after annotation patching.
- `tenants -> parties` service invocation is denied.
- Tenant lifecycle events reach `POST /tenants/events`.
- Unsubscribed or non-scoped app ids do not receive tenant lifecycle events.

---

## Hand-authored carve-outs

`redis/` and `falkordb/` are intentionally hand-authored vendor carve-outs. They are not
Aspirate-emitted application folders and should survive every future publish regeneration.

The Story 9.5 clean phase must preserve:

- `deploy/k8s/redis/`
- `deploy/k8s/falkordb/`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/namespace.yaml`
- `deploy/k8s/README.md`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/_lib/`

All other files and folders under `deploy/k8s/` are eligible for cleanup before Aspirate
regeneration, except the nine application service folders after they are regenerated.

Redis is MVP/non-production only: it uses `emptyDir` storage, no AUTH, no replication, and no
HA shape. Data is lost when the Pod is recreated or rescheduled. Production persistence,
AUTH, network policy, and HA hardening are deferred to
[Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md)
section 12.

FalkorDB is also MVP/non-production only: it uses the pinned `falkordb/falkordb:v4.18.6`
container image with `emptyDir` storage and exposes the Redis protocol on `falkordb:6379`.
Memories receives `ConnectionStrings__falkordb=falkordb:6379`; do not point it at the MVP
Redis service.

Keycloak is external to this kustomization. The platform-owned service is
`keycloak/keycloak` on port `8080`, and the realm is `tache` with issuer
`http://auth.tache.ai:8080/realms/tache`. `publish.ps1` reads the Keycloak service ClusterIP
and patches `hostAliases` for `auth.tache.ai` into the generated Parties workloads so OIDC
discovery and JWKS retrieval stay pod-reachable while issuer validation remains unchanged.

`eventstore-admin-ui` and `sample-blazor-ui` use server-side Keycloak direct-access grants.
Operators must pre-create Secret `hexalith-tache-ui-credentials` in namespace
`hexalith-parties` with keys `username` and `password`. `publish.ps1` validates those keys
and patches them as `EventStore__Authentication__Username` and
`EventStore__Authentication__Password` without printing values.

Do not put real values in docs, shell history, manifests, Kustomize generators, or env files.

---

## See also

- Refer to [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) for the canonical cluster topology, image registry policy, Dapr control-plane wiring, and reproducibility contracts.
- [`../zot/README.md`](../zot/README.md) — Zot OCI registry manifest tree (Story 9.1 deliverable).
- ADR **D-K8s-2** in `_bmad-output/planning-artifacts/architecture.md` — Zot pull-secret pipeline + dedicated `parties-publisher` build account.
- ADR **D-K8s-3** in `_bmad-output/planning-artifacts/architecture.md` — `-ConfirmContext` gate (replaces the v1 local-cluster regex allowlist).
- ADR **D-K8s-4** in `_bmad-output/planning-artifacts/architecture.md` — Epic 9 v2 greenfield rewrite rationale.
