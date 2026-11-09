# Catalog selector source-build validation — 2026-11-09

The three query types are compiled into `Hexalith.EventStore.Gateway`; the executable host excludes `Queries/**/*.cs`. The selector test now uses its existing global aliases in both dependency modes. Its entire namespace/test body is byte-identical to HEAD, including all assertions and selector cases. The host alias used for `Program` in the paging gateway tests is untouched.

## Results

| Check | Exit | Result |
| --- | --- | --- |
| Pinned 4.44.13 `prepare-build`, source | 0 | Prepared; existing HXW010 tool-range and HXW011 local-tag diagnostics retained |
| Mapped Debug build before fix | 1 | Exactly 3 CS0234 errors, 0 warnings, original lines 20–22 |
| Mapped Debug build after fix | 0 | 0 warnings, 0 errors; source mode and warnings-as-errors verified |
| Source catalog selectors | 0 | 9 passed; 0 failed/skipped |
| Source paging handler + gateway | 0 | 875 handler + 77 gateway = 952 passed; 0 failed/skipped |
| Package Debug build | 0 | 0 warnings, 0 errors; external EventStore 4.1.0 packages verified |
| Package catalog selectors | 0 | 9 passed; 0 failed/skipped |
| Package paging handler + gateway | 0 | 875 handler + 77 gateway = 952 passed; 0 failed/skipped |
| Package prerequisite class (separate existing failures) | 1 | 114 passed, 4 failed, 0 skipped; same four failure names as prior receipt |
| Warning/nested-submodule guard | 0 | Passed |
| Target-file whitespace check | 0 | Passed |
| Whole-tree whitespace check | 2 | Existing user edit at `PartySdkQueryHandlerTests.cs:281` is flagged; bytes preserved |
| Actual SDK version | 0 | 10.0.403 |
| Preservation check | 0 | All 3,685 protected files and dependency gitlinks unchanged |

[validation.json](validation.json) records every exact command, working directory, per-command environment, exit code, build counts, test counts and failure details. Compressed logs and xUnit XML are adjacent and SHA-256-bound by the receipt. `source-mapping-files.json.gz` preserves the generated imports; `preserved-inputs.json.gz` records the pre-change protected-file hashes. No historical receipt was regenerated.

Source commands use only `buildPreparation.environment` returned by the pinned tool. The mapping hash is `5da9b4b43131010d971a5c8c6e70106d9bcdaad7c40bcdf00c73a458a18cc96b`. Package commands clear every source-mapping import/environment key; evaluation confirms empty `HexalithEventStoreFromSource`, `HexalithSourceMappingMode` and mapping hash. Package assets identify EventStore Gateway/Contracts/Server/DomainService as 4.1.0 packages. No version, project-reference configuration or validation policy changed.

This is focused Debug compatibility verification. No new full-suite, CI Release or deployed-topology pass is claimed. The four existing fitness failures still prevent a clean broad suite. The earlier DW-27 spec, receipts and closure evidence retain their historical statements unchanged.

## Existing PlatformApiPrerequisitesTests failures

These are separately reproduced prerequisite/observation failures, outside this alias fix. Test names below omit the common `Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests.` prefix.

| Test | Existing mismatch |
| --- | --- |
| `CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals` | Builds observation expects `3c8096180ecf2aa539ac454815efbfceb8a92822`; selected gitlink is `27145e52ffab5623198ba5efdab5cdb71aa42d28` |
| `CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption(dependency: "McpCli")` | Observation expects `ae4a317f208829681dc8eec36a308bc36f3f9715`; selected gitlink is `1c1f06b46bbe9e115022be8af75b10085c8ac360` |
| `CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption(dependency: "Platform")` | Observation expects `0f4e27f1f78310bd731c10c19dc51bb1e0345d44`; selected gitlink is `e39a9a2587ec150d550510763ab69db305d13fdb` |
| `Matrix_ValidationEvidenceCommandsAreReproducible` | Fixed-string search for `8-7-server-persistence-and-snapshot-integration: backlog` in EventStore sprint status finds no match |

No dependency-observation reconciliation was attempted. The assertions and observations are unchanged.

## Environment application and reproduction

`environment` in each receipt entry contains applied overrides, after clearing the named mapping keys from the inherited process environment. It is not a complete environment dump. `relevant_inherited_environment` records the allowlisted build/dependency overrides present during these checks. The SDK actually used is recorded by `dotnet --version`. Both modes use the same output path sequentially; tests run immediately after their corresponding build. These receipts do not attest to retained source-mode binaries after package mode overwrites that output.

The following read-only comparison reproduces the protected-file check. The original snapshot selected every tracked file under `_bmad-output/`, the root tool manifest, `.gitmodules`, build props/targets, package props, `global.json`, the two pre-existing modified docs, the handler and gateway test files, production query service, prerequisite test class and test csproj, plus all pre-existing untracked files. It excludes directories/gitlinks; the complete gitlink comparison is recorded separately. Newly created artifacts for this fix are outside the initial snapshot.

```bash
python3 - <<'PY_CHECK'
from pathlib import Path
import gzip, hashlib, json
root = Path('/home/administrator/projects/hexalith/parties')
packet = root / '_bmad-output/implementation-artifacts/tests/catalog-selector-source-build-2026-11-09'
snapshot = json.loads(gzip.decompress((packet / 'preserved-inputs.json.gz').read_bytes()))
changed = [p for p, digest in snapshot.items()
           if not (root / p).is_file() or hashlib.sha256((root / p).read_bytes()).hexdigest() != digest]
print(f'{len(snapshot)} protected files; changed: {changed}')
assert not changed
PY_CHECK
```

To replay any recorded build/test with its intended dependency mode, first rerun the exact `prepare-source` command below to recreate its imports, then apply that check's recorded environment. Choose `source-build`, `source-selectors`, `source-paging`, `package-build`, `package-selectors` or `package-paging` for `check_name`. Run a mode's build before replaying its tests, because the output path is shared. This example defaults to the source build; changing `check_name` to `package-build` clears the mapper keys and applies the recorded empty package override set. On another checkout or mapping workspace, use its newly emitted source environment and command paths.

```bash
python3 - <<'PY_REPLAY'
from pathlib import Path
import json, os, subprocess
root = Path('/home/administrator/projects/hexalith/parties')
receipt = json.loads((root / '_bmad-output/implementation-artifacts/tests/catalog-selector-source-build-2026-11-09/validation.json').read_text())
check_name = 'source-build'
check = next(c for c in receipt['checks'] if c['name'] == check_name)
environment = os.environ.copy()
for key in check['cleared_environment_keys']:
    environment.pop(key, None)
environment.update(check['environment'])
raise SystemExit(subprocess.call(check['command'], cwd=root, env=environment))
PY_REPLAY
```

## Exact commands

All commands ran from `/home/administrator/projects/hexalith/parties`. The source build before/after and its two test commands use the emitted source environment recorded in `validation.json`. Package commands run with the recorded mapping keys removed. Reproducing in a new workspace requires running `prepare-build` there and applying its newly emitted environment, rather than retaining the temporary import paths below.

### prepare-source

```bash
dotnet tool run hexalith-module -- prepare-build --manifest module/hexalith.module-manifest.v2.json --workspace /tmp/parties-catalog-selector-20261109-lnazytho/mapping --mode source --output json
```

Exit: `0`.

### source-build-before

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug --disable-build-servers -m:1
```

Exit: `1`.

### source-selection

```bash
dotnet msbuild tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -nologo -getProperty:HexalithEventStoreVersion,HexalithEventStoreFromSource,HexalithSourceMappingMode,HexalithSourceMappingHash,Configuration,TargetPath,TreatWarningsAsErrors -getItem:ProjectReference
```

Exit: `0`.

### source-build

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug --disable-build-servers -m:1
```

Exit: `0`.

### source-selectors

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesCatalogGatewaySelectorTests -failSkips -result-xml /tmp/parties-catalog-selector-20261109-lnazytho/source-selectors.xml
```

Exit: `0`.

### source-paging

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests -failSkips -result-xml /tmp/parties-catalog-selector-20261109-lnazytho/source-paging.xml
```

Exit: `0`.

### package-selection

```bash
dotnet msbuild tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -nologo -getProperty:HexalithEventStoreVersion,HexalithEventStoreFromSource,HexalithSourceMappingMode,HexalithSourceMappingHash,Configuration,TargetPath,TreatWarningsAsErrors -getItem:ProjectReference
```

Exit: `0`.

### package-build

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug --disable-build-servers -m:1
```

Exit: `0`.

### package-selectors

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesCatalogGatewaySelectorTests -failSkips -result-xml /tmp/parties-catalog-selector-20261109-lnazytho/package-selectors.xml
```

Exit: `0`.

### package-paging

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests -failSkips -result-xml /tmp/parties-catalog-selector-20261109-lnazytho/package-paging.xml
```

Exit: `0`.

### package-prerequisites

```bash
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -result-xml /tmp/parties-catalog-selector-20261109-lnazytho/package-prerequisites.xml
```

Exit: `1`.

### warning-guard

```bash
bash scripts/check-no-warning-override.sh
```

Exit: `0`.

### whitespace

```bash
git diff --check -- tests/Hexalith.Parties.Tests/Gateway/PartiesCatalogGatewaySelectorTests.cs
```

Exit: `0`.

### sdk-version

```bash
dotnet --version
```

Exit: `0`. Result: `10.0.403`.

### whole-tree-whitespace

```bash
git diff --check
```

Exit: `2`. Reports trailing whitespace on the pre-existing modified comment at `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs:281` (the diagnostic includes its CRLF). That input matches the pre-change hash exactly. The focused selector-file check passes; no unrelated whitespace cleanup or validation override was applied.
