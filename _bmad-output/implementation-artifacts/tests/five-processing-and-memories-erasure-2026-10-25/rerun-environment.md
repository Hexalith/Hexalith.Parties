# Reproducing the affected checks

`rerun-affected.sh` resolves this checkout with Git and writes logs to a fresh temporary directory, or `FIVE_PROCESSING_LOG_DIR`. Package modes need no source mapping:

```bash
bash _bmad-output/implementation-artifacts/tests/five-processing-and-memories-erasure-2026-10-25/rerun-affected.sh client-package
bash _bmad-output/implementation-artifacts/tests/five-processing-and-memories-erasure-2026-10-25/rerun-affected.sh projections-package
```

Source mapping belongs to the repository-pinned `hexalith-module` tool. Restore that exact tool and use its supported source runner against the tracked v2 manifest:

```bash
dotnet tool restore
dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json --mode source --output json
```

This is the ordinary local runner: it builds and starts runner-owned resources. Record its run ID and workspace from its output/plan; keep that owned workspace alive while running checks. Set `FIVE_PROCESSING_SOURCE_WORKSPACE` to that workspace, then invoke a source mode such as `parties-source` or `projections-source`. The wrapper consumes the five generated imports and mapping hash defined by `SourceMappingMaterializer.Environment`; it verifies source mode and this checkout's root. It does not generate a competing mapping. Finish with the module tool's `down --manifest module/hexalith.module-manifest.v2.json --run-id <owned-run-id>` command. Use only root-declared dependencies, preserve other runs and never initialize nested submodules.

The authoritative bootstrap and mode behavior is documented in `references/Hexalith.Builds/README.md`, “G-4 Local Tools”; the pinned tool's `debug --help`/`down --help` give its exact current interface. A blocked runner/build is a blocker, not a reason to copy old mapping files or override its guards. The original run retained an unhealthy topology baseline and HXW006; the receipts do not claim a healthy full source environment.

`memories-runtime` uses the existing integration fixtures. Their normal lane owns RedisStack/FalkorDB test containers and a temporary Dapr sidecar; initialize placement/scheduler using the maintained Memories integration-lane prerequisites. The alternative native adapter contract is documented in `references/Hexalith.Memories/docs/operations/failure-recovery.md`. Select it only with `MEMORIES_TEST_OWNED_NATIVE=1` and explicit owned loopback RedisSearch/FalkorDB/placement/scheduler endpoints. The RedisSearch fixture requires `ON_TIMEOUT=FAIL`. The wrapper supplies no historical port defaults and never starts or stops another person's resources.

The `review1`/`review2` JSON and `review2-rerun.sh` remain historical records of the exact executed environment. Their absolute paths and log hashes describe those runs; use the portable wrapper for a new checkout rather than treating historical mappings as current authority.
