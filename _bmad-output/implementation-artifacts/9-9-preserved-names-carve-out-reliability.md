# Story 9.9: `$PreservedNames` Carve-Out Reliability in publish.ps1

Status: backlog

## Story

As an operator running `publish.ps1`,
I want the `$PreservedNames` list (`publish.ps1`, `teardown.ps1`, `README.md`, `namespace.yaml`, `keycloak`, `redis`) to actually preserve the listed files and directories across aspirate regeneration,
so that hand-authored carve-outs (`deploy/k8s/redis/`, `deploy/k8s/keycloak/`) survive a fresh publish without being silently overwritten by aspirate's emission.

## Discovery Context (2026-05-20)

Surfaced during the post-Story-9.5 cluster verification. Despite `redis` being in `$PreservedNames` (publish.ps1:204):

```pwsh
$PreservedNames = @("publish.ps1", "teardown.ps1", "README.md", "namespace.yaml", "keycloak", "redis")
```

aspirate 9.1.0's `dotnet aspirate generate` **regenerated** `deploy/k8s/redis/deployment.yaml` from scratch, overwriting the hand-authored carve-out (no-auth redis:7.4-alpine + emptyDir) with aspirate's default emission (`AddRedis()` → redis:8.6 + `--requirepass`). The publish.ps1 output line confirmed:

```
(✔) Done: Generating /home/quentindv/Hexalith.Parties/deploy/k8s/redis
```

The `$PreservedNames` mechanism in publish.ps1 Step 2 (preserved-name clean) protects entries from the `Remove-Item -Recurse -Force` cleanup, but **does not block aspirate from writing into the same path** during Step 3 (aspirate generate). The carve-out is effectively NOT preserved.

The Story 9.3 / 9.5 documentation describes redis (and keycloak) as "hand-authored carve-outs preserved by `$PreservedNames`" — this is currently false for redis. Keycloak survives only because aspirate's `AddKeycloak()` doesn't seem to emit into `keycloak/` directly (the resource handles auth differently — needs verification).

## Acceptance Criteria

1. **Redis carve-out survives publish.ps1**. After `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` on a clean working tree, `git diff deploy/k8s/redis/` returns empty (byte-identical to HEAD).

2. **Keycloak carve-out survives publish.ps1**. Same: `git diff deploy/k8s/keycloak/` returns empty.

3. **One of three mechanisms chosen**:
   - **Mechanism A — Backup-restore around aspirate**: before Step 3 (aspirate generate), snapshot the preserved directory contents to a temp location; after Step 7 (post-aspirate patches), restore the snapshot. Survives any aspirate emission.
   - **Mechanism B — `--exclude` aspirate flag** (if available): check aspirate 9.1.0 for a `--exclude-resource <name>` or `--skip-resource <name>` flag. Pass `redis` and (if needed) `keycloak`. Less code, cleaner.
   - **Mechanism C — Stop using `AddRedis()` / `AddKeycloak()`**: remove these from the AppHost composition. Reference the in-cluster Service names directly. Convergent with Story 9.8 Path C.

4. **Rationale documented as ADR amendment to D-K8s-2**: which mechanism was chosen, why, what failure modes it covers, what it doesn't.

5. **Test coverage**: extend `K8sManifestPublishTests` with a "carve-out preservation" assertion — apply publish.ps1's clean+aspirate logic against a fixture redis dir with sentinel content, verify the sentinel survives.

## Out of Scope

- Carve-out preservation for files NOT currently in `$PreservedNames` (e.g., adding namespace.yaml — already done in Story 9.5 T8 patch).
- Migrating away from carve-outs entirely (the hand-authored ones exist for valid reasons — Keycloak needs randomized admin password, Redis needs MVP-scope simplification).
- Aspirate version bump to a release that may resolve this (track separately if `aspirate 9.x.y+` introduces a `--skip-resource` flag).

## Dev Notes

- `publish.ps1` Step 2 clean logic (lines ~204-209):
  ```pwsh
  Get-ChildItem -Path $OutputDir -Force | Where-Object {
      $PreservedNames -notcontains $_.Name
  } | ForEach-Object {
      Remove-Item -Recurse -Force -LiteralPath $_.FullName
  }
  ```
  This is correct as far as it goes — but only protects against the explicit Remove-Item; doesn't stop aspirate's subsequent write.

- `dotnet aspirate generate --help` may show a `--exclude-resource` or similar flag in aspirate 9.x. Verify before assuming Mechanism A.

- The Story 9.3 commit `68fd117` added redis as a hand-authored carve-out without verifying that aspirate respects it. The committed `redis/deployment.yaml` reflects the carve-out intent but is silently overwritten by aspirate.

- Cross-reference Story 9.8 (Redis auth alignment) — the two stories share a dependency. If 9.8 Path B (disable AddRedis password) is chosen, 9.9 becomes "preserve the no-auth state after aspirate regenerates". If 9.8 Path C (drop AddRedis), 9.9 becomes "don't generate redis at all".

## References

- [Source: deploy/k8s/publish.ps1:204-209] — Step 2 preserved-name clean
- [Source: deploy/k8s/publish.ps1:211-245] — Step 3 aspirate generate (where overwriting happens)
- [Source: _bmad-output/implementation-artifacts/9-3-close-k8s-deployment-spec-gaps.md § AC5] — original "Redis hand-authored carve-out" claim
- [Source: _bmad-output/implementation-artifacts/9-5-zot-registry-build-push-pipeline.md § Tasks] — `$PreservedNames` mechanism documentation
- [aspirate 9.1.0 release notes] — check for carve-out / exclude flags
