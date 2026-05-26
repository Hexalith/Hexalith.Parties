# Zot OCI Registry — Deployment Manifests

This directory contains the applyable Kubernetes manifests that recreate the cluster-side Zot
OCI registry serving `registry.hexalith.com`. The Zot Deployment is the substrate that
Hexalith.Parties images are published to (Story 9.5 `publish.ps1`) and pulled from
(`imagePullSecrets: zot-pull-secret` on every Hexalith Deployment, Story 9.2 onwards).

> **Canonical reference:** See [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) §5 for the full Hexalith.Parties Kubernetes deployment topology, image registry policy, and reproducibility guarantees. Refer to ADR **D-K8s-2** (Zot pull-secret path + dedicated `parties-publisher` build account) and ADR **D-K8s-3** (`-ConfirmContext` gate) in `_bmad-output/planning-artifacts/architecture.md` for the operationally non-obvious decisions.

---

## Contents

| File | Resource | Notes |
|---|---|---|
| `namespace.yaml` | `Namespace/zot` | Distinct from `hexalith-parties` — registry concern isolated from application concern. |
| `configmap.yaml` | `ConfigMap/zot-config` | `config.json` payload with `accessControl.groups` (admins / builders / readers). Keys are **alphabetically sorted recursively** for byte-stable diffs across Zot versions. |
| `deployment.yaml` | `Deployment/zot` | Single-replica, `strategy: Recreate`. Image pinned to a digest-verified tag (see "Pinned tag rationale"). |
| `pvc.yaml` | `PersistentVolumeClaim/zot-pvc` | `storageClassName: local`, `accessModes: ReadWriteOnce`, capacity pinned to the live-cluster value. |
| `service.yaml` | `Service/zot` | `ClusterIP` — live-cluster `NodePort 30500` intentionally **not** propagated. |
| `ingress.yaml` | `Ingress/zot-ingress` | nginx Ingress with TLS edge-termination for `host: registry.hexalith.com`. |

## What this directory does NOT contain (and why)

- `Secret/zot-auth-secret` — the htpasswd file. Infra-team managed; **never committed**.
- `Secret/zot-tls` — the TLS cert/key for `registry.hexalith.com`. Infra-team / cert-manager managed; **never committed**.

See "Out-of-band Secret creation" below for the one-time bootstrap commands.

---

## Apply

> **Status (infra-team prerequisite):** Cluster-side Zot setup (htpasswd file creation,
> TLS cert provisioning, `accessControl.groups.builders` membership) is an infra-team task,
> not a developer task. The commands below capture the manifest shape that infra applied;
> manifest re-application on a fresh cluster is the documented operator path.
> The committed `configmap.yaml` grants the `builders` group read/create/update on `**`,
> which covers every image `publish.ps1` verifies before workload apply:
> `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`,
> `tenants`, and `memories`. The `parties-publisher` user must remain in that group.

### Pre-apply NodePort consumer audit (required before any apply against the live cluster)

The committed `service.yaml` drops the live-cluster `NodePort 30500` in favour of `ClusterIP`.
This Service mutation is **lossy if applied without consumer cutover** — any in-cluster or
external consumer that hits port 30500 today loses connectivity at apply time. The change is
reversible by re-adding `nodePort: 30500` and `type: NodePort` to `service.yaml` and re-applying,
but the connectivity gap during the mutation window is not. Before running
`kubectl apply -f deploy/zot/` against the live cluster, run the following three commands and
confirm no consumer depends on port 30500:

```bash
kubectl get endpoints -n zot zot -o yaml
kubectl get svc zot -n zot -o jsonpath='{.spec.ports}'
kubectl describe svc -n zot zot | grep -E 'NodePort|External'
```

**If a NodePort 30500 consumer exists**, choose one of these two remediation paths
**before** applying:

1. **Patch the committed manifest temporarily** — add `nodePort: 30500` back into
   `service.yaml` `ports[0]` and change `type` back to `NodePort`, then open a follow-up
   story to migrate consumers to the Ingress host `registry.hexalith.com` and re-cut over
   to `ClusterIP`.
2. **Coordinate the cutover** with the consumer owners before applying — confirm consumers
   have migrated to `registry.hexalith.com`, then apply the unmodified `service.yaml`.

### Apply

```bash
# Idempotent on a cluster that already has Zot bootstrapped:
kubectl apply -f deploy/zot/
```

**Clean-cluster bootstrap (namespace `zot` does not yet exist):** `kubectl apply -f` walks the
directory alphabetically, so `configmap.yaml`, `deployment.yaml`, and `ingress.yaml` apply
BEFORE `namespace.yaml` and emit `namespaces "zot" not found`. Apply the namespace first,
then the rest of the manifests:

```bash
kubectl apply -f deploy/zot/namespace.yaml
kubectl apply -f deploy/zot/
```

### Verify

```bash
kubectl get pod -n zot -l app=zot
kubectl logs -n zot deploy/zot --tail=50
# Anonymous access is denied (anonymousPolicy: []); curl prompts interactively for the password
# so it never lands in shell history. Authenticate as parties-publisher:
curl -u parties-publisher https://registry.hexalith.com/v2/_catalog
```

---

## Out-of-band Secret creation (one-time, infra-team owned)

Both Secrets below live in namespace `zot` and are **NOT** committed to the repo.

### `zot-auth-secret` (htpasswd file)

The `Secret/zot-auth-secret` holds the bcrypt-hashed credentials that the Zot `htpasswd`
auth backend reads from `/etc/zot/auth/htpasswd` (per `configmap.yaml` `http.auth.htpasswd.path`).

> **Note:** `htpasswd -nB <user>` prompts twice on TTY for each account (password + verify).
> Run the six lines below one at a time, typing the password (and verification) for each
> account before moving to the next. In a non-TTY context (CI, `bash -c`), use the batch
> form `htpasswd -nbB <user> <password>` instead — but be aware that the password then
> appears in process listings and shell history; prefer a password-manager pipe such as
> `pass show <entry> | htpasswd -inB <user>` (the `-i` flag reads the password from stdin).

```bash
# Start fresh — overwrite any prior staging file:
: > /tmp/zot-htpasswd

# Generate the htpasswd file, one interactive line per account in `accessControl.groups`:
htpasswd -nB parties-publisher >> /tmp/zot-htpasswd       # builders group, publish.ps1 consumer
htpasswd -nB github-ci          >> /tmp/zot-htpasswd       # builders group, CI runner
htpasswd -nB kaniko             >> /tmp/zot-htpasswd       # builders group, in-cluster builder
htpasswd -nB jpiquot            >> /tmp/zot-htpasswd       # admins group, emergency ops
htpasswd -nB qdassivignon       >> /tmp/zot-htpasswd       # admins group, emergency ops
htpasswd -nB kubernetes         >> /tmp/zot-htpasswd       # readers group, in-cluster pulls

# Create the Secret (the data-key MUST be literally `htpasswd` — the Deployment mounts
# /etc/zot/auth as a directory and Zot reads /etc/zot/auth/htpasswd):
kubectl create secret generic zot-auth-secret -n zot --from-file=htpasswd=/tmp/zot-htpasswd

# Shred the staging file:
shred -u /tmp/zot-htpasswd
```

Verify presence (no contents echoed):

```bash
kubectl get secret zot-auth-secret -n zot
```

### `zot-tls` (TLS cert + key for `registry.hexalith.com`)

The `Ingress/zot-ingress` terminates TLS at the cluster edge using `secretName: zot-tls`.
The cert + key are managed by cert-manager or the infra team's wildcard-cert practice — refer
to the cluster's standard cert-rotation runbook.

```bash
# Manual create (one-off — prefer cert-manager for rotation):
kubectl create secret tls zot-tls -n zot --cert=/path/to/registry.hexalith.com.crt --key=/path/to/registry.hexalith.com.key
```

Verify presence:

```bash
kubectl get secret zot-tls -n zot
```

---

## Pinned tag rationale

The live cluster runs `ghcr.io/project-zot/zot-linux-amd64:latest` with `imagePullPolicy: Always` —
this is a documented infra-team drift from the canonical reproducibility guarantees in
[Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) §11.
The committed `deployment.yaml` **pins** the image to a verified tag and switches to
`imagePullPolicy: IfNotPresent`:

```
image: ghcr.io/project-zot/zot-linux-amd64:v2.1.17
imagePullPolicy: IfNotPresent
```

**Digest-resolve path taken (Story 9.1 Subtask 2.3, path 1-3 — ideal, no fallback):**

1. Captured the live-cluster digest:
   ```bash
   kubectl get pod -n zot -l app=zot -o jsonpath='{.items[0].status.containerStatuses[0].imageID}'
   # → docker-pullable://ghcr.io/project-zot/zot-linux-amd64@sha256:2f4da11ec2ed0fccf8e93186bf9bdd7b7115a649a0b954c1a09f776d5199174d
   ```
2. Reverse-looked-up the tag for that digest:
   ```bash
   docker manifest inspect --verbose ghcr.io/project-zot/zot-linux-amd64:v2.1.17 | grep '"digest"' | head -1
   # → "digest": "sha256:2f4da11ec2ed0fccf8e93186bf9bdd7b7115a649a0b954c1a09f776d5199174d"
   ```
3. Confirmed `v2.1.17` is the current latest stable release at
   <https://github.com/project-zot/zot/releases> (verified 2026-05-21).

**Result:** zero functional change to the live cluster on `kubectl apply` (same image bits,
just an immutable name). The Deployment's `strategy.type: Recreate` rolls the Pod cleanly
when `imagePullPolicy: IfNotPresent` takes effect.

**Tag-bump path** (when Zot ships a new stable release the infra team wants to adopt):

1. Verify the new tag's digest with `docker manifest inspect --verbose ghcr.io/project-zot/zot-linux-amd64:vX.Y.Z`.
2. Update `deployment.yaml` `image:` to the new tag.
3. `kubectl apply -f deploy/zot/deployment.yaml` (Recreate strategy ensures clean rollover).

---

## ConfigMap re-snapshot convention

If a future task needs to capture additional Zot configuration changes back from the live
cluster (e.g. a new `accessControl.groups` member added by the infra team), **always normalize
the JSON payload through alphabetical key order before committing**. The raw
`kubectl get cm zot-config -n zot -o yaml` payload uses Zot's emitted-config field order,
which is **not** byte-stable across Zot versions — a future re-snapshot would silently flip
the diff if pasted raw.

```bash
# 1. Snapshot:
kubectl get cm zot-config -n zot -o yaml > /tmp/zot-config-live.yaml

# 2. Extract the embedded JSON:
python3 -c "import yaml,json,sys; print(yaml.safe_load(open('/tmp/zot-config-live.yaml'))['data']['config.json'])" > /tmp/config-raw.json
# (or equivalently with yq: yq '.data["config.json"]' /tmp/zot-config-live.yaml > /tmp/config-raw.json)

# 3. Normalize to alphabetical key order (jq -S sorts keys recursively):
jq -S '.' < /tmp/config-raw.json > /tmp/config-canonical.json

# 4. Embed back into configmap.yaml as a literal block scalar:
#    (manually replace the data."config.json" block; indent each line by 4 spaces)

# 5. Diff vs the committed file to confirm the change is what you expect:
diff /tmp/config-canonical.json <(python3 -c "import yaml,json; print(yaml.safe_load(open('deploy/zot/configmap.yaml'))['data']['config.json'])")
```

Story 9.7 will mechanize this byte-stability check via a fitness test asserting
`jq -S '.' deploy/zot/configmap.yaml.json-payload | diff -` returns zero.

---

## Capacity-bump path for `zot-pvc`

The committed PVC is sized at **20 GiB** (pinned to the live-cluster value verified 2026-05-21).
PVCs only grow — to expand storage:

```bash
# Verify the StorageClass allows volume expansion:
kubectl get storageclass local -o jsonpath='{.allowVolumeExpansion}{"\n"}'
# Expected: true

# Patch the live PVC (in-place expansion, no Pod restart needed for filesystem PVCs):
kubectl patch pvc zot-pvc -n zot -p '{"spec":{"resources":{"requests":{"storage":"50Gi"}}}}'

# Then update deploy/zot/pvc.yaml to match the new size and commit so the manifest stays idempotent:
sed -i 's/storage: 20Gi/storage: 50Gi/' deploy/zot/pvc.yaml
```

If the StorageClass does NOT support `AllowVolumeExpansion`, the path is destructive: backup
`/var/lib/registry` contents, delete the PVC + Pod, recreate with the new size, restore
backup. Coordinate with the infra team.

**Clean-cluster portability:** the committed `pvc.yaml` pins `storageClassName: local` to
match the production cluster. Clean-cluster bootstraps on kind / k3d / minikube / Docker
Desktop typically do NOT carry a `local` StorageClass — the PVC will stay `Pending`
indefinitely. Either remove the `storageClassName` line (PVC inherits the cluster default)
or patch it to match the target cluster's class (e.g. `standard` for kind, `local-path` for
k3d). The override should be a local edit on the bootstrap branch, not committed back.

---

## Why `Service/zot` is `ClusterIP`, not `NodePort`

The live cluster's `Service/zot` is `NodePort 30500` — a vestige of the initial 142-day-old
bootstrap when the registry was reachable directly via a node-IP port. The Ingress at
`registry.hexalith.com` (with TLS edge-termination and the supported `nginx.ingress.kubernetes.io/*`
annotations) is the **only** supported access path going forward; the NodePort is documented
as a live-cluster artefact, out-of-scope for committed-manifest reproducibility.

See "Pre-apply NodePort consumer audit" above before applying the `ClusterIP` cutover.

---

## See also

- [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) §5 — canonical Zot description, access control, tagging policy, pull-credential pipeline.
- ADR **D-K8s-2** in `_bmad-output/planning-artifacts/architecture.md` — Zot pull-secret pipeline + dedicated `parties-publisher` build account.
- ADR **D-K8s-3** in `_bmad-output/planning-artifacts/architecture.md` — `-ConfirmContext` gate (replaces the v1 local-cluster regex allowlist).
- ADR **D-K8s-4** in `_bmad-output/planning-artifacts/architecture.md` — Epic 9 v2 greenfield rewrite rationale.
- `deploy/k8s/README.md` — operator entry-point for the Hexalith app workloads (Stories 9.2 – 9.5).
