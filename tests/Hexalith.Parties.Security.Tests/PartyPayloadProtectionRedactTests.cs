using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Security;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

/// <summary>
/// Direct tests for the static <see cref="PartyPayloadProtectionService.RedactProtectedPayload"/>
/// helper and its <c>RebuildWithoutEncryptedMarkers</c> internals. These cover the post-erasure
/// tail-replay path, where lifecycle events must continue to flow even though the encryption
/// key has been destroyed; the redaction must be tolerant of corrupted payloads, deeply-nested
/// structures, and root-level encrypted markers.
/// </summary>
public sealed class PartyPayloadProtectionRedactTests
{
    private const string ProtectedFormat = "json+pdenc-v1";
    private const string RedactedFormat = "json-redacted";

    [Fact]
    public void RedactProtectedPayload_NonProtectedFormat_PassesThroughUnchanged()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""{"firstName":"Ada"}""");

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, "json");

        result.PayloadBytes.ShouldBeSameAs(payload);
        result.SerializationFormat.ShouldBe("json");
    }

    [Fact]
    public void RedactProtectedPayload_EmptyPayload_PassesThroughWithoutThrow()
    {
        // ArgumentNullException.ThrowIfNull does not reject empty arrays; JsonNode.Parse on
        // length-zero throws JsonException. The redaction path must tolerate this without
        // aborting projection delivery.
        byte[] empty = [];

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(empty, ProtectedFormat);

        result.PayloadBytes.Length.ShouldBe(0);
        result.SerializationFormat.ShouldBe(ProtectedFormat);
    }

    [Fact]
    public void RedactProtectedPayload_FlatEncryptedField_ReplacesWithJsonNull()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "firstName": {"$enc":true,"alg":"AES256GCM","kv":1,"n":"AAAA","t":"BBBB","c":"CCCC"},
              "type": "person"
            }
            """);

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        doc.RootElement.GetProperty("firstName").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("person");
    }

    [Fact]
    public void RedactProtectedPayload_NestedEncryptedField_ReplacesOnlyMarkerNotParent()
    {
        // A non-marker JsonObject containing a marker JsonObject must be rebuilt — not the
        // parent reassignment hazard described in the patch outcome at line 145 of the story.
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "personDetails": {
                "firstName": {"$enc":true,"alg":"AES256GCM","kv":1,"n":"x","t":"y","c":"z"},
                "lastName": {"$enc":true,"alg":"AES256GCM","kv":1,"n":"x","t":"y","c":"z"}
              },
              "isActive": true
            }
            """);

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        JsonElement person = doc.RootElement.GetProperty("personDetails");
        person.GetProperty("firstName").ValueKind.ShouldBe(JsonValueKind.Null);
        person.GetProperty("lastName").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void RedactProtectedPayload_RootLevelEncryptedMarker_ReturnsEmptyObjectNotJsonNull()
    {
        // Whole-payload $enc would previously collapse to JSON literal "null" bytes which then
        // deserialized to a null event and silently dropped the lifecycle event. The fix
        // substitutes an empty object so the event deserializes to a default-valued instance.
        byte[] payload = Encoding.UTF8.GetBytes(
            """{"$enc":true,"alg":"AES256GCM","kv":1,"n":"x","t":"y","c":"z"}""");

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        doc.RootElement.EnumerateObject().Count().ShouldBe(0);
    }

    [Fact]
    public void RedactProtectedPayload_ArrayWithEncryptedItems_ReplacesItemsNotArray()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "channels": [
                {"id":"c1","value":{"$enc":true,"alg":"x","kv":1,"n":"x","t":"y","c":"z"}},
                {"id":"c2","value":"clear"}
              ]
            }
            """);

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        JsonElement channels = doc.RootElement.GetProperty("channels");
        channels.GetArrayLength().ShouldBe(2);
        channels[0].GetProperty("id").GetString().ShouldBe("c1");
        channels[0].GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
        channels[1].GetProperty("value").GetString().ShouldBe("clear");
    }

    [Fact]
    public void RedactProtectedPayload_DeeplyNestedStructure_DoesNotStackOverflow()
    {
        // Build a 50-level deep structure with an encrypted marker at the leaf. The previous
        // RebuildWithoutEncryptedMarkers DeepClone-per-recursion implementation was O(N²) in
        // tree depth; the patch removed the up-front clone so this test covers the regression.
        StringBuilder sb = new();
        for (int i = 0; i < 50; i++)
        {
            sb.Append("{\"nested\":");
        }

        sb.Append("""{"$enc":true,"alg":"x","kv":1,"n":"x","t":"y","c":"z"}""");
        for (int i = 0; i < 50; i++)
        {
            sb.Append('}');
        }

        byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        // Walk to the leaf and assert it is null.
        JsonElement node = doc.RootElement;
        for (int i = 0; i < 50; i++)
        {
            node = node.GetProperty("nested");
        }

        node.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("\"true\"")]   // string instead of bool
    [InlineData("1")]          // number instead of bool
    [InlineData("null")]       // null instead of bool
    [InlineData("{}")]         // object instead of bool
    public void RedactProtectedPayload_CorruptedEncMarker_DoesNotThrow(string corruptedEncValue)
    {
        // IsEncryptedMarker.GetValue<bool>() previously threw InvalidOperationException on a
        // type mismatch, escaping the redaction path and aborting projection delivery on a
        // single corrupted event. The fix uses TryGetValue<bool> which returns false for
        // non-bool values so the marker is treated as not-encrypted (the field is preserved).
        byte[] payload = Encoding.UTF8.GetBytes(
            $$"""
            {
              "field": {"$enc":{{corruptedEncValue}},"alg":"x","kv":1}
            }
            """);

        Should.NotThrow(() => PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat));
    }

    [Fact]
    public void RedactProtectedPayload_EncMarkerIsBoolFalse_TreatsAsNotEncrypted()
    {
        // $enc:false explicitly says "not encrypted" — the field stays in the rebuilt tree.
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "field": {"$enc":false,"alg":"x","kv":1,"value":"clear"}
            }
            """);

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        using JsonDocument doc = JsonDocument.Parse(result.PayloadBytes);
        // Field not collapsed to null — the rebuilt tree retains the object since it is not a
        // "true" encrypted marker.
        doc.RootElement.GetProperty("field").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void RedactProtectedPayload_RoundTripsAsJsonRedactedFormat()
    {
        // The format flag is the audit/compliance signal: downstream pipelines must know that
        // this payload was redacted (not just plain JSON that was never encrypted). The
        // UnprotectEventPayloadAsync path short-circuits on this format to prevent
        // re-encryption of nulled-out leaves.
        byte[] payload = Encoding.UTF8.GetBytes("""{"firstName":{"$enc":true,"alg":"x","kv":1,"n":"a","t":"b","c":"c"}}""");

        PayloadProtectionResult result = PartyPayloadProtectionService.RedactProtectedPayload(payload, ProtectedFormat);

        result.SerializationFormat.ShouldBe(RedactedFormat);
        result.SerializationFormat.ShouldNotBe("json");
        result.SerializationFormat.ShouldNotBe(ProtectedFormat);
    }
}
