using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Owns synthetic actor rollback bytes and invokes existing SDK rebuild dispatchers.</summary>
internal sealed class PartyLegacyBackfillFixture
{
    /// <summary>The exclusively synthetic store.</summary>
    public const string StoreName = "statestore";
    private const string AppId = "parties-backfill-rehearsal";
    private static readonly DomainProjectionIdentityOptions s_identity = new() { AppId = AppId, ServiceVersion = "v1" };
    private static readonly IOptions<PartySdkReadModelOptions> s_options = Options.Create(new PartySdkReadModelOptions
    { ReadModelStateStoreName = StoreName });

    /// <summary>Gets the recreated SDK byte store.</summary>
    public InMemoryReadModelStore Store { get; private set; } = new();

    /// <summary>Gets complete captured synthetic source histories.</summary>
    public ProjectionRequest[] Histories { get; } =
    [
        History("tenant-a", "party-live",
            new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Owner" } },
            new PartyDisplayNameDerived { DisplayName = "Synthetic Updated", SortName = "Updated Synthetic" },
            new PartyDeactivated()),
        History("tenant-a", "party-erased",
            new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Erased", LastName = "Synthetic" } },
            new PartyErased { TenantId = "tenant-a", PartyId = "party-erased", ErasedAt = DateTimeOffset.UnixEpoch.AddSeconds(2) },
            new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Must", LastName = "Stay Suppressed" } }),
    ];

    /// <summary>Gets the captured actor and unrelated tenant bytes which must survive unchanged.</summary>
    public Dictionary<string, byte[]> Survivors { get; } = new(StringComparer.Ordinal);

    /// <summary>Seeds only actor state for the selected tenant; unrelated SDK rows are independent survivors.</summary>
    public async Task SeedAsync(CancellationToken token)
    {
        var index = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);
        foreach (ProjectionRequest history in Histories)
        {
            PartyDetail? detail = null;
            PartyIndexEntry? entry = null;
            foreach (ProjectionEventDto item in history.Events)
            {
                if (item.EventTypeName == nameof(PartyErased))
                {
                    detail = PartyDetailProjectionHandler.ApplyErasure(history.AggregateId, detail);
                    entry = null;
                }
                else
                {
                    var payload = item.EventTypeName switch
                    {
                        nameof(PartyCreated) => (Hexalith.EventStore.Contracts.Events.IEventPayload)JsonSerializer.Deserialize<PartyCreated>(item.Payload, PartiesJsonOptions.Default)!,
                        nameof(PartyDisplayNameDerived) => JsonSerializer.Deserialize<PartyDisplayNameDerived>(item.Payload, PartiesJsonOptions.Default)!,
                        _ => JsonSerializer.Deserialize<PartyDeactivated>(item.Payload, PartiesJsonOptions.Default)!,
                    };
                    detail = PartyDetailProjectionHandler.Apply(history.AggregateId, payload, detail) ?? detail;
                    if (detail?.IsErased != true)
                    {
                        entry = PartyIndexProjectionHandler.Apply(history.AggregateId, payload, entry) ?? entry;
                    }
                }
            }
            string actor = PartyActorIds.Detail(history.TenantId, history.AggregateId);
            // Representative Dapr actor backing keys: app || type || id || state name.
            string key = $"parties||PartyDetailProjectionActor||{actor}||{actor}";
            Store.SeedRaw(StoreName, key, detail!);
            Store.SeedRaw<object>(StoreName, key + ":last-sequence", history.Events[^1].SequenceNumber);
            if (entry is not null) index[history.AggregateId] = entry;
        }
        string indexActor = PartyActorIds.Index("tenant-a") + ":all";
        Store.SeedRaw(StoreName, $"parties||PartyIndexProjectionActor||{indexActor}||{indexActor}", index);
        Store.SeedRaw(StoreName, $"parties||PartyIndexProjectionActor||{indexActor}||tenant-a:party-index:manifest", new[] { "all" });
        ProjectionRequest foreign = History("tenant-b", "party-live", new PartyCreated { Type = PartyType.Organization });
        await new PartyDetailSdkProjectionHandler(Store, Store, s_options).ProjectAsync(foreign, "foreign-detail", token).ConfigureAwait(false);
        await new PartyIndexSdkProjectionHandler(Store, s_options).ProjectAsync(foreign, "foreign-index", token).ConfigureAwait(false);
        foreach ((string key, byte[] value) in Store.SnapshotRawEntries(StoreName)
            .Where(pair => pair.Key.Contains("||", StringComparison.Ordinal) || pair.Key.StartsWith("readmodel:tenant-b:", StringComparison.Ordinal)))
        {
            Survivors.Add(key, value);
        }
    }

    /// <summary>Recreates the workload fixture over its surviving external backend, including stable provider ETags.</summary>
    /// <returns>A fresh fixture and fresh handlers with no prior in-memory projection state.</returns>
    public PartyLegacyBackfillFixture Restart()
    {
        var restarted = new PartyLegacyBackfillFixture { Store = Store };
        foreach ((string key, byte[] value) in Survivors)
        {
            restarted.Survivors.Add(key, value.ToArray());
        }
        return restarted;
    }

    /// <summary>Builds a fresh handler/dispatcher service graph over the persisted store.</summary>
    public ServiceProvider Provider()
        => new ServiceCollection()
            .AddSingleton<IReadModelStore>(Store)
            .AddSingleton<IReadModelBatchStore>(Store)
            .AddSingleton<IReadModelBatchStagingStore>(Store)
            .AddSingleton<IAsyncDomainProjectionHandler>(new PartyDetailSdkProjectionHandler(Store, Store, s_options))
            .AddSingleton<IAsyncDomainProjectionHandler>(new PartyIndexSdkProjectionHandler(Store, s_options))
            .BuildServiceProvider();

    /// <summary>Runs an aggregate-owned rebuild using the SDK batch/marker protocol.</summary>
    public async Task<ProjectionDispatchResponse> DetailAsync(ProjectionRequest history, string operation, CancellationToken token)
    {
        using ServiceProvider provider = Provider();
        return await DomainProjectionDispatcher.RebuildAsync(provider, DetailRequest(history, operation),
            new ProjectionDispatchOptions(), s_identity, token).ConfigureAwait(false);
    }

    /// <summary>Builds the exact admitted request for a detail and processing rebuild.</summary>
    public static ProjectionDispatchRequest DetailRequest(ProjectionRequest history, string operation)
        => new(history, [PartyProjectionNames.Detail], operation, Catalog());

    /// <summary>Builds the tenant-shared index session identity.</summary>
    public static DomainSharedProjectionRebuildIdentity SharedIdentity(string operation)
        => new("tenant-a", "party", PartyProjectionNames.Index, operation, Catalog());

    /// <summary>Runs one persisted shared rebuild transition using fresh handlers.</summary>
    public async Task<DomainSharedProjectionRebuildResponse> SharedAsync(DomainSharedProjectionRebuildRequest request, CancellationToken token)
    {
        using ServiceProvider provider = Provider();
        return await DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request,
            new ProjectionDispatchOptions(), s_identity, token).ConfigureAwait(false);
    }

    /// <summary>Starts a captured, fingerprinted tenant inventory and accumulates complete histories.</summary>
    public async Task<DomainSharedProjectionRebuildResponse> AccumulateAsync(string operation, CancellationToken token)
    {
        var identity = SharedIdentity(operation);
        ProjectionRequest[] histories = Histories.OrderBy(history => history.AggregateId, StringComparer.Ordinal).ToArray();
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < histories.Length; ordinal++)
        {
            ProjectionRequest history = histories[ordinal];
            fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(fingerprint, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(history.AggregateId, false, history.Events));
        }
        var current = await SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: histories.ToDictionary(history => history.AggregateId, history => history.Events[^1].SequenceNumber)), token).ConfigureAwait(false);
        if (current.Status != ProjectionDispatchStatus.Completed) return current;
        for (int ordinal = 0; ordinal < histories.Length; ordinal++)
        {
            ProjectionRequest history = histories[ordinal];
            current = await SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
                identity, AggregateOrdinal: ordinal, AggregateId: history.AggregateId, Events: history.Events), token).ConfigureAwait(false);
            if (current.Status != ProjectionDispatchStatus.Completed) return current;
        }
        return current;
    }

    /// <summary>Finalizes the captured inventory, ready for SDK stage/commit/verify.</summary>
    public Task<DomainSharedProjectionRebuildResponse> FinalizeAsync(string operation, DomainSharedProjectionRebuildResponse current, CancellationToken token)
        => SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
            SharedIdentity(operation), ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint), token);

    /// <summary>Runs a shared lifecycle transition.</summary>
    public Task<DomainSharedProjectionRebuildResponse> TransitionAsync(string operation, DomainSharedProjectionRebuildAction action, CancellationToken token)
        => SharedAsync(new(DomainSharedProjectionRebuildProtocol.Version, action, SharedIdentity(operation)), token);

    /// <summary>Runs a fresh aggregate stage, commit or verify transition.</summary>
    public async Task<ProjectionDispatchResponse> DetailTransitionAsync(ProjectionRequest history, string operation, string action, CancellationToken token)
    {
        using ServiceProvider provider = Provider();
        ProjectionDispatchRequest request = DetailRequest(history, operation);
        return action switch
        {
            "stage" => await DomainProjectionDispatcher.StageRebuildAsync(provider, request, new ProjectionDispatchOptions(), s_identity, token).ConfigureAwait(false),
            "commit" => await DomainProjectionDispatcher.CommitRebuildAsync(provider, request, new ProjectionDispatchOptions(), s_identity, token).ConfigureAwait(false),
            _ => await DomainProjectionDispatcher.VerifyRebuildAsync(provider, request, new ProjectionDispatchOptions(), s_identity, token).ConfigureAwait(false),
        };
    }

    private static string Catalog() => ProjectionRouteCatalogFingerprint.Compute(AppId, "v1",
        [new ProjectionDispatchRoute("party", PartyProjectionNames.Detail), new ProjectionDispatchRoute("party", PartyProjectionNames.Index)]);

    private static ProjectionRequest History(string tenant, string party, params object[] payloads)
        => new(tenant, "party", party, payloads.Select((payload, index) => new ProjectionEventDto(
            payload.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json", index + 1, DateTimeOffset.UnixEpoch.AddSeconds(index + 1), "synthetic-correlation", "message-" + (index + 1))).ToArray());
}
