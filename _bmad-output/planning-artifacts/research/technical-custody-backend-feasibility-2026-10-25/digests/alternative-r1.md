# Non-AWS alternative evidence digest — round 1

Research date/access date: 2026-10-25. Status: complete. One bounded round, eight admitted primary sources, six web-tool calls; no second round needed because decisive gaps remain in the strongest examined candidate.

## Research firewall and epistemics

1. Never conclude from training data alone. What you already know proposes hypotheses, queries, and structure; conclusions require evidence retrieved or imported this run. A claim you cannot evidence is stated as an unverified belief or not at all.
2. The research firewall. Project context — briefs, PRDs, code, memory, {workflow.persistent_facts} — shapes what to ask, never what is true. It is inadmissible as evidence: every claim in a research artifact traces to a digest or import file with a source. Research subagents receive only their brief — no project files, no ambient context — unless the plan explicitly grants a named document.

No project files read. No deployments, credentials, or vendor contact used. Primary official vendor technical documentation only. Search snippets/crawl ages are not publication dates. Dates below are undated unless a publisher date is explicitly retrieved.

## Requirement being tested

One supportable non-AWS alternative must satisfy all of: immutable independent unit deadline D = original instant + 31,536,000 seconds (365 fixed days), exclusive at D; irrecoverability at D through outages, total power loss, historical key versions and backup restoration; expiry of plaintext/DEKs already held by paused or crashed application processes; unrelated live units survive selective expiry, maintenance and restore. This is an input criterion, not a claim established by project material.

## Sources admitted

| ID | source | publisher | pub_date (update distinguished) | accessed |
|---|---|---|---|---|
| A1 | [Key types, algorithms, and operations — Azure Key Vault Managed HSM](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/about-keys-details) | Microsoft Learn | updated 2026-05-22; publication date unspecified | 2026-10-25 |
| A2 | [Secure Key Release feature with AKV and Azure Confidential Computing](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation) | Microsoft Learn | updated 2026-09-09; publication date unspecified | 2026-10-25 |
| A3 | [Full backup and restore and selective key restore](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/backup-restore) | Microsoft Learn | updated 2026-04-30; publication date unspecified | 2026-10-25 |
| A4 | [Managed HSM soft-delete overview](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/soft-delete-overview) | Microsoft Learn | updated 2026-04-28; publication date unspecified | 2026-10-25 |
| A5 | [Azure Key Vault secure key release policy grammar](https://learn.microsoft.com/en-us/azure/key-vault/keys/policy-grammar) | Microsoft Learn | updated 2026-01-30; publication date unspecified | 2026-10-25 |
| A6 | [Protect intellectual property on Azure VMs with attestation-gated Secure Key Release](https://learn.microsoft.com/en-us/azure/virtual-machines/secure-key-release-pattern-trusted-launch) | Microsoft Learn | updated 2026-09-30; publication date unspecified | 2026-10-25 |
| A7 | [KeyReleasePolicy interface](https://learn.microsoft.com/en-us/javascript/api/@azure/keyvault-keys/keyreleasepolicy?view=azure-node-latest) | Microsoft Learn / Azure SDK documentation | undated in retrieved evidence | 2026-10-25 |
| G1 | [Destroy and restore key versions](https://docs.cloud.google.com/kms/docs/destroy-restore) | Google Cloud | updated 2026-10-22 UTC; publication date unspecified | 2026-10-25 |

## Findings recorded as evidence arrives

| ID | Claim | Source | Confidence | Class |
|---|---|---|---|---|
| F1 | Azure Managed HSM expired/not-yet-valid keys still support decrypt, release, unwrap and verify. Microsoft also permits clock-skew leeway of a few minutes for expiration processing. Therefore `exp` is not an exclusive destruction deadline or even a decrypt/release cutoff. | A1 | high | documented behavior + direct requirement mismatch |
| F2 | Azure Secure Key Release transfers an exportable HSM-protected key to an attested TEE according to a release policy. Microsoft describes it as an access-protection layer targeting an application/runtime configuration. The retrieved document does not promise lifetime enforcement or asynchronous erasure of released key copies. | A2 | high for release semantics; bounded absence only for erasure | documented behavior + evidence gap |
| F3 | Azure full backups include all keys, versions, attributes, tags and role assignments and are encrypted under keys associated with the HSM security domain. Full restore returns that prior state, removes current contents and disables ordinary data-plane operations during restoration. A retained backup is therefore a recovery surface that a selective-expiry design must explicitly address; encrypted backup alone is not deletion. | A3 | high | documented behavior + design implication |
| F4 | Azure selective key restore restores one key and all its versions from a backup and specifically requires that key to have been purged first. Thus purging the current key is not proof that an existing backup can never restore it. | A3 | high | documented behavior + direct requirement mismatch |
| F5 | Managed HSM soft-delete cannot be disabled. Deleted keys/HSMs remain recoverable for 7–90 calendar days. Permanent deletion requires a subsequent purge operation; optional purge protection blocks early purge. | A4 | high | documented behavior |
| F6 | Google Cloud KMS destruction is requested by scheduling a key version for destruction. During the configurable pending interval it can be restored. At the scheduled destruction time logical deletion from active systems starts and customer recovery becomes unavailable, but Google says key material may remain in its systems, including backups, for up to 45 days. This is not evidence of exact-instant cryptographic irrecoverability across backups. | G1 | high | documented behavior + verification gap |
| F7 | Azure supports marking a release policy immutable, preventing subsequent policy changes. This is a useful policy-integrity property, but the property is about policy mutability, not destruction of released key material. | A7; A2 | high | documented positive capability + scope distinction |
| F8 | The release-policy grammar evaluates signed environment claims and exports the key wrapped to a recipient-environment RSA public key. A token predicate, even if suitable for a time gate, would require a separate proof for exact current-time semantics and would not itself establish erasure of a prior recipient's memory. | A5; A2 | high for documented flow; medium for explicitly identified inference | documented behavior + inference |
| F9 | Microsoft's concrete SKR architecture explicitly assigns post-decryption protection to guest hardening; its release gate controls admission of keys. It recommends confidential computing for protection from host memory access, not deadline-triggered erasure. No read source supplies asynchronous expiry of already-held plaintext or DEKs in paused/crashed processes. | A6; A2 | high for scope; bounded absence for expiry | documented boundary + evidence gap |

## Candidate selection

Azure Managed HSM + Secure Key Release was selected for deeper examination because it explicitly combines a hardware key boundary with attested application key release. Google Cloud KMS/HSM was screened out as a *verified* exact-deadline/all-backups match by F6. This does not establish that Google customers can recover a destroyed version after the pending interval, nor that it cannot support other retention goals: customer unavailability and proof of cryptographic irrecoverability of every backup are distinct claims.

Azure release-policy grammar, policy immutability and the post-release application boundary were then examined. No strong primary-source lead for an Entrust/IBM mechanism satisfying paused-process erasure and a power-independent per-unit deadline emerged from this bounded investigation; no claim about those vendors' capabilities is made. They were not catalogued or deeply researched.

## Strict requirement assessment for Azure Managed HSM + SKR

| Requirement | Result from this evidence |
|---|---|
| Immutable deadline from original instant, exactly 365 fixed days, exclusive | Not verified. Immutable release policies exist (F7); `exp` permits relevant operations after expiration (F1). No retrieved source specifies the required original-instant binding or exact time semantics. |
| Irrecoverability at deadline under outage or total power loss | Not verified. No retrieved source documents a power-independent deadline destruction engine. A scheduled application deletion job would add an unproven dependency; it is not evidence. |
| All historical key versions and backup restore remain expired | Affirmative mismatch for ordinary purge with retained usable backups: selective restore can restore a purged key with every version (F4). A separate nonrestorable per-unit wrapping-root scheme would need evidence not supplied here. |
| Already-held plaintext/DEKs expire in paused/crashed processes | Not verified. SKR authorizes release; post-release memory lifecycle is a different boundary (F8–F9). Attestation or immutable release policy cannot be treated as evidence of memory erasure. |
| Unrelated live units survive expiry, maintenance and restore | Not verified end to end. Selective key restore exists, but full restore rolls back current HSM contents and temporarily disables its ordinary data-plane commands (F3). Per-unit layout, retained-root handling and restore isolation would need their own proof. |

## Decisive result

**No supportable non-AWS alternative meeting every strict requirement was verified.** Azure Managed HSM + SKR is the strongest examined candidate, but it cannot be presented as a matching backend on this evidence. `exp` is affirmatively insufficient (F1), ordinary purge is insufficient against retained usable backups (F4), and released plaintext/DEK expiry remains unsupported (F9). This is a bounded evidence conclusion, not a universal impossibility proof or a claim that another undocumented architecture cannot exist.

The decisive supplier/design evidence still needed is a contract covering the same immutable per-unit deadline across every live/backup/version copy and every recipient of plaintext, including paused/crashed execution and total power loss, while retaining unrelated units. None of the eight admitted sources provides that contract.
