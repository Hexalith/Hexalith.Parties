using System.Net;
using System.Text;
using System.Text.Json;
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
