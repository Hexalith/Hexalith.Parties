# Citation and semantic verification

Verified: 2026-10-25. Scope: the deepening report and its three evidence digests. No prior report was needed. Repository baseline/formatting instructions were read only for compliance, never as research evidence. No existing artifact changed; no deployment, credentials, vendor contact, or physical qualification.

Result: no substantive citation mismatch or silent relaxation of the strict contract found. One small wording correction is recommended before finalization.

## Necessary correction

In the concluding recommendation, replace “reuse one supported service” with “reuse one existing or managed service.” The research establishes documented mechanisms, not a support entitlement or a verified supported deployment. This edit avoids an unnecessary support assertion without changing the recommendation.

## Checks passed

- Google: the report correctly separates customer-unrecoverable destruction, the scheduled recovery period, and residual infrastructure copies. The primary destruction page describes customer recovery ending after the scheduled period and infrastructure removal within 45 days of the scheduled destruction time. The deep dive limits residual backup use to regional disaster recovery, excluding restoration of individual keys. The report does not portray these backups as customer-restorable deleted versions. [Google destroy/restore](https://docs.cloud.google.com/kms/docs/destroy-restore), [Google key-management deep dive](https://docs.cloud.google.com/docs/security/key-management-deep-dive).
- Azure pricing: the primary table explicitly lists an hourly fee per Managed HSM pool. Retrieved dollar amounts are placeholders; the report correctly gives no numeric quote or total-cost ranking. [Microsoft pricing](https://azure.microsoft.com/en-us/pricing/details/key-vault/).
- Azure mechanism: ordinary operations, optional attested SKR, logical-key deletion granularity, expiration exceptions, and retained-backup restoration are accurately carried from managed-options.md facts F2–F6 and its imported evidence. SKR is not presented as retracting released plaintext.
- OpenBao: current named-key deletion and irreversible version trimming are distinguished from the reversible decryption threshold. The reopened API supports those current-keyring operations; the digest separately supports the Raft snapshot recovery concern. Nothing claims current deletion edits an earlier snapshot. [OpenBao Transit API 2.6](https://openbao.org/docs/2.6.x/api/secret/transit/).
- Luna: the custom pair is expressly hypothetical. The host-retained intermediate-blob bypass is included, and two-device dependency/data-loss consequences are conditional on a protected pair protocol. Battery preservation is not treated as proof of autonomous FM execution or selective expiry.
- Recommendation: operating burden, fit, and construction consequences are identified as engineering judgments/inferences. No price totals, throughput estimates, complete vendor solution, or installed qualification is asserted. The strict outcome remains no qualified backend; the investment recommendation against custom hardware is separately stated.
- Proposed contract: explicit owner approval remains required. It changes destruction/holder guarantees, preserves original identity and deadline, scopes enforcement to controlled decision points, calls out in-flight/released plaintext, and leaves deletion Pending until evidenced. It never promises exact stopped-holder expiry under that narrower scope.

Exactly four primary URLs were reopened, listed above. Other checks used the granted digests' primary-source evidence; they were not independently refreshed. This verifies citation fidelity and inference boundaries, not the physical guarantees, production configuration, or commercial support.
