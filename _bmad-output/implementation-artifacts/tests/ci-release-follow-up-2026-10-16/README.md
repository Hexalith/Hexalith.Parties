# October 16 CI/release follow-up evidence

Baseline: Parties `493730941329d1bca3b4f6b2f6a67ffe305c2443`, plus this
uncommitted implementation and concurrent owner query changes. These local
receipts do not establish committed-source CI or publication.

The owned central catalog selects SignalR.Common 10.0.13 and its Options /
DependencyInjection.Abstractions prerequisites. NuGet audit remains active.
The Web SDK prunes Common from UI package assets, so the UI-only
`RuntimeFrameworkVersion=10.0.13` floor is verified in both built and published
runtime configuration. Local runtimes currently stop at 10.0.12; UI starts
require 10.0.13. The container liveness gate remains required.

The topology fixture models a fresh Redis container with no volume or
persistence, routes every modelled state/pubsub component through the SDK's
dynamic metadata provider before startup, and rejects unmodelled component
paths. All sidecars receive the private address. The marker helper only reads
the private endpoint and activates through the existing authenticated API;
shared locking, snapshots, marker deletion and restoration are removed.
Shutdown stops workers/sidecars before application disposal removes Redis,
then cleans the owned mTLS resources. Failed cleanup retains ownership evidence
for retry, and an unconfirmed shutdown preserves the control plane.

| Check | Result | Local log |
| --- | --- | --- |
| Package-mode solution Release build | Exit 0; 0 errors, 2 upstream NU1903 warnings | `/tmp/parties-oct16-solution-build-final.log` |
| All 15 individual projects | 3,070 passed; 7 skipped; 0 failed | `/tmp/parties-oct16-all-projects.log`; TRX: `/tmp/parties-oct16-all-results` |
| Private routing, startup failures and cleanup ordering | 10/10, no skips | `/tmp/parties-oct16-isolation-tests.log` |
| Scoped mTLS bootstrap and cleanup | 15/15, including foreign resources and resulting state | `/tmp/parties-oct16-bootstrap-tests.log` |
| Current source prerequisite receipts | 33/33, no skips | `/tmp/parties-oct16-current-receipt-tests.log` |
| Nine manifest packages at 0.0.0-ci-test | Pack and metadata validation pass | `/tmp/parties-oct16-pack.log`; `/tmp/parties-oct16-package-validation.log` |
| Isolated client and portal consumers | Both pass; 0 warnings/errors | `/tmp/parties-oct16-consumer-validation.log` |
| UI publish, serialized | Exit 0; published runtime floors 10.0.13 | `/tmp/parties-oct16-ui-publish-serial.log` |
| Required-mode live gateway | 1 failed, 0 skipped; Sentry startup blocked | `/tmp/parties-oct16-private-gateway-final.log` |

Commands:

```text
dotnet restore Hexalith.Parties.slnx -p:UseNuGetDeps=true -p:UseHexalithProjectReferences=false
dotnet build Hexalith.Parties.slnx --configuration Release -m:1 -p:UseNuGetDeps=true -p:UseHexalithProjectReferences=false
pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory /tmp/parties-oct16-all-results
python3 scripts/pack-release-packages.py /tmp/parties-oct16-packages 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py /tmp/parties-oct16-packages
python3 scripts/validate-consumer-package-references.py /tmp/parties-oct16-packages --work-directory /tmp/parties-oct16-consumers
dotnet publish src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj --configuration Release --no-restore --no-build --output /tmp/parties-oct16-ui-publish -m:1
GITHUB_ACTIONS=true dotnet tests/Hexalith.Parties.IntegrationTests/bin/Release/net10.0/Hexalith.Parties.IntegrationTests.dll -method '*CreatePartyCommand_ThroughEventStoreGateway_CompletesWithPersistedEventCountAsync'
```

The full solution warning originates solely in the separately built upstream
EventStore SignalR diagnostic source project, whose own catalog still selects
Common 10.0.12. Shared catalogs and gitlinks remain unchanged. Initial
Common-only restore exposed NU1109 prerequisites; those were fixed centrally.
The initial parallel UI publish hit MSB4166; the serialized retry passed.

The local all-project topology had 45 passes and seven explicit skips: six
historical health deferrals and the gateway reporting that Parties failed to
start. Required-mode execution on final code fails before AppHost startup:
Sentry reports `failed to add target /var/run/dapr/credentials: no space left on
device` (file watcher exhaustion). This is a blocker, not live gateway proof.
No host limits, required assertions, ACLs or mTLS policy were changed.

Container identity snapshots after the all-project run matched the complete
pre-test inventory, with no fixture Redis or scoped mTLS containers remaining.
The final required-mode attempt also removed its control plane and trust
bundle. All pre-existing containers remain. Two unrelated security/tunnel
containers subsequently appeared from another session and were preserved.
No unrelated Redis marker was read or modified by the fixture.

`bash scripts/check-no-warning-override.sh`, `bash -n scripts/aspire-start-mtls.sh`,
`git -c core.whitespace=cr-at-eol diff --check`, and the gitlink RC gates
`--worktree`, `--diff v1.3.0`, and `--diff v1.1.1` pass. The ledger selects only
the seven existing committed pointers changed since v1.3.0 under the invoked
spec's release authorization. Historical source observations and approvals
remain intact. Git publication, fresh exact-source CI and Release remain owned
by the parent workflow.

Final parent verification after review patches: 15 projects passed; 3,114 tests
passed, seven skipped, zero failed. CI checks passed 128/128; topology passed
49 with seven explicit skips. The full Release build has zero errors and the
same two upstream NU1903 warnings. All review patch groups have regression
coverage. Fresh exact-source CI and release publication are still pending.
