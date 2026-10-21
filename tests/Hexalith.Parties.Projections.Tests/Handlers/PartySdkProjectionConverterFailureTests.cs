using System.Reflection;
using System.Text.Json;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Projections.Serialization;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public sealed class PartySdkProjectionConverterFailureTests : IDisposable
{
    private const string PrivatePayload = "SyntheticPrivateConverterValue8472";
    private readonly List<string> _eventNames = [];

    [Theory]
    [InlineData(nameof(FormatException), "json")]
    [InlineData(nameof(OverflowException), "json")]
    [InlineData(nameof(FormatException), "json-redacted")]
    [InlineData(nameof(OverflowException), "json-redacted")]
    public void ConversionFailuresSkipBadPayloadAndContinueWithoutLeaking(string exceptionType, string format)
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();
        ProjectionEventDto bad = FailingEvent(exceptionType, format);
        ProjectionEventDto good = ValidEvent();

        var results = PartySdkProjectionFold.DeserializeNew([bad, bad, good], long.MinValue, logger).ToArray();

        results.Length.ShouldBe(2);
        results[0].Event.ShouldBe(bad);
        results[0].Payload.ShouldBeNull();
        results[0].AdvanceCheckpoint.ShouldBeTrue();
        results[1].Payload.ShouldBeOfType<PartyDeactivated>();
        results[1].AdvanceCheckpoint.ShouldBeTrue();
        (LogLevel Level, string Message, Exception? Exception) diagnostic = logger.Records.ShouldHaveSingleItem();
        diagnostic.Level.ShouldBe(format == "json" ? LogLevel.Warning : LogLevel.Information);
        diagnostic.Message.ShouldContain(exceptionType);
        diagnostic.Message.ShouldContain(format == "json" ? "failed to deserialize live event" : "dropped redacted event");
        diagnostic.Message.ShouldNotContain(PrivatePayload);
        diagnostic.Exception.ShouldBeNull();
        PartySdkProjectionFold.GetDeliveryFailureReason([bad, good], long.MinValue).ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(FormatException), "json", "Failed")]
    [InlineData(nameof(OverflowException), "json", "Failed")]
    [InlineData(nameof(FormatException), "json-redacted", "Redacted")]
    [InlineData(nameof(OverflowException), "json-redacted", "Redacted")]
    public void ConversionFailureProducesTheCorrectProcessingActivity(string exceptionType, string format, string outcome)
    {
        ProjectionRequest request = new("synthetic-tenant", "party", "synthetic-party",
            [FailingEvent(exceptionType, format), ValidEvent()]);

        PartyProcessingSdkReadModel processing = PartyProcessingActivityFold.Fold(request, current: null);

        processing.Records.Select(static record => record.Outcome).ShouldBe([outcome, "Succeeded"]);
        processing.Records.Select(static record => record.SequenceNumber).ShouldBe([1L, 2L]);
        processing.LastSequenceNumber.ShouldBe(2);
    }

    [Fact]
    public void AlreadyCheckpointedConversionFailureIsNotDeserializedOrLogged()
    {
        var logger = new RecordingLogger<PartyDetailSdkProjectionHandler>();

        var results = PartySdkProjectionFold.DeserializeNew(
            [FailingEvent(nameof(FormatException), "json"), ValidEvent()], 1, logger).ToArray();

        results.ShouldHaveSingleItem().Payload.ShouldBeOfType<PartyDeactivated>();
        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public void UnrelatedConverterFailuresStillPropagate()
    {
        Should.Throw<IOException>(() => PartySdkProjectionFold.DeserializeNew(
            [FailingEvent(nameof(IOException), "json")], long.MinValue).ToArray());
        Should.Throw<OperationCanceledException>(() => PartySdkProjectionFold.DeserializeNew(
            [FailingEvent(nameof(OperationCanceledException), "json")], long.MinValue).ToArray());
    }

    /// <summary>Removes only this test instance's synthetic resolver registrations, including after a failed assertion.</summary>
    public void Dispose()
    {
        if (_eventNames.Count == 0)
        {
            return;
        }

        object cache = typeof(PartyEventTypeResolver)
            .GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        MethodInfo remove = cache.GetType().GetMethods().Single(static method =>
            method.Name == "TryRemove" && method.GetParameters().Length == 2);
        foreach (string name in _eventNames)
        {
            _ = remove.Invoke(cache, [name, null]);
        }

        _eventNames.Clear();
    }

    /// <summary>Registers an isolated real-converter event until this fixture is disposed.</summary>
    internal ProjectionEventDto FailingEvent(string exceptionType, string format)
    {
        // Like the existing ambiguity tests, fault-inject only a unique resolver-cache entry.
        // The runtime allowlist and the shared read-only JSON options stay untouched.
        string eventName = "SyntheticConverterEvent" + Guid.NewGuid().ToString("N");
        Type resolver = typeof(PartyEventTypeResolver);
        object cache = resolver.GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Type outcomeType = typeof(ResolveOutcome);
        object outcome = Activator.CreateInstance(outcomeType, [typeof(ProjectionConverterFailureEvent), false])!;
        ((bool)cache.GetType().GetMethod("TryAdd")!.Invoke(cache, [eventName, outcome])!).ShouldBeTrue();
        _eventNames.Add(eventName);
        return new ProjectionEventDto(eventName,
            JsonSerializer.SerializeToUtf8Bytes(exceptionType + ":" + PrivatePayload), format,
            1, DateTimeOffset.UnixEpoch, "synthetic-correlation");
    }

    private static ProjectionEventDto ValidEvent()
        => new(nameof(PartyDeactivated),
            JsonSerializer.SerializeToUtf8Bytes(new PartyDeactivated(), PartiesJsonOptions.Default),
            "json", 2, DateTimeOffset.UnixEpoch.AddSeconds(1), "synthetic-correlation");
}
