# GDPR Processing Activity Records

Processing activity records are derived from persisted party events and exposed through the `GetProcessingRecords` EventStore query.

Each record is tenant and party scoped and contains only bounded audit metadata:

- `partyId`
- `tenantId`
- `sequenceNumber`
- `eventType`
- `operationCategory`
- `timestamp`
- `actorId`
- `correlationId`
- `outcome`
- `summary`

The schema intentionally excludes raw command payloads, raw query payloads, exported package content, tokens, claims dictionaries, contact values, identifiers, names, and restriction reason text.

The summary field uses stable operation descriptions such as `Consent recorded.`, `Processing restricted.`, and `Party erasure requested.` It must not include free text from commands or decrypted personal data.

Erased parties keep their processing activity records because the records are audit metadata and do not require decrypting erased personal data.
