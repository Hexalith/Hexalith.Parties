using System.Text.Json.Serialization;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Fixed-size, value-free drop counts carried by one shared rebuild candidate.</summary>
internal sealed class PartyProjectionRebuildDiagnostics
{
    /// <summary>Gets or sets the count of unsupported serialization formats.</summary>
    public long NonJson { get; set; }

    /// <summary>Gets or sets the count of unresolved event types.</summary>
    public long UnknownType { get; set; }

    /// <summary>Gets or sets the count of ambiguous short event type names.</summary>
    public long AmbiguousType { get; set; }

    /// <summary>Gets or sets the count of corrupt live payloads.</summary>
    public long CorruptPayload { get; set; }

    /// <summary>Gets or sets the count of undecodable redacted payloads.</summary>
    public long RedactedPayload { get; set; }

    /// <summary>Gets or sets the count of whole-payload redactions.</summary>
    public long WholePayloadRedacted { get; set; }

    /// <summary>Gets or sets the count of non-redacted null payloads.</summary>
    public long NullPayload { get; set; }

    /// <summary>Gets whether unexpected drops require a warning rather than information.</summary>
    [JsonIgnore]
    public bool HasUnexpectedDrops => NonJson > 0 || UnknownType > 0 || AmbiguousType > 0
        || CorruptPayload > 0 || NullPayload > 0;

    /// <summary>Gets whether any drop was observed, without summing potentially saturated counts.</summary>
    [JsonIgnore]
    public bool HasDrops => HasUnexpectedDrops || RedactedPayload > 0 || WholePayloadRedacted > 0;

    /// <summary>Records an unsupported serialization format.</summary>
    public void RecordNonJson() => NonJson = Increment(NonJson);

    /// <summary>Records an unresolved event type.</summary>
    public void RecordUnknownType() => UnknownType = Increment(UnknownType);

    /// <summary>Records an ambiguous short event type name.</summary>
    public void RecordAmbiguousType() => AmbiguousType = Increment(AmbiguousType);

    /// <summary>Records a corrupt live payload.</summary>
    public void RecordCorruptPayload() => CorruptPayload = Increment(CorruptPayload);

    /// <summary>Records an undecodable redacted payload.</summary>
    public void RecordRedactedPayload() => RedactedPayload = Increment(RedactedPayload);

    /// <summary>Records a whole-payload redaction.</summary>
    public void RecordWholePayloadRedacted() => WholePayloadRedacted = Increment(WholePayloadRedacted);

    /// <summary>Records a non-redacted null payload.</summary>
    public void RecordNullPayload() => NullPayload = Increment(NullPayload);

    private static long Increment(long count) => count == long.MaxValue ? count : count + 1;
}
