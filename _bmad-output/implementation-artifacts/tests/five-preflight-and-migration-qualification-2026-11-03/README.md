# Five deferred items: preflight and migration qualification

DW-159 and DW-205–207 are resolved. DW-58 advances through eight executed
disposable-provider controls and remains open for deployment qualification.
The batch changes tests and documentation only. `identities.json` records the
full revisions, ten committed gitlinks and evaluated package families.

| Check | Result | Evidence |
| --- | --- | --- |
| Current packaged runtime preparation | 10 projects rebuilt; exit 0; zero warnings/errors | `current-runtime.txt`, `current-runtime-prepared.json` |
| CI preparation controls before review | 57 passed, zero skipped | `ci-tests.xml` |
| Changed real-compiler control after review | 1 passed, zero skipped | `ci-final.xml` |
| Supervisor controls under two CPU workers | 30 passed in each of three runs | `supervisor-1.xml` through `supervisor-3.xml` |
| Existing Contracts package controls | 5 passed, zero skipped | `contracts-package.xml` |
| Final Parties classes | 26 passed, zero skipped, including 8 real-provider controls | `parties-reviewed.xml` |
| Current I16 selection assertion | 1 passed, zero skipped | `selection.xml` |
| Provider cleanup | Sidecar stopped; container removed; independently verified absent | `provider.json` |

The affected CI, Contracts and Parties assemblies build with zero warnings and
errors. `verification.json` binds the final sources, assemblies and receipts to
their SHA-256 hashes. Paths in public receipts are normalized to
`${REPOSITORY_ROOT}`, `${USER_HOME}` and `${TEMP_DIR}`. Original command output
and private provider configuration remain outside the repository.

## Commands and execution scope

Commands run from the repository root:

```bash
bash scripts/prepare-aspire-runtime-hosts.sh --gateway-profile packaged --configuration Release
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Debug -m:1
dotnet build tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release -m:1
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release -m:1
```

Invoke the built assemblies directly with these xUnit v3 filters. Each run also
used `-result-xml ${TEMP_DIR}/parties-five-preflight-<name>.xml`; the initial CI
run used its deprecated `-xml` alias, which emitted a notice but executed all
57 cases.

```bash
dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.ExternalRuntimeCompilationTests -class Hexalith.Parties.Ci.Tests.AspireRuntimePreparationTests
dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.ExternalRuntimeCompilationTests
dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll -class Hexalith.Parties.Contracts.Tests.Package.PackageTestProcessTests
dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll -class Hexalith.Parties.Contracts.Tests.Package.ContractsPackageTests
PARTIES_BACKFILL_STATE_RUNTIME_CONFIG=${PRIVATE_OWNED_CONFIG} dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillTests -class Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillRuntimeTests -class Hexalith.Parties.Tests.FitnessTests.PackageCatalogFallbackTests -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests
dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -method Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests.CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals
```

The supervisor command ran three times with two owned Python `while True: pass`
workers active. Each test run had a 180-second subprocess timeout. Both workers
were terminated and awaited in `finally`; all three commands exited 0.

The compiler fixture packs real local archives, then executes the unchanged
runtime-preparation script and validator using the native Tenants administrator
verifier source. Missing and compatible contracts share version `9.9.9` and use
separate caches. The negative control requires actual CS0246 for both missing
contracts, no later runtime build and no completion manifest. The compatible
control requires all ten actual build receipts. Hostile environment/import/cache
inputs are sanitized before every child launch without global environment changes.

## Actual provider boundary

The provider was a fresh loopback Redis container at the immutable image digest
in `provider.json`, with a private Dapr 1.18.7 sidecar. Redis persistence was
disabled for disposable cleanup; neither Redis nor Dapr was restarted during
the test. The sidecar used a unique `parties-backfill-rehearsal-` app identity,
`backfill-` state component, app-prefixed keys, private API token, isolated
components directory, and disabled placement/scheduler endpoints. HTTP/gRPC
metadata and an independently observed SDK-written Redis probe bind the selected
endpoints to that app and backend before synthetic seeding.

The final eight controls cover absent and existing canonical rows, expected
ETags, supported aggregate one-shot rebuilds, shared-index stage/commit/verify,
erasure, processing coverage, and nine exact actor/foreign-tenant survivor values.
Six race variants conditionally update only detail, processing or index after
preparation and before the real SDK stage call. They assert exact refusal codes
and every winner/sibling value. The decorator forwards real SDK operations and
does not fabricate provider results. Raw Redis access is restricted to independent
synthetic historical actor seeding/inspection and the endpoint-binding probe;
all migration writes use the SDK. Fresh clients, stores and handlers are built
for each transition/read.

No workload-process restart, aggregate staged-resume, installed-workload
authentication, production capture, writer drain, complete history/key inventory,
deployed cutover or rollback acceptance is established. DW-58 remains open.
The whole topology, deploy, UI accessibility and all-project test lanes were not
run; focused success gives them no parity credit.

## Intermediate diagnostics and independent review

`parties-build-initial.txt` retains the first helper build's 16 CA2007 errors;
they were corrected with `ConfigureAwait(false)`, without suppression.
`parties-classes.xml` records the first provider fixture's three actor-key failures
and an existing documentation-inventory failure. Concurrent main commit
`708f4308b3c67157137b728374b97b1890778c49` independently corrected that inventory;
it is preserved, not included in this batch's changes.

`parties-final.xml` records three later fixture failures from unguarded aggregate
staged verification and competition after staged reservation. The final fixture
uses the documented aggregate one-shot lifecycle plus fresh persisted reads, and
injects competition before real staging. These superseded fixture trials do not
establish a defect in the supported owner workflow and receive no qualification
credit. `parties-tests.xml` records an initial combined class/method filter that
selected zero tests; the corrected class and method commands are separate.

Blind review produced eight findings. Seven fixture/coverage gaps were patched
and independently rechecked. The process-restart request is retained as an
explicit unqualified boundary of the authorized DW-58 advancement. The root spec
records each finding, verdict and evidence; no new review work is deferred.

`commit-message.txt` is the exact candidate validated by the repository-pinned
commitlint CLI. `commitlint.txt` preserves successful validation. No package,
gitlink, production source or deployment selection changes in this batch.
