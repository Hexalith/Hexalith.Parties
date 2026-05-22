# GDPR Key Rotation and Crypto-Shredding

Hexalith.Parties uses two distinct key operations in the v1.1 GDPR model.

Tenant key rotation changes the tenant-level key material that protects party-level keys. It is an operational security activity: active party key wrapping metadata is updated tenant by tenant, progress is recorded after each safe unit, and reads remain available while old and new wrapping metadata coexist. Tenant rotation status is bounded to tenant id, operation id, phase, counts, failure categories, timestamps, and correlation id. It must not expose key material, wrapped key bytes, raw provider errors, tokens, decrypted personal data, or party display fields.

Party key rotation is the existing per-party domain operation. `RotatePartyKey` and `PartyEncryptionKeyRotated` create a new party key version for one party. They do not perform tenant-wide rotation and should not be treated as satisfying tenant key rotation requirements.

Crypto-shredding is erasure. Destroying a party's readable key versions makes that party's personal data unrecoverable. Tenant key rotation must skip erased parties and must never call create, rotate, or recovery paths for destroyed party keys. Erased-party status remains privacy preserving: operators may see stable erased/verification state, but cryptographic exception text and stale personal data stay hidden.

Operationally, tenant key rotation is resumable and reversible at the metadata level until each party key is safely rewrapped. Crypto-shredding is terminal for the erased party's personal data and is not a transient tenant-rotation failure.
