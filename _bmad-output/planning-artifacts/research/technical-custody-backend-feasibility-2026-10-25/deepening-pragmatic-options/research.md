---
title: Practical custody options — deeper comparison
type: technical
topic: custody backend feasibility
decision: avoid custom hardware; choose a supportable guarantee before backend implementation
source: primary vendor documentation and imported prior research
status: complete
created: 2026-10-25
updated: 2026-10-25
---

**My practical recommendation is to stop the custom Luna/FM path for this feature.** None of the examined options satisfies the complete current contract on verified evidence. Buying more sophisticated key custody does not solve expiry of plaintext already released to a paused application. If the strict contract remains mandatory, keep the feature unavailable and do not implement a speculative backend. If owners explicitly approve a narrower, enforceable lifecycle contract, assess existing OpenBao first; use the ordinary managed key service in the application's existing cloud when that avoids operating another key platform. Add hardware custody or confidential computing only for a separate, stated security need.

This deepening changes the investment recommendation from “a capped hardware feasibility investigation is an option” to “do not fund that investigation by default.” The earlier feasibility conclusion stands. Branch B, immutable identity and original binding-effective-at + exactly 31,536,000 seconds with an exclusive boundary remain fixed. No contract, approval, deployment or evidence status is changed by this report.

**The common mechanism.** A data-encryption key (DEK) encrypts one unit. A wrapping key protects that DEK so ordinary storage can retain ciphertext plus a wrapped DEK. For a read, the service authorizes the request, checks the original deadline, unwraps the DEK and obtains plaintext. Some backends can perform payload crypto remotely so the DEK stays inside the service. In either case, application plaintext becomes a separate copy. Envelope encryption is documented technology; tying all seven existing custody operations, copies, time, receipts and recovery to the contract is still project engineering. [1][2][6][7]

A crucial trap: **one DEK per unit does not automatically make each unit independently erasable.** If a backup retains the wrapped DEK and its shared wrapping key survives, it can still be decrypted. Deleting a shared wrapping key instead loses every dependent unit. Independently deleting one unit therefore needs independently destroyable key material and complete version/copy coverage. Day buckets or a tenant-wide key cannot substitute for each unit's original exact deadline. This is a design inference from the documented wrapping and recovery mechanisms. [2][3][7][9]

| Approach | What it buys | Main operating burden | Meets the complete current contract? |
| --- | --- | --- | --- |
| Existing OpenBao Transit | Central crypto with the platform technology already identified in the proposal | Team-operated service, key/backup recovery and lifecycle integration | No verified match |
| Azure Managed HSM, ordinary API | Dedicated managed hardware key custody | Hourly pool, security-domain/backup ownership and remote-service dependency | No verified match |
| Azure SKR added to Managed HSM | Restricts key release to an attested runtime | Attestation, runtime policy and recipient-memory lifecycle | No verified match |
| Google Cloud KMS/HSM | Managed wrapping/crypto; optional HSM protection through the KMS API | Key/version inventory, operations charges and destruction lifecycle | No verified match |
| Luna with custom paired FMs | Potentially moves bespoke crypto/policy logic inside hardware | Custom protocol/firmware, hardware operation, qualification and data-loss exposure | No verified match |

Operating-burden assessments are engineering judgments, not measured cost or performance comparisons. No candidate is verified to meet the complete current contract; convenience cannot compensate for that missing evidence.

1. **Reuse OpenBao Transit — the first conditional choice for this workspace.**

   **How it works.** A narrow Platform custody adapter authorizes each unit and calls Transit to encrypt/decrypt, while EventStore stores the resulting ciphertext and original-unit metadata. Direct Transit operations avoid distributing DEKs to application code; envelope mode instead returns a DEK for local encryption and adds that key to the holder inventory. Use separate profile/history purpose keys and independent per-unit deletion boundaries. [1][2]

   **Pros.** Reuses the technology and operating knowledge already identified in the proposal. It requires no custom hardware firmware, purchase cycle or enclave runtime. Existing custody interfaces can contain provider-specific calls. These are project-fit judgments, conditional on actual ownership, safe separation and capacity.

   **Cons.** Your team still manages availability, updates, access policies, seal recovery and backups. Raising `min_decryption_version` is reversible. Trimming or deleting keys affects current state; prior backups require separate handling. OpenBao documents restoration of earlier Raft state, so disabling plaintext key export alone does not remove the snapshot recovery path. A ledger restored with the same snapshot cannot independently prove it is current. [1][2][3]

   **Verdict.** Smallest plausible addition for an explicitly revised ordinary retention model. It is not a qualified strict-custody backend. Reusing the current shared deployment is not automatically safe: a dedicated ownership/backup boundary may be needed, and that operating cost must be assessed before choosing it. Do not deploy a second Vault product or a custom secrets engine merely to implement a deadline check.

2. **Azure Managed HSM, with SKR treated as an optional extra.**

   **How it works.** With the ordinary API, the application asks the managed HSM service to use its wrapping key. A plaintext DEK can return for local data crypto; the service's wrapping key stays in Managed HSM. Microsoft operates a dedicated customer pool and handles its hardware maintenance and failover. You still own permissions, the security domain and recovery procedures. Independently erasable units need separate logical keys; Azure key deletion affects the logical key rather than one selected version. [4][6][10]

   **Pros.** Removes appliance operation from your team and offers dedicated hardware custody. It is a sensible candidate when Azure is already the application's platform and dedicated tenancy/control is an actual requirement. The ordinary API does not require an attestation stack. [4][6]

   **Cons.** Managed HSM has a pool-hour billing model, so its cost is not just proportional to a few crypto calls. Network/identity access becomes a runtime dependency. Key expiration still permits decrypt/release/unwrap/verify, and retained usable backups can restore purged key versions. Recovery protection and irreversible deletion are different objectives. [5][6][9]

   **What SKR adds.** Secure Key Release exports a key to a runtime whose attestation meets the release policy. It is relevant when the threat model includes the host inspecting computation. It adds attestation, workload measurement and a protected execution/runtime boundary. It does not retract an already released key or plaintext from a paused process. [8]

   **Verdict.** Ordinary Managed HSM can be appropriate for dedicated managed custody under an approved compatible lifecycle. Do not add SKR solely for retention expiry. Neither path meets the present strict contract. If dedicated HSM tenancy is unnecessary, evaluate the ordinary key service tier in the existing cloud rather than selecting a dedicated pool because its name sounds stronger; that is a future scope/cost assessment, not an alternative qualified here.

3. **Google Cloud KMS, optionally with HSM protection.**

   **How it works.** Cloud KMS wraps the application's DEKs; application storage retains the ciphertext and wrapped keys. Choosing HSM protection changes key custody without requiring a separate application API design. Google operates the service and hardware infrastructure. Standard managed HSM protection here means the ordinary multitenant service, not a separate dedicated-HSM offering. [7][11]

   **Pros.** Managed operations, a common KMS interface and a bill based on active key versions plus crypto operations. It is a practical candidate if workloads already run on Google Cloud. It offers a simpler operating model than owning appliances, though that does not itself establish lower total cost. [11][12]

   **Cons.** Independent per-unit wrapping keys and rotation increase the inventory of billable active versions; disabled and pending-destruction versions still count. Destruction has a recoverable scheduled phase, so scheduling at expiry does not mean completion then. Starting destruction earlier would make that key unusable before the required window ends. Google distinguishes customer-unrecoverable destruction from later deletion across its infrastructure; its residual backups are for regional disaster recovery, not customer restoration of individual destroyed versions. That is stronger than saying a customer can restore any deleted key, but it still does not verify this exact all-copy deadline contract. [11][12][13]

   **Verdict.** A credible ordinary managed-key choice for an existing Google Cloud deployment, conditional on an accepted lifecycle. Do not introduce a new cloud just for this feature. Hardware protection does not solve already-released plaintext or turn a provider destruction schedule into the original exclusive deadline.

4. **Luna HSMs with custom paired Functionality Modules — reject for this feature's current investment.**

   **How it would work.** Each original unit has a DEK and two independent wrapping roots, one per device. A custom protocol double-wraps the DEK, keeps key use and payload crypto inside hardware, binds the immutable deadline, and tries to erase both unit roots autonomously. Ciphertext backups retain only the final wrapped envelope. An independent authority controls admission/currentness, and replacement pairs take new units while old pairs drain to their last original expiry. This is proposed engineering assembled from documented FM/crypto building blocks, not a vendor-supported complete design. [14][16]

   **Pros.** Could confine key use and policy more tightly than ordinary application-side checks, provide locally controlled hardware custody and restrict normal expiry to a single unit. These are conditional design benefits requiring proof; they are not demonstrated guarantees.

   **Cons.** A naive double-wrap flow exposes an intermediate blob that a host could retain, bypassing one wrapping layer later. Enforcing a real two-root dependency requires a protected inter-device protocol. With that dependency and no root backups, loss of either required root permanently loses the unit; loss of a device's root set loses its dependent cohort. Both devices must remain usable for reads. A long maintenance drain ties availability to old hardware. These are construction-dependent consequences, not assumptions about a specific RAM power domain. [14][16] (engineering inference)

   Thales documents custom FM distribution/backup constraints and separate validation obligations; ordinary client HA does not handle FM-specific functions. Battery preservation of cryptographic material does not document autonomous FM execution or selective deadline erasure through power loss. Adding these components still leaves the application plaintext problem. [15][17][18]

   **Verdict.** This is a specialist security-product development effort. It is disproportionate for an ordinary actor-history feature. Reopen it only for a distinctive business requirement and after a written, exact-model/firmware vendor capability statement provides a credible mechanism for the defining guarantee. No hardware purchase, prototype or supplier engagement is authorized here.

**The simplest path worth approving next.** The first decision is the guarantee, before the vendor. Under the unchanged contract, the recommendation is **none of these backends**. Current Q1/Q4/Q6 remain unresolved; Q2/Q3/Q5/Q7, F1 and installed qualification do not disappear by choosing a product. The [five owner decisions and complete Q/D/F/B mapping](../research.md) remain the handoff; this deepening changes decision 1's recommended investment to stopping Luna R&D by default. Any revised scope would require explicit owner decisions before changing those bindings or accepting a delivery date.

If owners want a conventional solution, they must explicitly amend two claims: (a) physical/cryptographic destruction of every recoverable copy at the exact original instant through the stated outages, and (b) expiry of plaintext/DEKs already released into stopped/crashed holders. An implementable candidate contract would specify controlled service decision points that deny when trusted time is at/after the original deadline, fail closed when time/authority is unavailable, separately bounded and observable key/copy destruction, and explicit treatment of in-flight outputs and previously released plaintext. Deletion stays Pending until evidence supports completion. This would preserve the 365-day timestamp and Branch B identity, but materially change the destruction/holder guarantee. It has not been approved.

After such approval, start with one thin provider using the existing `IIdentityHistoryCustody` seam and existing owner work mechanisms: immutable original unit/deadline; separate profile/history protection; current authorization and deadline checks; independently deletable key material; complete copy/backup inventory; and restore quarantine reconciled against surviving authority before serving. Implement the minimum controls required by that approved scope. Avoid custom FMs, a new generic policy platform, cross-cloud key chains and SKR without a separate need. Benchmark unit/key count and read traffic before choosing direct Transit versus local envelope crypto.

My conditional preference is **existing OpenBao first, ordinary managed KMS in the already-used cloud second**. The strongest argument against OpenBao is operator burden and shared recovery scope; managed service custody wins if those cannot be owned safely. To preserve flexibility, retain the existing provider interface and authenticated provider-neutral unit/deadline metadata. Provider keys and protected backups are not automatically portable: changing providers may require authorized unwrap/rewrap while old units remain live, without extending their deadlines. [7][9]

This is the pragmatic reduction: choose an honest, enforceable contract and reuse one existing or managed service. If the stronger contract remains mandatory, stop this implementation path rather than accumulating infrastructure around an unverified guarantee.

Sources: facts trace to the three [deepening digests](digests/) and the preserved prior report/digests. All sources were accessed or imported from evidence retrieved in this conversation on 2026-10-25. Dates below are displayed update dates unless stated; unspecified publication dates are not invented. Vendor-specific capability confidence is medium/single-primary-source; the paused-holder and backup caveats have independent OS/runtime/provider corroboration in the prior report. Proposed architecture and fit recommendations are explicitly inferences. No price totals or performance estimates are asserted.

| Ref | Supports | Primary source | Date |
| --- | --- | --- | --- |
| [1] | Transit operation and reversible version threshold | [OpenBao Transit 2.6](https://openbao.org/docs/2.6.x/secrets/transit/) | undated |
| [2] | Transit data keys/delete/trim/backup | [OpenBao Transit API 2.6](https://openbao.org/docs/2.6.x/api/secret/transit/) | undated |
| [3] | Earlier cluster-state restoration | [OpenBao Raft API 2.6](https://openbao.org/docs/2.6.x/api/system/storage/raft/) | undated |
| [4] | Dedicated pool and managed hardware | [Microsoft Managed HSM overview](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/overview) | updated 2026-10-14 |
| [5] | Hourly pool cost model | [Microsoft Key Vault pricing](https://azure.microsoft.com/en-us/pricing/details/key-vault/) | undated; retrieved prices have placeholders |
| [6] | Ordinary crypto and expiration exceptions | [Microsoft Managed HSM key operations](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/about-keys-details) | updated 2026-05-22 |
| [7] | DEK/KEK flow and custody | [Google envelope encryption](https://docs.cloud.google.com/kms/docs/envelope-encryption) | updated 2026-10-22 |
| [8] | Attested key release | [Microsoft Secure Key Release](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation) | updated 2026-09-09 |
| [9] | Purged-key/version backup restore | [Microsoft Managed HSM backup/restore](https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/backup-restore) | updated 2026-04-30 |
| [10] | Logical-key deletion granularity | [Microsoft Delete Key API](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/delete-key/delete-key?view=rest-keyvault-keys-2025-07-01) | undated; API version 2025-07-01 |
| [11] | Managed HSM and regional recovery backups | [Google key-management deep dive](https://docs.cloud.google.com/docs/security/key-management-deep-dive) | content September 2026; updated 2026-10-20 |
| [12] | Active-version/operation billing | [Google Cloud KMS pricing](https://cloud.google.com/kms/pricing) | effective 2025-03-17; publication unspecified |
| [13] | Scheduled and infrastructure destruction | [Google destroy/restore key versions](https://docs.cloud.google.com/kms/docs/destroy-restore) | updated 2026-10-22 |
| [14] | Custom FM execution/memory | [Thales PCIe FM SDK](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/PDF_PCI/FM%20SDK%20Programming%20Guide.pdf) | updated 2026-10-19 |
| [15] | HA, validation and backup constraints | [Thales FM deployment constraints](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/admin_hsm/fms/constraints.htm) | updated 2026-07-17 |
| [16] | Hardware AEAD building blocks | [Thales AES-GCM mechanism](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/sdk/mechanisms/CKM_AES_GCM.htm) | updated 2026-06-18 |
| [17] | Battery preservation scope | [Thales PCIe battery procedure](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/install/pci_hw_install/battery_replace.htm) | updated 2026-07-08 |
| [18] | Paused execution and retained memory | [Linux system sleep states](https://docs.kernel.org/admin-guide/pm/sleep-states.html) | undated |

Freshness: the [deepening revision check](source-staleness.json) flags seven old update/effective dates under one-month compatibility and three-month pricing-model windows, despite current retrieval. Recheck service limits, prices and exact firmware applicability before selection or spend; older source dates do not themselves invalidate a freshly retrieved page. [Undated sources](claims-undated.json) receive no invented dates. No numeric price quote or budget is asserted. The prior [staleness calculation](../source-staleness.json) remains preserved. Exact model support and all physical guarantees remain unqualified.

Validation: the 18-reference bibliography passes its mechanical check, and independent semantic verification found no substantive mismatch; its wording correction and three prose corrections were applied. Structure review required no changes. All 26 files captured for this deepening remain unchanged; the existing memlog was appended through its script. See [citation verification](digests/citation-verification.md), [validation](validation.json) and [preservation](preservation-result.json). Only the new companion artifacts and append-only memlog changed. No functional tests, deployment, credential retrieval, vendor contact or physical qualification occurred. These checks do not establish Available.
