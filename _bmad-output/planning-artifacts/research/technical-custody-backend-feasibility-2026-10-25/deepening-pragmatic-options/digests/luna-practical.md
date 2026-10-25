# Luna HSM options: operation, tradeoffs, and practical recommendation

Prepared/accessed: 2026-10-25. Evidence imported from the explicitly granted [prior Luna digest](../../digests/luna-r1.md), retrieved this research run. No project files supplied facts. No fresh web calls, vendor contact, deployment, credentials, physical tests, or subagents. Original publication dates are unavailable unless stated; vendor “Last Updated” dates below are update dates, not original publication dates.

## Recommendation for a small product feature

**Do not start custom Luna hardware/FM research for this feature on present evidence.** A conventional HSM is useful for keeping wrapping keys outside application memory. The proposed custom two-HSM system adds a bespoke security protocol and hardware lifecycle without establishing the defining promise: irreversible destruction exactly at an immutable deadline, including complete external power loss and clock faults. [S1–S6; recommendation/inference, high confidence given the unresolved requirement]

Keep the strict retention requirement unchanged in the decision. Existing public documentation does not qualify this candidate against it. Further work is prudent only if this capability is a distinctive business need strong enough to justify specialist security engineering, vendor engagement, and destructive qualification. The first gate should be a written, model/firmware-specific vendor answer about deadline authority and autonomous destruction under power/clock faults. Without that answer, stop this hardware path. This is a product investment recommendation, not proof that a vendor-engineered solution is impossible.

## What ordinary HSM use would do

An ordinary design generates a per-unit data-encryption key (DEK), encrypts the unit, and stores its encrypted DEK under an HSM-held wrapping key. The application checks access and retention policy and asks the HSM to unwrap/use the DEK. At expiry, a service deletes the required key and every eligible backup/copy. Alternatively, the HSM can perform the data cryptography itself so the DEK need not leave its boundary. Native AES-GCM operations are documented. [S3,S6; architectural illustration/inference]

This is familiar key protection, and it can use standard administration, supported backup/restore, and ordinary HA where configured. It does not establish independent, exact-time expiry while the control service is unavailable. Luna's standard start/end policy is explicitly scoped to encrypt/sign/wrap and is off by default; it does not document irreversible deletion or decrypt/unwrap expiry. The HSM SO can synchronize HSM time to the host. [S3,S4,S5; documented facts, high confidence]

The practical benefit is smaller custom scope. The limitation is decisive here: ordinary HSM key custody alone does not meet the strict autonomous destruction requirement. A dual-HSM arrangement does not fix that by itself. [Inference from S3–S5]

## How the proposed custom pair would have to work

The following is a **design hypothesis**, not a supported Thales reference architecture. For unit `u`, call its DEK `D_u` and its independent per-unit wrapping roots `A_u` and `B_u`. A dedicated pair can hold many units' distinct roots; sharing one root across different expiry dates would prevent independent destruction of those units.

1. **Admit the unit and bind its deadline.** The trusted enrollment procedure supplies the same unit ID and absolute deadline to both HSMs. Each custom FM must record and enforce that binding without an administrative route to extend it or restore an older state. An upload should count as admitted only after both agree. This authority, immutable state, and rollback behavior still need engineering and vendor confirmation; a signed FM alone is not evidence of them. [Hypothesis; S1,S5,S7 explain the underlying gaps]

2. **Generate independent secrets.** HSM A generates `A_u` and `D_u`; HSM B independently generates `B_u`. The roots must never be returned through native key export, FM responses, diagnostics, backups, or cloning. SMFS can hold FM-private keys outside Cryptoki visibility; normal FM RAM is a separate storage choice. The developer supplies the SMFS key lifecycle. Choosing volatile-only storage does not prove its exact failure or persistence behavior. [Hypothesis using documented building blocks S1,S3,S4]

3. **Encrypt the unit and protect the DEK.** Inside A's security boundary, use `D_u` for authenticated payload encryption and wrap/encrypt `D_u` under `A_u`. B then protects that inner blob under `B_u`. Bind the unit ID and deadline into authenticated metadata so blobs cannot simply be relabeled. Host storage keeps ciphertext and the final double-wrapped blob. Native AEAD exists; the complete FM call sequence, key handling, and cross-device composition remain unqualified. [Hypothesis using S1,S6]

4. **Keep intermediate material protected.** A naive sequence sends the inner wrapped-DEK blob through the host before B adds its layer, or returns that blob to the host after B removes its layer. The host could retain it and later bypass B. A strict two-root dependency therefore needs a protected A↔B protocol during both enrollment and reads, with no route accepting retained intermediate blobs. Host-carried encrypted messages are conceivable; a direct physical link is not inherently necessary. This protocol is an engineering hypothesis, not a documented Luna pair feature. [Security inference from the hypothetical nested construction; S1,S6,S8 document components, not this guarantee]

5. **Read only through both devices.** Both FMs check the bound deadline and request authority. B removes its layer through that protected protocol; A recovers `D_u` only inside its boundary and performs payload AEAD there. The host must not receive `D_u`, and ordinary PKCS#11 access must not bypass the FM policy. Authorized plaintext can still be returned to the requesting user. The claim can concern future decryption of retained ciphertext; it cannot retract plaintext a user already retained. [Hypothesis and cryptographic scope inference]

6. **Destroy the per-unit roots at the deadline.** Both FMs erase their own root plus live/cached copies. With a correctly enforced two-root dependency, irreversible destruction of either required root prevents later recovery; independent deletion paths could reduce reliance on one path. However, the present documentation does not establish immutable time, autonomous FM scheduling during external power loss, clock-failure destruction, or exact-time erasure. Checking a deadline on the next request is access refusal, not proof that the root was destroyed at the deadline. [Hypothesis; absence of qualification documented in imported digest/S1,S2,S5]

7. **Keep recovery from undoing expiry.** Root backups, cloned roots, exported DEKs, persistent caches, and restorable old state would need to be absent or independently subject to the same irreversible expiry. With no root backup, recovery means accepting permanent loss rather than restoring an old key. Standard cloning is designed to copy keys securely; it is not proof that copies share the proposed destruction semantics. [Hypothesis and recovery consequence; S3,S4,S8]

## Power, selective expiry, and maintenance

The card battery is documented to preserve cryptographic material across host power loss. The FM guide separately lists persistent tamper-protected SMFS and `malloc()` RAM. **Neither statement establishes that an FM CPU/scheduler runs on battery, nor that it does not.** The documented battery property must not be substituted for an autonomous deadline-destruction mechanism. [S1,S2; high confidence in the distinction, battery execution unverified]

For independent expiry, erase `A_u`/`B_u` for that unit; do not erase a shared cohort root. The proposed maintenance plan routes new units to a replacement pair and leaves the old pair serving its original cohort until its last deadline, potentially 365 days. That avoids moving existing roots. It still requires the old pair to survive the full interval and leaves reset, upgrade, battery replacement, tamper, and failure behavior unresolved. This drain plan is a proposed operational mitigation, not a vendor guarantee. [Design consequence/hypothesis based on S1,S2,S4]

If neither root can be backed up and both are required, loss of either unit root permanently loses that unit. Loss of a card's entire volatile root set can permanently lose every unit depending on that set. This consequence assumes the protected pair protocol actually prevents intermediate-material bypass. Having two required HSMs is a two-party dependency, not availability redundancy. [Inference from the proposed construction]

## Benefits and burdens

| Option | Practical benefits | Practical limitations and burden |
|---|---|---|
| Standard HSM custody | Native cryptography; wrapping roots remain inside the device; supported key administration and configured recovery options. | Application remains part of deadline enforcement; ordinary expiry settings do not document the required destruction promise. |
| Custom FM on one HSM | Could keep DEKs, payload AEAD, unit policy, and deletion logic inside one boundary; avoids trusting an application to obey every key-use rule. | Custom code, secure state/time design, fault handling, testing, signing/update governance, vendor support clarification; no demonstrated offline immutable-deadline mechanism. |
| Custom FM pair with distinct per-unit roots | Could add independent root custody and two destruction opportunities; avoids copying old roots if pairs are drained. | All single-FM burdens plus a protected pair protocol; both devices required for service; no backups means permanent loss; pair replacement and long drain periods consume hardware/operations capacity. |

Rows are engineering assessments grounded in the documented feature boundaries [S1–S8], not vendor performance, price, or support commitments. No dollar estimates are made.

Luna Client HA does not handle FM-specific operations; the application must implement its distribution. SMFS backup/restore is also custom. Overall FIPS compliance requires validation of the custom FM, not merely selecting certified native firmware. Enabling FM policy is a destructive, permanent HSM-wide change, and FM-generated/derived objects have `always-sensitive`/`never-extractable` false; the bespoke path must therefore be examined directly rather than relying on those labels. [S4; documented facts, high confidence]

The firmware rollback documentation also differs between a blanket FM rollback prohibition and a prohibition phrased around versions before 7.4.0. Treat the selected model/firmware's actual controls—and separate FM replacement/downgrade controls—as unresolved. [S4,S7; high confidence that the public wording differs]

## Source metadata

All accessed 2026-10-25 and imported from the granted digest. Publisher Thales Group unless noted. Confidence is high in these documented statements; confidence in the complete proposed system is **unqualified/unverified**. Publication date is undated/unavailable; listed dates are vendor update timestamps. The prior digest contains the wider historical and version evidence.

| ID | Official source URL/title | Vendor update date | Relevant evidence |
|---|---|---|---|
| S1 | [Luna PCIe HSM 7 FM SDK Guide](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/PDF_PCI/FM%20SDK%20Programming%20Guide.pdf) | 2026-10-19 | FM execution, RAM/SMFS distinction, internal APIs, developer key lifecycle and FM replacement |
| S2 | [Replacing the Luna PCIe HSM 7 Battery](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/install/pci_hw_install/battery_replace.htm) | 2026-07-08 | Battery preserves keys; replacement power and tamper consequences |
| S3 | [Partition Capabilities and Policies](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/admin_partition/policies/partition_capabilities_and_policies.htm) | 2026-10-16 | Start/end enforcement scope; separate cloning and wrapping permissions |
| S4 | [FM Deployment Constraints](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/admin_hsm/fms/constraints.htm) | 2026-07-17 | HA, backup, FIPS, attributes, permanent destructive enablement, rollback wording |
| S5 | [hsm time sync](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/lunacm/commands/hsm/hsm_time_sync.htm) | 2024-12-13 | SO sync to host; automatic-sync tolerance; no immutable-clock statement |
| S6 | [CKM_AES_GCM](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/sdk/mechanisms/CKM_AES_GCM.htm) | 2026-06-18 | Native authenticated encryption and wrap/unwrap building blocks |
| S7 | [Rolling Back the Luna HSM Firmware](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/admin_hsm/updates/fw_rollback.htm) | 2025-06-11 | Firmware rollback zeroization and blanket FM rollback prohibition wording |
| S8 | [Universal Cloning](https://thalesdocs.com/gphsm/luna/7/docs/pci/Content/admin_partition/key_cloning/universal_cloning_for_luna-hsm.htm) | 2026-07-23 | Secure key migration, not the proposed protected independent-root pair protocol |

No written vendor confirmation or physical qualification was obtained. Missing public evidence remains missing evidence, not a claim that a privately engineered mechanism cannot exist.
