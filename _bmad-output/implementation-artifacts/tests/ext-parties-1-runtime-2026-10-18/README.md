# EXT-PARTIES-1 runtime delivery check — 2026-10-18

## Source and Local result

The current Parties source commit is `ee4a4b8736131c0e7ce480b6dc419dcc0c266c3b`.
The EventStore source commit is `ab8fa115c486875be3a22860a273d5d68a26c749`.
The Platform source commit is `2ac8e73c09fe631c5620723b54784d2e28e8941f`.
The Platform commit was made concurrently by Jérôme Piquot through `/pushall`;
it includes the login authority correction and an unrelated Conversations gitlink.
The login correction requires one authenticated issuer/subject pair from the
same identity and an active actor registry entry. Three new regressions failed
before the correction and pass afterward.

From the Parties repository, the post-correction Local command was:

```bash
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-agent-20261018/local-after-platform-fix -ArtifactsDirectory /tmp/ext-parties-1-agent-20261018/artifacts-after-platform-fix -MemoriesRoot /tmp/ext-parties-1-agent-20261018/memories-package-mode
```

It exited 0. The copied `local-evidence.json`, twelve xUnit XML files, and
matching build/test logs show **1,991 passed, zero failed, skipped, or not run**
across the twelve required owner lanes. Every build reports zero warnings and
errors. The Platform Identity XML includes the three new regression cases.
This is Local source evidence, not installed runtime qualification.

## Accepted Live command preflight

The exact registered owner command was executed from the Agents repository:

```bash
test "$(git -C ../parties rev-parse HEAD)" = "56cb4401c4179d431385ad3d931fefa402e4fc5a" && test -z "$(git -C ../parties status --porcelain)" && pwsh -NoProfile -File ../parties/eng/verify-ext-parties-1.ps1 -Mode Live -EventStoreRoot ../eventstore -PlatformRoot ../platform -EvidenceDirectory /tmp/ext-parties-1-live
```

It exited **1 at the first exact-target check**: observed Parties HEAD is
`ee4a4b8736131c0e7ce480b6dc419dcc0c266c3b`. The `pwsh -Mode Live`
operand did not execute. The current script also explicitly exits 1 if invoked
directly because complete authenticated, persisted P-01–P-10 lanes are not
installed; do not bypass the registered command to try it.

## Replacement packet status

`ee4a4b8736131c0e7ce480b6dc419dcc0c266c3b` is an immutable Parties
source candidate, not an accepted replacement compatibility target. The
existing contract and required Levels 4 and 5 remain unchanged. No new
integration date or command has been owner-accepted for this candidate.

Proposed `TargetVersionOrCommit`:
`ee4a4b8736131c0e7ce480b6dc419dcc0c266c3b`.

Proposed `Command:` from the Agents repository root:

```bash
test "$(git -C ../parties rev-parse HEAD)" = "ee4a4b8736131c0e7ce480b6dc419dcc0c266c3b" && test -z "$(git -C ../parties status --porcelain)" && test "$(git -C ../eventstore rev-parse HEAD)" = "ab8fa115c486875be3a22860a273d5d68a26c749" && test -z "$(git -C ../eventstore status --porcelain)" && test "$(git -C ../platform rev-parse HEAD)" = "2ac8e73c09fe631c5620723b54784d2e28e8941f" && test -z "$(git -C ../platform status --porcelain)" && pwsh -NoProfile -File ../parties/eng/verify-ext-parties-1.ps1 -Mode Live -EventStoreRoot ../eventstore -PlatformRoot ../platform -EvidenceDirectory /tmp/ext-parties-1-live
```

This proposed command cannot establish
availability: the source still lacks installed Live lanes and the required
runtime authority/custody/restore targets. No register status or target was
changed.

## Remaining installation inputs

Hexalith.Platform owns production and staging composition. The accepted
Branch B Organization identity and `party-actor-retention-v1` policy remain
365 fixed days from binding effective-at, with exclusive expiry. Still needed:

1. Deployed Parties gateway URL; two immutable tenant IDs; scoped provisioner,
   identity writer, and reader credential references; current application
   actor/role issuer and binding references.
2. Installed policy record/version, purpose-specific production
   `IIdentityHistoryCustody` provider, independent authenticated source and
   lifecycle authority, and actor-free successor certificate producer.
3. Durable all-copy inventory and destruction receipts, including backup/key
   generations, cleanup worker ownership and exact lost-acknowledgement lookup.
4. Quarantined nonrollback restore target and surviving authority/fencing proof;
   authenticated restart, restore, and failure-injection targets.
5. Complete P-01–P-10 authenticated persisted-state verifier implementation
   and a clean exact checkout for an owner-accepted replacement command.

The 2026-10-18 read-only Kubernetes metadata query against the configured
`/home/administrator/.kube/hexalith-production` context found an isolated
`ext-secrets-1-isolated` namespace but no named Parties workload or gateway.
That observation does not rule out a service outside Kubernetes and does not
qualify a Parties deployment.
