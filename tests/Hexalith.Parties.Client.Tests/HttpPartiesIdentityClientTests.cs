using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.ValueObjects;
using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class HttpPartiesIdentityClientTests
{
    /// <summary>Invalid successful HTTP bodies cannot acknowledge any binding operation.</summary>
    public static TheoryData<string, string> InvalidCommandAcknowledgments
    {
        get
        {
            string[] replies =
            [
                "", "{bad", "null", "[]", "true", "17", "\"unrelated\"", "{}",
                """{"unrelated":true}""",
                """{"success":true}""",
                """{"messageId":"$attempt"}""",
                """{"correlationId":null}""",
                """{"correlationId":""}""",
                """{"correlationId":"   "}""",
                """{"correlationId":"another-attempt"}""",
                """{"correlationId":17}""",
                """{"correlationId":true}""",
                """{"correlationId":[]}""",
                """{"correlationId":{}}""",
                """{"correlationId":"$attempt","messageId":""}""",
                """{"correlationId":"$attempt","messageId":"   "}""",
                """{"correlationId":"$attempt","messageId":"another-attempt"}""",
                """{"correlationId":"$attempt","messageId":17}""",
                """{"correlationId":"$attempt","success":false}""",
                """{"correlationId":"$attempt","rejected":true}""",
                """{"correlationId":"$attempt","Success":false}""",
                """{"correlationId":"$attempt","Rejected":true}""",
                """{"correlationId":"$attempt","success":"true"}""",
                """{"correlationId":"$attempt","success":null}""",
                """{"correlationId":"$attempt","rejected":"false"}""",
                """{"correlationId":"$attempt","rejected":null}""",
                """{"correlationId":"$attempt","success":true,"rejected":true}""",
                """{"correlationId":"another-attempt","correlationId":"$attempt"}""",
                """{"correlationId":"another-attempt","CorrelationId":"$attempt"}""",
                """{"correlationId":"$attempt","messageId":"another-attempt","MessageId":null}""",
                """{"correlationId":"$attempt","messageId":"another-attempt","messageId":"$attempt"}""",
            ];
            var data = new TheoryData<string, string>();
            foreach (string operation in new[] { "establish", "rebind", "revoke" })
            {
                foreach (string reply in replies)
                {
                    data.Add(operation, reply);
                }
            }

            return data;
        }
    }

    /// <summary>Successful transport alone does not prove the submitted binding attempt was accepted.</summary>
    [Theory]
    [MemberData(nameof(InvalidCommandAcknowledgments))]
    public async Task BindingCommand_InvalidAcknowledgment_IsUnavailable(string operation, string reply)
    {
        int sent = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            sent++;
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(reply.Replace("$attempt", command.CorrelationId, StringComparison.Ordinal), Encoding.UTF8, "application/json"),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };

        PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(
            () => SubmitBindingAsync(new HttpPartiesIdentityClient(http), operation));

        failure.Status.ShouldBe(503);
        failure.Detail.ShouldBe("Authoritative identity evidence is unavailable.");
        sent.ShouldBe(1);
    }

    /// <summary>Binding acknowledgments preserve optional fields and the gateway's accepted status.</summary>
    [Theory]
    [InlineData("establish", false)]
    [InlineData("rebind", false)]
    [InlineData("revoke", false)]
    [InlineData("establish", true)]
    [InlineData("rebind", true)]
    [InlineData("revoke", true)]
    public async Task BindingCommand_ValidAcknowledgments_PreserveCompatibility(string operation, bool includeMessageId)
    {
        var seen = new List<SubmitCommandRequest>();
        using var http = new HttpClient(new Handler(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/commands");
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            seen.Add(command);
            command.MessageId.ShouldBe(command.CorrelationId);
            command.MessageId.ShouldNotBeNullOrWhiteSpace();
            command.Tenant.ShouldBe("tenant-a");
            command.Domain.ShouldBe("party");
            command.AggregateId.ShouldBe("party-1");
            command.CommandType.ShouldBe(operation switch
            {
                "establish" => typeof(EstablishHumanActorBinding).FullName,
                "rebind" => typeof(RebindHumanActorBinding).FullName,
                _ => typeof(RevokeHumanActorBinding).FullName,
            });
            command.Payload.GetProperty("logicalId").GetString().ShouldBe("logical");
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new SubmitCommandResponse(command.CorrelationId!,
                    ResultPayload: includeMessageId ? JsonSerializer.SerializeToElement(new { bindingVersion = 1 }, PartiesJsonOptions.Default) : null,
                    MessageId: includeMessageId ? command.MessageId : null), options: PartiesJsonOptions.Default),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };
        var client = new HttpPartiesIdentityClient(http);

        await SubmitBindingAsync(client, operation);
        await SubmitBindingAsync(client, operation);

        seen.Count.ShouldBe(2);
        seen[0].MessageId.ShouldNotBe(seen[1].MessageId);
        seen[0].CorrelationId.ShouldNotBe(seen[1].CorrelationId);
        seen[0].Payload.GetRawText().ShouldBe(seen[1].Payload.GetRawText());
    }

    /// <summary>Valid alternate envelope casing and explicit non-rejection flags remain supported.</summary>
    [Theory]
    [InlineData("""{"CorrelationId":"$attempt","MessageId":"$attempt","Success":true,"Rejected":false}""")]
    [InlineData("""{"correlationId":"$attempt","messageId":null,"success":true,"rejected":false,"extension":"ignored"}""")]
    public async Task BindingCommand_CompatibleEnvelope_IsAccepted(string reply)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply.Replace("$attempt", command.CorrelationId, StringComparison.Ordinal), Encoding.UTF8, "application/json"),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };

        await SubmitBindingAsync(new HttpPartiesIdentityClient(http), "establish");
    }

    /// <summary>The shared acknowledgment guard preserves validated provisioning results.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Provisioning_RequiresAcknowledgmentAndMatchingResult(bool matchingResult)
    {
        const string agentId = "01HX0000000000000000000001";
        var identity = new AgentPartyIdentity("tenant-a", agentId, AgentPartyIdMapping.Create("tenant-a", agentId),
            1, "logical", "fingerprint", DateTimeOffset.UnixEpoch);
        var result = new AgentPartyProvisioningResult(
            matchingResult ? identity : identity with { LogicalId = "other-logical" }, 1, "source");
        using var http = new HttpClient(new Handler(async request =>
        {
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new SubmitCommandResponse(command.CorrelationId!,
                    JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default), command.MessageId), options: PartiesJsonOptions.Default),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };
        var client = new HttpPartiesIdentityClient(http);

        if (matchingResult)
        {
            (await client.ProvisionAgentPartyAsync("tenant-a", new(identity), TestContext.Current.CancellationToken)).ShouldBe(result);
        }
        else
        {
            PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => client.ProvisionAgentPartyAsync(
                "tenant-a", new(identity), TestContext.Current.CancellationToken));
            failure.Status.ShouldBe(503);
        }
    }

    /// <summary>An earlier delivery acknowledgment cannot satisfy a fresh retry.</summary>
    [Theory]
    [InlineData("establish")]
    [InlineData("rebind")]
    [InlineData("revoke")]
    public async Task BindingCommand_RetryWithPreviousAcknowledgment_IsUnavailable(string operation)
    {
        var seen = new List<SubmitCommandRequest>();
        using var http = new HttpClient(new Handler(async request =>
        {
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            seen.Add(command);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new SubmitCommandResponse(seen[0].CorrelationId!, MessageId: seen[0].MessageId), options: PartiesJsonOptions.Default),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };
        var client = new HttpPartiesIdentityClient(http);

        await SubmitBindingAsync(client, operation);
        PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => SubmitBindingAsync(client, operation));

        failure.Status.ShouldBe(503);
        seen.Count.ShouldBe(2);
        seen[0].MessageId.ShouldNotBe(seen[1].MessageId);
        seen[0].Payload.GetRawText().ShouldBe(seen[1].Payload.GetRawText());
    }

    /// <summary>A well-formed acknowledgment cannot override an HTTP rejection.</summary>
    [Theory]
    [InlineData("establish", HttpStatusCode.Unauthorized)]
    [InlineData("rebind", HttpStatusCode.UnprocessableEntity)]
    [InlineData("revoke", HttpStatusCode.ServiceUnavailable)]
    public async Task BindingCommand_HttpFailure_PreservesStatus(string operation, HttpStatusCode status)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            SubmitCommandRequest command = (await request.Content!.ReadFromJsonAsync<SubmitCommandRequest>(
                PartiesJsonOptions.Default, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
            return new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(new SubmitCommandResponse(command.CorrelationId!, MessageId: command.MessageId), options: PartiesJsonOptions.Default),
            };
        })) { BaseAddress = new Uri("https://gateway.test/") };

        PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => SubmitBindingAsync(new HttpPartiesIdentityClient(http), operation));

        failure.Status.ShouldBe((int)status);
    }

    /// <summary>Cancellation while buffering the response propagates to the binding caller.</summary>
    [Theory]
    [InlineData("establish")]
    [InlineData("rebind")]
    [InlineData("revoke")]
    public async Task BindingCommand_CancellationDuringResponseRead_Propagates(string operation)
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new CancellationResponseContent(reading),
        }))) { BaseAddress = new Uri("https://gateway.test/") };

        Task pending = SubmitBindingAsync(new HttpPartiesIdentityClient(http), operation, cancellation.Token);
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
        finally
        {
            await cancellation.CancelAsync();
        }

        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
    }

    private static Task SubmitBindingAsync(HttpPartiesIdentityClient client, string operation, CancellationToken? cancellationToken = null)
        => operation switch
        {
            "establish" => client.EstablishHumanActorBindingAsync("tenant-a", new("tenant-a", "party-1", "01HX0000000000000000000001",
                1, 1, 0, DateTimeOffset.UnixEpoch, "logical", "synthetic"), cancellationToken ?? TestContext.Current.CancellationToken),
            "rebind" => client.RebindHumanActorBindingAsync("tenant-a", new("tenant-a", "party-1", "01HX0000000000000000000001",
                1, 1, 0, DateTimeOffset.UnixEpoch, "logical", "synthetic"), cancellationToken ?? TestContext.Current.CancellationToken),
            "revoke" => client.RevokeHumanActorBindingAsync("tenant-a", new("tenant-a", "party-1", "01HX0000000000000000000001",
                1, 1, 0, DateTimeOffset.UnixEpoch, "logical", "synthetic"), cancellationToken ?? TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    /// <summary>Delayed replies must remain usable at the client completion instant.</summary>
    [Theory]
    [InlineData("current-valid")]
    [InlineData("current-interval-expiry")]
    [InlineData("current-custody-expiry")]
    [InlineData("current-source-rollback")]
    [InlineData("current-binding-rollback")]
    [InlineData("current-future-observation")]
    [InlineData("historical-closed-valid")]
    [InlineData("historical-custody-expiry")]
    [InlineData("historical-future-action")]
    public async Task DelayedReply_ValidatesCompletionTime(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset now = start.AddHours(2);
        const string actor = "01HX0000000000000000000001";
        var binding = new HumanActorBindingEvidence("tenant-a", "party-1", actor, 1, 1, start, start.AddDays(5), "writer", "operator",
            new("synthetic", "party-actor-history-v1", start.AddDays(10), 1, "custody", true, true, true));
        bool historical = scenario.StartsWith("historical", StringComparison.Ordinal);
        if (historical)
        {
            binding = binding with { ValidUntil = start.AddHours(1) };
        }
        else if (scenario == "current-custody-expiry")
        {
            binding = binding with { ValidUntil = binding.Custody.ExpiresAt };
        }

        DateTimeOffset observedAt = scenario == "current-future-observation" ? now.AddHours(1) : start.AddHours(1);
        var historicalResult = new HumanActorBindingResult(HumanActorBindingOutcome.Resolved, 1, "tenant-a", "party-1", start, 4, "observation", binding)
            { BindingSourcePosition = 2 };
        var currentResult = new PartyIdentityResult(PartyIdentityOutcome.Resolved,
            new(1, "tenant-a", "party-1", PartyIdentityClassification.Human, true, false, false, 4, observedAt, "observation", binding));
        JsonElement payload = historical ? JsonSerializer.SerializeToElement(historicalResult, PartiesJsonOptions.Default)
            : JsonSerializer.SerializeToElement(currentResult, PartiesJsonOptions.Default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(_ => { entered.SetResult(); return pending.Task; }))
            { BaseAddress = new Uri("https://gateway.test/") };
        TimeProvider clock = NSubstitute.Substitute.For<TimeProvider>();
        NSubstitute.SubstituteExtensions.Returns(clock.GetUtcNow(), _ => now);
        var client = new HttpPartiesIdentityClient(http, clock);
        Task result = historical
            ? client.ResolveHumanActorBindingAtAsync("tenant-a", new("tenant-a", "party-1", start, actor, 1), TestContext.Current.CancellationToken)
            : client.ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1", actor), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        now = scenario switch
        {
            "current-interval-expiry" => binding.ValidUntil!.Value,
            "current-custody-expiry" or "historical-custody-expiry" => binding.Custody.ExpiresAt,
            "current-source-rollback" => observedAt.AddTicks(-1),
            "current-binding-rollback" or "historical-future-action" => start.AddTicks(-1),
            _ => now,
        };
        pending.SetResult(Json(new SubmitQueryResponse("c", payload)));
        if (scenario is "current-valid" or "historical-closed-valid")
        {
            await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
        else
        {
            PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            failure.Status.ShouldBe(503);
        }
    }

    [Fact]
    public async Task HistoricalReply_RequiresOriginalBindingPositionInsideCurrentCheckpoint()
    {
        DateTimeOffset at = DateTimeOffset.UtcNow.AddDays(-1);
        const string actor = "01HX0000000000000000000001";
        var evidence = new HumanActorBindingEvidence("tenant-a", "party-1", actor, 1, 1, at, at.AddDays(10), "writer", "operator",
            new("synthetic", "party-actor-history-v1", at.AddDays(10), 1, "custody", true, true, true));
        foreach (long position in new long[] { 0, 5, 2 })
        {
            var result = new HumanActorBindingResult(HumanActorBindingOutcome.Resolved, 1, "tenant-a", "party-1", at, 4, "observation", evidence)
                { BindingSourcePosition = position };
            using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default))))))
                { BaseAddress = new Uri("https://gateway.test/") };
            var client = new HttpPartiesIdentityClient(http);
            if (position == 2)
            {
                HumanActorBindingResult resolved = await client.ResolveHumanActorBindingAtAsync("tenant-a", new("tenant-a", "party-1", at, actor, 1), TestContext.Current.CancellationToken);
                resolved.BindingSourcePosition.ShouldBe(2);
                resolved.SourcePosition.ShouldBe(4);
            }
            else
            {
                await Should.ThrowAsync<PartiesClientException>(() => client.ResolveHumanActorBindingAtAsync("tenant-a", new("tenant-a", "party-1", at, actor, 1), TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public async Task ConcurrentTenantCalls_KeepEveryRequestAndEvidenceInItsExplicitScope()
    {
        var seen = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var http = new HttpClient(new Handler(async request =>
        {
            JsonElement body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
            string tenant = body.GetProperty("tenant").GetString()!;
            string party = body.GetProperty("aggregateId").GetString()!;
            seen.Add(tenant);
            await Task.Yield();
            return Json(new SubmitQueryResponse("correlation", JsonSerializer.SerializeToElement(new PartyIdentityResult(PartyIdentityOutcome.Resolved,
                new(1, tenant, party, PartyIdentityClassification.Organization, true, false, false, 1, DateTimeOffset.UtcNow, "observation", null)), PartiesJsonOptions.Default)));
        })) { BaseAddress = new Uri("https://gateway.test/") };
        var client = new HttpPartiesIdentityClient(http);
        PartyIdentityResult[] results = await Task.WhenAll(client.ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1"), TestContext.Current.CancellationToken),
            client.ResolvePartyIdentityAsync("tenant-b", new("tenant-b", "party-1"), TestContext.Current.CancellationToken));
        results[0].Evidence!.TenantId.ShouldBe("tenant-a");
        results[1].Evidence!.TenantId.ShouldBe("tenant-b");
        seen.Order().ShouldBe(["tenant-a", "tenant-b"]);
    }

    [Theory]
    [InlineData(999, 1, "tenant-a", false)]
    [InlineData(0, 0, "tenant-a", false)]
    [InlineData(1, 0, "tenant-a", false)]
    [InlineData(3, 0, "tenant-a", false)]
    [InlineData(1, 999, "tenant-a", false)]
    [InlineData(1, 3, "tenant-a", false)]
    [InlineData(1, 2, "tenant-b", false)]
    [InlineData(1, 2, "tenant-a", true)]
    public async Task UnknownOutcomeClassificationForeignOrDegradedReply_IsUnavailable(int outcome, int classification, string tenant, bool degraded)
    {
        var result = new PartyIdentityResult((PartyIdentityOutcome)outcome, new(1, tenant, "party-1", (PartyIdentityClassification)classification,
            true, false, false, 1, DateTimeOffset.UtcNow, "observation", null));
        var reply = new SubmitQueryResponse("correlation", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default), Metadata: new QueryResponseMetadata { IsDegraded = degraded });
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(reply)))) { BaseAddress = new Uri("https://gateway.test/") };
        var client = new HttpPartiesIdentityClient(http);
        await Should.ThrowAsync<PartiesClientException>(() => client.ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(PartyIdentityClassification.Unknown)]
    [InlineData(PartyIdentityClassification.Human)]
    [InlineData(PartyIdentityClassification.Organization)]
    public async Task IneligibleWithSupportedClassification_ReturnsTypedDenial(PartyIdentityClassification classification)
    {
        var result = new PartyIdentityResult(PartyIdentityOutcome.Ineligible, new(1, "tenant-a", "party-1", classification,
            true, false, false, 1, DateTimeOffset.UtcNow, "observation", null));
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default))))))
        {
            BaseAddress = new Uri("https://gateway.test/"),
        };

        PartyIdentityResult received = await new HttpPartiesIdentityClient(http).ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1"), TestContext.Current.CancellationToken);
        received.ShouldBe(result);
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("null")]
    [InlineData("camel-case")]
    [InlineData("pascal-case")]
    [InlineData("numeric")]
    public async Task IneligibleUnknownClassification_MustBeExplicitOnTheWire(string representation)
    {
        var result = new PartyIdentityResult(PartyIdentityOutcome.Ineligible, new(1, "tenant-a", "party-1", PartyIdentityClassification.Unknown,
            true, false, false, 1, DateTimeOffset.UtcNow, "observation", null));
        JsonNode reply = JsonSerializer.SerializeToNode(new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default)), PartiesJsonOptions.Default)!;
        JsonObject evidence = reply["payload"]!["evidence"]!.AsObject();
        evidence.Remove("classification");
        if (representation != "omitted")
        {
            evidence[representation == "pascal-case" ? "Classification" : "classification"] = representation switch
            {
                "null" => null,
                "numeric" => JsonValue.Create(0),
                _ => JsonValue.Create("Unknown"),
            };
        }

        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(reply)))) { BaseAddress = new Uri("https://gateway.test/") };
        Task<PartyIdentityResult> call = new HttpPartiesIdentityClient(http).ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1"), TestContext.Current.CancellationToken);
        if (representation is "omitted" or "null")
        {
            (await Should.ThrowAsync<PartiesClientException>(() => call)).Status.ShouldBe(503);
        }
        else
        {
            (await call.ConfigureAwait(true)).ShouldBe(result);
        }
    }

    [Theory]
    [InlineData(PartyIdentityOutcome.Unavailable)]
    [InlineData(PartyIdentityOutcome.Ineligible)]
    [InlineData(PartyIdentityOutcome.Conflict)]
    public async Task NonresolvedOutcome_CannotCarryAnOtherwiseValidHumanBinding(PartyIdentityOutcome outcome)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string actor = "01HX0000000000000000000001";
        var binding = new HumanActorBindingEvidence("tenant-a", "party-1", actor, 1, 1, now.AddDays(-1), now.AddDays(1), "writer", "operator",
            new("synthetic", "party-actor-history-v1", now.AddDays(1), 1, "custody", true, true, true));
        var result = new PartyIdentityResult(outcome, new(1, "tenant-a", "party-1", PartyIdentityClassification.Human,
            true, false, false, 1, now, "observation", binding));
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default))))))
        {
            BaseAddress = new Uri("https://gateway.test/"),
        };
        (await Should.ThrowAsync<PartiesClientException>(() => new HttpPartiesIdentityClient(http)
            .ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1", actor), TestContext.Current.CancellationToken))).Status.ShouldBe(503);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("tenant")]
    [InlineData("party")]
    [InlineData("position")]
    [InlineData("observation-id")]
    [InlineData("observation-time")]
    [InlineData("future-observation")]
    [InlineData("undefined-classification")]
    [InlineData("agent-classification")]
    [InlineData("human-binding")]
    [InlineData("unknown-binding")]
    [InlineData("organization-binding")]
    [InlineData("failed-query")]
    [InlineData("degraded")]
    [InlineData("stale")]
    public async Task MalformedIneligibleEvidence_StillFailsClosed(string corruption)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string actor = "01HX0000000000000000000001";
        var evidence = new PartyIdentityEvidence(1, "tenant-a", "party-1", PartyIdentityClassification.Unknown,
            true, false, false, 1, now, "observation", null);
        evidence = corruption switch
        {
            "version" => evidence with { ContractVersion = 2 },
            "tenant" => evidence with { TenantId = "tenant-b" },
            "party" => evidence with { PartyId = "party-2" },
            "position" => evidence with { SourcePosition = 0 },
            "observation-id" => evidence with { ObservationId = " " },
            "observation-time" => evidence with { ObservedAt = default },
            "future-observation" => evidence with { ObservedAt = now.AddDays(1) },
            "undefined-classification" => evidence with { Classification = (PartyIdentityClassification)999 },
            "agent-classification" => evidence with { Classification = PartyIdentityClassification.Agent },
            "human-binding" => evidence with { Classification = PartyIdentityClassification.Human },
            "organization-binding" => evidence with { Classification = PartyIdentityClassification.Organization },
            _ => evidence,
        };
        if (corruption.EndsWith("-binding", StringComparison.Ordinal))
        {
            evidence = evidence with { HumanBinding = new("tenant-a", "party-1", actor, 1, 1,
                now.AddDays(-1), now.AddDays(1), "writer", "operator",
                new("synthetic", "party-actor-history-v1", now.AddDays(1), 1, "custody", true, true, true)) };
        }

        var reply = new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(new PartyIdentityResult(PartyIdentityOutcome.Ineligible, evidence), PartiesJsonOptions.Default),
            Success: corruption != "failed-query", Metadata: new QueryResponseMetadata { IsDegraded = corruption == "degraded", IsStale = corruption == "stale" });
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(reply)))) { BaseAddress = new Uri("https://gateway.test/") };
        PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => new HttpPartiesIdentityClient(http)
            .ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1", actor), TestContext.Current.CancellationToken));
        failure.Status.ShouldBe(503);
    }

    [Fact]
    public async Task MalformedOrExplicitRejectedReply_CannotBeSuccess()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad", Encoding.UTF8, "application/json") })))
            { BaseAddress = new Uri("https://gateway.test/") };
        await Should.ThrowAsync<PartiesClientException>(() => new HttpPartiesIdentityClient(http).ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1"), TestContext.Current.CancellationToken));
        using var rejected = new HttpClient(new Handler(_ => Task.FromResult(Json(new { success = false, correlationId = "c" }))))
            { BaseAddress = new Uri("https://gateway.test/") };
        await Should.ThrowAsync<PartiesClientException>(() => new HttpPartiesIdentityClient(rejected).EstablishHumanActorBindingAsync("tenant-a",
            new("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, 0, DateTimeOffset.UtcNow, "logical", "synthetic"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CurrentHumanWithExpiredIntervalOrWrongPurpose_Denies()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var binding = new HumanActorBindingEvidence("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, now.AddDays(-2), now.AddDays(-1), "writer", "operator",
            new("synthetic", "other-purpose", now.AddDays(1), 1, "custody", true, true, true));
        var result = new PartyIdentityResult(PartyIdentityOutcome.Resolved, new(1, "tenant-a", "party-1", PartyIdentityClassification.Human, true, false, false, 1, now, "observation", binding));
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(new SubmitQueryResponse("c", JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default))))))
            { BaseAddress = new Uri("https://gateway.test/") };
        await Should.ThrowAsync<PartiesClientException>(() => new HttpPartiesIdentityClient(http).ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1", binding.ActorId), TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value, PartiesJsonOptions.Default), Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
}
