# Event Handler Patterns

This guide provides handler patterns for every Hexalith.Parties event type. Use it alongside the [sample integration project](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) for working reference code.

For event wire format and subscription setup, see [event-subscribing.md](event-subscribing.md).
For production broker configuration, see [event-publishing.md](event-publishing.md).
For broker-specific deployment templates, see [Kafka pub/sub](../deploy/dapr/pubsub-kafka.yaml), [RabbitMQ pub/sub](../deploy/dapr/pubsub-rabbitmq.yaml), and [Azure Service Bus pub/sub](../deploy/dapr/pubsub-servicebus.yaml).

## Quick Reference

| Event | Handler Action | Code Reference |
|-------|---------------|----------------|
| `PartyCreated` | Create local record (person or org) | [PartyEventHandler.cs L175](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PersonDetailsUpdated` | Update person display name | [PartyEventHandler.cs L221](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `OrganizationDetailsUpdated` | Update org display name | [PartyEventHandler.cs L240](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `ContactChannelAdded` | Add to local contact cache | [PartyEventHandler.cs L202](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `ContactChannelUpdated` | Update local contact cache | [PartyEventHandler.cs L259](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `ContactChannelRemoved` | Remove from local cache | [PartyEventHandler.cs L292](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PreferredContactChannelChanged` | Update preferred contact flag | [PartyEventHandler.cs L313](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `IdentifierAdded` | Add to local identifiers | [PartyEventHandler.cs L345](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `IdentifierRemoved` | Remove from local identifiers | [PartyEventHandler.cs L365](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PartyDeactivated` | Flag local record inactive | [PartyEventHandler.cs L385](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PartyReactivated` | Re-flag active | [PartyEventHandler.cs L404](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PartyDisplayNameDerived` | Update derived display/sort name | [PartyEventHandler.cs L422](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `IsNaturalPersonChanged` | Update type flag | [PartyEventHandler.cs L441](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PartyMerged` | v2 placeholder — log and skip | [PartyEventHandler.cs L453](../samples/Hexalith.Parties.Sample/PartyEventHandler.cs) |
| `PartyErased` | **MANDATORY** — nullify references, replace names | [PartyErased Handler](#partyerased-handler-mandatory) |
| Rejection events | Typically ignore; optionally alert on failures | [Rejection Events](#rejection-events) |

## Key Handler Rules

Before implementing any handler, ensure these rules are followed:

1. **Always return 200 OK** — even for duplicates, unknown events, and missing aggregates. DAPR retries on non-2xx responses, causing redelivery loops.
2. **Implement idempotent deduplication** — use `ConcurrentDictionary<string, bool>` keyed on the CloudEvents `id` header, with a `{correlationId}:{sequenceNumber}` fallback for local testing.
3. **Guard aggregate sequence** — store the highest processed `sequenceNumber` per aggregate and skip or reconcile older events, especially when the broker or subscriber deployment cannot prove per-aggregate ordering.
4. **Use order-tolerant projection updates** — prefer idempotent `set` operations over incremental `delta` operations.
5. **Do NOT log event payloads** — structured logging with correlation IDs only (Security Rule #5). Event payloads may contain personal data.
6. **Handle unknown event types gracefully** — log and return 200 OK.
7. **Normalize event type names before dispatch** — published CloudEvents usually carry fully qualified .NET type names such as `Hexalith.Parties.Contracts.Events.PartyCreated`, so convert them to their short names before switching on them.

```csharp
// Idempotent deduplication pattern
if (!_processedEventIds.TryAdd(eventId, true))
{
    logger.LogInformation("Skipping already-processed event {EventId}", eventId);
    return Results.Ok();
}

if (!TryAcceptSequence(envelope.AggregateId, envelope.SequenceNumber))
{
    logger.LogInformation(
        "Skipping older event sequence {SequenceNumber} for {AggregateId}",
        envelope.SequenceNumber,
        envelope.AggregateId);
    return Results.Ok();
}

// Published CloudEvents typically use fully qualified event names.
string eventType = NormalizeEventTypeName(envelope.EventTypeName);

static string NormalizeEventTypeName(string eventTypeName)
{
    int separator = eventTypeName.LastIndexOf('.');
    return separator >= 0 ? eventTypeName[(separator + 1)..] : eventTypeName;
}
```

## Read Model Scope and Privacy

Subscriber-owned read models should store the smallest useful representation for their bounded context. Start with the stable `partyId`, the last processed aggregate `sequenceNumber`, and operational metadata such as `correlationId` or `timestamp`. Add display names, contact values, identifiers, or natural-person flags only when your application actually needs them.

For reference-only integrations, store the stable `partyId` and your own relationship metadata instead of copying person names, contact channel values, identifier values, dates of birth, or organization details. If your application denormalizes personal data for display or search, it becomes responsible for its own retention, access control, audit, and future erasure cleanup.

```csharp
public sealed record LocalPartyReference
{
    public required string PartyId { get; init; }

    public long LastSequenceNumber { get; set; }

    public DateTimeOffset LastObservedAt { get; set; }

    // Add display/contact/identifier fields only when the bounded context needs them.
}
```

## Handler Patterns by Event Type

### PartyCreated

**When to handle:** Your application stores local records referencing parties (customers, vendors, contacts).

**When to ignore:** Your application only reacts to party changes and doesn't maintain a local party registry.

The `PartyCreated` event carries a `Type` field (`"Person"` or `"Organization"`) and the corresponding details object. Dispatch based on the type to build your local record:

```csharp
// Assumes you already normalized envelope.EventTypeName before switching.
case "PartyCreated":
    var payload = Deserialize<PartyCreatedPayload>(payloadJson);
    if (payload is null) break;

    string displayName = payload.Type == "Person" && payload.PersonDetails is not null
        ? $"{payload.PersonDetails.FirstName} {payload.PersonDetails.LastName}"
        : payload.OrganizationDetails?.LegalName ?? "Unknown";

    // Create your local record with the party ID and display name.
    // Use set-based creation (idempotent) rather than insert-if-not-exists.
    _localStore[partyId] = new LocalRecord
    {
        PartyId = partyId,
        DisplayName = displayName,
        IsActive = true,
    };
    break;
```

**Key properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string` | `"Person"` or `"Organization"` |
| `PersonDetails` | `PersonDetailsPayload?` | First name, last name, date of birth, prefix, suffix |
| `OrganizationDetails` | `OrganizationDetailsPayload?` | Legal name, trading name |

> **Personal data awareness:** `PersonDetails` fields (`FirstName`, `LastName`, `DateOfBirth`, `Prefix`, `Suffix`) are marked with `[PersonalData]` and will be encrypted at rest in v1.1. At publish time, events contain decrypted data — subscribers never see encrypted values.

### PersonDetailsUpdated / OrganizationDetailsUpdated

**When to handle:** Your application displays party names or stores name-derived data.

Update the local display name when person or organization details change. These events carry the full updated details object — use last-write-wins semantics:

```csharp
case "PersonDetailsUpdated":
    var personPayload = Deserialize<PersonDetailsUpdatedPayload>(payloadJson);
    if (personPayload?.PersonDetails is null) break;

    if (_localStore.TryGetValue(partyId, out var personRecord))
    {
        // Last-write-wins: set the display name to the new value.
        // This is order-tolerant — replaying events in any order converges.
        personRecord.DisplayName =
            $"{personPayload.PersonDetails.FirstName} {personPayload.PersonDetails.LastName}";
    }
    break;

case "OrganizationDetailsUpdated":
    var orgPayload = Deserialize<OrganizationDetailsUpdatedPayload>(payloadJson);
    if (orgPayload?.OrganizationDetails is null) break;

    if (_localStore.TryGetValue(partyId, out var orgRecord))
    {
        orgRecord.DisplayName = orgPayload.OrganizationDetails.LegalName;
    }
    break;
```

> **Personal data awareness:** `PersonDetails` fields are marked `[PersonalData]`. If your application caches these values, ensure your own GDPR compliance measures cover them.

### ContactChannelAdded / ContactChannelUpdated / ContactChannelRemoved

**When to handle:** Your application maintains a local contact cache (emails, phones, addresses).

Use order-tolerant update patterns. Track contacts by their `ContactChannelId` (a stable identifier) rather than by position or index:

```csharp
case "ContactChannelAdded":
    var addPayload = Deserialize<ContactChannelAddedPayload>(payloadJson);
    if (addPayload is null) break;

    if (_localStore.TryGetValue(partyId, out var addRecord))
    {
        // Upsert by ContactChannelId — idempotent and order-tolerant.
        addRecord.Contacts[addPayload.ContactChannelId] =
            new LocalContact(addPayload.Type, addPayload.Value, addPayload.IsPreferred);
    }
    break;

case "ContactChannelUpdated":
    var updatePayload = Deserialize<ContactChannelUpdatedPayload>(payloadJson);
    if (updatePayload is null) break;

    if (_localStore.TryGetValue(partyId, out var updateRecord))
    {
        // Merge updated fields with existing values.
        // Nullable properties mean "unchanged" — preserve the existing value.
        if (updateRecord.Contacts.TryGetValue(updatePayload.ContactChannelId, out var existing))
        {
            updateRecord.Contacts[updatePayload.ContactChannelId] = existing with
            {
                Type = updatePayload.Type ?? existing.Type,
                Value = updatePayload.Value ?? existing.Value,
                IsPreferred = updatePayload.IsPreferred ?? existing.IsPreferred,
            };
        }
    }
    break;

case "ContactChannelRemoved":
    var removePayload = Deserialize<ContactChannelRemovedPayload>(payloadJson);
    if (removePayload is null) break;

    if (_localStore.TryGetValue(partyId, out var removeRecord))
    {
        removeRecord.Contacts.Remove(removePayload.ContactChannelId);
    }
    break;
```

> **Personal data awareness:** The `Value` field of contact channels (email addresses, phone numbers, postal addresses) is marked `[PersonalData]`.

### PreferredContactChannelChanged

**When to handle:** Your application uses the preferred contact for notifications or display.

This event signals that a different contact channel of the same type should become the preferred one:

```csharp
case "PreferredContactChannelChanged":
    var prefPayload = Deserialize<PreferredContactChannelChangedPayload>(payloadJson);
    if (prefPayload is null) break;

    if (_localStore.TryGetValue(partyId, out var prefRecord)
        && prefRecord.Contacts.TryGetValue(prefPayload.ContactChannelId, out var selectedChannel))
    {
        // Demote all channels of the same type, then promote the selected one.
        foreach (var kvp in prefRecord.Contacts)
        {
            if (kvp.Value.Type == selectedChannel.Type)
            {
                prefRecord.Contacts[kvp.Key] = kvp.Value with
                {
                    IsPreferred = kvp.Key == prefPayload.ContactChannelId
                };
            }
        }
    }
    break;
```

### IdentifierAdded / IdentifierRemoved

**When to handle:** Your application tracks party identifiers (VAT numbers, SIRET, national IDs).

Track identifiers by their `IdentifierId` and derive counts from the collection size — never use increment/decrement:

```csharp
case "IdentifierAdded":
    var idAddPayload = Deserialize<IdentifierAddedPayload>(payloadJson);
    if (idAddPayload is null) break;

    if (_localStore.TryGetValue(partyId, out var idAddRecord))
    {
        // Set-based: upsert by IdentifierId. Count derived from collection size.
        idAddRecord.Identifiers[idAddPayload.IdentifierId] =
            new LocalIdentifier(idAddPayload.Type, idAddPayload.Value);
        idAddRecord.IdentifierCount = idAddRecord.Identifiers.Count;
    }
    break;

case "IdentifierRemoved":
    var idRemovePayload = Deserialize<IdentifierRemovedPayload>(payloadJson);
    if (idRemovePayload is null) break;

    if (_localStore.TryGetValue(partyId, out var idRemoveRecord))
    {
        idRemoveRecord.Identifiers.Remove(idRemovePayload.IdentifierId);
        idRemoveRecord.IdentifierCount = idRemoveRecord.Identifiers.Count;
    }
    break;
```

### PartyDeactivated / PartyReactivated

**When to handle:** Your application needs to flag or soft-delete records when a party becomes inactive.

These are marker events with no additional properties. Use absolute state assignment (not toggles):

```csharp
case "PartyDeactivated":
    if (_localStore.TryGetValue(partyId, out var deactivateRecord))
    {
        // Absolute state: safe to replay multiple times.
        deactivateRecord.IsActive = false;
    }
    // If party not found locally, acknowledge without error.
    break;

case "PartyReactivated":
    if (_localStore.TryGetValue(partyId, out var reactivateRecord))
    {
        reactivateRecord.IsActive = true;
    }
    break;
```

**Soft-delete considerations:** When a party is deactivated, your application should decide whether to:
- Hide the record from search results but keep it accessible by ID
- Block new transactions referencing this party
- Display a visual indicator (e.g., "Inactive") in the UI

When reactivated, reverse these measures. Do not delete data on deactivation — the party may be reactivated later.

### PartyDisplayNameDerived

**When to handle:** Your application displays or sorts by party names.

This event provides a pre-computed display name and sort name. Use these instead of computing names from person/org details:

```csharp
case "PartyDisplayNameDerived":
    var namePayload = Deserialize<PartyDisplayNameDerivedPayload>(payloadJson);
    if (namePayload is null) break;

    if (_localStore.TryGetValue(partyId, out var nameRecord))
    {
        nameRecord.DisplayName = namePayload.DisplayName;
        nameRecord.SortName = namePayload.SortName;
    }
    break;
```

### IsNaturalPersonChanged

**When to handle:** Your application applies different business rules based on whether a party is a natural person (individual) vs. a legal entity.

This event signals that the party's natural person classification has changed. This may have data handling implications (e.g., GDPR applies to natural persons):

```csharp
case "IsNaturalPersonChanged":
    var personFlagPayload = Deserialize<IsNaturalPersonChangedPayload>(payloadJson);
    if (personFlagPayload is null) break;

    if (_localStore.TryGetValue(partyId, out var personFlagRecord))
    {
        personFlagRecord.IsNaturalPerson = personFlagPayload.IsNaturalPerson;
        // If your application has GDPR-specific logic, re-evaluate applicability here.
    }
    break;
```

### PartyMerged (v2 Placeholder)

**When to handle:** Not yet — this is a forward-compatibility placeholder. When v2 is released, handlers should:

1. Look up the survivor party by `SurvivorPartyId`
2. Merge data from the merged party into the survivor
3. Remove or redirect the merged party's local record

Until v2, log and acknowledge:

```csharp
case "PartyMerged":
    // v2 placeholder: log and acknowledge without processing.
    // When v2 is released, implement merge logic:
    //   var mergePayload = Deserialize<PartyMergedPayload>(payloadJson);
    //   1. Find survivor record by mergePayload.SurvivorPartyId
    //   2. Merge merged party's data into survivor
    //   3. Remove or redirect merged party's local record
    logger.LogInformation(
        "PartyMerged acknowledged for {PartyId} (v2 not yet implemented)", partyId);
    break;
```

### Rejection Events

Rejection events are published when commands fail (per architectural Decision D3). They implement `IRejectionEvent` and carry an optional `Message` property.

The 13 rejection events are:

| Event | Meaning |
|-------|---------|
| `CompositeOperationConflict` | Composite command conflicted with concurrent changes |
| `ContactChannelNotFound` | Referenced contact channel does not exist |
| `IdentifierNotFound` | Referenced identifier does not exist |
| `PartyCannotAddDuplicateChannel` | Contact channel already exists for this party |
| `PartyCannotAddDuplicateIdentifier` | Identifier already exists for this party |
| `PartyCannotBeCreatedWithInvalidId` | Party ID failed validation |
| `PartyCannotBeCreatedWithoutOrganizationDetails` | Organization party missing required details |
| `PartyCannotBeCreatedWithoutPersonDetails` | Person party missing required details |
| `PartyCannotBeCreatedWithoutType` | Party type not specified |
| `PartyCannotBeDeactivatedWhenInactive` | Party is already inactive |
| `PartyCannotBeReactivatedWhenActive` | Party is already active |
| `PartyNotFound` | Referenced party does not exist |
| `PartyTypeMismatch` | Operation attempted on wrong party type |

**When to handle:** Most subscribers should ignore rejection events. Consider handling them only if your application:
- Needs to display command failure feedback to end users
- Monitors command success rates for operational dashboards
- Implements compensating transactions triggered by failures

**When to ignore:** If your application only maintains a read projection of party data, rejection events are irrelevant — the projection only reflects successful state changes.

```csharp
// Rejection events fall through to the default case.
// If you need to handle them, check for the rejection event types explicitly:
default:
    if (eventType.StartsWith("PartyCannot") || eventType.EndsWith("NotFound")
        || eventType == "CompositeOperationConflict" || eventType == "PartyTypeMismatch")
    {
        // Optional: alert on command failures for monitoring.
        logger.LogInformation("Rejection event '{EventType}' for {PartyId}", eventType, partyId);
    }
    break;
```

---

## PartyErased Handler (MANDATORY)

> **WARNING: PartyErased subscription is mandatory for ALL consuming applications regardless of which other events they handle.**
>
> If your application stores **any** reference to a party ID — whether as a foreign key, a display name cache, a search index entry, or any other form — you **must** handle `PartyErased` to remain GDPR-compliant.

### Background

`PartyErased` is a v1.1 GDPR event. It fires **after** crypto-shredding destroys the party's per-party encryption key. The event payload contains **only** the `partyId` — no personal data is included.

When you receive `PartyErased`, you must clean up all local references to the erased party. Failure to do so creates dangling references that violate GDPR right-to-erasure requirements.

### Complete Handler Implementation

The following example shows a complete `PartyErased` handler for an invoice management system — Clara's journey from the product requirements:

```csharp
case "PartyErased":
    HandlePartyErased(partyId, logger);
    break;

// ...

private static void HandlePartyErased(string partyId, ILogger logger)
{
    // Step 1: Find all local records referencing the erased partyId.
    // In a real application, query your database for all records with this party reference.
    List<Invoice> affectedInvoices = _invoiceStore.Values
        .Where(inv => inv.CustomerPartyId == partyId)
        .ToList();

    foreach (Invoice invoice in affectedInvoices)
    {
        // Step 2: Nullify the party reference (clear the foreign key).
        invoice.CustomerPartyId = null;

        // Step 3: Replace display names with "[Erased Party]".
        invoice.CustomerDisplayName = "[Erased Party]";

        // Step 4: Preserve the record itself.
        // Invoices have independent legal retention requirements (e.g., 7 years).
        // Do NOT delete the invoice — only remove party-identifying information.
    }

    // Step 5: Log the erasure handling for audit trail.
    // Use structured logging with the partyId only — no personal data to log.
    logger.LogInformation(
        "PartyErased handled: nullified party reference in {Count} invoices for {PartyId}",
        affectedInvoices.Count,
        partyId);
}
```

### Handler Checklist

When implementing your `PartyErased` handler, verify:

- [ ] All local records referencing the `partyId` are found (query all tables/collections)
- [ ] Party foreign keys are nullified (set to `null`)
- [ ] Display names are replaced with `"[Erased Party]"` (not deleted)
- [ ] Records with independent legal retention are preserved (invoices, contracts, audit logs)
- [ ] The erasure is logged for audit trail (party ID only, no personal data)
- [ ] The handler returns 200 OK even if no local records are found (idempotent)

### Testing Strategy

Test your `PartyErased` handler by verifying:

1. **Reference nullification:** After handling, all local records that referenced the erased party should have `null` party references.
2. **Display name replacement:** All display names should show `"[Erased Party]"`.
3. **Record preservation:** Records with independent retention (invoices, contracts) should still exist — only party-referencing fields are cleaned.
4. **Audit logging:** The handler should produce a log entry recording how many records were affected.
5. **Idempotency:** Handling the same `PartyErased` event twice should be safe (no errors, no double-counting).

---

## Dangling Reference Guidance

### What Happens When a Party Is Erased

When a party is erased via GDPR crypto-shredding:

1. The per-party encryption key is destroyed, making encrypted personal data unrecoverable
2. `PartyErased` fires with only the `partyId` in the payload
3. All subscribing applications receive the event and must clean up local references
4. Any application that **fails to handle** `PartyErased` is left with **dangling references** — party IDs that point to erased (non-existent) parties

### Detecting Dangling References

Dangling references can occur when:
- An application was offline when `PartyErased` was delivered (and dead-letter processing failed)
- A new application is deployed that wasn't subscribed to `PartyErased` during the erasure
- A bug in the handler missed some references

**Detection patterns:**

1. **Referential integrity audits:** Periodically query your local store for party IDs that no longer exist in the Parties service. Use the `GET /api/parties/{id}` endpoint — a 404 response indicates a potentially erased party.

2. **Gap checking:** Compare your local party ID set against the Parties service's active party list. Any IDs present locally but missing from the service may be erased.

3. **Scheduled cleanup jobs:** Run a nightly or weekly job that:
   - Collects all unique party IDs referenced in your local store
   - Batch-queries the Parties service for their existence
   - Flags or cleans up references to non-existent parties

### Cleanup Strategies

When you find dangling references:

1. **Nullification:** Set the party foreign key to `null`. The record remains but no longer points to a specific party.
2. **Placeholder replacement:** Replace display names with `"[Erased Party]"` so users see a meaningful indicator rather than blank fields or errors.
3. **Archival:** Move affected records to an archive table where dangling references are expected and won't cause operational issues.

### Foreign Key Management Strategies

When designing your schema to reference party IDs:

**Nullable party IDs (recommended):**
```sql
-- Party reference is nullable — supports erasure without cascading deletes
CREATE TABLE invoices (
    id UUID PRIMARY KEY,
    customer_party_id UUID NULL,        -- Nullable: set to NULL on erasure
    customer_display_name TEXT NOT NULL, -- Replaced with "[Erased Party]" on erasure
    amount DECIMAL NOT NULL,
    -- ... other invoice fields with independent retention
);
```

**Soft references vs. hard references:**

| Strategy | Description | Erasure Impact |
|----------|-------------|----------------|
| **Soft reference** (recommended) | Nullable FK, no cascade | Set to `NULL`, replace display name |
| **Hard reference** | Non-nullable FK with cascade | **Dangerous** — cascade delete destroys records with independent retention |
| **Denormalized copy** | Store display name alongside FK | Nullify FK, replace copied name with `"[Erased Party]"` |

> **Never use cascading deletes with party foreign keys.** Records like invoices, contracts, and audit logs have independent legal retention requirements and must not be deleted when a party is erased.

### Concrete Example: Invoice System (Clara's Journey)

Clara, a backend developer on invoice management, subscribes to party events:

**Setup:**
- Stores `partyId` as a nullable foreign key in the `invoices` table
- Subscribes to `PersonDetailsUpdated`, `OrganizationDetailsUpdated`, and `PartyErased`
- Has 4 invoices referencing party `p-12345` (customer: "Marie Dupont")

**When `PartyErased` fires for `p-12345`:**

1. Clara's handler finds 4 invoices where `customer_party_id = 'p-12345'`
2. Sets `customer_party_id = NULL` on all 4 invoices
3. Replaces `customer_display_name` with `"[Erased Party]"` on all 4 invoices
4. Invoices remain in the system (7-year legal retention requirement)
5. Logs: `"PartyErased handled: nullified party reference in 4 invoices for p-12345"`

**Result:** The invoices are preserved for legal compliance, but no longer contain any information that could identify the erased party.

---

## Tolerant Deserialization Guidance

### Unknown Field Handling

`System.Text.Json` ignores unknown properties by default. No special configuration is needed:

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() },
};
```

When a new field is added to an event payload (e.g., a future `MiddleName` field on `PersonDetailsUpdated`), existing subscribers safely ignore it. No code changes are required.

### Missing Optional Field Handling

Use nullable properties in your deserialization types. When a field is absent from the JSON payload, it deserializes to `null`:

```csharp
public sealed record ContactChannelUpdatedPayload
{
    public required string ContactChannelId { get; init; }

    // Nullable: absent means "unchanged". Use the existing value.
    public string? Type { get; init; }
    public string? Value { get; init; }
    public bool? IsPreferred { get; init; }
}
```

**Documented defaults for nullable fields:**

| Event | Nullable Field | Default When Absent |
|-------|---------------|-------------------|
| `ContactChannelUpdated` | `Type`, `Value`, `IsPreferred` | Preserve existing value |
| `PartyCreated` | `PersonDetails`, `OrganizationDetails` | `null` (check `Type` to determine which is relevant) |
| `OrganizationDetailsPayload` | `TradingName` | `null` |
| `PersonDetailsPayload` | `DateOfBirth`, `Prefix`, `Suffix` | `null` |

### Additive Event Evolution

The Hexalith.Parties event contract evolves additively:

1. **New optional fields** may be added to existing event payloads. Existing subscribers ignore them (see [Unknown Field Handling](#unknown-field-handling)).
2. **New event types** may be introduced. Existing subscribers receive them in the `default` switch case and return 200 OK.
3. **Existing fields are never removed or renamed.** This guarantees backward compatibility.

### Tolerant Reader Pattern Example

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    // camelCase: matches the wire format serialization convention.
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

    // Case-insensitive: tolerates minor casing differences from future producers.
    PropertyNameCaseInsensitive = true,

    // String enums: deserializes enum values from their string names.
    Converters = { new JsonStringEnumConverter() },

    // Unknown properties are IGNORED by default in System.Text.Json.
    // No need to set JsonSerializerOptions.UnmappedMemberHandling.
};

// Deserialize with tolerance: unknown fields ignored, missing optionals become null.
T? payload = JsonSerializer.Deserialize<T>(payloadJson, _jsonOptions);
```

### Forward-Compatibility Examples

**`PartyMerged` (v2):** Already present in the contracts as a placeholder. Current subscribers acknowledge it without processing. When v2 ships, subscribers add a handler in the `switch` statement — no breaking changes.

**`PartyErased` (v1.1):** Will arrive as a new event type. Subscribers without a handler will hit the `default` case and return 200 OK. Subscribers that have implemented the [PartyErased handler](#partyerased-handler-mandatory) will process it immediately.

**Consent, restriction, export, and processing-record events (v1.1+):** Contract naming leaves room for consent lifecycle, processing restriction, data export, and processing activity events. MVP consumers are not required to process these unavailable capabilities as active behavior; keep tolerant default handling until your integration explicitly opts into the future GDPR workflow.

This additive evolution pattern means:
- **Producers** can ship new events without coordinating with subscribers
- **Subscribers** can add handlers at their own pace
- **No breaking changes** — old subscribers continue to work with new event types
