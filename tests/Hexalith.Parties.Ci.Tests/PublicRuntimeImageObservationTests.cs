namespace Hexalith.Parties.Ci.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Verifies actual owned image measurement with byte fixtures and no real Docker, environment or network operations.</summary>
public sealed class PublicRuntimeImageObservationTests
{
    /// <summary>Measures every requested actual fake file and rechecks the same live container after all reads.</summary>
    /// <param name="timestamp">The real Docker timestamp precision to exercise with controlled bytes.</param>
    [Theory]
    [InlineData("valid")]
    [InlineData("nanoseconds")]
    public async Task BoundedReaderMeasuresTheFiveActualFileBodiesAsync(string timestamp)
    {
        PublicRuntimeImageFixture fixture = new(timestamp);
        JsonElement actual = await PublicRuntimeImageObservations.ObserveImageAsync(PublicRuntimeImageFixture.RunId,
            fixture.ReadAsync, TestContext.Current.CancellationToken);
        actual.GetProperty("image").GetProperty("imageReference").GetString().ShouldBe(PublicRuntimeImageObservations.ImageReference);
        JsonElement bodies = actual.GetProperty("bodies");
        bodies.EnumerateObject().Count().ShouldBe(5);
        foreach (JsonProperty body in bodies.EnumerateObject())
        {
            string name = body.Name + ".dll";
            byte[] measured = PublicRuntimeImageFixture.Body(name);
            body.Value.GetProperty("sha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(measured)));
            body.Value.GetProperty("bytes").GetInt32().ShouldBe(measured.Length);
            body.Value.GetProperty("path").GetString().ShouldBe("/app/" + name);
            body.Value.GetProperty("role").GetString().ShouldBe("running-public-image-file");
        }
        fixture.Commands.Count(command => command[0] == "inspect").ShouldBe(2);
        fixture.Commands.Count(command => command[0] == "cp").ShouldBe(5);
        fixture.Commands.ShouldAllBe(command => command[0] == "ps" || command[0] == "inspect" || command[0] == "cp");
    }

    /// <summary>Foreign ownership, unknown metadata, mutable images and unsafe archive bodies cannot yield an observation.</summary>
    /// <param name="mutation">The actual protocol boundary to violate.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate-inventory")]
    [InlineData("malformed-id")]
    [InlineData("ambiguous")]
    [InlineData("foreign-id")]
    [InlineData("foreign-owner")]
    [InlineData("tag-only")]
    [InlineData("foreign-image")]
    [InlineData("bad-content-id")]
    [InlineData("stopped")]
    [InlineData("unknown-field")]
    [InlineData("changed-image")]
    [InlineData("changed-start")]
    [InlineData("linked-file")]
    [InlineData("unsafe-path")]
    [InlineData("empty-file")]
    [InlineData("extra-file")]
    [InlineData("oversized")]
    public async Task UnverifiedRunningImageOrFileRefusesAsync(string mutation)
    {
        PublicRuntimeImageFixture fixture = new(mutation);
        await Should.ThrowAsync<InvalidDataException>(() => PublicRuntimeImageObservations.ObserveImageAsync(
            PublicRuntimeImageFixture.RunId, fixture.ReadAsync, TestContext.Current.CancellationToken));
        if (mutation is "foreign-id" or "foreign-owner" or "tag-only" or "stopped" or "unknown-field")
            fixture.Commands.ShouldNotContain(command => command[0] == "cp");
    }

    /// <summary>Changed actual file bytes produce changed measured identities; an expected static digest is never returned instead.</summary>
    [Fact]
    public async Task TamperedFileBytesRemainMeasuredInsteadOfBeingReplacedByExpectedHashesAsync()
    {
        PublicRuntimeImageFixture fixture = new("changed-bytes");
        JsonElement actual = await PublicRuntimeImageObservations.ObserveImageAsync(PublicRuntimeImageFixture.RunId,
            fixture.ReadAsync, TestContext.Current.CancellationToken);
        actual.GetProperty("bodies").GetProperty("Hexalith.Parties.Contracts").GetProperty("sha256").GetString()
            .ShouldNotBe(Convert.ToHexStringLower(SHA256.HashData(PublicRuntimeImageFixture.Body("Hexalith.Parties.Contracts.dll"))));
    }

    /// <summary>Cancellation prevents any real or fake client operation instead of extending the inherited functional deadline.</summary>
    [Fact]
    public async Task CancelledObservationDoesNotStartAClientAsync()
    {
        using CancellationTokenSource bound = new();
        await bound.CancelAsync();
        PublicRuntimeImageFixture fixture = new();
        await Should.ThrowAsync<OperationCanceledException>(() => PublicRuntimeImageObservations.ObserveImageAsync(
            PublicRuntimeImageFixture.RunId, fixture.ReadAsync, bound.Token));
        fixture.Commands.ShouldBeEmpty();
    }
}
