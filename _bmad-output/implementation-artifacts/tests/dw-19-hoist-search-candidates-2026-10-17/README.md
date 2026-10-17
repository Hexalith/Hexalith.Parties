# DW-19 validation — 2026-10-17

Both search providers now prepare the normalized full phrase and candidate array once per search. Matching and scoring remain unchanged, including the original token-count denominator. DW-20 remains separate and open.

Validation uses Debug assets and source references from the Parties root. The final build has zero warnings/errors; the focused provider, compatibility and performance classes pass 80/80, and the full service assembly passes 1098/1098 without failures, errors, skips or unrun tests. All four independent review findings were patched with focused tests; no new work was deferred.

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Search.SearchQueryCandidateTests -class Hexalith.Parties.Tests.Search.LocalFuzzyPartySearchProviderTests -class Hexalith.Parties.Tests.Search.SemanticPartySearchProviderTests -class Hexalith.Parties.Tests.Search.LocalFuzzySearchPerformanceBenchmarkTests -class Hexalith.Parties.Tests.Search.SemanticSearchPerformanceBenchmarkTests -result-xml /tmp/dw-19-validation/focus-final.xml
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -result-xml /tmp/dw-19-validation/full-final.xml
bash scripts/check-no-warning-override.sh
node node_modules/@commitlint/cli/cli.js --edit /tmp/dw-19-validation/commit-message.txt --verbose
git -c core.whitespace=cr-at-eol diff --cached --check
```

The baseline focus ran the initial nine cases before changing production: seven behavioral cases pass and both allocation cases fail. Increasing a two-token query from 7 to 129 characters added 2,400,728 bytes over 10K nonmatching entries in each provider. With the fix, this growth is 968 bytes. The fixed 64 KiB regression bound is far above constant query preparation and far below the former per-entry cost.

The additional array regression compares 2 versus 32 tokens with empty display names and no structured fields, preventing per-token matching allocations from confounding the measurement. An isolated mutation kept the full phrase hoisted but moved candidate-array creation back into EvaluateEntry: both query-length cases pass, while both array regressions fail with over 2 MB of growth. The final sources were restored, rebuilt, and verified with the complete final focus and service run. This proves the checks detect either half of DW-19 independently. See focus-array-mutation.xml and validation.json for exact observations.

Compatibility coverage pins whitespace/diacritic normalization, duplicate tokens, zero normalized tokens, three-token partial coverage, fuzzy-token versus synthetic-phrase behavior, cross-field semantic metadata, stable ordering and paging. Existing single-token, filter, erasure and cancellation tests also pass. The unchanged 500 ms latency gates remain green; measured samples are in focus-final.xml and validation.json.

Representative raw build/focused logs and XML are retained. Repository prefixes, machine/user metadata and trailing whitespace are removed; result counts and measurements are unchanged. full-summary.txt retains the exact full-run summary without unrelated host logs; raw full XML/logs remain under /tmp/dw-19-validation for this session.

The exact Parties AppHost was started, inspected and stopped with Aspire. Parties and security were healthy, and the 20-second Parties healthy wait succeeded. EventStore, Tenants and UI reported Finished, so full-topology acceptance is not claimed. No package-mode/Release or external deployment qualification was attempted for this local hot-path change.

The new C# test file uses the required CRLF endings; the Git whitespace check explicitly recognizes CR at line ends. Existing source-file endings are preserved to keep the production diff focused.
