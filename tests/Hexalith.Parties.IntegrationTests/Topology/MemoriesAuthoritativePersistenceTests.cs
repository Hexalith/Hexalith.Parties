using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Routing;
using NSubstitute;
using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises diagnostic lifetime and actual persistence failures independently of an unavailable native-proof gateway.</summary>
public sealed class MemoriesAuthoritativePersistenceTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("native-proof")]
    [InlineData("event-digest")]
    [InlineData("substituted-incarnation")]
    public async Task NativeReaderVerifiesDigestsAndIndependentStoredProof(string mutation)
    {
        AggregateIdentity identity = new("owned-tenant", "memories-cases", "owned-case");
        var item = new StreamReadEvent(1, "CaseCreatedEvent", [1, 2], "json", 1, "message-1", null, null, DateTimeOffset.UnixEpoch, null)
            { NativeEventDigest = new string('a', 64) };
        NativeSourceProof proof = NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            identity, 1, item.MessageId, item.NativeEventDigest);
        var stored = new SourceProof(proof.CodecVersion, proof.TenantId, proof.Domain, proof.AggregateId, proof.SourceIncarnation,
            proof.HeadSequence, proof.HeadMessageId, proof.CumulativeDigest, proof.RetainedFloor, proof.Erased);
        if (mutation == "native-proof") { proof = proof with { CumulativeDigest = new string('b', 64) }; }
        if (mutation == "event-digest") { item = item with { NativeEventDigest = new string('b', 64) }; }
        if (mutation == "substituted-incarnation")
        {
            proof = NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW"),
                identity, 1, item.MessageId, item.NativeEventDigest);
        }
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            request.RequireNativeProof.ShouldBeTrue();
            StreamReadEvent[] events = request.ToSequence == 0 ? [] : [item];
            return new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, events,
                new StreamReadMetadata(request.FromSequence, request.ToSequence, events.Length == 0 ? null : 1, 1, events.Length, false, null) { NativeProof = proof });
        });
        if (mutation == "none")
        {
            AuthoritativeEventStream verified = await MemoriesAuthoritativePersistence.ReadVerifiedNativeStreamAsync(gateway, identity, stored, TestContext.Current.CancellationToken);
            verified.NativeProof.ShouldBe(proof); verified.Events.Single().NativeEventDigest.ShouldBe(item.NativeEventDigest);
        }
        else
        {
            InvalidDataException failure = await Should.ThrowAsync<InvalidDataException>(() => MemoriesAuthoritativePersistence.ReadVerifiedNativeStreamAsync(
                gateway, identity, stored, TestContext.Current.CancellationToken));
            if (mutation == "substituted-incarnation") { failure.Message.ShouldContain("independent stored actor metadata"); }
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("current")]
    public async Task CatalogGenerationEvidenceRecordsTheActualSentOlderOrCurrentEpoch(string mode)
    {
        var declaration = new RouteContractDeclaration("computed", "query", "tenants", "Namespace.Resolve.v1", null, "v1", null, "workload",
            EventStoreOperationIdentifiers.DomainServiceQuery, null, [], new("Namespace.Resolve", "exact", ["Namespace.Resolve.v1"], "domain-service", "computed", []));
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Seal(new(2, 1, "Development", false, [declaration],
            [new("computed", 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "owned", "query", "v1", "workload", declaration.Operation, false, "*")], [], "", ""));
        var host = new RouteCatalogHostIdentity("owned", "owned", new string('b', 64), new string('c', 64), "owned-authentication");
        var activation = new RouteCatalogActivation("owned-attempt", RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), null, [host], "Development", "owned-proof");
        var state = new RouteCatalogActivationState(7, activation, null, null, [], 0, []);
        long? observed = null, sent = null;
        using var handler = new OwnedRouteCatalogBindingHandler(_ => Task.FromResult(state), (_, actual, transmitted, _, _) => { observed = actual; sent = transmitted; })
            { Mode = mode, InnerHandler = new GenerationReceiver(mode) };
        using var client = new HttpClient(handler);
        using HttpResponseMessage response = await client.GetAsync("http://owned/", TestContext.Current.CancellationToken);
        observed.ShouldBe(7); sent.ShouldBe(mode == "missing" ? null : mode == "stale" ? 6L : 7L);
    }

    private sealed class GenerationReceiver(string mode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (mode == "missing") { request.Headers.Contains(RouteCatalogInvocationHandler.EpochHeader).ShouldBeFalse(); }
            else { request.Headers.GetValues(RouteCatalogInvocationHandler.EpochHeader).Single().ShouldBe(mode == "stale" ? "6" : "7"); }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    [Fact]
    public void ScopedReplayGrantPreservesTheExistingUnscopedOperatorClaims()
    {
        var reader = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var unscoped = reader.ReadJwtToken(HealthChecks.PartiesAspireTopologyFixture.CreateAdministratorToken());
        var scoped = reader.ReadJwtToken(HealthChecks.PartiesAspireTopologyFixture.CreateAdministratorToken("owned-replay-tenant"));
        unscoped.Claims.ShouldNotContain(claim => claim.Type == PartiesClaimTypes.EventStoreTenant);
        scoped.Claims.Single(claim => claim.Type == PartiesClaimTypes.EventStoreTenant).Value.ShouldBe("owned-replay-tenant");
        scoped.Claims.Where(claim => claim.Type is not (PartiesClaimTypes.EventStoreTenant or "exp" or "nbf" or "iat"))
            .Select(claim => claim.Type + "=" + claim.Value).Order(StringComparer.Ordinal).ShouldBe(
                unscoped.Claims.Where(claim => claim.Type is not ("exp" or "nbf" or "iat"))
                    .Select(claim => claim.Type + "=" + claim.Value).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("duplicate")]
    [InlineData("missing-output")]
    [InlineData("wrong-revision")]
    public void CanonicalAuthorityRefusesEitherOmittedOutputOrInvalidRevision(string fault)
    {
        string[] units = fault switch { "first" => ["second"], "second" => ["first"], "duplicate" => ["first", "first"], _ => ["first", "second"] };
        object[] outputs = fault == "missing-output" ? [new { Revision = 1, MemoryUnitId = "first" }]
            : [new { Revision = 1, MemoryUnitId = "first" }, new { Revision = fault == "wrong-revision" ? 1 : 2, MemoryUnitId = "second" }];
        using JsonDocument authority = JsonDocument.Parse(JsonSerializer.Serialize(new { UnitIds = units, CanonicalOutputs = outputs }));
        Should.Throw<InvalidDataException>(() => MemoriesAuthoritativePersistence.CanonicalUnits(authority.RootElement));
    }

    [Fact]
    public void BothCanonicalOutputsRetainIndependentRevisionAuthority()
    {
        using JsonDocument authority = JsonDocument.Parse("""{"UnitIds":["first","second"],"CanonicalOutputs":[{"Revision":1,"MemoryUnitId":"first"},{"Revision":2,"MemoryUnitId":"second"}]}""");
        MemoriesAuthoritativePersistence.CanonicalUnits(authority.RootElement).ShouldBe(["first", "second"]);
        MemoriesAuthoritativePersistence.DeletionRequestedAt(["MemoryUnitDeletionRequestedEvent"], 1).ShouldBeTrue();
        MemoriesAuthoritativePersistence.DeletionRequestedAt(["CaseCreatedEvent", "CaseDeletionRequestedEvent"], 1).ShouldBeFalse();
        MemoriesAuthoritativePersistence.DeletionRequestedAt(["CaseCreatedEvent", "CaseDeletionRequestedEvent"], 2).ShouldBeTrue();
    }

    [Fact]
    public async Task ActorDiagnosticsRetainABoundedPrefix()
    {
        using var body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new string('a', 100000)));
        string retained = await MemoriesAuthoritativePersistence.ReadDiagnosticBodyAsync(body, TestContext.Current.CancellationToken);
        retained.ShouldBe(new string('a', 32768) + " [truncated]");
        body.Position.ShouldBe(32769);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiagnosticWriteFailurePreservesGatewayRefusalAndCaptureFailure(bool failCapture)
    {
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        var captureFailure = new IOException("actor diagnostics failed");
        using var operation = new CancellationTokenSource();
        await operation.CancelAsync();
        string directory = Path.Combine(Path.GetTempPath(), "native-proof-refusal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        bool captureEntered = false, persistenceEntered = false;
        try
        {
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(
                primary, async token =>
                {
                    operation.IsCancellationRequested.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    token.CanBeCanceled.ShouldBeTrue();
                    captureEntered = true;
                    await Task.Delay(1, token).ConfigureAwait(false);
                    if (failCapture) { throw captureFailure; }
                }, async (captured, token) =>
                {
                    captured.ShouldBe(failCapture ? captureFailure : null);
                    token.IsCancellationRequested.ShouldBeFalse();
                    persistenceEntered = true;
                    // A directory is an invalid file destination on every supported filesystem.
                    await File.WriteAllTextAsync(directory, "synthetic refusal", token).ConfigureAwait(false);
                }));
            captureEntered.ShouldBeTrue(); persistenceEntered.ShouldBeTrue();
            failure.InnerExceptions.ShouldContain(primary);
            (failure.InnerExceptions[^1] is IOException or UnauthorizedAccessException).ShouldBeTrue();
            if (failCapture) { failure.InnerExceptions.ShouldContain(captureFailure); }
        }
        finally { Directory.Delete(directory); }
    }

    [Fact]
    public async Task CanceledOperationStillCapturesDiagnosticsThenRethrowsTheOriginalRefusal()
    {
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        using var operation = new CancellationTokenSource();
        await operation.CancelAsync();
        string path = Path.Combine(Path.GetTempPath(), "native-proof-captured-" + Guid.NewGuid().ToString("N") + ".txt");
        bool captured = false;
        try
        {
            EventStoreGatewayException failure = await Should.ThrowAsync<EventStoreGatewayException>(() => MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(
                primary, async token =>
                {
                    operation.IsCancellationRequested.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    token.CanBeCanceled.ShouldBeTrue();
                    await Task.Delay(1, token).ConfigureAwait(false);
                    captured = true;
                }, (captureFailure, token) =>
                {
                    captureFailure.ShouldBeNull();
                    captured.ShouldBeTrue();
                    token.IsCancellationRequested.ShouldBeFalse();
                    return File.WriteAllTextAsync(path, "synthetic refusal", token);
                }));
            failure.ShouldBeSameAs(primary);
            (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ShouldBe("synthetic refusal");
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task TimedOutCaptureFinishesCancellationCleanupBeforeFreshBudgetPersistence()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var primary = new EventStoreGatewayException(503, "Native proof unavailable");
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false, persisted = false;
        string path = Path.Combine(Path.GetTempPath(), "native-proof-cleaned-" + Guid.NewGuid().ToString("N") + ".txt");
        Task capture = MemoriesAuthoritativePersistence.CaptureNativeRefusalAsync(primary, async captureToken =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, captureToken).ConfigureAwait(false); }
            finally
            {
                cleanupEntered.TrySetResult();
                await finishCleanup.Task.ConfigureAwait(false);
                cleaned = true;
            }
        }, async (failure, persistenceToken) =>
        {
            cleaned.ShouldBeTrue("Receipt persistence must follow actual cooperative cancellation cleanup.");
            failure.ShouldBeOfType<TaskCanceledException>();
            persistenceToken.IsCancellationRequested.ShouldBeFalse();
            await Task.Delay(30, persistenceToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(path, "captured after cleanup", persistenceToken).ConfigureAwait(false);
            persisted = true;
        }, captureBudget: TimeSpan.FromMilliseconds(20));
        try
        {
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            capture.IsCompleted.ShouldBeFalse();
            persisted.ShouldBeFalse();
            finishCleanup.TrySetResult();
            EventStoreGatewayException failure = await Should.ThrowAsync<EventStoreGatewayException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5), token));
            failure.ShouldBeSameAs(primary);
            persisted.ShouldBeTrue();
            (await File.ReadAllTextAsync(path, token)).ShouldBe("captured after cleanup");
        }
        finally { finishCleanup.TrySetResult(); File.Delete(path); }
    }
}
