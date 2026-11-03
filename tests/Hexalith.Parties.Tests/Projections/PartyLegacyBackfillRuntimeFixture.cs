using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Recreates SDK clients and handlers for every transition over one owned durable provider.</summary>
internal sealed class PartyLegacyBackfillRuntimeFixture
{
    private readonly PartyLegacyBackfillRuntimeConfiguration _configuration;
    private readonly PartyLegacyBackfillFixture _seed = new();
    private readonly DomainProjectionIdentityOptions _identity;

    /// <summary>Creates a unique synthetic tenant within the explicitly disposable provider.</summary>
    /// <param name="configuration">The private owned provider configuration.</param>
    public PartyLegacyBackfillRuntimeFixture(PartyLegacyBackfillRuntimeConfiguration configuration)
    {
        _configuration = configuration;
        _identity = new() { AppId = configuration.DaprAppId, ServiceVersion = "v1" };
        Histories = _seed.Histories.Select(history => history with
        {
            TenantId = Tenant,
            Events = history.Events.Select(item => item.EventTypeName == nameof(PartyErased)
                ? item with { Payload = JsonSerializer.SerializeToUtf8Bytes(
                    JsonSerializer.Deserialize<PartyErased>(item.Payload, PartiesJsonOptions.Default)! with { TenantId = Tenant }, PartiesJsonOptions.Default) }
                : item).ToArray(),
        }).ToArray();
    }

    /// <summary>Gets the unique synthetic migration tenant.</summary>
    public string Tenant { get; } = "synthetic-backfill-" + Guid.NewGuid().ToString("N");

    /// <summary>Gets complete synthetic histories with a live and an erased owner.</summary>
    public ProjectionRequest[] Histories { get; }

    /// <summary>Gets exact physical rollback and foreign-tenant bytes captured before rebuilding.</summary>
    public Dictionary<string, byte[]> Survivors { get; } = new(StringComparer.Ordinal);

    /// <summary>Loads only explicitly owned loopback endpoints, skipping the opt-in lane when absent.</summary>
    public static async Task<PartyLegacyBackfillRuntimeConfiguration> ConfigurationAsync(CancellationToken token)
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_BACKFILL_STATE_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path),
            "Set PARTIES_BACKFILL_STATE_RUNTIME_CONFIG to an owned disposable Redis/Dapr provider.");
        PartyLegacyBackfillRuntimeConfiguration configuration = JsonSerializer.Deserialize<PartyLegacyBackfillRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token).ConfigureAwait(false), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Migration rehearsal configuration is empty.");
        configuration.OwnedDisposableStore.ShouldBeTrue("The migration rehearsal must never target a shared or deployed store.");
        foreach (string endpoint in new[] { configuration.DaprHttpEndpoint, configuration.DaprGrpcEndpoint })
        {
            Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri).ShouldBeTrue();
            Uri address = uri.ShouldNotBeNull();
            address.IsLoopback.ShouldBeTrue();
            address.Scheme.ShouldBe("http");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.StateStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.DaprApiToken);
        configuration.DaprAppId.ShouldStartWith("parties-backfill-rehearsal-");
        configuration.StateStoreName.ShouldStartWith("backfill-");
        Uri.TryCreate("redis://" + configuration.RedisEndpoint, UriKind.Absolute, out Uri? redisUri).ShouldBeTrue();
        Uri redisAddress = redisUri.ShouldNotBeNull();
        redisAddress.IsLoopback.ShouldBeTrue();
        redisAddress.Port.ShouldBeGreaterThan(0);
        using var http = new HttpClient { BaseAddress = new Uri(configuration.DaprHttpEndpoint) };
        http.DefaultRequestHeaders.Add("dapr-api-token", configuration.DaprApiToken);
        using HttpResponseMessage response = await http.GetAsync("/v1.0/metadata", token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        metadata.RootElement.GetProperty("id").GetString().ShouldBe(configuration.DaprAppId);
        metadata.RootElement.GetProperty("components").EnumerateArray().ShouldContain(component =>
            component.GetProperty("name").GetString() == configuration.StateStoreName
            && component.GetProperty("type").GetString() == "state.redis");
        using DaprClient grpc = new DaprClientBuilder().UseHttpEndpoint(configuration.DaprHttpEndpoint)
            .UseGrpcEndpoint(configuration.DaprGrpcEndpoint).UseDaprApiToken(configuration.DaprApiToken).Build();
        DaprMetadata grpcMetadata = await grpc.GetMetadataAsync(token).ConfigureAwait(false);
        grpcMetadata.Id.ShouldBe(configuration.DaprAppId);
        grpcMetadata.Components.ShouldContain(component => component.Name == configuration.StateStoreName && component.Type == "state.redis");
        using IConnectionMultiplexer redis = await RedisAsync(configuration).ConfigureAwait(false);
        string probeKey = "synthetic-backfill-provider-binding:" + Guid.NewGuid().ToString("N");
        string expectedProbe = Guid.NewGuid().ToString("N");
        string physicalProbe = configuration.DaprAppId + "||" + probeKey;
        try
        {
            await new DaprReadModelStore(grpc).SaveAsync(configuration.StateStoreName, probeKey, expectedProbe, token).ConfigureAwait(false);
            RedisValue probe = await redis.GetDatabase().HashGetAsync(physicalProbe, "data").ConfigureAwait(false);
            byte[] probeBytes = ((byte[]?)probe).ShouldNotBeNull("The independently inspected Redis endpoint must back the selected SDK/Dapr component.");
            JsonSerializer.Deserialize<string>(probeBytes, grpc.JsonSerializerOptions).ShouldBe(expectedProbe);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await grpc.DeleteStateAsync(configuration.StateStoreName, probeKey, cancellationToken: cleanup.Token).ConfigureAwait(false);
        }
        (await redis.GetDatabase().KeyExistsAsync(physicalProbe).ConfigureAwait(false)).ShouldBeFalse();
        return configuration;
    }

    /// <summary>Copies only representative synthetic actor and foreign-tenant seed bytes to the actual provider.</summary>
    public async Task SeedAsync(CancellationToken token)
    {
        await _seed.SeedAsync(token).ConfigureAwait(false);
        using DaprClient dapr = Client();
        using IConnectionMultiplexer redis = await RedisAsync().ConfigureAwait(false);
        foreach ((string key, byte[] bytes) in _seed.Survivors)
        {
            string target = key.Replace("tenant-a", Tenant, StringComparison.Ordinal)
                .Replace("tenant-b", Tenant + "-foreign", StringComparison.Ordinal);
            if (target.Contains("||", StringComparison.Ordinal))
            {
                // Independent test setup models Redis actor backing hashes. Dapr's state API
                // deliberately rejects actor delimiters; no migration code writes these keys.
                await redis.GetDatabase().HashSetAsync(target,
                    [new HashEntry("data", bytes), new HashEntry("version", "1")]).ConfigureAwait(false);
            }
            else
            {
                await dapr.SaveByteStateAsync(_configuration.StateStoreName, target, bytes, cancellationToken: token).ConfigureAwait(false);
            }
            Survivors.Add(target, await RawAsync(target, token).ConfigureAwait(false));
            Survivors[target].ShouldBe(bytes);
        }
    }

    /// <summary>Reads physically persisted bytes through a newly constructed Dapr client.</summary>
    public async Task<byte[]> RawAsync(string key, CancellationToken token)
    {
        if (key.Contains("||", StringComparison.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            using IConnectionMultiplexer redis = await RedisAsync().ConfigureAwait(false);
            RedisValue actorValue = await redis.GetDatabase().HashGetAsync(key, "data").ConfigureAwait(false);
            byte[] bytes = ((byte[]?)actorValue).ShouldNotBeNull();
            RedisValue version = await redis.GetDatabase().HashGetAsync(key, "version").ConfigureAwait(false);
            version.ToString().ShouldBe("1");
            return bytes;
        }
        using DaprClient dapr = Client();
        (ReadOnlyMemory<byte> value, string etag) = await dapr.GetByteStateAndETagAsync(
            _configuration.StateStoreName, key, cancellationToken: token).ConfigureAwait(false);
        etag.ShouldNotBeNullOrWhiteSpace();
        return value.ToArray();
    }

    /// <summary>Reads SDK-visible persisted state through a fresh client and SDK store.</summary>
    public async Task<ReadModelEntry<TValue>> ReadAsync<TValue>(string key, CancellationToken token) where TValue : class
    {
        using DaprClient dapr = Client();
        return await new DaprReadModelStore(dapr).GetAsync<TValue>(_configuration.StateStoreName, key, token).ConfigureAwait(false);
    }

    /// <summary>Introduces a synthetic competing canonical authority using the SDK store.</summary>
    public async Task SaveAsync<TValue>(string key, TValue value, CancellationToken token) where TValue : class
    {
        using DaprClient dapr = Client();
        await new DaprReadModelStore(dapr).SaveAsync(_configuration.StateStoreName, key, value, token).ConfigureAwait(false);
    }

    /// <summary>Writes a real competing value under the ETag independently captured before preparation.</summary>
    public async Task<bool> TrySaveAsync<TValue>(string key, TValue value, string etag, CancellationToken token) where TValue : class
    {
        using DaprClient dapr = Client();
        return await new DaprReadModelStore(dapr).TrySaveAsync(_configuration.StateStoreName, key, value, etag, token).ConfigureAwait(false);
    }

    /// <summary>Invokes one aggregate stage, commit or verify with fresh handlers and a fresh SDK store.</summary>
    public async Task<ProjectionDispatchResponse> DetailAsync(ProjectionRequest history, string operation, string action, CancellationToken token,
        Func<ReadModelBatch, CancellationToken, Task>? beforeStage = null)
    {
        using DaprClient dapr = Client();
        using ServiceProvider provider = Provider(dapr, beforeStage);
        var request = new ProjectionDispatchRequest(history, [PartyProjectionNames.Detail], operation, Catalog());
        return action switch
        {
            "rebuild" => await DomainProjectionDispatcher.RebuildAsync(provider, request, new ProjectionDispatchOptions(), _identity, token).ConfigureAwait(false),
            "stage" => await DomainProjectionDispatcher.StageRebuildAsync(provider, request, new ProjectionDispatchOptions(), _identity, token).ConfigureAwait(false),
            "commit" => await DomainProjectionDispatcher.CommitRebuildAsync(provider, request, new ProjectionDispatchOptions(), _identity, token).ConfigureAwait(false),
            "verify" => await DomainProjectionDispatcher.VerifyRebuildAsync(provider, request, new ProjectionDispatchOptions(), _identity, token).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    /// <summary>Captures and finalizes the complete tenant inventory using fresh SDK graphs for every transition.</summary>
    public async Task<DomainSharedProjectionRebuildResponse> PrepareIndexAsync(string operation, CancellationToken token)
    {
        ProjectionRequest[] histories = Histories.OrderBy(history => history.AggregateId, StringComparer.Ordinal).ToArray();
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < histories.Length; ordinal++)
        {
            ProjectionRequest history = histories[ordinal];
            fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(fingerprint, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(history.AggregateId, false, history.Events));
        }
        DomainSharedProjectionRebuildIdentity identity = SharedIdentity(operation);
        DomainSharedProjectionRebuildResponse current = await SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Begin, identity, CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: histories.ToDictionary(history => history.AggregateId, history => history.Events[^1].SequenceNumber)), token).ConfigureAwait(false);
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        for (int ordinal = 0; ordinal < histories.Length; ordinal++)
        {
            ProjectionRequest history = histories[ordinal];
            current = await SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
                identity, AggregateOrdinal: ordinal, AggregateId: history.AggregateId, Events: history.Events), token).ConfigureAwait(false);
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        }
        current.AcceptedAggregateCount.ShouldBe(histories.Length);
        return await SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
            identity, ExpectedAggregateCount: current.AcceptedAggregateCount, ExpectedInventoryFingerprint: current.InventoryFingerprint), token).ConfigureAwait(false);
    }

    /// <summary>Invokes a shared stage, commit or verify using a new SDK graph.</summary>
    public Task<DomainSharedProjectionRebuildResponse> IndexAsync(string operation, DomainSharedProjectionRebuildAction action, CancellationToken token,
        Func<ReadModelBatch, CancellationToken, Task>? beforeStage = null)
        => SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, action, SharedIdentity(operation)), token, beforeStage);

    private DaprClient Client() => new DaprClientBuilder().UseHttpEndpoint(_configuration.DaprHttpEndpoint)
        .UseGrpcEndpoint(_configuration.DaprGrpcEndpoint).UseDaprApiToken(_configuration.DaprApiToken).Build();

    private Task<ConnectionMultiplexer> RedisAsync() => RedisAsync(_configuration);

    private static Task<ConnectionMultiplexer> RedisAsync(PartyLegacyBackfillRuntimeConfiguration configuration) => ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
    {
        EndPoints = { configuration.RedisEndpoint },
        AbortOnConnectFail = true,
        ConnectTimeout = 5000,
        SyncTimeout = 5000,
    });

    private ServiceProvider Provider(DaprClient dapr, Func<ReadModelBatch, CancellationToken, Task>? beforeStage = null)
    {
        var store = new DaprReadModelStore(dapr);
        IOptions<PartySdkReadModelOptions> options = Options.Create(new PartySdkReadModelOptions
        { ReadModelStateStoreName = _configuration.StateStoreName });
        return new ServiceCollection().AddSingleton<IReadModelStore>(store).AddSingleton<IReadModelBatchStore>(store)
            .AddSingleton<IReadModelBatchStagingStore>(beforeStage is null ? store : new PartyLegacyBackfillCompetingWriterStore(store, beforeStage))
            .AddSingleton<IAsyncDomainProjectionHandler>(new PartyDetailSdkProjectionHandler(store, store, options))
            .AddSingleton<IAsyncDomainProjectionHandler>(new PartyIndexSdkProjectionHandler(store, options)).BuildServiceProvider();
    }

    private DomainSharedProjectionRebuildIdentity SharedIdentity(string operation)
        => new(Tenant, "party", PartyProjectionNames.Index, operation, Catalog());

    private string Catalog() => ProjectionRouteCatalogFingerprint.Compute(_configuration.DaprAppId, "v1",
        [new ProjectionDispatchRoute("party", PartyProjectionNames.Detail), new ProjectionDispatchRoute("party", PartyProjectionNames.Index)]);

    private async Task<DomainSharedProjectionRebuildResponse> SharedAsync(DomainSharedProjectionRebuildRequest request, CancellationToken token,
        Func<ReadModelBatch, CancellationToken, Task>? beforeStage = null)
    {
        using DaprClient dapr = Client();
        using ServiceProvider provider = Provider(dapr, beforeStage);
        return await DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request, new ProjectionDispatchOptions(), _identity, token).ConfigureAwait(false);
    }
}
