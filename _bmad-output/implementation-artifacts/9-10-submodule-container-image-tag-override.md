# Story 9.10: Submodule `ContainerImageTag` Default Override (cross-submodule)

Status: backlog

## Story

As an operator running `publish.ps1` with a MinVer-resolved tag (e.g., `0.1.0`),
I want all 7 container images (including those built from the `Hexalith.EventStore` and `Hexalith.Tenants` submodules) to be tagged with the same MinVer version,
so that the cluster-wide image set is coherent and AC4 (no `:staging-latest`, no `:latest`) is satisfied for every consumer Deployment, not just the projects rooted in `Hexalith.Parties`.

## Discovery Context (2026-05-20)

Surfaced during the first successful `publish.ps1` run after Story 9.5 review. The `dotnet aspirate generate --container-image-tag 0.1.0` invocation passed the flag for all 7 services, BUT the dotnet publish per-project command emitted by aspirate showed:

```
✅ parties           → -p:ContainerImageTag="0.1.0"
✅ parties-mcp       → -p:ContainerImageTag="0.1.0"
✅ memories          → -p:ContainerImageTag="0.1.0"
❌ eventstore        → -p:ContainerImageTag="staging-latest"
❌ eventstore-admin  → -p:ContainerImageTag="staging-latest"
❌ eventstore-admin-ui → -p:ContainerImageTag="staging-latest"
❌ tenants           → -p:ContainerImageTag="staging-latest"
```

The 4 services from the `Hexalith.EventStore` and `Hexalith.Tenants` submodules silently picked up `staging-latest` from their own `Directory.Build.targets`:

```xml
<!-- Hexalith.EventStore/Directory.Build.targets:17 -->
<ContainerImageTag Condition="'$(ContainerImageTags)' == '' and '$(ContainerImageTag)' == ''">staging-latest</ContainerImageTag>
```

This default has a guard condition (`Condition="'$(ContainerImageTag)' == ''"`) that should suppress the default when the property is set externally, BUT aspirate's `--container-image-tag` flag does not appear to set the MSBuild property before evaluation of the submodule's Directory.Build.targets — so the condition triggers and the default `staging-latest` wins.

## Root Cause Analysis (needs verification)

Two possible mechanisms:

1. **Directory.Build.targets ordering**: aspirate passes `-p:ContainerImageTag=0.1.0` on the dotnet publish command line. MSBuild evaluates command-line properties FIRST. Then it imports Directory.Build.targets. The `Condition="'$(ContainerImageTag)' == ''"` check should be false at this point because the property IS set. So the default should NOT trigger. **But it does**. Either the condition evaluation differs across MSBuild versions, or aspirate isn't actually passing the property to the submodule projects.

2. **Aspirate per-project flag resolution**: aspirate may resolve the image tag flag per-project based on each project's MSBuild context. If the submodule project has its own MinVerVersion resolution that returns empty, or if aspirate reads the `ContainerImageTag` MSBuild property directly (which evaluates to `staging-latest` after Directory.Build.targets applies), it may pass that to dotnet publish instead of the `--container-image-tag` flag value.

Need to instrument: spawn `dotnet aspirate generate --container-image-tag 0.1.0 --verbose` and inspect the exact `dotnet publish` invocation aspirate emits for each project.

## Acceptance Criteria

1. **All 7 consumer images tagged with the same MinVer tag** after `publish.ps1`. Verified via:
   ```bash
   for svc in eventstore eventstore-admin eventstore-admin-ui parties parties-mcp tenants memories; do
     kubectl -n hexalith-parties get deploy $svc -o jsonpath='{.spec.template.spec.containers[0].image}{"\n"}'
   done | sort -u
   # → returns exactly ONE unique image-tag suffix (e.g., :0.1.0)
   ```

2. **One of three mechanisms chosen and recorded as ADR**:
   - **Path A — Remove the default from submodule Directory.Build.targets**: cross-submodule PR to `Hexalith.EventStore` + `Hexalith.Tenants` removing the `staging-latest` default. Follows Story 9.3 AC1 submodule-PR precedent.
   - **Path B — Aspirate `--container-image-tag` enforcement**: invoke dotnet publish with an additional flag that overrides Directory.Build.targets, e.g., `-p:ContainerImageTag=0.1.0` (already passed) AND `-p:_ContainerImageTagOverride=true` that the submodule's Directory.Build.targets can check. Requires submodule cooperation anyway.
   - **Path C — Re-tag in Zot post-build**: after aspirate completes, use `crane tag` or `oras` to add an alias from `:staging-latest` to `:0.1.0` in Zot. Hides the underlying issue; not recommended but quick.

3. **Lint coverage**: extend `K8sWorkload-LatestImageTag` or add `K8sWorkload-NonMinVerTag` to fail on any `registry.hexalith.com/*` image NOT matching the MinVer regex.

4. **Cross-submodule PR pattern**: if Path A chosen, the submodule edits follow the Story 9.3 AC1 EventStore PR pattern (PR to submodule, then bump submodule pointer in Parties).

## Out of Scope

- Non-Hexalith images (`docker.io/library/redis`, `quay.io/keycloak/keycloak`) — these are vendor images and have their own tagging policies.
- CI/CD wiring of MinVer for the submodules (their own `v*` git tags) — can stay on `:staging-latest` for now; only Parties' publish.ps1 needs alignment.
- Multi-tag emission per build (e.g., both `:0.1.0` AND `:latest`) — outside Story 9.10 scope; track separately.

## Dev Notes

- Submodule files with the default:
  - `Hexalith.EventStore/Directory.Build.targets:17`
  - `Hexalith.Tenants/Directory.Build.targets:17`
  - (recursive copies via nested submodule worktrees; the canonical sources are the two above)
- The condition `Condition="'$(ContainerImageTags)' == '' and '$(ContainerImageTag)' == ''"` is correct in spirit — it's supposed to be a fallback default. The bug is somewhere in aspirate's per-project property propagation.
- aspirate's GitHub: https://github.com/prom3theu5/aspirational-manifests — file an issue with the verbose log if Path A isn't viable.
- `Hexalith.Memories/Directory.Build.targets` does NOT have this default, which is why memories correctly received `0.1.0`.

## References

- [Source: Hexalith.EventStore/Directory.Build.targets:17] — staging-latest default
- [Source: Hexalith.Tenants/Directory.Build.targets:17] — same
- [Source: src/Hexalith.Parties.AppHost/Program.cs] — composition that feeds aspirate
- [Source: deploy/k8s/publish.ps1] — orchestrates the aspirate call
- [Source: _bmad-output/implementation-artifacts/9-5-zot-registry-build-push-pipeline.md] — first publish run that exposed this
- [Source: _bmad-output/implementation-artifacts/9-3-close-k8s-deployment-spec-gaps.md § AC1] — cross-submodule PR precedent
