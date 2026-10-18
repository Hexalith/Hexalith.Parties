# DW-46 generated projection-fold logging verification

The implementation moves the seven existing projection-fold diagnostics into
`PartySdkProjectionFoldLog`, retaining their templates, structured fields,
severities, caller logger instances, and checkpoint behavior. Event IDs are
4802–4808 in the prior helper declaration order; event names are generated
method names. Exception values are reduced to type-name strings in the catch
block before any logging method receives them. Dependency configuration and
shared test helpers are unchanged.

## Commands and results

These outputs record the initial implementation checks. Copied evidence paths
are normalized from the workspace prefix to repository-relative paths.

Run from the repository root:

```bash
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

Passed: exit 0, zero warnings and errors (`build.txt`). The first invocation
reported CS8604 in two new test assertions because `ShouldBeAssignableTo`
returns a nullable value. Adding `ShouldNotBeNull` fixed the test assertions;
the original output is retained in `build-initial-nullability-failure.txt`.

```bash
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionLoggingTests -result-xml _bmad-output/implementation-artifacts/tests/dw-46-generated-projection-fold-logging-2026-10-18/focused.xml
```

Passed: exit 0, 11/11 tests, zero errors, failures, skips, or not-run tests
(`focused.txt`, `focused.xml`). The focused coverage pins all seven templates,
field names and values, levels, numeric IDs, generated event names, null logged
exceptions, disabled filtering, and identical fold results without a logger.
Additional structured converter-failure assertions reuse the existing isolated
converter fixture and verify that private exception messages never enter log
state or formatted output.

```bash
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -result-xml _bmad-output/implementation-artifacts/tests/dw-46-generated-projection-fold-logging-2026-10-18/projections.xml
```

Passed: exit 0, 346/346 tests, zero errors, failures, skips, or not-run tests
(`projections.txt`, `projections.xml`). This includes the existing diagnostic,
checkpoint, and converter privacy coverage.

```bash
bash scripts/check-no-warning-override.sh
git -c core.whitespace=cr-at-eol diff --check
```

Both passed with exit 0 (`build-gate.txt`, `whitespace.txt`). The following
additional check covers all three changed C# files, including untracked files:

```bash
python3 - <<'CHECK'
from pathlib import Path
paths = [
    Path('src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFold.cs'),
    Path('src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFoldLog.cs'),
    Path('tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionLoggingTests.cs'),
]
for path in paths:
    data = path.read_bytes()
    assert b'\n' not in data.replace(b'\r\n', b''), path
    assert data.endswith(b'\r\n'), path
    assert all(line.rstrip(b' \t') == line for line in data.splitlines()), path
print('PASS: CRLF, final newline, and no trailing whitespace in changed C# files.')
CHECK
```

Passed with exit 0 (`source-whitespace.txt`).

## Review-fix checks

Review fixes add public XML documentation, qualify the producer-controlled
metadata privacy boundary, document the event-ID migration for operators, and
strengthen rendered-message and private-message `JsonException` coverage.
Structured-state inspection now uses `IEnumerable<KeyValuePair<string,
object?>>`; filtering assertions require no sink calls and unchanged fold
results without pinning generator call counts.

```bash
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --configuration Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll -class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionLoggingTests -result-xml _bmad-output/implementation-artifacts/tests/dw-46-generated-projection-fold-logging-2026-10-18/review-fix-focused.xml
```

The build passed with exit 0 and zero warnings/errors (`review-fix-build.txt`).
The focused logging class passed 13/13 tests with zero errors, failures, skips,
or not-run tests (`review-fix-focused.txt`, `review-fix-focused.xml`). The first
review-fix build reported CA1062 when the expected-message assertion first used
the public theory's `template` parameter. An explicit null guard fixed that
issue; its original output is retained in `review-fix-build-guard-failure.txt`.
Only the focused logging class was rerun after these edits; the parent workflow
owns the full post-review verification.

## Scope and remaining work

The pre-change Aspire baseline and its security SSL readiness failure are
recorded in the approved spec; this library refactor did not rerun that topology.
The independent review and DW-46 ledger closure are owned by the parent workflow.
DW-47 category redesign remains deferred. No commit or push was performed by the
implementation subagent.

## Final parent verification

After all review patches, the parent reran the spec's full verification:

- Debug/source build: exit 0, zero warnings/errors (`final-build.txt`).
- Focused logging class: 13/13 pass, no failures/errors/skips/not-run (`final-focused.txt`, `final-focused.xml`).
- Full projection assembly: 348/348 pass, no failures/errors/skips/not-run (`final-projections.txt`, `final-projections.xml`).
- Warning-override and whitespace gates: exit 0 (`final-build-gate.txt`, `final-whitespace.txt`).
- CRLF, final-newline and trailing-whitespace checks passed for all changed C# files.

`validation.json` records exact commands, exit codes, test counts and source hashes.
All three review layers completed. Eight small findings were corrected; two
low-value suggestions were rejected with recorded evidence in the spec. No new
deferred work was added. DW-46 is closed; DW-47 remains unchanged. The existing
Aspire security readiness failure remains a baseline observation. Copied test
and build outputs use repository-relative paths.
