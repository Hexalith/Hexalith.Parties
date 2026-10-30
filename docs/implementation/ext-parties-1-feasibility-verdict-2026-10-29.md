# EXT-PARTIES-1 — existing-resource feasibility verdict

**Stop full custody work on the evidenced existing configuration. Retain the operational-security milestone; Live remains stopped.** The current backup policy cannot substantiate the approved destruction bound, and no independently administered lifecycle/time authority surviving the required restore cuts is established. This is a no-go for proceeding with the unchanged resources/configuration, not proof that every undisclosed existing resource is unsuitable.

This continues the [approved assessment direction](ext-parties-1-applied-direction-2026-10-29.md). The two-engineer-day ceiling and zero new infrastructure spend are already approved; neither is requested again. No engineer-day exhaustion or named allocation is inferred. The assessment ends early at the evidenced stop conditions. No production backend, qualification, credential renewal, infrastructure or backup change was performed.

## Evidence classification and findings

Fresh observations below were collected on October 29 at approximately 16:47–16:48 UTC. Earlier October 29 observations are explicitly identified as retained evidence, not re-executed checks. Owner approvals remain limited to the existing assessment direction and previously approved contract bounds. Missing appointments, acceptance and technical proofs remain unknown.

### Operational profile: recovery required on the available evidence

The newer [provider reconciliation health record](/home/administrator/hexalith-secrets-evidence/ext-secrets-1/provider-inventory-reconciliation-20261029TJ6KL9LyH/health.json), observed at 13:13:12 UTC, reports custody and inventory `/ready` **503**, with liveness and controller health passing. Authority storage readiness is 200 at revision `e19e68c9537246148141ec9d3841cfe4:19`; that does not establish a valid current profile. Its [deadlines record](/home/administrator/hexalith-secrets-evidence/ext-secrets-1/provider-inventory-reconciliation-20261029TJ6KL9LyH/deadlines.json) retains elapsed October 28 deadlines and **null current provider lease state**. No legitimately current accepted operational profile was located in these sources.

The [concrete recovery plan](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/operational-recovery-plan.json) binds the cluster, namespace UID, observed workload UIDs/images, original profile, authority revision, credential references, public provider observation commands, preservation prerequisites, sequencing and abort/reconciliation behavior. It prepares recovery; it is not an executable approval packet. Future successor operation IDs, exact write targets/revisions, windows and owner authorization cannot be fabricated from expired inputs.

Preserve the Secret metadata-reconciliation blocker: UID `a97fa037-798b-46b6-9232-76e600480142`, original RV `36774577` to observed RV `37223806`, and the historical only-label write-proof requirement. Current metadata cannot reconstruct intervening write history. The newer [owner-return record](/home/administrator/hexalith-secrets-evidence/ext-secrets-1/provider-inventory-reconciliation-20261029TJ6KL9LyH/owner-return-required.json) also records an independent historical Agents-register preservation mismatch. That remains its owner's separate reconciliation; this assessment does not edit the register or accept the drift.

### Local snapshots: deletion logic observed, actual disposal unverified

The fresh [Kubernetes receipt](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/kubernetes-read-only.json) captures the installed snapshot job command. It runs daily at `30 0 * * *`, with last-success metadata `2026-10-29T00:30:05Z`, and:

- Saves dated snapshots and checksums on `openbao-snapshots`, publishes `openbao.snap` atomically, then runs `find ... -name 'openbao-*.snap' -mtime +14 -delete` and the corresponding checksum cleanup.
- Does not expire `openbao.snap` by age. Failed capture before the cleanup statements can leave older snapshots untouched. The script's “14 days” comment is not a hard 14-day destruction guarantee.
- Does not upload to Scaleway in this command. Historical off-node transfers are a separate workflow; daily local success does not establish daily off-node backup.

The previously observed PVC/PV remains a local `node1` Retain volume. Job-object TTL and PVC reclaim policy do not prove file/key destruction. The [file-stat attempt](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/local-snapshot-stat.json) used the documented existing SSH transport and failed with exit 255: the recorded private-key path is missing and authentication was denied. No credential was renewed or alternative privileged credential acquired. No running pod in the observed OpenBao namespace mounts this snapshot PVC; its two observed snapshot pods have completed. No reader pod was created.

A [filename/stat-only scan](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/local-recovery-copy-metadata.json) found no `.snap` files or matching non-document seal files in the two local recovery-evidence roots. That limited scan proves neither absence of renamed/encrypted bundles nor disposal on the node, other workstations or storage media.

### Scaleway: mismatch confirmed in current configuration

The [Scaleway GET receipt](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/scaleway-read-only.json) uses only the existing validator application credential, passed privately to curl on stdin. It reads the existing Scaleway endpoint, not AWS. No object payload or credential value is recorded.

| Fresh observation | Consequence |
| --- | --- |
| Versioning Enabled; default COMPLIANCE lock 30 days; AES256 server-side encryption | Lock expiry permits later disposal; it is not disposal evidence. Provider-managed encryption is not per-original-unit crypto-erasure. |
| `expire-openbao`: current expiration 31 days; noncurrent expiration one day; multipart abort one day | The documented mismatch is still installed. No configuration correction has happened. |
| Complete paginated listing: 8,896 object versions, 37 noncurrent versions, zero delete markers | Includes historical/noncurrent entries; it is a time-bounded listing, not an atomic census or proof about copies elsewhere. |
| `openbao/`: six current versions, comprising three snapshots and three checksum files; no noncurrent versions in this prefix | Snapshots dated September 29, October 1 and October 14 remain present. Each snapshot is 96,801 bytes. |
| `evidence/`: 87 versions including two noncurrent versions; no expiry rule | These records must remain classified. No key content was read; key-free status of every record cannot be inferred from filenames. |
| Six exact-version retention reads return COMPLIANCE deadlines; legal-hold reads return HTTP 404 `NoSuchObjectLockConfiguration` | Retention is observed. The legal-hold error is preserved, not silently converted into a verified OFF response. |
| Replication configuration GET returns HTTP 405 `NotImplemented` | No inference of absent provider replication or exceptional copies. |

The [version inventory](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/bucket-version-inventory.json) records exact keys, versions, sizes and timestamps. The oldest snapshot is version `1790687096892113`, uploaded September 29 at 13:04:56 UTC; its lock expired October 29 at 13:04:56.892 UTC and it was still listed after 16:47 UTC. This is observed persistence beyond 30 days from upload, **not an assertion that a particular actor-history unit has breached its E-relative deadline**. No such installed unit was qualified.

The current bucket policy still names the organization owner with `s3:*`, the writer with get/put/list but no delete action in its statement, and the validator application plus Phuong's user with read access. These are bucket-policy facts, not a fresh effective-IAM audit of all organization grants. The historical setup reports broader Administrator group grants; current membership and all restore subjects remain unverified.

**Resolution of the 31-day mismatch:** reject this unchanged backup route as evidence of A-02 compliance. A copy containing a key just before E can remain beyond E+30 days under the installed rule. Simply changing 31 to 30 would still fail to establish actual version deletion, processing headroom, exceptional-copy disposal and the E-relative bound. Post-E capture of a still-present key can also push a new 30-day immutable lock past E+30 days. This assessment neither weakens the bound nor changes the bucket.

A future separately approved existing-resource design must show, for every recoverable version, latest possible key capture time C, immutable retention L, disposal/verification margin M, and **C + L + M <= E + 2,592,000 seconds**, or prove irreversible destruction of every independently usable key-recovery route by that deadline. It must cover current/noncurrent versions, replicas, legal holds, retries/outages, local latest snapshots, seal dependencies and exceptional copies. A missed or unknown disposal stays Pending/breach; no deletion success or physical erasure is inferred. Without that evidence, full custody stays stopped.

### Recovery keys and historical restores

The [October 1 restore summary](../../../platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-0/20261001t061620z/summary.md) records successful isolated OpenBao recovery and removal of temporary seal-key copies, but explicitly says host-directory deletion was inferred from PV deletion, not verified on the node. Its gate expired October 2. The retained recovery policy identifies temporary copies of `Secret/openbao/openbao-seal`; current pod metadata still references that source Secret. We read no Secret values.

This confirms a recovery-key dependency, not a complete inventory or destruction schedule for source seals, escrow, node/etcd bundles, offline material or privileged restore operators. Those paths remain unresolved. Historical successful recovery is not non-revival evidence, and an existing restored OpenBao state cannot establish its own currentness.

### Independent authority/time: none established

The installed HMAC authority remains expressly [same-cluster, local-PVC operational separation](../../../platform/eng/ext-secrets-1/README.md#isolated-current-profile-and-revocation-authority-2026-10-27). Neither its healthy storage nor its revision proves survival of node/application-host, authority-state or combined OpenBao/ledger restore cuts. Another pod, mount or namespace would not fix this.

Scaleway provides an existing off-node immutable storage candidate, but no evidenced current fence/epoch service, authenticated time with <=100 ms uncertainty and <=1 second age, rollback rejection, independent administration, recovery exclusion or lost-authority behavior. The historical optional validator is not an installed currentness authority. No qualifying alternative was found in the inspected owner packets, recovery records and installed census. An undisclosed existing service remains possible; it must be evidenced before reopening this route. Do not provision one under this assessment.

### Capacity and accountable people

Fresh `kubectl top` reports OpenBao pods at 46m/61m/40m CPU and 76/164/74 MiB memory; `node1` at 1,175m CPU (3%) and 17,684 MiB memory (14%). These are instantaneous utilization observations, not sustained headroom or workload capacity. Prior requests/limits remain allocations, not measurements.

Existing October 1 evidence measures OpenBao recovery RPO at 42 seconds. Its source inventory records 10 ACL policies, two auth mounts, 10 KV paths and seven secret mounts; the three listed historical snapshots are each 96,801 bytes. These are small historical recovery measurements, not direct-Transit throughput, key-family scale or destruction latency. No measured intended history-unit/generation counts, write/read rates, p95/p99 latency, copy volume or cleanup backlog/drain headroom was located. No load test or key creation was run.

The [existing prerequisites](../../../platform/docs/implementation/ext-parties-1-platform-prerequisites-2026-10-18/README.md) name Administrator `jpiquot@itaneo.com` in cluster recovery records but explicitly grant no Parties fault/restore/destruction scope. The [destination setup](../../../platform/_bmad-output/implementation-artifacts/evidence/epic-4/recovery-destination-setup.md#independent-validator-identity) names `pduong@itaneo.com` as optional validator; that is not a recovery-deputy appointment or competence proof. Platform sprint items `6-3-name-the-recovery-deputy` and `8-14-prove-the-deputy-s-recovery-capability` remain backlog. An assessment implementer, task-specific accountable recovery lead and different qualified Human deputy are not established. The assistant is not an appointment.

## Smallest remaining owner return

Return one referenced packet through the existing [Platform owner form](../../../platform/docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json); do not create a competing policy register:

1. Name the existing-allocation assessment implementer and accountable operational recovery lead, plus a different Human deputy, or supply their actual appointment references. Existing Administrator/validator labels alone do not answer this.
2. For the retained operational milestone, return the authentic historical metadata-scope acceptance, separate historical register-mismatch disposition, and fresh provider public observations/current accepted profile—or owner-specific exact recovery inputs completing the prepared plan. The local file-disposal observation needs an owner-produced sanitized inventory/deletion record or a still-valid existing read-only transport reference; the documented SSH key is missing. Include source seal/escrow/node-bundle copy owners and actual restore privileges/procedure in `backupAndRestore`.
3. **Only if full custody is to reopen:** identify an already operated independently administered authority/time mechanism with cut-survival evidence, and an existing-resource all-copy disposal construction meeting the equation above; attach any existing intended-workload measurements. If none exists, say so and retain the stop. No new infrastructure request or repeated approval of the cap/bounds is necessary.

## Preservation and limits

Historical evidence, original owner forms, approvals and ledgers were not rewritten. The Agents register remains unchanged. The new [validation receipt](../../_bmad-output/implementation-artifacts/tests/ext-parties-1-feasibility-2026-10-29/validation.json) checks preserved files, source hashes, local links and the new evidence's internal counts. It is document/evidence validation only. Raw reads, the SSH failure and source hashes are retained in the new evidence directory.

EXT-PARTIES-1 remains Committed/unavailable at accepted target `56cb4401c4179d431385ad3d931fefa402e4fc5a`, missed date `2026-10-08`. Replacement target/date and installed history hashes remain unset. All design/operational approvals, exact accepted targets, installed hashes, supportable owner integration date, full guarded B15 path and exact restore/fault permissions remain prerequisites to Live; no qualification was run. Operational recovery alone cannot make custody-dependent Human/history paths available.
