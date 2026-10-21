using System.Reflection;
using System.Text;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Serialization;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Verifies whole-payload redaction cannot turn an empty payload into a domain fact.</summary>
public sealed class PartySdkProjectionRedactedCheckpointTests
{
    /// <summary>Checks current and historical root-redaction shapes without depending on the CLR event type.</summary>
    [Theory]
    [InlineData(nameof(PartyDeactivated), "{}", "json-redacted")]
    [InlineData(nameof(PartyDeactivated), " \r\n{ \t }\n", "JSON-REDACTED")]
    [InlineData(nameof(PartyDeactivated), " \tnull\r\n", "json-redacted")]
    [InlineData(nameof(PartyCreated), "{}", "json-redacted")]
    [InlineData(nameof(PartyCreated), " \r\n{ \t }\n", "JSON-REDACTED")]
    [InlineData(nameof(PartyCreated), " \tnull\r\n", "json-redacted")]
    [InlineData("Future.Events.UnknownEvent", "{}", "json-redacted")]
    [InlineData("Future.Events.UnknownEvent", " \r\n{ \t }\n", "JSON-REDACTED")]
    [InlineData("Future.Events.UnknownEvent", " \tnull\r\n", "json-redacted")]
    public void WholePayloadRedactionAdvancesWithoutApplyingOrResolvingEventType(
        string eventTypeName, string payload, string format)
    {
        AssertCheckpointOnly(Event(eventTypeName, payload, format));
    }

    /// <summary>Confirms an ambiguous CLR type cannot block a wholly redacted event checkpoint.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData(" \r\n{ \t }\n")]
    [InlineData(" \tnull\r\n")]
    public void WholePayloadRedactionDoesNotRequireAnUnambiguousEventType(string payload)
    {
        // The Contracts assembly has no actual short-name collision. Isolate a synthetic
        // resolver outcome, as the existing ambiguity tests do, and remove it even on failure.
        string eventTypeName = "SyntheticRedactedAmbiguous" + Guid.NewGuid().ToString("N");
        Type resolver = typeof(PartyEventTypeResolver);
        object cache = resolver.GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Type outcomeType = typeof(ResolveOutcome);
        object ambiguous = Activator.CreateInstance(outcomeType, [null, true])!;
        ((bool)cache.GetType().GetMethod("TryAdd")!.Invoke(cache, [eventTypeName, ambiguous])!).ShouldBeTrue();
        try
        {
            AssertCheckpointOnly(Event(eventTypeName, payload, "json-redacted"));
        }
        finally
        {
            MethodInfo remove = cache.GetType().GetMethods().Single(static method =>
                method.Name == "TryRemove" && method.GetParameters().Length == 2);
            _ = remove.Invoke(cache, [eventTypeName, null]);
        }
    }

    /// <summary>Checks ordering, duplicate filtering, checkpoint filtering, and valid-tail continuation.</summary>
    [Fact]
    public void WholePayloadRedactionDeduplicatesAndContinuesWithTheValidTail()
    {
        ProjectionEventDto redacted = Event(nameof(PartyDeactivated), "{}", "json-redacted");
        ProjectionEventDto tail = Event(nameof(PartyDeactivated), "{}", "json") with
        {
            SequenceNumber = 2,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1),
        };
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();

        var folded = PartySdkProjectionFold.DeserializeNew([tail, redacted, redacted, tail], long.MinValue, logger).ToArray();

        folded.Select(static item => item.Event.SequenceNumber).ShouldBe([1L, 2L]);
        folded[0].Payload.ShouldBeNull();
        folded[1].Payload.ShouldBeOfType<PartyDeactivated>();
        folded.ShouldAllBe(static item => item.AdvanceCheckpoint);
        logger.Records.ShouldHaveSingleItem();
        PartySdkProjectionFold.GetDeliveryFailureReason([tail, redacted, redacted, tail], long.MinValue).ShouldBeNull();

        var skippedLogger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        PartySdkProjectionFold.DeserializeNew([redacted, tail], 1, skippedLogger)
            .ShouldHaveSingleItem().Payload.ShouldBeOfType<PartyDeactivated>();
        skippedLogger.Records.ShouldBeEmpty();
        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(
            new ProjectionRequest("tenant-private", "party", "party-private", [tail, redacted, redacted, tail]), current: null);
        processing.Records.Select(static record => record.Outcome).ShouldBe(["Redacted", "Succeeded"]);
        processing.LastSequenceNumber.ShouldBe(2);
    }

    /// <summary>Keeps delivery-level sequence validation authoritative for root-redacted events.</summary>
    [Fact]
    public void WholePayloadRedactionDoesNotBypassSequenceGapsOrConflictingDuplicates()
    {
        ProjectionEventDto redacted = Event("Future.Events.UnknownEvent", "{}", "json-redacted");

        PartySdkProjectionFold.GetDeliveryFailureReason([redacted with { SequenceNumber = 2 }], long.MinValue)
            .ShouldBe(PartySdkProjectionFold.DeliverySequenceGapReason);
        PartySdkProjectionFold.GetDeliveryFailureReason([redacted with { SequenceNumber = 3 }], 1)
            .ShouldBe(PartySdkProjectionFold.DeliverySequenceGapReason);
        PartySdkProjectionFold.GetDeliveryFailureReason([redacted, redacted with { Payload = "null"u8.ToArray() }], long.MinValue)
            .ShouldBe(PartySdkProjectionFold.ConflictingDuplicateEventReason);
    }

    /// <summary>Preserves unresolved behavior for payloads outside the whole-payload redaction contract.</summary>
    [Theory]
    [InlineData("Future.Events.UnknownEvent", "{}", "json")]
    [InlineData("Future.Events.UnknownEvent", "{\"value\":null}", "json-redacted")]
    [InlineData("Future.Events.UnknownEvent", "{ not valid json", "json-redacted")]
    [InlineData(nameof(PartyDeactivated), "null", "json")]
    [InlineData(nameof(PartyDeactivated), "{}", "protobuf")]
    public void OtherUnresolvedPayloadsStillBlockTheCheckpoint(string eventTypeName, string payload, string format)
    {
        ProjectionEventDto @event = Event(eventTypeName, payload, format);

        var result = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue).ShouldHaveSingleItem();

        result.Payload.ShouldBeNull();
        result.AdvanceCheckpoint.ShouldBeFalse();
        PartySdkProjectionFold.GetDeliveryFailureReason([@event], long.MinValue)
            .ShouldBe(PartySdkProjectionFold.UnresolvedOrUnsupportedEventReason);
    }

    /// <summary>Preserves both real empty JSON events and useful fields of partly redacted events.</summary>
    [Fact]
    public void OrdinaryEmptyJsonAndNonemptyFieldRedactionRemainApplicable()
    {
        ProjectionEventDto ordinary = Event(nameof(PartyDeactivated), "{}", "JSON");
        ProjectionEventDto fieldRedacted = Event(nameof(PartyCreated),
            "{\"type\":\"Person\",\"personDetails\":null}",
            "JsOn-ReDaCtEd") with { SequenceNumber = 2 };
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();

        var results = PartySdkProjectionFold.DeserializeNew([ordinary, fieldRedacted], long.MinValue, logger).ToArray();

        results[0].Payload.ShouldBeOfType<PartyDeactivated>();
        PartyCreated created = results[1].Payload.ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Person);
        created.PersonDetails.ShouldBeNull();
        results.ShouldAllBe(static result => result.AdvanceCheckpoint);
        logger.Records.ShouldBeEmpty();
    }

    private static void AssertCheckpointOnly(ProjectionEventDto @event)
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();

        var result = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue, logger).ToArray().ShouldHaveSingleItem();

        result.Event.ShouldBe(@event);
        result.Payload.ShouldBeNull();
        result.AdvanceCheckpoint.ShouldBeTrue();
        PartySdkProjectionFold.HasUnresolvedNewEvent([@event], long.MinValue).ShouldBeFalse();
        PartySdkProjectionFold.GetDeliveryFailureReason([@event], long.MinValue).ShouldBeNull();
        (LogLevel Level, string Message, Exception? Exception) diagnostic = logger.Records.ShouldHaveSingleItem();
        diagnostic.Level.ShouldBe(LogLevel.Information);
        diagnostic.Message.ShouldContain("whole-payload-redacted");
        diagnostic.Message.ShouldNotContain("tenant-private");
        diagnostic.Message.ShouldNotContain("party-private");
        diagnostic.Message.ShouldNotContain("correlation-private");
        diagnostic.Exception.ShouldBeNull();

        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(
            new ProjectionRequest("tenant-private", "party", "party-private", [@event]), current: null);
        processing.Records.ShouldHaveSingleItem().Outcome.ShouldBe("Redacted");
        processing.Records[0].OperationCategory.ShouldBe("Other");
        processing.LastSequenceNumber.ShouldBe(1);
        processing.ProjectedAt.ShouldBe(@event.Timestamp);
    }

    private static ProjectionEventDto Event(string eventTypeName, string payload, string format)
        => new(eventTypeName, Encoding.UTF8.GetBytes(payload), format, 1,
            DateTimeOffset.UnixEpoch, "correlation-private");
}
