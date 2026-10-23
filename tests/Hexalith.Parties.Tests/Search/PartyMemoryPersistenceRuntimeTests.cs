using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Opt-in wire qualification against the actual SDK/Dapr store without remote ingestion.</summary>
public sealed class PartyMemoryPersistenceRuntimeTests
{
    [Fact(Timeout = 60000)]
    public async Task ActualSdkPersistencePreservesLegacyListsEtagsProbeDeletionAndCanonicalReceipts()
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_STATE_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set PARTIES_MEMORIES_STATE_RUNTIME_CONFIG to private owned Dapr endpoints and credentials.");
        CancellationToken token = TestContext.Current.CancellationToken;
        PartyMemoryRuntimeConfiguration config = JsonSerializer.Deserialize<PartyMemoryRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("State runtime configuration is empty.");
        string tenant = $"synthetic-state-{Guid.NewGuid():N}";
        string party = $"synthetic-party-{Guid.NewGuid():N}";
        string key = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        IOptions<PartyMemoryUnitMappingStoreOptions> options = Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = config.StateStoreName });
        string intentKey = key.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        string probeParty = $"_health-probe-{Guid.NewGuid():N}";
        string probeKey = PartyMemoryUnitMappingStore.BuildKey("_health-probe", probeParty);
        bool primaryFailed = false;
        try
        {
            using (DaprClient seed = Dapr(config))
            {
                byte[] legacy = Encoding.UTF8.GetBytes("[{\"MemoryUnitId\":\"legacy-workflow\",\"SourceUri\":\"urn:synthetic-source\",\"CaseId\":null}]");
                await seed.SaveByteStateAsync(config.StateStoreName, key, legacy, cancellationToken: token);
            }

            using (DaprClient current = Dapr(config))
            {
                var sdk = new DaprReadModelStore(current);
                var mappings = Mappings(sdk, options);
                (await mappings.GetMappingsAsync(tenant, party, token)).ShouldBe([new("legacy-workflow", "urn:synthetic-source")]);
                await mappings.RecordMappingAsync(tenant, party, "unit-one", "urn:synthetic-source", "captured-case", token);
                await mappings.RecordMappingAsync(tenant, party, "legacy-workflow", "urn:synthetic-source", "captured-case", token);
                IReadOnlyList<PartyMemoryUnitMappingEntry> snapshot = await mappings.GetMappingsAsync(tenant, party, token);
                snapshot.Count.ShouldBe(2);
                snapshot.ShouldAllBe(item => item.CaseId == "captured-case");
                await mappings.RecordMappingAsync(tenant, party, "unit-two", "urn:synthetic-source", "captured-case", token);
                (await mappings.RemoveMappingsAsync(tenant, party, snapshot, token)).ShouldBe(1);
            }

            using (DaprClient restarted = Dapr(config))
            {
                var sdk = new DaprReadModelStore(restarted);
                var mappings = Mappings(sdk, options);
                IReadOnlyList<PartyMemoryUnitMappingEntry> retained = await mappings.GetMappingsAsync(tenant, party, token);
                retained.ShouldBe([new("unit-two", "urn:synthetic-source", "captured-case")]);
                (ReadOnlyMemory<byte> before, string previousEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, key, cancellationToken: token);
                previousEtag.ShouldNotBeNullOrWhiteSpace();
                using (JsonDocument json = JsonDocument.Parse(before))
                {
                    json.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
                    json.RootElement.GetArrayLength().ShouldBe(1);
                }

                (await mappings.RemoveMappingsAsync(tenant, party, retained, token)).ShouldBe(0);
                (ReadOnlyMemory<byte> after, string nextEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, key, cancellationToken: token);
                Encoding.UTF8.GetString(after.Span).ShouldBe("[]");
                nextEtag.ShouldNotBeNullOrWhiteSpace();
                nextEtag.ShouldNotBe(previousEtag);

                await mappings.RecordMappingAsync("_health-probe", probeParty, "_probe-unit", "urn:_probe", "_probe-case", token);
                (ReadOnlyMemory<byte> probe, string probeEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, probeKey, cancellationToken: token);
                probe.IsEmpty.ShouldBeFalse();
                probeEtag.ShouldNotBeNullOrWhiteSpace();
                await mappings.ClearMappingsAsync("_health-probe", probeParty, token);
                (ReadOnlyMemory<byte> erasedProbe, string erasedProbeEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, probeKey, cancellationToken: token);
                erasedProbe.IsEmpty.ShouldBeTrue();
                erasedProbeEtag.ShouldBeNullOrEmpty();

                var intents = new PartyMemoryIngestionIntentStore(sdk, options);
                var reserved = new PartyMemoryIngestionIntent("synthetic-attempt", "captured-case", "urn:synthetic-source");
                (await intents.RegisterAsync(tenant, party, reserved, token)).Created.ShouldBeTrue();
                await intents.UpdateAsync(tenant, party, reserved with { WorkflowInstanceId = "synthetic-workflow", MemoryUnitId = "synthetic-canonical" }, token);
            }

            using (DaprClient fresh = Dapr(config))
            {
                var sdk = new DaprReadModelStore(fresh);
                var intents = new PartyMemoryIngestionIntentStore(sdk, options);
                PartyMemoryIngestionIntent persisted = (await intents.GetAsync(tenant, party, token)).ShouldHaveSingleItem();
                persisted.WorkflowInstanceId.ShouldBe("synthetic-workflow");
                persisted.MemoryUnitId.ShouldBe("synthetic-canonical");
                await intents.BeginCleanupAsync(tenant, party, token);
                (await intents.RemoveAsync(tenant, party, [persisted], token)).ShouldBe(0);
            }

            using (DaprClient final = Dapr(config))
            {
                var sdk = new DaprReadModelStore(final);
                ReadModelEntry<PartyMemoryIngestionInventory> finalState = await sdk.GetAsync<PartyMemoryIngestionInventory>(config.StateStoreName, intentKey, token);
                finalState.ETag.ShouldNotBeNullOrWhiteSpace();
                PartyMemoryIngestionInventory inventory = finalState.Value.ShouldNotBeNull();
                inventory.CleanupRequested.ShouldBeTrue();
                inventory.PermanentCleanup.ShouldBeTrue();
                inventory.Intents.ShouldBeEmpty();
                inventory.CleanupReceipts.ShouldNotBeNull().ShouldHaveSingleItem().MemoryUnitId.ShouldBe("synthetic-canonical");
                (await Mappings(sdk, options).GetMappingsAsync(tenant, party, token)).ShouldBeEmpty();
            }
        }
        catch
        {
            primaryFailed = true;
            throw;
        }
        finally
        {
            await CleanupOwnedKeysAsync(config, [key, intentKey, probeKey], primaryFailed);
        }
    }

    [Fact(Timeout = 60000)]
    public async Task ActualSdkRejectsTwoClientStaleEtagsAndCompetingAbsentKeyCreation()
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_STATE_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set PARTIES_MEMORIES_STATE_RUNTIME_CONFIG to private owned Dapr endpoints and credentials.");
        CancellationToken token = TestContext.Current.CancellationToken;
        PartyMemoryRuntimeConfiguration config = JsonSerializer.Deserialize<PartyMemoryRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("State runtime configuration is empty.");
        using DaprClient firstClient = Dapr(config);
        using DaprClient secondClient = Dapr(config);
        var first = new DaprReadModelStore(firstClient);
        var second = new DaprReadModelStore(secondClient);
        string tenant = $"synthetic-conflict-{Guid.NewGuid():N}";
        string party = $"synthetic-party-{Guid.NewGuid():N}";
        string key = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        string absentKey = PartyMemoryUnitMappingStore.BuildKey(tenant, "absent-" + party);
        bool primaryFailed = false;
        try
        {
            List<PartyMemoryUnitMappingEntry> original = [new("original", "urn:synthetic", "case")];
            (await first.TrySaveAsync(config.StateStoreName, key, original, string.Empty, token)).ShouldBeTrue();
            ReadModelEntry<List<PartyMemoryUnitMappingEntry>> firstRead = await first.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, key, token);
            ReadModelEntry<List<PartyMemoryUnitMappingEntry>> secondRead = await second.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, key, token);
            firstRead.ETag.ShouldNotBeNullOrWhiteSpace();
            secondRead.ETag.ShouldBe(firstRead.ETag);
            List<PartyMemoryUnitMappingEntry> winner = [new("winner", "urn:synthetic", "case")];
            List<PartyMemoryUnitMappingEntry> stale = [new("stale", "urn:synthetic", "case")];
            (await first.TrySaveAsync(config.StateStoreName, key, winner, firstRead.ETag!, token)).ShouldBeTrue();
            (await second.TrySaveAsync(config.StateStoreName, key, stale, secondRead.ETag!, token)).ShouldBeFalse();
            (await second.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, key, token)).Value.ShouldBe(winner);
            (ReadOnlyMemory<byte> winnerBytes, _) = await secondClient.GetByteStateAndETagAsync(config.StateStoreName, key, cancellationToken: token);
            JsonSerializer.Deserialize<List<PartyMemoryUnitMappingEntry>>(winnerBytes.Span, secondClient.JsonSerializerOptions).ShouldBe(winner);

            ReadModelEntry<List<PartyMemoryUnitMappingEntry>> absentFirst = await first.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, absentKey, token);
            ReadModelEntry<List<PartyMemoryUnitMappingEntry>> absentSecond = await second.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, absentKey, token);
            absentFirst.Value.ShouldBeNull();
            absentSecond.Value.ShouldBeNull();
            absentFirst.ETag.ShouldBeNullOrEmpty();
            absentSecond.ETag.ShouldBeNullOrEmpty();
            Task<bool> createFirst = first.TrySaveAsync(config.StateStoreName, absentKey, winner, string.Empty, token);
            Task<bool> createSecond = second.TrySaveAsync(config.StateStoreName, absentKey, stale, string.Empty, token);
            bool[] created = await Task.WhenAll(createFirst, createSecond);
            created.Count(value => value).ShouldBe(1);
            List<PartyMemoryUnitMappingEntry> expected = created[0] ? winner : stale;
            (await first.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, absentKey, token)).Value.ShouldBe(expected);
            (ReadOnlyMemory<byte> createdBytes, string createdEtag) = await secondClient.GetByteStateAndETagAsync(config.StateStoreName, absentKey, cancellationToken: token);
            JsonSerializer.Deserialize<List<PartyMemoryUnitMappingEntry>>(createdBytes.Span, secondClient.JsonSerializerOptions).ShouldBe(expected);
            createdEtag.ShouldNotBeNullOrWhiteSpace();
        }
        catch
        {
            primaryFailed = true;
            throw;
        }
        finally
        {
            await CleanupOwnedKeysAsync(config, [key, absentKey], primaryFailed);
        }
    }

    /// <summary>Removes only this test's generated synthetic keys using fresh conditional erase evidence.</summary>
    private static async Task CleanupOwnedKeysAsync(PartyMemoryRuntimeConfiguration config, IReadOnlyList<string> keys, bool preservePrimaryFailure)
    {
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using DaprClient cleanupClient = Dapr(config);
            var sdk = new DaprReadModelStore(cleanupClient);
            foreach (string key in keys.Distinct(StringComparer.Ordinal))
            {
                bool erased = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    (bool present, string etag) = await sdk.TryReadEtagAsync(config.StateStoreName, key, budget.Token).ConfigureAwait(false);
                    if (!present || await sdk.TryEraseAsync(config.StateStoreName, key, etag, budget.Token).ConfigureAwait(false))
                    {
                        erased = true;
                        break;
                    }
                }

                if (!erased)
                {
                    throw new InvalidOperationException("Synthetic state cleanup exhausted conditional erase retries.");
                }
            }
        }
        catch (Exception exception) when (preservePrimaryFailure)
        {
            // Cleanup has its own budget and must not replace the original qualification failure.
            TestContext.Current.TestOutputHelper?.WriteLine($"Synthetic state cleanup remained pending ({exception.GetType().Name}); primary test failure preserved.");
        }
    }

    private static DaprClient Dapr(PartyMemoryRuntimeConfiguration config)
        => new DaprClientBuilder().UseHttpEndpoint(config.DaprHttpEndpoint).UseGrpcEndpoint(config.DaprGrpcEndpoint)
            .UseDaprApiToken(config.DaprApiToken).Build();

    private static PartyMemoryUnitMappingStore Mappings(IReadModelStore store, IOptions<PartyMemoryUnitMappingStoreOptions> options)
        => new(store, options, NullLogger<PartyMemoryUnitMappingStore>.Instance);
}
