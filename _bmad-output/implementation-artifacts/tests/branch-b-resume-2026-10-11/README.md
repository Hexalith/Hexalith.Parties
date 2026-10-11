# Branch B current-source continuation — 2026-10-11

Parties now inherits EventStore `3.120.0` from the tracked shared package catalog after removal of its obsolete `3.117.1` override. A normal Debug package-mode build of `src/Hexalith.Parties/Hexalith.Parties.csproj` exited 0 with zero warnings and errors. Six Parties owner test projects built in Debug source mode with `UseHexalithProjectReferences=true`, `UseNuGetDeps=false`, the root-declared EventStore/Commons source paths, and `HexalithEventStoreVersion=3.120.0`. Their selected classes passed **697/697**, zero failed/skipped/not-run. The first UI execution omitted `HEXALITH_PARTIES_SOURCE_ROOT` and failed five source-discovery cases; the corrected run in [parties](parties/) passed **72/72** and supersedes it. The edited Platform Identity P1 adapter class also passed **104/104** in [platform](platform/).

The latest complete Local invocation was:

```powershell
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/parties/references/Hexalith.EventStore -PlatformRoot /home/administrator/projects/hexalith/parties/references/Hexalith.Platform -EvidenceDirectory /tmp/ext-parties-1-agent-local-final-20261011 -ArtifactsDirectory /tmp/ext-parties-1-agent-local-final-20261011/artifacts -MemoriesRoot /tmp/ext-parties-1-agent-local-final-20261011/optional-memories-package-mode
```

It exited **1** at Platform Custody. Before that point, EventStore Contracts, Client, Server and PayloadProtection passed **37/343/403/33**, and Platform Identity admission passed **2**. Six Platform Custody source CA1062 findings were then fixed. The Custody source project built with zero warnings/errors. The exact post-fix test-project command below still exits **1**, with **38 errors** across 11 unrelated Platform Custody test files (29 CA2007, 8 CA1062, 1 CA1822); its [raw build log](gate/custody-test-build.txt) is retained. This is a blocker to a complete current Local pass, not a skipped lane.

```bash
dotnet build tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj -c Debug --no-restore --artifacts-path /tmp/ext-parties-1-agent-local-final-20261011/artifacts -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/parties/references/Hexalith.EventStore -p:HexalithCommonsRoot=/home/administrator/projects/hexalith/parties/references/Hexalith.Commons -p:HexalithEventStoreVersion=3.120.0 -p:NuGetAudit=false -m:1 --nologo -v:q
```

The frozen matrix has passing local coverage in the retained Parties XML: `ExactLostAckRetry_ReturnsOriginalWithoutAnotherCreationAndRejectsChangedIntent` and rejection-only creation (provision); `CurrentHuman_RequiresCompleteSourceAndCurrentExactActor` and erasure denial (current); `HistoricalReplay_BeforeBoundaryReturnsOriginalAndAtBoundaryReturnsSuccessor` plus restored history (history); `WrongAuthorizedActor_DeniesBeforeSourceRead` and `ConcurrentTenantCalls_KeepEveryRequestAndEvidenceInItsExplicitScope` (isolation). This is local test coverage, not persisted installed acceptance. The [verification manifest](verification-manifest.json) records each copied XML receipt and SHA-256 digest. No full compiled-source/PDB or unchanged-whole-worktree audit was performed for this continuation.

Live was invoked as:

```powershell
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Live -EvidenceDirectory /tmp/ext-parties-1-agent-live-20261011
```

It exited **1** before endpoint access for 12 missing target, credential, policy and custody inputs; the exact list is in [live-gate.txt](gate/live-gate.txt). No production `IIdentityHistoryCustody` backend, qualified all-copy destruction/nonrollback restore, approved successor continuation, or installed P-01–P-10 persisted probes/targets was found. Both original spec execution tasks remain open. No installed identity availability is established.
