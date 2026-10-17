using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>A synthetic event whose converter exercises real deserializer failure boundaries.</summary>
[JsonConverter(typeof(ProjectionConverterFailureEventConverter))]
public sealed record ProjectionConverterFailureEvent : IEventPayload;
