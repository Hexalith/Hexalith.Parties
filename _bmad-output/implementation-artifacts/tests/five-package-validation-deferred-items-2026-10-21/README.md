# Five package validation deferred items

Baseline: `1bed6c139f7699bc717182763c04099ea75abc5b`.

This batch completes the four unnumbered DW-67/DW-69 follow-ups for
support-version normalization, restored FrontComposer/Tenants versions,
inherited fallback package folders and inherited disabled feeds, plus DW-188.

`validation.json` records exact commands and outcomes. The baseline isolation
probe reproduces both inherited-setting defects; baseline-restored-versions.json
shows stale FrontComposer/Tenants selections proceeding to build. The initial
regression log demonstrates rejection of NuGet-produced normalized archives and
unintended fallback restore. Its new-signature error is superseded by the
separate baseline restored-graph probe.

The maintained Python regressions generate real Release SDK packages and run
real offline NuGet restores with disposable feeds and caches. Restored-graph
fixtures check stale versions, prereleases, nonzero revisions, missing required
IDs, case variants and project substitutions before consumer build. The CI
assembly runs these suites and 47 fixtures executing the actual registry-floor
Bash with fake GitHub/NuGet responses. Fixtures perform no publication.

The required Aspire baseline used existing build output and isolated ports;
resources waited on the external security endpoint. The selected Parties host
was stopped before compilation. No fresh topology or full nine-package Release
consumer qualification is claimed by these focused checks.

NuGet identity normalization follows the official package-version reference:
https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#normalized-version-numbers

Independent review corrected environment-based fallback inheritance and strengthened
distinct-family selection, real normalized-package restore, pinned SDK selection,
pagination/reachability and manifest/status fixtures. The environment regression
is retained red before its correction and green afterwards. Runnable baseline
probes bind their source to the full starting Git revision; exact commands are
recorded in validation.json. The unrelated existing malformed-prerelease grammar
issue is reproduced by deferred-prerelease-probe.py and remains in the ledger.

Final result: Debug CI build has zero warnings/errors; all 238 affected CI cases
pass with no failures or skips. Warning/submodule, Python syntax and whitespace
checks pass. Follow-up independent review confirms no remaining material
introduced issue. Pinned commitlint 21.2.2 validates commit-message.txt exactly.
