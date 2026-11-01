# Dependency synchronization and release verification — 2026-11-01

The retained registry inventory is the initial observation, before selecting
Commons 2.30.8 and PolymorphicSerializations 1.19.7. Each package row includes
its authoritative NuGet version-index URL. SDK metadata at
https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json
reported stable SDK 10.0.403; every root-owned .NET repository already pins it.

Source selection does not grant historical platform or custody qualification.
The shared catalog audit keeps that distinction explicit. Final hosted CI and
release identities will be recorded when the external operations complete.

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
