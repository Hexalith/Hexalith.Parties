# Native Admin UI source-mapping validation — 2026-11-10

The Parties test host now leaves source selectors to the verified mapper and
retains its explicit package selectors outside source mode. The native UI still
uses the hosted SDK profile, real Razor assembly, original assets and artifact
provenance. Mapping guards and warnings-as-errors were not changed.

Baseline: `87a7b600353b8967ab7b537c1e8dbd4a4b1b5cdc` on `main`, initially clean.
SDK: `10.0.403`; module tool: pinned `4.47.0`. Full root gitlinks, commands, working directories,
explicit environment overrides, UTC intervals, exits and log hashes are in
`verification.json`. The commands inherit the process environment after clearing
the seven mapping keys; no warning/audit/version overrides were added. Historical
receipts and dependency selections remain unchanged.

| Check | Source Debug | Package Release |
| --- | --- | --- |
| IntegrationTests build, forced restore, serialized | 0 warnings/errors | 0 warnings/errors |
| Development catalog configuration | 11 pass | 11 pass |
| Actual generator and preparation/cleanup | 13 pass | 13 pass |
| Catalog model finalization | 33 pass | 33 pass |
| Actual private Admin UI composition | 4 pass | 4 pass |
| Catalog gateway selectors | 24 pass | 24 pass |
| Nested build profile regression | 4 pass | 4 pass |

Final focused total: **178 passed, zero failures/skips**. These are direct xUnit
assembly runs with `-failSkips`, not project filters or a full solution suite.
The profile fixture executes the production nested MSBuild target against a tiny
producer and observes both Restore and Build inputs plus its returned Reference.
It covers source mapping, package mapping and ordinary unmapped Debug and Release builds.

The unchanged baseline IntegrationTests build fails with the reported HXW006.
Before the fix, the new profile regression fails its source case and passes its
two package cases. These red runs are retained separately. After the fix, a
negative `PrepareForBuild` probe with a deliberately false source selector still
exits 1 with HXW006. A second probe independently conflicts UseNuGetDeps and
also exits 1 with HXW006. `check-no-warning-override.sh` passes. Native assets files
show all six Hexalith dependencies are projects in source mode and packages in
Release; `native-dependency-modes.json` is derived from those archived originals.

The source mapping hash is
`a969d0173bbc6bf0f254f32d86370b4d95d78d1eada5f44d9a2704fd723d1839`;
package mapping hash is
`76982f02c05ed524c4eec2c3ca33e68f075f2a16bcc77f59a02b05419a1dc96c`.
`*-mapping.tar.gz` archives the generated inputs and imported pinned-tree props; prepare JSON preserves emitted
environments. XML/log gzip archives preserve raw command output except the
AppHost baseline, which retains only resource name/state observations. That
baseline had resources waiting for security readiness and was stopped before
edits. It does not establish a healthy runtime topology.

`*-artifact-hashes.json` binds the integration test, test host and native UI
assemblies immediately after each build. Archived native artifact TSV and
project.assets.json retain provenance and restored dependency selection. Package
verification ran after source tests because the native project's shared obj
restore state changes with mode. Run a normal restore when switching modes.

To reproduce at the same root gitlinks, use a fresh absolute workspace per mode:

```sh
dotnet tool run hexalith-module -- prepare-build \
  --manifest module/hexalith.module-manifest.v2.json \
  --mode source --workspace /tmp/new-unique-source-workspace --output json
```

Save stdout as preparation JSON and apply its `buildPreparation.environment` to
the corresponding command array from `verification.json`. Use a fresh package
workspace and `--mode package` for Release. Do not hand-author source selectors
or reuse archived absolute imports as a new authority. For example:

```python
import json, os, subprocess
preparation = json.load(open('/tmp/fresh-preparation.json'))
receipt = json.load(open(
    '_bmad-output/implementation-artifacts/tests/'
    'hxw006-admin-ui-source-mapping-2026-11-10/verification.json'))
keys = receipt['checks']['source-after-build']['environmentOverrides']
env = os.environ.copy()
for key in keys:
    env.pop(key, None)
env.update(preparation['buildPreparation']['environment'])
command = receipt['checks']['source-after-build']['command']
subprocess.run(command, env=env, check=True,
    cwd=receipt['checks']['source-after-build']['workingDirectory'])
```

Test commands also record temporary XML destinations; choose new writable paths.
The ambient Git whitespace check reports the repository-required CRLF as
trailing whitespace in the csproj. The recorded CRLF-aware check keeps blank
line, end-of-line and indentation checks active and passes. No whitespace,
mapping or warning policy configuration was edited.

Every supporting file is indexed by SHA-256 in verification.json; the receipt
excludes itself from that index. The profile fixture uses an unrelated sentinel
version so it detects a hardcoded production SDK cohort.

Scope: build and focused compatibility evidence only. No deployed acceptance,
owner signoff, full solution test or package publication qualification is claimed.
