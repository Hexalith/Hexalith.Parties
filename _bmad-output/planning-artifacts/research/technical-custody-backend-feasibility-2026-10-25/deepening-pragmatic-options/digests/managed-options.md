# Managed non-AWS options: practical operating comparison

Research/access date: 2026-10-25. Scope: Azure Managed HSM, its optional Secure Key Release feature, and Google Cloud KMS with software or ordinary multitenant HSM protection. Six new primary sources; prior evidence imported only from the explicitly granted `digests/alternative-r1.md`. No deployments, credentials, vendor contact, or other project files. No numeric price comparison is needed to explain the cost models.

1. Never conclude from training data alone. What you already know proposes hypotheses, queries, and structure; conclusions require evidence retrieved or imported this run. A claim you cannot evidence is stated as an unverified belief or not at all.
2. The research firewall. Project context — briefs, PRDs, code, memory, {workflow.persistent_facts} — shapes what to ask, never what is true. It is inadmissible as evidence: every claim in a research artifact traces to a digest or import file with a source. Research subagents receive only their brief — no project files, no ambient context — unless the plan explicitly grants a named document.

The input requirement remains strict: immutable per-unit deadline at original instant plus exactly 365 fixed days, exclusive; irrecoverability through outages, power loss, versions and restore; expiry of plaintext/DEKs in paused or crashed processes; unrelated units survive. This comparison does not relax that requirement or certify any option against it.

## Sources

Dates are publisher-reported updates/effective dates, not inferred publication dates or search crawl ages. All accessed 2026-10-25.

| ID | source | publisher | pub_date |
|---|---|---|---|
| N1 | [Azure Managed HSM overview](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/overview) | Microsoft Learn | updated 2026-10-14; publication unspecified |
| N2 | [Azure Key Vault pricing — Managed HSM Pools](https://azure.microsoft.com/en-us/pricing/details/key-vault/) | Microsoft Azure | undated |
| N3 | [Delete Key, REST API 2025-07-01](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/delete-key/delete-key?view=rest-keyvault-keys-2025-07-01) | Microsoft Learn | publication/update unspecified; API version is not a publication date |
| N4 | [Envelope encryption](https://docs.cloud.google.com/kms/docs/envelope-encryption) | Google Cloud | updated 2026-10-22 UTC; publication unspecified |
| N5 | [Cloud KMS pricing](https://cloud.google.com/kms/pricing) | Google Cloud | prices effective 2025-03-17; publication/update unspecified |
| N6 | [Cloud KMS encryption and key management deep dive](https://docs.cloud.google.com/docs/security/key-management-deep-dive) | Google Cloud | substantive content says September 2026; page footer updated 2026-10-20 UTC |

Imported sources below retain the prior digest's verified metadata; A1 and G1 were also retrieved again during this pass.

| ID | source | publisher | pub_date |
|---|---|---|---|
| A1 | [Managed HSM key types and operations](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/about-keys-details) | Microsoft Learn | updated 2026-05-22 |
| A2 | [Secure Key Release concepts](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation) | Microsoft Learn | updated 2026-09-09 |
| A3 | [Managed HSM backup and restore](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/backup-restore) | Microsoft Learn | updated 2026-04-30 |
| A4 | [Managed HSM soft-delete](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/soft-delete-overview) | Microsoft Learn | updated 2026-04-28 |
| A6 | [Attestation-gated SKR VM pattern](https://learn.microsoft.com/en-us/azure/virtual-machines/secure-key-release-pattern-trusted-launch) | Microsoft Learn | updated 2026-09-30 |
| G1 | [Destroy and restore Cloud KMS key versions](https://docs.cloud.google.com/kms/docs/destroy-restore) | Google Cloud | updated 2026-10-22 UTC |

## Documented mechanisms and burdens

Every row below is a sourced fact; confidence is high. Practical implications follow separately.

| ID | claim | source | class |
|---|---|---|---|
| F1 | Azure Managed HSM is a dedicated single-customer HSM pool with its own security domain. Microsoft handles provisioning, patching, maintenance and hardware failover. It uses the Key Vault API and offers per-key access control. | N1 | vendor-documented mechanism |
| F2 | An ordinary Azure application can request wrap/unwrap, encrypt/decrypt or signing operations using a service-held key. Creating a normal key does not release its value to the client; `get` does not return secret material. Attestation is a requirement of the separate `release` operation, not listed for ordinary cryptographic operations. | A1 | vendor-documented mechanism |
| F3 | Azure's delete operation acts on a logical key and cannot remove individual versions. Managed HSM uses the same API. Soft-delete retains deleted keys for 7–90 days; permanent purge is separate and may be blocked by purge protection. | N3; N1; A4 | deletion granularity and recovery |
| F4 | Managed HSM backup covers every key/version and associated metadata. Selective restore can recover a previously purged key with all its versions. Full restore replaces current contents and disrupts ordinary data-plane access while running. | A3 | restore burden |
| F5 | Managed HSM pool pricing is an hourly fee per pool. The separate pricing rows for Premium vault keys/transactions must not be mistaken for the Managed HSM pool model. Azure's page provides region/currency selection; this retrieval returned placeholders for dollar amounts. | N2 | cost model; numeric quotation unavailable |
| F6 | SKR exports an exportable key to an attested runtime matching a release policy. It adds an attestation/release flow. Microsoft's concrete pattern treats protection after decryption as a guest-runtime concern; confidential computing protects memory from the host. | A2; A6 | optional feature and trust boundary |
| F7 | In envelope encryption, a locally generated DEK encrypts the data; a centrally managed KEK wraps that DEK. Store ciphertext and wrapped DEK together. For reads, the service unwraps the DEK and the application decrypts locally. The Cloud KMS KEK stays inside the service. Many DEKs can share one KEK. | N4 | application mechanism |
| F8 | Google exposes software and HSM keys through Cloud KMS. The ordinary `HSM` backend uses multitenant hardware; it shares the KMS API/client libraries. Google runs the HSM infrastructure and redundant key backups. The separately documented `HSM_SINGLE_TENANT` mode is outside this comparison and its cost model. | N6 | operating mechanism and scope |
| F9 | Google destroys individual key versions through a scheduled operation: default pending period 30 days, configurable at key creation. During that period restoration cancels destruction; scheduled versions cannot perform crypto. Customer recovery then ends, but deletion from all infrastructure/backups can take 45 days. Imported material retained elsewhere can be re-imported. | G1; N6 | deletion lifecycle |
| F10 | Google says residual key material in backups is for regional disaster recovery, not restoration of individual keys. This narrows the restore concern; it does not establish the strict original-deadline/power-loss contract. | N6 | documented restriction; bounded verification gap |
| F11 | Google bills active key versions according to protection level plus cryptographic operations. Enabled, disabled and pending-destruction versions count as active; destroyed versions are free. Administrative operations, including creating a rotation version, are free, but retained new/old active versions and cryptographic re-encryption still affect usage. | N5 | cost model |

Azure `exp` still allows decrypt/release/unwrap/verify after expiration [A1, imported finding F1]. Neither SKR nor HSM protection supplies evidence of deadline-triggered erasure of already returned plaintext/DEKs. That unresolved boundary applies even when key custody itself is managed well [A2, A6, N4; inference].

## What per-unit deletion actually changes

The following are design inferences from F3, F7 and F9, not additional vendor guarantees.

With a shared KEK, backups of a deleted wrapped DEK remain decryptable while that KEK survives. Destroying the KEK affects every unit it protects. Per-unit DEKs therefore do not automatically provide independent hardware destruction [N4; inference].

For independent managed-key deletion, use an independent logical KEK per unit. Azure deletion then isolates other logical keys; Google destruction must cover all versions used by that unit. Separate keys are easier to reason about than unrelated units represented as rotation versions. This increases lifecycle records and, on Google, billable versions [A1, N3, G1, N5; inference].

## Practical pros, cons and when worthwhile

These are recommendations/inferences from the documented facts above, not measured performance or price rankings.

| Option | How it works in plain terms | Practical advantages | Practical disadvantages | Worthwhile when |
|---|---|---|---|---|
| Azure Managed HSM, ordinary API | The server asks a dedicated managed HSM pool to unwrap its DEK; the server uses that DEK to read the unit. | Dedicated custody and customer security domain; familiar Key Vault interface; Microsoft operates the hardware. | Pool-hour cost floor; customer recovery/domain and backup procedures; API/network dependency; logical-key deletion granularity and recoverable copies. | Already using Azure and dedicated tenancy/root-of-trust control is a stated requirement. |
| Add Azure SKR | An attested workload receives an exportable key and performs operations inside its selected runtime. | Constrains which measured runtime receives key material; confidential computing can protect it from host access. | Additional runtime, policy and attestation integration; exported-key/plaintext lifetime still needs application design. Adds no verified strict expiry benefit. | A separate threat model requires protecting computation from the host or restricting release to a measured runtime. |
| Google Cloud KMS, optionally HSM | The server uses the managed KMS API to wrap/unwrap DEKs. Select HSM protection when hardware-backed custody is required. | One interface for software/HSM; Google operates key infrastructure/backups; spend follows versions and crypto usage rather than the Azure pool meter. | Per-unit keys and rotation versions increase billed inventory; recovery window delays destruction; remote service dependency and provider-controlled infrastructure lifecycle. | Already using Google Cloud, or seeking ordinary managed custody without a dedicated Azure pool requirement. |

## Lock-in and final recommendation

Provider lock-in is an engineering inference: ciphertext depends on provider keys/APIs. Azure protected backups are not portable raw-key exports [A1, A3]; Google retains its KEK [N4]. Migration normally requires the old key to unwrap and rewrap DEKs, or separately retained imported material [A1, G1; inference]. Imported duplicates are additional copies to expire. An API adapter does not make keys portable.

For ordinary retention, use the hosting cloud's managed service and envelope encryption, with keys matching the required independent deletion boundary [N4, A1; recommendation]. Prefer software protection unless hardware custody is required; choose dedicated Azure custody only for dedicated-control requirements [N1, N6; recommendation]. Add SKR only for its separate runtime trust goal [A2, A6; recommendation]. These suggestions assume ordinary retention is acceptable.

If every strict requirement remains mandatory, **neither managed option is recommended as a verified solution**. Exact original-instant expiry across backups, outages, power loss and paused plaintext remains unverified. A more elaborate hierarchy or attestation layer must not be presented as having closed those gaps without new evidence.
