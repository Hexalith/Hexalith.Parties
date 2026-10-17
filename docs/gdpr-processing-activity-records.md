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

Newly projected records use the operation categories `PartyCommand`, `Consent`, `Restriction`, `Erasure`, and `Other`. Events without a recognized category, including unresolved events and payloads that cannot be decoded, use `Other`; `eventType` retains the short event name separately. A redacted event that still decodes to a recognized payload keeps its mapped category. Category assignment does not change the record's outcome or projection checkpoint rules.

Previously persisted records remain readable with their original categories. Use the supported EventStore projection rebuild to derive the current categories from retained events; deploying this change does not rewrite existing records in place.

Erased parties keep their processing activity records because the records are audit metadata and do not require decrypting erased personal data.
