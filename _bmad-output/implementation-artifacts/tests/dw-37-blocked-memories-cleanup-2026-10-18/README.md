# DW-37 blocked Memories cleanup coverage

Eight new cases exercise the existing missing-CaseId branch using the production mapping-store class and the existing serialized Dapr test double. Tests assert retained inventory, no unscoped requests, progress and privacy diagnostics, continued cleanup of eligible units, and recovery through restored fallback configuration or persisted-case repair. Executable production source is unchanged.

The final Debug/source build has zero warnings/errors. The cleanup class passes 41/41 and the full service assembly passes 1195/1195 without failures/errors/skips. All three Blind Hunter findings are patched and verified. No new deferred work.

| Validation | Exact command | Retained output |
| --- | --- | --- |
| Build | `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` | [Build output](build.txt) |
| Cleanup class | `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Search.PartyMemoryCleanupServiceTests -result-xml /tmp/dw-37-validation/focus-final.xml` | [Runner output](focus.txt) |
| Full service assembly | `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -result-xml /tmp/dw-37-validation/full-final.xml` | [Runner summary](full-summary.txt) |
| Warning guard | `bash scripts/check-no-warning-override.sh` | [Validation manifest](validation.json) |
| Whitespace | `git -c core.whitespace=cr-at-eol diff --cached --check` | [Validation manifest](validation.json) |
| Commit message | `node node_modules/@commitlint/cli/cli.js --edit /tmp/dw-37-validation/commit-message.txt --verbose` | [Commitlint output](commitlint.txt) |

Commands run from the Parties root. Build output replaces the absolute workspace prefix with a relative path. Full output retains the runner summary only; test totals in the manifest are independently extracted from xUnit XML. XML remains in the temporary validation directory and its SHA-256 is recorded in the manifest.

The separate runtime baseline command `aspire start --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --format Json` exited 2 with MSB4166 child-node exits and canceled NuGet restore requests. A subsequent `aspire describe --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --format Json` reported no running AppHost. No full-topology qualification is claimed. [Validation manifest](validation.json) records the exact blocker separately from the successful tests.
