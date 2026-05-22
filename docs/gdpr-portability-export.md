# GDPR Portability Export

The `ExportPartyData` EventStore query returns a `PartyDataPortabilityPackage` for one party and one tenant.

The package includes readable party detail data, contact channels, identifiers, consent records, restriction status, projection freshness metadata, processing activity summaries, and bounded audit metadata (`partyId`, `tenantId`, `exportedAt`, `exportedBy`, and `correlationId`).

Restriction policy: a restricted party can still be exported for an authorized administrator or DPO. The package status is `RestrictedExported`, and the current restriction state remains visible in the party detail.

Privacy-preserving outcomes:

- Erased parties return status `Erased` with no `Party` payload.
- Unavailable personal data returns status `PersonalDataUnavailable` with no partial `Party` payload.
- Export filenames must be derived from party id plus UTC export timestamp only; tenant ids, display names, contact values, identifiers, and free-text reasons are not allowed in filenames.
- Logs and telemetry for export operations must use bounded metadata only and must not include the exported payload.
