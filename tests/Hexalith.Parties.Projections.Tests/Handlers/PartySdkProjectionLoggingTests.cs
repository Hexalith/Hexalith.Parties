using System.Reflection;
using System.Text;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Verifies projection-fold diagnostic compatibility, filtering, and payload privacy.</summary>
public sealed class PartySdkProjectionLoggingTests : IDisposable
{
    private const string PrivatePayload = "SyntheticPrivateProjectionPayload9381";
    private const string PrivateCorrelation = "synthetic-private-correlation";
    private readonly List<string> _ambiguousEventNames = [];

    /// <summary>Preserves each diagnostic's metadata and rendered text while honoring disabled logging.</summary>
    /// <param name="eventId">The approved stable numeric event identifier.</param>
    /// <param name="eventName">The generated logging method name used as the event name.</param>
    /// <param name="level">The established diagnostic severity.</param>
    /// <param name="template">The exact original message template.</param>
    /// <param name="advanceCheckpoint">Whether the dropped event advances the checkpoint.</param>
    [Theory]
    [InlineData(4802, "NonJsonEventDropped", LogLevel.Warning,
        "Party projection received event {EventTypeName} with non-JSON serialization format '{SerializationFormat}'. Event dropped without advancing the checkpoint.", false)]
    [InlineData(4803, "UnknownEventTypeDropped", LogLevel.Warning,
        "Party projection could not resolve event type '{EventTypeName}'. Event dropped without advancing the checkpoint.", false)]
    [InlineData(4804, "AmbiguousEventTypeDropped", LogLevel.Warning,
        "Party projection found an ambiguous short event-type name '{EventTypeName}' (multiple types share this short name). Event dropped without advancing the checkpoint.", false)]
    [InlineData(4805, "PayloadDeserializationFailed", LogLevel.Warning,
        "Party projection failed to deserialize live event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType}. Event dropped and checkpoint advanced past it.", true)]
    [InlineData(4806, "RedactedEventDropped", LogLevel.Information,
        "Party projection dropped redacted event {EventTypeName} at sequence {SequenceNumber} with {ExceptionType} (expected post-erasure deserialization failure). Checkpoint advanced.", true)]
    [InlineData(4807, "WholePayloadRedactedEventDropped", LogLevel.Information,
        "Party projection dropped whole-payload-redacted event {EventTypeName} at sequence {SequenceNumber} (no deserialization error). Checkpoint advanced.", true)]
    [InlineData(4808, "NullPayloadEventDropped", LogLevel.Warning,
        "Party projection resolved and deserialized event {EventTypeName} at sequence {SequenceNumber} without error, but it produced no payload. Event dropped without advancing the checkpoint.", false)]
    public void DeserializeNewPreservesDiagnosticContractAndHonorsFiltering(
        int eventId, string eventName, LogLevel level, string template, bool advanceCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(template);
        ProjectionEventDto @event = CreateEvent(eventId);
        ILogger<PartyDetailSdkProjectionHandler> logger = CreateLogger(enabled: true);
        ILogger<PartyDetailSdkProjectionHandler> disabledLogger = CreateLogger(enabled: false);

        var results = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue, logger).ToArray();
        var disabledResults = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue, disabledLogger).ToArray();
        var silentResults = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue).ToArray();

        var result = results.ShouldHaveSingleItem();
        result.Event.ShouldBe(@event);
        result.Payload.ShouldBeNull();
        result.AdvanceCheckpoint.ShouldBe(advanceCheckpoint);
        disabledResults.ShouldBe(results);
        silentResults.ShouldBe(results);
        disabledLogger.ReceivedCalls().Where(static call => call.GetMethodInfo().Name == "Log").ShouldBeEmpty();

        object?[] args = logger.ReceivedCalls().Where(static call => call.GetMethodInfo().Name == "Log")
            .ShouldHaveSingleItem().GetArguments();
        args[0].ShouldBe(level);
        EventId actualEventId = args[1].ShouldBeOfType<EventId>();
        actualEventId.Id.ShouldBe(eventId);
        actualEventId.Name.ShouldBe(eventName);
        args[3].ShouldBeNull();
        Dictionary<string, object?> state = args[2].ShouldBeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>().ShouldNotBeNull()
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        var expectedFields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventTypeName"] = @event.EventTypeName,
            ["{OriginalFormat}"] = template,
        };
        if (eventId == 4802)
        {
            expectedFields["SerializationFormat"] = "protobuf";
        }

        if (eventId >= 4805)
        {
            expectedFields["SequenceNumber"] = 42L;
        }

        if (eventId is 4805 or 4806)
        {
            expectedFields["ExceptionType"] = "JsonException";
        }

        state.Keys.Order(StringComparer.Ordinal).ShouldBe(expectedFields.Keys.Order(StringComparer.Ordinal));
        foreach (KeyValuePair<string, object?> field in expectedFields)
        {
            state[field.Key].ShouldBe(field.Value);
        }

        string rendered = ((Delegate)args[4]!).DynamicInvoke(args[2], args[3]).ShouldBeOfType<string>();
        string expectedMessage = template
            .Replace("{EventTypeName}", @event.EventTypeName, StringComparison.Ordinal)
            .Replace("{SerializationFormat}", "protobuf", StringComparison.Ordinal)
            .Replace("{SequenceNumber}", "42", StringComparison.Ordinal)
            .Replace("{ExceptionType}", "JsonException", StringComparison.Ordinal);
        rendered.ShouldBe(expectedMessage);
        rendered.ShouldNotContain(PrivatePayload);
        rendered.ShouldNotContain(PrivateCorrelation);
    }

    /// <summary>Logs converter failure types and event metadata without private exception messages.</summary>
    /// <param name="exceptionType">The exception type thrown by the payload converter.</param>
    /// <param name="format">The live or redacted serialization format.</param>
    /// <param name="eventId">The approved stable numeric event identifier.</param>
    /// <param name="eventName">The generated logging method name used as the event name.</param>
    /// <param name="level">The established diagnostic severity.</param>
    [Theory]
    [InlineData("JsonException", "json", 4805, "PayloadDeserializationFailed", LogLevel.Warning)]
    [InlineData("JsonException", "json-redacted", 4806, "RedactedEventDropped", LogLevel.Information)]
    [InlineData("FormatException", "json", 4805, "PayloadDeserializationFailed", LogLevel.Warning)]
    [InlineData("OverflowException", "json", 4805, "PayloadDeserializationFailed", LogLevel.Warning)]
    [InlineData("FormatException", "json-redacted", 4806, "RedactedEventDropped", LogLevel.Information)]
    [InlineData("OverflowException", "json-redacted", 4806, "RedactedEventDropped", LogLevel.Information)]
    public void DeserializeNewLogsOnlySafeConverterFailureFields(
        string exceptionType, string format, int eventId, string eventName, LogLevel level)
    {
        using var failures = new PartySdkProjectionConverterFailureTests();
        ProjectionEventDto @event = failures.FailingEvent(exceptionType, format);
        ILogger<PartyDetailSdkProjectionHandler> logger = CreateLogger(enabled: true);

        var results = PartySdkProjectionFold.DeserializeNew([@event], long.MinValue, logger).ToArray();

        var result = results.ShouldHaveSingleItem();
        result.Payload.ShouldBeNull();
        result.AdvanceCheckpoint.ShouldBeTrue();
        object?[] args = logger.ReceivedCalls().Where(static call => call.GetMethodInfo().Name == "Log")
            .ShouldHaveSingleItem().GetArguments();
        args[0].ShouldBe(level);
        EventId actualEventId = args[1].ShouldBeOfType<EventId>();
        actualEventId.Id.ShouldBe(eventId);
        actualEventId.Name.ShouldBe(eventName);
        args[3].ShouldBeNull();
        Dictionary<string, object?> state = args[2].ShouldBeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>().ShouldNotBeNull()
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        state.Keys.Order(StringComparer.Ordinal).ShouldBe(new[]
        {
            "EventTypeName", "SequenceNumber", "ExceptionType", "{OriginalFormat}",
        }.Order(StringComparer.Ordinal));
        state["EventTypeName"].ShouldBe(@event.EventTypeName);
        state["SequenceNumber"].ShouldBe(1L);
        state["ExceptionType"].ShouldBe(exceptionType);
        string privateValue = Encoding.UTF8.GetString(@event.Payload).Split(':')[1].Trim('"');
        foreach (object? value in state.Values)
        {
            if (value is string text)
            {
                text.ShouldNotContain(privateValue);
            }
        }

        string rendered = ((Delegate)args[4]!).DynamicInvoke(args[2], args[3]).ShouldBeOfType<string>();
        string expectedMessage = format == "json"
            ? $"Party projection failed to deserialize live event {@event.EventTypeName} at sequence 1 with {exceptionType}. Event dropped and checkpoint advanced past it."
            : $"Party projection dropped redacted event {@event.EventTypeName} at sequence 1 with {exceptionType} (expected post-erasure deserialization failure). Checkpoint advanced.";
        rendered.ShouldBe(expectedMessage);
        rendered.ShouldNotContain(privateValue);
    }

    /// <summary>Removes this test instance's isolated ambiguity registrations.</summary>
    public void Dispose()
    {
        if (_ambiguousEventNames.Count == 0)
        {
            return;
        }

        object cache = typeof(PartyEventTypeResolver)
            .GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        MethodInfo remove = cache.GetType().GetMethods().Single(static method =>
            method.Name == "TryRemove" && method.GetParameters().Length == 2);
        foreach (string eventName in _ambiguousEventNames)
        {
            _ = remove.Invoke(cache, [eventName, null]);
        }

        _ambiguousEventNames.Clear();
    }

    private static ILogger<PartyDetailSdkProjectionHandler> CreateLogger(bool enabled)
    {
        ILogger<PartyDetailSdkProjectionHandler> logger = Substitute.For<ILogger<PartyDetailSdkProjectionHandler>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(enabled);
        logger.ClearReceivedCalls();
        return logger;
    }

    private ProjectionEventDto CreateEvent(int eventId)
    {
        string eventTypeName = eventId switch
        {
            4803 => "SyntheticUnknownProjectionLoggingEvent",
            4804 => RegisterAmbiguousEvent(),
            _ => nameof(PartyCreated),
        };
        string format = eventId switch
        {
            4802 => "protobuf",
            4806 or 4807 => "json-redacted",
            _ => "json",
        };
        string payload = eventId switch
        {
            4805 or 4806 => "{\"Private\":\"" + PrivatePayload + "\",",
            4807 or 4808 => "null",
            _ => "{}",
        };
        return new ProjectionEventDto(eventTypeName, Encoding.UTF8.GetBytes(payload), format,
            42, DateTimeOffset.UnixEpoch, PrivateCorrelation);
    }

    private string RegisterAmbiguousEvent()
    {
        // The Contracts allowlist has no real short-name collision. As in the existing fold
        // tests, inject a unique cache entry without changing that allowlist or JSON options.
        string eventName = "SyntheticAmbiguousProjectionLoggingEvent" + Guid.NewGuid().ToString("N");
        Type resolver = typeof(PartyEventTypeResolver);
        object cache = resolver.GetField("s_resolvedCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Type outcomeType = resolver.GetNestedType("ResolveOutcome", BindingFlags.NonPublic)!;
        object outcome = outcomeType.GetProperty("Ambiguous", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        ((bool)cache.GetType().GetMethod("TryAdd")!.Invoke(cache, [eventName, outcome])!).ShouldBeTrue();
        _ambiguousEventNames.Add(eventName);
        return eventName;
    }
}
