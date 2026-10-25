# Backup and restore primary-source digest

Retrieved/accessed 2026-10-25. Publication dates undated in retrieved pages. Publisher: OpenBao project. Sources are version-pinned 2.6.x documentation (the proposal observes a generic 2.6.2 controller, not qualified custody).

- O1: https://openbao.org/docs/2.6.x/api/system/storage/raft/ — snapshot restore installs the selected prior cluster state; force restore bypasses seal-consistency checking. Confidence medium (single official primary documentation); class documented capability. This means a lifecycle ledger restored with that cut cannot by itself witness that it is the newest state. This is an inference about the required authority boundary, not proof of any particular installed recovery.
- O2: https://openbao.org/docs/2.6.x/api/secret/transit/ — optional plaintext key backup returns key configuration and all versions; restore reconstructs those versions. Confidence medium (single official primary documentation); class documented capability. Denied current decrypt or deleting a live key object does not demonstrate destruction of a retained restorable backup. Configuration prohibitions and separate external-root envelopes are proposed mitigations until independently inventoried/proven.

Independent publisher corroboration: Microsoft Managed HSM backup/restore documentation in alternative-r1.md also explicitly permits restoring purged keys from earlier backups. Together these show that ordinary current-key deletion plus backup restoration is not sufficient; they do not show that every custom no-backup architecture is impossible.

Written confirmation/qualification: exact installed backup/seal/key generations and all recovery paths, surviving monotonic time/authority witness outside every restore cut, stale/offline devices, positive live-unit restore control and negative expired-unit restore. No physical experiments performed.
