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


## Bounded processing history and cutover

With a generation-capable SDK selected, the canonical processing row retains at
most 32 inline records. Larger histories
use constant-size metadata and sequence buckets containing at most 32 records.
Live writes coordinate Detail, processing metadata and the affected buckets in
one SDK batch. Duplicate sequences stay deduplicated; a resolved event replaces
its retained failed fact without changing the historical order. Erasure retains
these PII-free Article 30 facts and the bucket pointer. Source-capable erasure
also stages oversized legacy audit lists before publishing its coordinated
tombstones, so retention cannot exceed the canonical batch budget. Immutable
erasure staging resumes from the observed legacy ETag after an application restart.

Legacy conversion and full rebuilds prepare disjoint immutable generations.
Preparation performs no writes. The EventStore SDK stages create-only values in
resumable groups of at most 32 operations and a conservative 512-KiB group
budget, below the unchanged 100-operation/1-MiB batch gate. Only a successful
coordinated Detail/metadata commit selects that generation. Optimistic ETags,
newer checkpoints and retained erasure fences prevent stale promotion. Abandoned
generations never become authoritative.

Before deploying a cutover, drain every older processing writer. Older binaries
cannot honor bucket authority. Matching EventStore SDK generation support must
be published and adopted first; the unchanged currently selected package preserves its complete legacy live
and rebuild behavior. Automatic bucket conversion stays disabled until that
capability is published and adopted. Source verification
is not package-publication or deployment evidence. No deployed conversion is
performed by this change.

`GetProcessingRecords` without SDK paging still returns the complete authorized
array. Data portability exports also retain the complete history and allocate
in proportion to its size. An SDK paging envelope requests 1–100 records and
returns a protected continuation in response paging metadata. The cursor binds
tenant, party, authenticated caller, page size and projection revision. A write
invalidates older cursors; restart from the first page. Missing/corrupt buckets
or a concurrent revision change fail rather than certifying a partial history.
`IAdminPortalGdprClient.GetProcessingRecordsPageAsync` exposes this additive path.

Bucket metadata also records the first and last retained sequence. Its count must
cover that contiguous interval exactly, and its bucket bounds must agree with
those sequence boundaries. Each fetched bucket must contain every retained
sequence in its intersection with that interval. This bounds page validation to
the fetched buckets while rejecting missing, empty or truncated coverage. Both
paged reads and complete exports revalidate the canonical metadata ETag after
loading records; a write during loading fails the read.

Unresolved deliveries retain audit facts without advancing either canonical
checkpoint. Bounded SDK manifests remain recoverable while pending; repeated
unresolved delivery recovers the original decision and performs no new write
when the facts already agree. Conditional writes and large-delivery parts use
bounded deterministic identities. A later resolved delivery replaces failed
facts and advances Detail and Processing together.
