# Story 9.7: Memories.Server Redis Connection String

Status: backlog

## Story

As an operator deploying Hexalith.Parties to Kubernetes,
I want `memories.Server` to either (a) not require a direct Redis connection string when running under Kubernetes via Dapr, OR (b) receive `ConnectionStrings__redis` from the AppHost-composed manifests,
so that the `memories` pod actually starts up after a clean deploy.

## Discovery Context (2026-05-20)

Discovered during the post-Story-9.5 code review when verifying the cluster state after applying the Redis revert (T1/T2 in the 9.5 review). After Redis NOAUTH was fixed, 7 of 9 pods recovered to `Running` state. The remaining `memories-*` pod stayed in `CrashLoopBackOff` (both containers — app AND daprd, restart count >100) with the following app log:

```
Unhandled exception. System.InvalidOperationException:
Connection string 'redis' is required. Start the server through AppHost or set ConnectionStrings__redis.
   at Program.<<Main>$>g__ConnectRequiredMultiplexer|0_67(IConfiguration configuration, String connectionName)
     in /home/quentindv/Hexalith.Parties/Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs:line 3160
```

## Root Cause (Two-Sided)

The `Hexalith.Memories.Server` app requires `ConnectionStrings__redis` at startup to construct a `IConnectionMultiplexer` (line 3160 of `Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs`). This is a direct Redis connection at the app layer, separate from Dapr's state store / pubsub.

In `src/Hexalith.Parties.AppHost/Program.cs:116`, memories is composed as:

```csharp
IResourceBuilder<ProjectResource> memories = builder.AddProject<Projects.Hexalith_Memories_Server>("memories")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "memories", Config = memoriesAccessControlConfigPath })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub))
    .WaitFor(eventStoreResources.StateStore)
    .WaitFor(eventStoreResources.PubSub);
```

The composition only uses `.WithReference()` on Dapr-side resources (StateStore + PubSub). It does NOT call `.WithReference(redis)` on memories. As a result, Aspirate does NOT emit a `ConnectionStrings__redis=redis:6379` env var into the `memories-env` ConfigMap. The deployed pod has only `ASPNETCORE_URLS`, `HTTP_PORTS`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` — no Redis connection string.

Two valid design choices exist; this story is to pick one:

### Option A — AppHost-side fix
Add `.WithReference(redis)` to the memories composition in `AppHost/Program.cs`. Aspirate then emits `ConnectionStrings__redis` into `memories-env`. Simple, follows the Aspire model.

**Risk**: couples memories tightly to the Parties topology Redis. If memories is later reused outside this topology, it carries a direct-Redis-dependency assumption.

### Option B — Memories-side fix (cross-submodule)
Refactor `Hexalith.Memories.Server` to use Dapr state store / pubsub instead of a direct `IConnectionMultiplexer`. Removes the Redis-direct dependency at the app layer.

**Risk**: cross-submodule change (per project-context rule "Treat sibling directories as root-level submodules; avoid editing them unless the task explicitly crosses that boundary"). May not be feasible if memories has logic that requires raw Redis features (streams, pub/sub specifics).

### Option C — Documented operator override
Document the requirement and add `ConnectionStrings__redis=redis:6379` to `deploy/k8s/memories/kustomization.yaml` as a hand-authored carve-out (in addition to or instead of the AppHost-side fix). Cheap; acknowledges the existing pattern of hand-authored carve-outs in `deploy/k8s/{redis,keycloak}/`.

## Acceptance Criteria

1. **Memories pod reaches `Ready` state on a clean publish run.** After `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` against a cluster with `dapr init -k` + the `deploy/dapr/` Components applied + Redis running without auth, `kubectl -n hexalith-parties wait --for=condition=Ready pod -l app=memories --timeout=180s` succeeds.

2. **Decision recorded as ADR.** The chosen path (A, B, or C) is captured as an ADR in `_bmad-output/planning-artifacts/architecture.md`, with rejected alternatives documented. If Option B is chosen, the cross-submodule PR pattern from Story 9.3 AC1 is followed.

3. **Memories-env ConfigMap carries the connection string** (Option A or C) OR memories code no longer requires it (Option B). Verified by `kubectl -n hexalith-parties get cm memories-env -o jsonpath='{.data}' | grep -i redis` (A/C) OR by reading the memories Program.cs and confirming no `ConnectRequiredMultiplexer("redis")` call (B).

4. **Lint coverage** — extend `K8sTopology-*` or add `K8sWorkload-MissingConnectionString` category in `deploy/validate-deployment.ps1` that fires when a Deployment depends on Redis (per the topology graph) but has no `ConnectionStrings__redis` env var. Severity TBD by sub-skill chosen.

## Out of Scope

- Other apps that may have similar Redis-direct-connection dependencies (parties? eventstore?). This story is memories-specific.
- Migrating away from Redis to another backing store. The choice of Redis as memories' substrate is established by Story 9.3 ADR 9.3-2.
- The `parties-mcp` ImagePullBackOff bug (different surface — image not pushed at expected tag) — tracked separately if it re-surfaces; current workaround is `:latest` in `deploy/k8s/parties-mcp/deployment.yaml`.

## Dev Notes

- The pod-side error is at `Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs:line 3160`. The 3160-line Program.cs is structured as the Aspire builder + service-collection wiring; the `ConnectRequiredMultiplexer` helper is invoked from the `RedisSearchService` registration around line 142.
- Aspirate's resource-reference resolution maps `.WithReference(IResourceBuilder<RedisResource>)` to `ConnectionStrings__<resourceName>` env vars (per `Aspire.Hosting.Kubernetes`'s emit logic). Confirm in aspirate 9.1.0 source if Option A is chosen.
- Cross-submodule rule: edits to `Hexalith.Memories/` MUST go through the EventStore-style submodule-PR pattern (see Story 9.3 AC1 for the precedent). Option B requires this.
- Pre-existing — the bug was introduced in commit `68fd117` (Story 9.3 close) when memories was first added to the deployable topology. It was masked by other deploy failures (Redis NOAUTH at runtime) until the 9.5 code review surfaced it.

## References

- [Source: src/Hexalith.Parties.AppHost/Program.cs:104-127] — current memories composition (incomplete `.WithReference()` chain)
- [Source: Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs:142,3160] — the `ConnectRequiredMultiplexer("redis")` call site
- [Source: deploy/k8s/memories/kustomization.yaml] — current memories-env ConfigMap (no ConnectionStrings)
- [Source: _bmad-output/implementation-artifacts/9-5-zot-registry-build-push-pipeline.md § Review Findings] — discovery context (post-9.5-review cluster verification)
- [Source: _bmad-output/implementation-artifacts/9-3-close-k8s-deployment-spec-gaps.md § AC1] — submodule-PR pattern precedent (if Option B chosen)
- [Source: _bmad-output/planning-artifacts/architecture.md § ADR D-K8s-2] — Zot registry as image substrate (orthogonal but referenced by ADR 9.3-2)
