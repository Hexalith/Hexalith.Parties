# Hexalith.Parties — Kubernetes Deployment

> **Canonical reference:** For the full cluster topology, configuration sources, operator
> workflow, reproducibility guarantees, and MVP boundaries, see
> [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).

This folder is the operator entry-point for deploying Hexalith.Parties to a Kubernetes
cluster. Today on `main`, the folder contains only this README — the application manifests,
operator scripts, and shared helpers are filled in across **Stories 9.2 → 9.5** (see the
roadmap callout below).

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
> lands. Until then, manifests under `deploy/k8s/` are intentionally empty pending Stories
> 9.2 / 9.3 / 9.4 / 9.5; manifest correctness is verified manually against the cleanliness
> regex table in `_bmad-output/implementation-artifacts/9-1-zot-oci-registry-and-deployment-documentation.md` AC9.

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

<!-- This folder listing anticipates Stories 9.2–9.5; entries are not present on main until that story ships. -->

> **🗺️ Roadmap — this folder fills in across Stories 9.2 → 9.5**
>
> Today on `main`, this folder contains only this README. The following entries land in
> subsequent v2 stories:
>
> | Path | Owning story | Purpose |
> |---|---|---|
> | `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/` | Story 9.2 | Aspirate-emitted per-service manifests |
> | `redis/`, `keycloak/` | Story 9.3 | Hand-authored carve-outs |
> | `namespace.yaml`, `kustomization.yaml` | Story 9.2 | Top-level wiring |
> | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1` | Story 9.5 | Operator scripts + shared context-gate helper |
>
> Track progress in `_bmad-output/implementation-artifacts/sprint-status.yaml`.

---

## See also

- Refer to [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) for the canonical cluster topology, image registry policy, Dapr control-plane wiring, and reproducibility contracts.
- [`../zot/README.md`](../zot/README.md) — Zot OCI registry manifest tree (Story 9.1 deliverable).
- ADR **D-K8s-2** in `_bmad-output/planning-artifacts/architecture.md` — Zot pull-secret pipeline + dedicated `parties-publisher` build account.
- ADR **D-K8s-3** in `_bmad-output/planning-artifacts/architecture.md` — `-ConfirmContext` gate (replaces the v1 local-cluster regex allowlist).
- ADR **D-K8s-4** in `_bmad-output/planning-artifacts/architecture.md` — Epic 9 v2 greenfield rewrite rationale.
