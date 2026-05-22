# Story 9.8: Aspire Redis Auth ↔ Dapr Component Password Alignment

Status: backlog

## Story

As an operator running `publish.ps1` against the cluster,
I want Aspire's `AddRedis()` default `--requirepass` behavior to be either disabled OR matched by `redisPassword` configuration on the Dapr State Store and Pub/Sub Components,
so that 5 daprd sidecars (eventstore, eventstore-admin, parties, tenants, memories) do not crash with `NOAUTH Authentication required` on every fresh publish.

## Discovery Context (2026-05-20)

Surfaced during the post-Story-9.5 cluster verification phase. After Phase A of the 9.5 review (revert Redis to no-auth) was applied to the cluster, all daprd sidecars recovered (7/9 pods Running). On the next `publish.ps1` run, aspirate **regenerated** `deploy/k8s/redis/` with `image: docker.io/library/redis:8.6`, `--requirepass $REDIS_PASSWORD`, and a `REDIS_PASSWORD=<literal>` ConfigMap. The Dapr Components (`deploy/dapr/statestore.yaml` + `deploy/dapr/pubsub.yaml`) have no `redisPassword` metadata, so:

```
[INIT_COMPONENT_FAILURE]: initialization error occurred for pubsub (pubsub.redis/v1):
redis streams: error connecting to redis at redis.hexalith-parties.svc.cluster.local:6379:
NOAUTH Authentication required.
```

This creates a regression loop: every `publish.ps1` run reverts the operator's no-auth workaround, breaking the cluster. The operator must hand-edit `redis/deployment.yaml` after every publish.

## Root Cause

`src/Hexalith.Parties.AppHost/Program.cs:108`:

```csharp
IResourceBuilder<RedisResource> redis = builder.AddRedis("redis");
```

Aspire 13.x's `AddRedis()` introduced a **default `--requirepass` + auto-generated password** for security hardening (Aspire 13.x release notes). The password is exposed as `REDIS_PASSWORD` in the generated `redis-env` ConfigMap.

The 9.3-era Dapr Component manifests were authored when `AddRedis()` had no password by default. They were not updated when the project rolled to Aspire 13.x:

```yaml
# deploy/dapr/statestore.yaml — current (broken)
spec:
  metadata:
  - name: redisHost
    value: redis.hexalith-parties.svc.cluster.local:6379
  # ← MISSING: - name: redisPassword
  - name: actorStateStore
    value: "true"
```

## Acceptance Criteria

1. **Daprd sidecars connect to Redis on a fresh publish.** After `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` against a clean cluster, `kubectl -n hexalith-parties get pods -l 'app in (eventstore,eventstore-admin,parties,tenants,memories)' -o jsonpath='{.items[*].status.containerStatuses[?(@.name=="daprd")].ready}'` returns `true true true true true`. No `NOAUTH Authentication required` errors in any daprd log.

2. **Decision recorded as ADR.** One of three paths chosen and documented:
   - **Path A — Match the password**: extend `deploy/dapr/statestore.yaml` + `pubsub.yaml` with `redisPassword: ${REDIS_PASSWORD}` (env-var injection) OR `secretKeyRef` to a Secret. Requires `daprd` sidecar to have access to the same env var or Secret. Production-ready.
   - **Path B — Disable password in AppHost**: override `AddRedis()` to suppress the password (`.WithEnvironment("REDIS_PASSWORD", "")` or equivalent Aspire API). Aligns with the 9.3 "MVP: no AUTH" comment. Less secure but matches the current state's design intent.
   - **Path C — Replace `AddRedis()` with a hand-authored carve-out**: stop using Aspire's Redis abstraction; write redis/deployment.yaml + service.yaml + Dapr Components manually. Decouples Parties from Aspire's defaults. Highest implementation effort.

3. **Carve-out preservation works**: if Path A or C is chosen, the redis carve-out must survive `publish.ps1` runs without regeneration (closes Story 9.9 dependency).

4. **Story 9.9 / `$PreservedNames` carve-out reliability** (related): track and verify that aspirate 9.1.0 actually respects the carve-out for `redis/` — currently it does NOT (regenerates regardless).

## Out of Scope

- Production-grade Redis (StatefulSet + PVC + TLS). The MVP scope is `emptyDir`-backed; auth is the only hardening that should land in 9.8.
- Redis password rotation procedures.
- Other Aspire-resource auth defaults (e.g., if `AddPostgres()` is added later, it has the same pattern — handle when it arrives).
- Cross-submodule fixes (e.g., `Hexalith.Memories.Server` direct Redis connection — Story 9.7 handles that).

## Dev Notes

- Aspire 13.x's `AddRedis()` source: review `src/RedisHosting/` in the Aspire SDK or `Aspire.Hosting.Redis` package for the password-generation logic. The relevant property may be `RedisResource.PasswordParameter`.
- Dapr Redis component metadata reference: https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/
- Env-var injection in Dapr Components uses `{env:VAR}` syntax or `secretRef`. Test that the daprd sidecar has the env var visible (via the pod template env section).
- The `REDIS_PASSWORD` literal currently lands in the `redis-env` ConfigMap. If Path A is chosen, the Dapr daprd sidecar must mount this ConfigMap as env source — verify via `kubectl describe pod` after publish.
- Cross-reference Story 9.9 (`$PreservedNames` reliability) — both stories converge on "stop aspirate from owning redis/".

## References

- [Source: src/Hexalith.Parties.AppHost/Program.cs:101-108] — `AddRedis("redis")` composition
- [Source: deploy/dapr/statestore.yaml] — Dapr Redis state store (no `redisPassword`)
- [Source: deploy/dapr/pubsub.yaml] — Dapr Redis pub/sub (no `redisPassword`)
- [Source: _bmad-output/implementation-artifacts/9-5-zot-registry-build-push-pipeline.md] — Story 9.5 discovery context
- [Source: _bmad-output/planning-artifacts/architecture.md § ADR D-K8s-2] — Redis MVP scope ("no AUTH, no TLS, no PVC")
- [Aspire 13 changelog] — verify the exact release that flipped `AddRedis()` default to require-password
