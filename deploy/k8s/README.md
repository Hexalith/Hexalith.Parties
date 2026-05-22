# Hexalith.Parties — Kubernetes Deployment

> **Canonical reference:** For the full cluster topology, configuration sources, operator
> workflow, reproducibility guarantees, and MVP boundaries, see
> [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).

This folder is the operator entry-point for deploying Hexalith.Parties to a Kubernetes
cluster. Today on `main`, it contains the Story 9.2 Aspirate-emitted application service
folders, the Story 9.3 hand-authored Redis and Keycloak carve-outs, Story 9.4's Dapr CR
set under `../dapr/`, and top-level namespace and Kustomize wiring. Operator scripts and
shared helpers are filled in by **Story 9.5** (see the roadmap callout below).

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

> **Status (forward reference — Story 9.5):** This command becomes available when Story 9.5
> lands. Until then, committed manifests under `deploy/k8s/` and `deploy/dapr/` are
> validated manually with `kubectl kustomize deploy/k8s/`, Dapr CR client dry-runs, and
> the story-specific cleanliness checks.

```pwsh
pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>
```

> **Status (forward reference — Story 9.5):** The teardown command becomes available when
> Story 9.5 lands. The shared `-ConfirmContext` gate (delivered via
> `deploy/k8s/_lib/Confirm-KubeContext.ps1`) compares the active `kubectl config current-context`
> against `<name>` and exits 2 on mismatch — see ADR D-K8s-3.

```pwsh
pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>
```

The active context is echoed once at the start of every run for auditability (per ADR D-K8s-3).

---

> **🗺️ Roadmap — this folder fills in across Stories 9.2 → 9.5**
>
> | Path | Owning story | Purpose |
> |---|---|---|
> | `../zot/` | Story 9.1 | Delivered: Zot OCI registry, credentials, and deployment documentation |
> | `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/` | Story 9.2 | Delivered: Aspirate-emitted per-service manifests |
> | `namespace.yaml`, `kustomization.yaml` | Story 9.2 | Delivered: top-level namespace and Kustomize wiring |
> | `redis/`, `keycloak/` | Story 9.3 | Delivered: hand-authored vendor carve-outs outside Aspirate regeneration |
> | `deploy/dapr/` control-plane CRs | Story 9.4 | Delivered: Dapr Components, ACL, Subscriptions, and Resiliency |
> | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1` | Story 9.5 | Forward reference: operator scripts + shared context-gate helper |
>
> Track progress in `_bmad-output/implementation-artifacts/sprint-status.yaml`.

---

## Dapr control-plane CRs

`deploy/dapr/` is Source 2 from the canonical
[Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).
Story 9.4 delivers the committed production CR set only; Story 9.5 applies it in this order:
Components -> Resiliency -> Configurations -> Subscriptions. Do not add the Dapr CRs to
`deploy/k8s/kustomization.yaml`.

Story 9.5 also owns the control-plane install and runtime activation steps:

- Run `dapr init -k` against the active `kubectl` context unless `-SkipDaprInit` is passed.
- Keep the Dapr control plane in `dapr-system`, never in `hexalith-parties`.
- Treat the canonical Dapr runtime baseline as `1.14.4`; if an existing cluster has a
  different cluster-wide version, warn rather than block.
- Run `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server`, then apply
  `deploy/dapr/resiliency.yaml` before Configurations and Subscriptions; fail publish with
  exit code 1 on validation errors using bounded output.
- Patch generated Deployment annotations from `dapr.io/config: tracing` to the per-service
  configuration names: `eventstore` -> `accesscontrol`, `eventstore-admin` ->
  `accesscontrol-eventstore-admin`, `parties` -> `accesscontrol-parties`, `tenants` ->
  `accesscontrol-tenants`, and `memories` -> `accesscontrol-memories`.

Runtime smoke tests for Story 9.5:

- Unauthorized app id is denied.
- Wrong method or path is denied.
- `eventstore -> parties POST /process` is allowed after annotation patching.
- `tenants -> parties` service invocation is denied.
- Tenant lifecycle events reach `POST /tenants/events`.
- Unsubscribed or non-scoped app ids do not receive tenant lifecycle events.

---

## Hand-authored carve-outs

`redis/` and `keycloak/` are intentionally hand-authored vendor carve-outs. They are not
Aspirate-emitted application folders and should survive every future publish regeneration.

The Story 9.5 clean phase must preserve:

- `deploy/k8s/redis/`
- `deploy/k8s/keycloak/`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/namespace.yaml`
- `deploy/k8s/README.md`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`

All other files and folders under `deploy/k8s/` are eligible for cleanup before Aspirate
regeneration, except the seven application service folders after they are regenerated.

Redis is MVP/non-production only: it uses `emptyDir` storage, no AUTH, no replication, and no
HA shape. Data is lost when the Pod is recreated or rescheduled. Production persistence,
AUTH, network policy, and HA hardening are deferred to
[Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md)
section 12.

Keycloak admin credentials are not committed as YAML. Story 9.5 bootstraps the
`hexalith-keycloak-admin` Secret imperatively. It creates
`KC_BOOTSTRAP_ADMIN_USERNAME=admin` and a random 24-byte
`KC_BOOTSTRAP_ADMIN_PASSWORD` on first publish, then preserves both values on re-publish.
Until that script exists, operators may temporarily precreate the Secret without printing
or committing real values:

```bash
KEYCLOAK_ADMIN_PASSWORD="$(openssl rand -base64 24)"
kubectl create secret generic hexalith-keycloak-admin --dry-run=client -o yaml \
  --from-literal=KC_BOOTSTRAP_ADMIN_USERNAME=admin \
  --from-file=KC_BOOTSTRAP_ADMIN_PASSWORD=<(printf '%s' "$KEYCLOAK_ADMIN_PASSWORD") \
  -n hexalith-parties |
  kubectl apply -f -
unset KEYCLOAK_ADMIN_PASSWORD
```

Do not put real values in docs, shell history, manifests, Kustomize generators, or env files.

---

## See also

- Refer to [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) for the canonical cluster topology, image registry policy, Dapr control-plane wiring, and reproducibility contracts.
- [`../zot/README.md`](../zot/README.md) — Zot OCI registry manifest tree (Story 9.1 deliverable).
- ADR **D-K8s-2** in `_bmad-output/planning-artifacts/architecture.md` — Zot pull-secret pipeline + dedicated `parties-publisher` build account.
- ADR **D-K8s-3** in `_bmad-output/planning-artifacts/architecture.md` — `-ConfirmContext` gate (replaces the v1 local-cluster regex allowlist).
- ADR **D-K8s-4** in `_bmad-output/planning-artifacts/architecture.md` — Epic 9 v2 greenfield rewrite rationale.
