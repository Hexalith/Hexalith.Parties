using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Protects the SDK projection protocol from Parties payload enum conventions.</summary>
public sealed class PartyProjectionProtocolSerializationTests
{
    /// <summary>The real host returns numeric SDK statuses and retains string Party payload enums.</summary>
    /// <param name="status">The successful dispatch outcome returned by the SDK endpoint.</param>
    /// <param name="wireValue">The numeric protocol value understood by the Gateway.</param>
    [Theory]
    [InlineData(ProjectionDispatchStatus.Completed, 0)]
    [InlineData(ProjectionDispatchStatus.AlreadyCompleted, 1)]
    public async Task ActualHostResultsOkPreservesProjectionProtocolAndPartyPayloadEnumsAsync(
        ProjectionDispatchStatus status, int wireValue)
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateClient();
        HttpJsonOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value;
        options.SerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        var response = new ProjectionDispatchResponse(1,
            [new ProjectionDispatchOutcome("party-detail", status, null, null)]);

        // The published SDK endpoint uses Results.Ok; exercise its real host services
        // without registering test-specific JSON options or projection handlers.
        using JsonDocument protocol = await ExecuteOkAsync(factory.Services, response);
        JsonElement outcome = protocol.RootElement.GetProperty("outcomes")[0];
        outcome.GetProperty("status").ValueKind.ShouldBe(JsonValueKind.Number);
        outcome.GetProperty("status").GetInt32().ShouldBe(wireValue);
        ProjectionDispatchResponse decoded = protocol.RootElement.Deserialize<ProjectionDispatchResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        decoded.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(status);

        using JsonDocument party = await ExecuteOkAsync(factory.Services, new CreateParty
        {
            PartyId = "party-protocol-test",
            Type = PartyType.Person,
        });
        party.RootElement.GetProperty("type").GetString().ShouldBe("Person");
        party.RootElement.GetProperty("partyId").GetString().ShouldBe("party-protocol-test");
        party.RootElement.TryGetProperty("personDetails", out _).ShouldBeFalse();
        JsonSerializer.Serialize(PartyType.Person, PartiesJsonOptions.Default).ShouldBe("\"Person\"");
    }

    private static async Task<JsonDocument> ExecuteOkAsync<T>(IServiceProvider services, T value)
    {
        using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            RequestAborted = TestContext.Current.CancellationToken,
        };
        context.Response.Body = body;

        await Results.Ok(value).ExecuteAsync(context).ConfigureAwait(false);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.ContentType.ShouldBe("application/json; charset=utf-8");
        body.Position = 0;
        return await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
