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

For event-derived erasure tombstones, both the Detail and Processing read models
use the payload's `PartyErased.ErasedAt`, including its offset. The nested
`PartyDetail.ErasedAt` and `LastModifiedAt` use that same payload value. This is
the domain erasure instant, which can differ from the event-envelope timestamp.
The processing record's `timestamp` remains the envelope timestamp in UTC;
`ProjectedAt` continues to track the newest incorporated source or cleanup time.
Later checkpoint advancement and duplicate delivery preserve the erasure instant.
Cleanup-created tombstones retain their existing cleanup-time policy.

Older processing tombstones may contain the envelope time. Deployment and
incremental redelivery do not rewrite already checkpointed values. Use the
supported EventStore full projection rebuild from retained, readable events to
rederive both tombstones from the payload; no in-place data migration is needed.
Select domain `party` and named projection `party-detail` for the affected tenant
and aggregate. Its full-replay handler replaces Detail and Processing together
in one coordinated SDK batch. Have EventStore orchestrate the rebuild through
the [internal SDK rebuild surface](api-contracts.md#4-internal--operational-endpoints),
which is restricted to EventStore invocation; a normal incremental replay or a
`party-index` rebuild does not perform this repair. After completion, verify both
read models' `ErasedAt` against the retained `PartyErased` payload while retaining
the processing record's envelope timestamp.
