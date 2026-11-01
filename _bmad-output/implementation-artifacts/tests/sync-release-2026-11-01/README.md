# Dependency synchronization and release verification — 2026-11-01

The retained registry inventory is the initial observation, before selecting
Commons 2.30.8 and PolymorphicSerializations 1.19.7. Each package row includes
its authoritative NuGet version-index URL. SDK metadata at
https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json
reported stable SDK 10.0.403; every root-owned .NET repository already pins it.

Source selection does not grant historical platform or custody qualification.
The shared catalog audit keeps that distinction explicit. The final hosted CI and
release identities are retained below and in the adjacent JSON receipts.

## Completed local checks

- `dotnet build Hexalith.Parties.Standalone.slnx --configuration Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1`
  passed with zero warnings and errors after the package updates.
- `dotnet tool restore` restored module CLI 4.41.0; `dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json --manifest references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json --manifest references/Hexalith.Memories/module/hexalith.module-manifest.v2.json`
  returned `validated: Success`.
- Builds `pwsh -NoProfile -File Tools/validate-package-version-audit.ps1`
  validated 314 packages, 154 families and one source after reconciling upstream.
- Builds `pwsh -NoProfile -File Tools/test-package-version-audit-generator.ps1`
  passed 115 scenarios. The shared audit preserves upstream provenance at
  `88776c37e0b3a5572c4c6af7a7a9e1de57d44cde`.
- `npm ci` passed under Node 24.22.1/npm 11.19.1. The current upstream bundled
  npm dependency reports 37 moderate and one high advisory. Supported
  `npm audit fix` does not resolve them; its forced alternative downgrades
  semantic-release. No forced downgrade or override was applied.

Local transient command logs are under `/tmp/parties-release-20261101`.
Hosted exact-source runs provide durable final CI/publication logs and artifacts.

- `dotnet tool run hexalith-module -- prepare-build --manifest module/hexalith.module-manifest.v2.json --workspace /tmp/parties-release-20261101/source-preparation --mode source --output json` returned exit0/status prepared under tool4.41.0. The retained JSON records nonblocking Platform-tag and catalog-selection diagnostics. This prepares imports; it does not claim a source build or live topology pass.
- Independent review identified and fixed whole-pair Zot selection, early registry authentication, current tool documentation, and effective package assertions. Updated workflow contracts passed69/69.

## Completed publication

[Release v3.0.0](https://github.com/Hexalith/Hexalith.Parties/releases/tag/v3.0.0)
was published from `639d6e98b496429eb0490d1f8feeec97bd1a3280`.
[CI 41303194270](https://github.com/Hexalith/Hexalith.Parties/actions/runs/41303194270)
passed, including the Aspire runtime suite. Exact-source RC, commitlint and
CodeQL also passed.
[Release 41306016150](https://github.com/Hexalith/Hexalith.Parties/actions/runs/41306016150)
passed admission, publication and registry verification. Both production
checkpoints were approved automatically under the user's explicit instruction.

- `hosted-verification.json` binds the successful runs to the released source,
  records all ten clean root submodules and the publication artifact identity.
- `publication-verification.json` is the hosted validator's result: eleven
  NuGet packages and three container images, each with linux/amd64 and
  linux/arm64 manifests and matching source provenance.
- `nuget-publication-proof.json` records independent downloads of all eleven
  version 3.0.0 packages. Their nuspec repository commits match the release,
  and their SHA-256 hashes match the hosted downloads. NuGet repository signing
  changes the unsigned GitHub asset bytes; no byte equivalence with those
  unsigned archives is asserted.
- `release-approval-monitor.txt` records the two automatic production approvals
  and the successful release conclusion.

SDK 10.0.403 was already the latest stable SDK at the registry observation.
The updated package graph selects Commons 2.30.8, PolymorphicSerializations
1.19.7 and EventStore 3.130.2; module CLI is 4.41.0. The upstream bundled npm
advisories remain the dependency limitation described above.
