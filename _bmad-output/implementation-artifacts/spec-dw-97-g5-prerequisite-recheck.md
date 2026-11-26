---
title: 'DW-97 G5 prerequisite recheck'
type: 'chore'
created: '2026-11-26'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem**

DW-97 tracks Parties Story 8.7, which requires an approved EventStore payload-protection provider. The last dated prerequisite recheck predates the current dependency revisions.

**Approach**

Verify the selected source revisions, effective package version, producer closure and release enrollment, and retained Parties provider. Record the observed gate and link the evidence from DW-97 and the G5 prerequisite matrix. Preserve the existing DW-185 edits. When approval or provider delivery is pending, keep DW-97 open and Story 8.7 blocked. Limit the recheck to documentation and source inspection. Runtime parity and switch-back need separate execution against an approved provider.

</frozen-after-approval>

## Implementation Notes

### 2026-11-26 result

G5 is `needs-additive-api`. DW-97 remains `open` and Parties Story 8.7 remains `blocked`. Parties Story 8.6 is `done`, satisfying the predecessor requirement.

| Identity checked | Observed value |
| --- | --- |
| Parties baseline | `3eb2aee3884604d1c36b06e82b427c1789740ca8` |
| EventStore root gitlink and clean checkout | `7435254cc1a148031224eebe1aafd53a8659b2cd` |
| Builds root gitlink and clean checkout | `055d7df5b49669d5b705ce163b0b2e187ecb9a50` |
| Default Release EventStore properties for the Parties Security test project | `HexalithEventStoreVersion=5.4.0` and `HexalithCatalogEventStoreVersion=5.4.0`. `HexalithEventStoreFromSource` and `UseHexalithProjectReferences` evaluated to empty. |

- EventStore 8.7 generic persistence and snapshot integration is `in-progress`. Release integration, Parties dual-provider parity, post-v2 rollback and G5 closure (8.8–8.11) are `backlog`. The selected tree has no tracked Story 8.11 closure packet, and the Parties matrix still records I2/I19a policy-hook classification as pending. EventStore 8.12 completed a bounded Dapr Kubernetes crypto qualification. The owning 8.7 spec leaves generic Server persistence open and names EventStore Story 3.21, PF-02 and target Platform custody and cluster qualification among its pending prerequisites. [Owner sprint status](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml#L294) · [Owner 8.7 spec](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-8-7-server-persistence-and-snapshot-integration.md#L280) · [Parties prerequisite matrix](story-8-3-platform-api-prerequisite-matrix.md)
- At the EventStore gitlink, Server links the core source and defaults to `NoOpEventPayloadProtectionService`. The generic `AddEventStorePayloadProtection` registration refuses enabled startup. The separate `AddInteractionPayloadProtection` opt-in requires host-provided services for interaction protection. The core and Azure Key Vault projects are non-packable and have no standalone entries in the selected release inventory or Builds catalog. [Server registration](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs#L159) · [Generic registration](../../references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/PayloadProtectionServiceCollectionExtensions.cs#L16) · [Interaction registration](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/InteractionPayloadProtectionServiceCollectionExtensions.cs#L44) · [Release inventory](../../references/Hexalith.EventStore/tools/release-packages.json)
- The Parties retention inventory has all 18 MOVE files, five KEEP files and the adapter. All 24 files, the local compatibility harness and the DI source match the Parties baseline byte for byte. The local key backend, protection service and adapter registrations are present, including the interface binding to the adapter. [Retention inventory](8-7-data-protection-extraction.md#file-ownership-and-change-boundaries) · [Parties registration](../../src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs#L211)

Adoption requires the EventStore 8.11 owner and security approval packet, bound to the approved provider/package or root gitlink, and a recorded policy-hook decision. Dual-provider replay and GDPR parity, the seven deletion proofs and an exercised post-v2 switch-back remain open. Production KMS qualification is tracked separately.

### Verification

- Ran the 32 structural commands in the matrix's **Payload protection engine package** row. Thirty commands returned 0 as expected. The two searches marked `expected no matches` returned 1. Also inspected the two gitlinks and checkout statuses, the owner sprint status and committed tree, release enrollment and the Parties retention inventory. [Matrix observation](story-8-3-platform-api-prerequisite-matrix.md#story-87-g5-prerequisite-recheck--2026-11-26)
- `run-with-temp dotnet msbuild tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -p:Configuration=Release -getProperty:HexalithEventStoreVersion,HexalithCatalogEventStoreVersion,HexalithEventStoreFromSource,UseHexalithProjectReferences` exited 0. This evaluated project properties. Product, dual-provider, GDPR and rollback tests were unrun.
- `run-with-temp bash scripts/check-no-warning-override.sh` exited 0.

To rerun the 32 matrix commands, run the following from the Parties root. The result applies to the checked-out dependency revisions.

```bash
run-with-temp python3 - <<'PYTHON'
from pathlib import Path
import re
import shlex
import subprocess

matrix = Path('_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md')
row = next(line for line in matrix.read_text().splitlines() if line.startswith('| Payload protection engine package |'))
commands = re.findall(r'`(rg -n -F [^`]+|test ! -f [^`]+)`(\s*\(expected no matches\))?', row)
assert len(commands) == 32
for command, expects_no_match in commands:
    expected = 1 if expects_no_match else 0
    result = subprocess.run(shlex.split(command), capture_output=True, text=True, check=False)
    assert result.returncode == expected, (command, expected, result.returncode, result.stderr)
print(f'{len(commands)}/32 G5 structural checks matched expected exit codes.')
PYTHON
```

## Review Triage Log

- Low, patched. The canonical matrix row described the November 9 observation as current. It now dates it as historical and links to the November 26 observation. The Story 8.7 metadata now records the date and identities for the current recheck.
- Medium, patched. The verification link led to the summary alone. Added the rerun command and the observed exit-code counts.
- Medium, patched. EventStore Story 3.21 and PF-02 were missing from the upstream dependency summary. The receipt now names both alongside target Platform custody and cluster qualification.
- False as a review finding. The spec uses `in-progress` while review is open and advances to `done` at closure. DW-97 and Story 8.7 retain their existing statuses.
