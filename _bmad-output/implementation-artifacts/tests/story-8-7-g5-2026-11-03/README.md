# Story 8.7 G5 prerequisite receipt — 2026-11-03

Story 8.7 remains blocked. These are structural prerequisite checks, not
shared-provider, GDPR, rollback, KMS, or production-readiness proofs.
The baseline is Parties `fcf7cbc86076ce43f0982e4959da9691feaf2d3a`.

| Check | Result |
| --- | --- |
| `python3 _bmad-output/implementation-artifacts/tests/story-8-7-g5-2026-11-03/audit.py` | Exit 0; 32/32 G5 matrix commands; baseline comparisons for all 24 retained files plus the harness and DI source; four DI registrations; all ten mapped dependency gitlinks/clean checkouts. Output: `audit.json`. |
| Default Release package evaluation below | Exit 0; EventStore `3.132.2`; source selectors empty. The observed Builds default is `3.132.1`. |
| Pinned source preparation below | Exit 0; mapping hash `85abceed351e6c74267f582bac8849146847f991a503657beb12c18ddff68f63`. HXW009: Builds differs from Platform's recorded pin; HXW010: pinned tool 4.41.1 falls outside Platform's 4.43.0+ range; HXW011: local Platform release tag missing. These advisories grant no compatibility approval. |
| Focused mapped Debug test-project build below | Exit 1; 0 warnings, 15 errors. `Hexalith.Tenants.Client` references missing `Hexalith.EventStore.Client.Projections.Checkpointed` types (CS0234/CS0246). Full output: `source-build.log.txt`. No fresh test assembly was produced. |
| `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` | Not executed because its test-project build failed; no xUnit pass credited. |
| `bash scripts/check-no-warning-override.sh` | Exit 0; no warning-override or nested-submodule regression. |

Run from the repository root:

```bash
python3 _bmad-output/implementation-artifacts/tests/story-8-7-g5-2026-11-03/audit.py \
  > _bmad-output/implementation-artifacts/tests/story-8-7-g5-2026-11-03/audit.json

dotnet msbuild tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj \
  -p:Configuration=Release \
  -getProperty:HexalithEventStoreVersion,HexalithEventStoreFromSource,UseHexalithProjectReferences

dotnet tool run hexalith-module -- prepare-build \
  --manifest module/hexalith.module-manifest.v2.json \
  --workspace /tmp/parties-story-8-7-g5-source-20261103 \
  --mode source --output json

env \
  CustomBeforeDirectoryBuildProps=/tmp/parties-story-8-7-g5-source-20261103/source-mapping/SourceMapping.targets \
  CustomAfterMicrosoftCommonProps=/tmp/parties-story-8-7-g5-source-20261103/source-mapping/SourceMapping.props \
  CustomBeforeDirectoryBuildTargets=/tmp/parties-story-8-7-g5-source-20261103/source-mapping/SourceMapping.before-targets.props \
  CustomAfterDirectoryBuildTargets=/tmp/parties-story-8-7-g5-source-20261103/source-mapping/SourceMapping.late.props \
  AfterMicrosoftNETSdkTargets=/tmp/parties-story-8-7-g5-source-20261103/source-mapping/SourceMapping.final.targets \
  HEXALITH_SOURCE_MAPPING_HASH=85abceed351e6c74267f582bac8849146847f991a503657beb12c18ddff68f63 \
  Configuration=Debug \
  dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj \
  -c Debug -m:1 -p:NuGetAudit=false --verbosity minimal

bash scripts/check-no-warning-override.sh
git -c core.whitespace=cr-at-eol diff --check
```

Preparation requires a fresh private output directory. A replay must choose
another fresh path and use the returned environment/hash. The first attempt
incorrectly supplied the occupied repository root as `--workspace`; it refused
with exit 2 / HXW007 before generating output. The fresh `/tmp` attempt above
corrected that invocation. No tool/dependency version or warning gate was changed.

EventStore 8.1–8.4 are done, 8.5 is in progress, and 8.6–8.11 are backlog.
The internal core remains non-packable and Server still registers its no-op.
The AzureKeyVault project, package catalog/release enrollment, and owner 8.11
closure packet are absent. I2/I19a classification and named G5 approval remain
pending. Consumable runtime/backend and persistence integration, dual-provider
GDPR parity, all seven deletion proofs, and post-v2 switch-back remain unproven.
Production KMS is a separate release gate. Local production code, public APIs,
DI, dependencies, gitlinks, rollback files, and blocked sprint status are retained.

`source-mapping.json` is the original pinned-tool mapping manifest from the
failed build preparation. Its content hash matches the exact environment above;
`audit.json` records its SHA-256 and all ten mapped dependencies' committed
root gitlinks, checkouts, and cleanliness, including Tenants and Platform.
The audit compares all 24 retained files, the compatibility harness, and the
DI registration source byte-for-byte with the recorded Parties baseline.
It validates the I2/I19a decision column specifically; placeholder owner/date
cells cannot substitute for a pending decision. The mapping manifest and raw
build output retain their original bytes.
