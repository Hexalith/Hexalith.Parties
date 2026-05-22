# GDPR Erased Party Status

Erased parties must be distinguishable from missing, inactive, restricted, and transient key-store failure states.

Supported surfaces use these stable signals:

- Detail/admin status: `IsErased=true`, `ErasedAt`, and erasure status `Erased`.
- Export: `PartyDataPortabilityPackage.Status=Erased` with no `Party` payload.
- Processing records: audit metadata remains available without decrypting erased personal data.
- Commands: mutation attempts return a `PartyErasureInProgress` rejection with `Status=Erased` and the bounded message `Party is erased and no longer inspectable.`
- List/search/picker surfaces exclude erased entries or show only an erased status where the UI already supports that state.

Responses must not expose destroyed-key messages, cryptographic exception text, stale display names, contact values, identifiers, or raw command/query payloads.
