"""Write current acceptance links only after complete final semantic verification."""
import gzip,json
from pathlib import Path
private=Path(__file__).resolve().parent;root=private.parents[2]
packet=root/'_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16';review=packet/'review-2'
verification=json.loads((review/'verification.json').read_text());source=json.loads((review/'source.json').read_text());closure=json.loads(gzip.decompress((review/'closure.json.gz').read_bytes()))
assert verification['ImplementationComplete'] and verification['AllJobsAwaited'] and verification['ScratchRemoved'] and verification['FalseClosureMutationRejected'] and closure['Closed']
assert len(source['Files'])==39 and verification['FinalMainCases']==6
review_text=f"""# Runner interruption review-loop 2

Current acceptance uses the isolated current-layout source 39 Release cohort in [source.json](source.json), [cohort.json](cohort.json), [matrix.json](matrix.json) and [verification.json](verification.json). The runtime child is `Hexalith.Parties.Runtime.IntegrationTests.dll`. Explicit consumer dependencies are EventStore 5.1.0, Memories 4.0.0 and FrontComposer 6.0.0; the current-source Memories host remains separately identified. Canonical baseline a575 and approved frozen intent are unchanged.

All six real held-pidfd SIGINT cases pass from one unchanged wrapper/prepared/child cohort. All 32 process, 88 cleanup, 38 unit + 22 runtime consumer, 5 startup, 7 configuration and 5 queue controls pass with zero targeted skips. The actual owned persisted-state mutation refuses signaling, preserves runner survival, restores exact bytes, then completes real SIGINT and normal cleanup. Ordinary six-restart wrapper/actual-child, coordination cancellation and all four intended live disposal modes pass. Replay guards remain enabled.

[Independent closure](closure.json.gz) verifies {closure['CapturedLifetimes']} exact captured lifetimes, {closure['CapturedContainerIds']} containers, {closure['CapturedNetworkIds']} networks and {closure['CapturedAnonymousVolumeNames']} anonymous volumes absent, with no unreaped selected zombies. Foreign sentinels survive and main cases use no emergency recovery. All final jobs/helpers were awaited, parent post-execution hashes matched, and owned scratch was removed. Positive semantic verification and the false-closure/wrong-session controls pass after updated integrity hashes.

The shared exclusive supervisor reaps exited adopted children during active commands while preserving the direct command status. Its timeout control admits a real detached child before exercising the finite timeout; failed admission cannot bypass cleanup. Configuration-only controls use a directly built model. The real generic AppHost remains in live and SIGINT lanes: SDK 13.6.5 resumes its background entrypoint at Build, where the AppHost independently calls Run. The earlier service-getter causal assessment is superseded.

Every earlier cohort is historical. This includes original v1/review-1, ancestry failures, pre-active-reaping closure failures, source 27 f472/8ad/f3e4 before upstream input drift, the native Debug attempt (MSB4018/readiness refusal with 71/100,387 input/output drifts), temporary-provider Release 45, direct-model adapter/model failures and the unadmitted timeout control 66. Their failed aggregates and emergency reconciliations remain unqualified. Failed legacy focused 47/49/51 still lack original authenticated SDK custody. Available present-state observations do not reconstruct it. Native startup contention is observed; it does not establish every readiness cause.

Detailed receipts/source snapshots use deterministic gzip (`mtime=0`); decode with `gzip -cd FILE.gz`. Readable indexes are commands.json, commands-current-layout.json, source.json, cohort.json, matrix.json and verification.json. [artifact-index.json.gz](artifact-index.json.gz) records transport/decoded hashes; [SHA256SUMS.gz](SHA256SUMS.gz) covers current files. `verify-review.py` requires semantic closure, exact six-session agreement, actual passing methods, source/cohort binding and mandatory phase proofs. Raw originals and credentials remain private; public paths and credentials are normalized.

[Current marker controls](current-controls-selection.json.gz) bind both consumer/controller kernel births to the actual exclusive command 62 to 63 interval, matching custody/outcome identities and three actual passing XML rows. The three older command 45 controls remain historical. The original six-receipt enumeration refusal, verifier and indexes remain under packet-before-marker-selection with its diagnostic receipt; archived indexes describe that earlier snapshot. [Independent pre/post readbacks](independent-parent/parent-final-cohort-post-audit.json.gz) bind the complete source/input/archive/helper closure and actual executed cohort.

The previous dated-root README/index/checksums are preserved byte-for-byte in [historical-root](historical-root/). Active dated-root integrity indexes describe the current complete tree; review-1 and its own indexes remain unchanged. Archived manifests describe their original snapshots.

Interrupted business work remains incomplete. Disposable backing removal supplies no physical business-erasure or historical-completeness claim. Publication/adoption, real historical inventory, older-writer exclusion, deployment/workload/key/trust custody and owner acceptance remain open. Independent final parent review and authorized main publication remain the parent handoff.
"""
(review/'README.md').write_text(review_text)
(packet/'README.md').write_text("""# Runner interruption evidence

Current source 39 qualification is [review-2/README.md](review-2/README.md). Its source/cohort, six-case matrix, verification and semantic closure indexes select the final isolated current-layout Release cohort. Earlier source variants supply historical evidence only; original failures remain failures.

The complete [review-1](review-1/README.md) packet and its own integrity indexes remain unchanged. Original v1 evidence stays at the dated root. Exact previous dated-root README, artifact-index and SHA256SUMS are archived under [review-2/historical-root](review-2/historical-root/). Active root checksum indexes cover the current complete tree; archived indexes describe their original snapshots.

Detailed receipts use deterministic gzip; decode with `gzip -cd FILE.gz`. Current integrity hashes establish bytes; review-2 semantic verification separately requires truthful final closure and exact selected sessions. Private raw credentials/logs remain outside this public packet. External production/owner gates remain open.
""")
readme=root/'tests/README.md';text=readme.read_text();old="Review-loop 2 execution results are pending until its dated verification indexes are\ncomplete; previous cohorts do not qualify these new source bytes."
new=f"""Review-loop 2 source 39 passes six actual SIGINT cases from one isolated current-layout
Release wrapper/prepared/runtime-child cohort. All 32 process, 88 cleanup, 60 exact
consumer rows across 38 unit and 22 runtime tests, 5 startup, 7 configuration and 5 queue
controls pass with zero targeted skips. The real changed-state refusal, ordinary
six-restart wrapper/actual child, coordination cancellation and four live modes pass.
Independent closure finds {closure['CapturedLifetimes']} captured lifetimes, {closure['CapturedContainerIds']} containers, {closure['CapturedNetworkIds']} networks and {closure['CapturedAnonymousVolumeNames']}
anonymous volumes absent, with no selected zombies or main emergency recoveries.
All final jobs were awaited and their scratch removed. Both integrity-rehashed false-closure
and wrong-session packet mutations are rejected.

Configuration-only controls build a direct model without an AppHost entrypoint;
real live/SIGINT paths retain generic AppHost startup and actual lifetime checks.
The exclusive supervisor reaps exited adopted children during active commands,
preserving direct exit status. Timeout verification requires real child admission.
The preferred mapped Debug builds passed; its actual native runtime refused before
a checkpoint with observed MSB4018 build contention and later readiness cancellation.
It drifted 71 of 100,387 frozen inputs/outputs and remains unqualified. The explicit
serialized Release fallback passed. Source 27 and all other failed/intermediate
cohorts remain historical; the original SDK-custody limits and external gates remain
open. Current details and exact commands are in the review-2 packet."""
assert old in text;text=text.replace(old,new,1);readme.write_bytes(text.replace('\n','\r\n').encode())
ledger=root/'_bmad-output/implementation-artifacts/deferred-work.md';text=ledger.read_text();lines=text.splitlines();count=0
for i,line in enumerate(lines):
    if 'verification_runner_interruption_2026_11_16:' not in line:continue
    indent=line[:len(line)-len(line.lstrip())]
    lines[i]=indent+f"verification_runner_interruption_2026_11_16: Current review-loop 2/source 39: `tests/five-runner-interruption-2026-11-16/review-2/` binds one unchanged isolated current-layout Release wrapper/prepared/actual Runtime.IntegrationTests child cohort. Six actual SIGINT cases, 32 process, 88 cleanup, 38+ 22 exact consumer, 5 startup, 7 configuration and 5 queue controls, real persisted-state refusal/restoration, ordinary six-restart wrapper/actual child, coordination cancellation and all four live modes pass with zero targeted skips or main emergency recoveries. Independent exact readback confirms {closure['CapturedLifetimes']} captured lifetimes, {closure['CapturedContainerIds']} containers, {closure['CapturedNetworkIds']} networks and {closure['CapturedAnonymousVolumeNames']} anonymous volumes absent. All final jobs were awaited/their scratch removed; semantic false-closure/wrong-session controls pass. Prior v1/review-1/source 27/nativeDebug/intermediate failures remain historical; original 47/49/51 authenticated SDK custody remains unestablished. Observed native MSB4018/readiness refusal justifies the retained explicit Release fallback; old NU1109 is historical. Interrupted business work and all existing production/publication/historical-inventory/writer-exclusion/deployment/owner-acceptance gates remain OPEN. Final independent parent review/publication pending."
    count+=1
assert count==5
ledger.write_bytes(('\r\n'.join(lines)+'\r\n').encode())
spec=root/'_bmad-output/implementation-artifacts/spec-five-runner-interruption-deferred-items.md';text=spec.read_text();text=text.replace('- [ ]','- [x]')
text+='\nFinal implementation handoff: isolated current-layout source 39 and one unchanged explicit Release wrapper/prepared/runtime-child cohort pass all six actual SIGINT cases, all focused/protocol/configuration/queue controls, real persisted-state refusal/restoration, ordinary six-restart wrapper/actual child, coordination cancellation and all four live modes. Final current indexes and independent exact closure are in review-2. All failures remain versioned; native Debug runtime input drift/readiness refusal is unqualified. All final jobs/helpers were awaited and their scratch removed after independent parent post-execution file/input readback. Semantic packet verification passes and rejects integrity-rehashed false closure/wrong sessions. Static checks and pinned 21.2.4 current-config candidate preflight pass. Final independent parent review and authorized main publication remain pending; the implementation agent performed no staging, commits or remote operations; the parent staged the task diff for independent review. External business/owner gates remain open.\n';spec.write_text(text)
print(json.dumps({'FinalDocsWritten':True,'LedgerMappings':count,'SourceFiles':39}))
