using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>Throws the requested failure with the synthetic private payload as its message.</summary>
public sealed class ProjectionConverterFailureEventConverter : JsonConverter<ProjectionConverterFailureEvent>
{
    /// <inheritdoc/>
    public override ProjectionConverterFailureEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string message = reader.GetString()!;
        string kind = message.Split(':', 2)[0];
        throw kind switch
        {
            nameof(FormatException) => new FormatException(message),
            nameof(OverflowException) => new OverflowException(message),
            nameof(IOException) => new IOException(message),
            nameof(OperationCanceledException) => new OperationCanceledException(message),
            _ => new JsonException(message),
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ProjectionConverterFailureEvent value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
