# ADR: Consumer identity to party_id binding

Status: Accepted

Date: 2026-06-10

## Context

Epics 4 and 5 need a signed-in Consumer to be scoped to exactly one Party before any `/me`
surface can show data. The host already owns OIDC sign-in, keeps tokens server-side, maps
roles from Keycloak/tache, and consumes a verified `party_id` claim fail-closed through
`PartyIdClaimResolver`. `RoleLandingRedirect` routes a bound Consumer to `/me` and an
unbound or ambiguous Consumer to `NoPartyBinding`. `ISelfScopedPartiesClient` is the only
Consumer data-access path and injects the resolved party id instead of accepting one from
the caller.

The remaining AR-Gap-Binding question is how a Consumer identity becomes bound to that
verified `party_id`. The mapping must support tenancy, auditability, production Keycloak
/ tache operation, and fail-closed runtime behavior. It must not be stored in the Parties
event stream.

## Decision

Use an admin-link provisioning flow for MVP. An authorized Admin, TenantOwner, or support
operator links an existing IdP user to an existing Party after verification. The runtime
binding is emitted as the IdP `party_id` claim through the existing Keycloak/tache mapper
shape. A small identity-binding audit store records the operator decision and current
binding state outside the Parties event stream.

This gives the Consumer area a concrete build target: provision through an operator flow,
write the verified IdP user attribute, emit one `party_id` claim, and keep the existing
fail-closed UI and self-scope seams as consumers of that claim.

## Selected Mechanism

Selected option: `admin-link`.

The IdP is the runtime source of the `party_id` claim. The small binding store is the
operator audit and reconciliation source. Parties domain events remain focused on Party
business changes and never carry identity-to-party mappings.

The committed development realm already proves the expected claim shape: the
`hexalith-parties-ui` client has a `party-id-mapper` that maps user attribute `party_id`
to claim `party_id` on id token, access token, and userinfo with `multivalued=false`.
The production tache realm must provide the same logical mapper contract.

## Alternatives Considered

`admin-link`: best MVP fit. It reuses the existing IdP claim mapper, avoids consumer
self-claim account-takeover risk, keeps implementation narrow, and makes verification
an explicit operator responsibility. It needs an operator audit trail because an IdP user
attribute alone is not enough to explain who linked what, when, and why.

`self-registration`: rejected for MVP. It can be valuable later, but it requires identity
proofing, duplicate-party prevention, recovery flows, privacy-safe lookup UX, and fraud
controls before a Consumer can safely claim or create a Party binding. Without those
controls it risks account takeover and privacy leakage.

`IdP federation`: rejected for MVP. It is appropriate when an upstream provider can assert
a stable, tenant-specific external identity that already maps to a Party. It adds federation
contracts, tenant-specific mapping rules, operational dependency on upstream IdPs, and
more production configuration than the current Keycloak/tache setup requires for the first
Consumer release.

## Trade-offs

Admin-link is lower risk and faster to ship, but it is operator-mediated and does not give
Consumers a self-service onboarding path. The audit store adds one small persistence surface,
but it avoids overloading the Parties event stream with identity provisioning concerns.

The runtime continues to depend on IdP claim correctness. That is acceptable because the
current host already consumes `party_id` as a verified claim and fails closed when the claim
is missing, empty, or ambiguous. The binding store is not used by Consumer pages at request
time; it supports audit, reconciliation, deletion, and rotation workflows.

## Provisioning / Onboarding Flow

1. A Consumer IdP account exists or is created in the tenant's Keycloak/tache realm.
2. An authorized operator finds the existing Party record and verifies it is the account
   holder's Party using tenant-approved evidence outside the Parties event stream.
3. The operator creates or updates the binding through the Story 4.2 provisioning surface.
4. The provisioning flow writes the binding audit record and sets the IdP user attribute
   `party_id` to the verified Party id for that tenant.
5. The IdP emits exactly one `party_id` claim through the parties UI client mapper.
6. On next sign-in or session refresh, `PartyIdClaimResolver` resolves the single claim and
   `RoleLandingRedirect` sends the Consumer to `/me`.
7. If the attribute is absent, empty, duplicated, or removed, the Consumer reaches
   `NoPartyBinding` and no data-access call is made.

## Binding Data Shape

Binding placement: IdP claim plus small binding store.

Store ownership: the identity-binding provisioning component introduced by Story 4.2. It is
outside `Hexalith.Parties.Contracts`, the Parties event stream, Party projections, and
EventStore command/event history.

Lookup key: `{tenant, idp_issuer, idp_subject}`. The current active record points to a
single `party_id`.

Fields:

- `tenant`
- `idp_issuer`
- `idp_subject`
- `party_id`
- `status` (`Active`, `Suspended`, `Removed`)
- `bound_by_subject`
- `bound_at_utc`
- `verification_reference` (opaque internal reference, no PII)
- `reason_code`
- `updated_by_subject`
- `updated_at_utc`
- `version` or `etag`

Update path: only authorized operators may create, suspend, remove, or rotate a binding.
Every update records who performed it and when. Rotation creates a new active `party_id`
state and supersedes the previous state; the IdP attribute is updated to match the active
binding.

Deletion/removal behavior: removing or suspending a binding clears the IdP `party_id`
attribute. The next Consumer sign-in or session refresh fails closed to `NoPartyBinding`.
Historical audit metadata may be retained according to tenant retention policy, but it must
not include real PII, decoded JWT payloads, secrets, or Party event data.

## Security / Privacy Guardrails

- The binding store and IdP attribute are the only identity-to-party mapping locations.
  The Parties event stream must never contain this mapping.
- The browser never receives bearer tokens and never calls EventStore directly.
- A Consumer principal must carry the `Consumer` role and exactly one non-empty `party_id`
  claim before reaching data screens.
- `eventstore:tenant`, normalized by `PartiesClaimsTransformation`, remains part of the
  effective scope: `{tenant, party_id}`.
- Operators must not use list/search disclosure as consumer proof. Verification evidence
  belongs to the onboarding process and audit reference, not to the Consumer runtime.
- Logs, test fixtures, docs, and sample records use synthetic ids such as
  `party-readonly-001`; no real PII or decoded JWT payloads are recorded.

## Implementation Impact

Story 4.2 implements provisioning only. It must preserve these existing consumers and
seams:

- `PartyIdClaimResolver` remains the fail-closed single-claim resolver.
- `RoleLandingRedirect` continues to route bound Consumers to `/me` and unbound Consumers
  to `NoPartyBinding`.
- `NoPartyBinding` remains the neutral unbound onboarding/error state.
- `ISelfScopedPartiesClient` remains the only Consumer data-access path and never accepts
  a caller-supplied party id.
- The Keycloak/tache realm mapper shape remains user attribute `party_id` to claim
  `party_id`, `multivalued=false`, emitted to id token, access token, and userinfo where
  the host requires it.

Story 4.2 likely changes:

- the Keycloak/tache realm configuration and topology tests for the `party-id-mapper`,
  Consumer seed user, and unbound Consumer case;
- a small identity-binding provisioning surface or service owned outside the Parties
  event stream;
- operator authorization around create/suspend/remove/rotate binding actions;
- tests that prove a bound Consumer reaches `/me` and an unbound/ambiguous Consumer reaches
  `NoPartyBinding`.

Story 4.2 must not add Parties commands, events, projections, actors, public actor-host
endpoints, DAPR ACL expansion, or browser token flows for this binding.

## Test Strategy

Static topology tests should pin the realm mapper contract: `party-id-mapper`, source user
attribute `party_id`, claim name `party_id`, `multivalued=false`, and emission on the token
surfaces used by the host. Seed-user tests should include a bound Consumer with synthetic
`party_id` and an unbound Consumer.

Host tests should continue to cover `PartyIdClaimResolver` and `RoleLandingRedirect` for
present, absent, empty, and ambiguous claims. Consumer flow tests for Story 4.2 should prove
that a provisioned user reaches `/me`, while unbound or removed bindings fail closed to
`NoPartyBinding`.

Provisioning tests should cover create, duplicate active binding rejection, rotation,
suspend/remove, unauthorized operator denial, and reconciliation when the audit store and
IdP attribute drift.

## Consequences

Epics 4 and 5 can be estimated against an accepted mechanism: admin-link, IdP runtime claim,
and small external binding audit store. ConsumerPortal work can assume the existing
fail-closed resolver and self-scoped accessor rather than redesigning them.

Future self-registration or federation can be added as new provisioning options later, but
they must feed the same runtime contract: one verified `party_id` claim or no data access.
