using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

// Run only after copying this standalone project outside the repository, so its
// exact old packages cannot inherit or change the current central package graph.
const string storeName = "statestore";
const string detailKey = "readmodel:tenant-a:party:party-detail:party-1:detail";
const string processingKey = "readmodel:tenant-a:party:party-detail:party-1:processing-records";
var scope = new ReadModelBatchScope(storeName, "tenant-a", "party", "party-1", "party-detail", "legacy-3127");
// Independent canonical inputs use the pre-generation public model shape. No
// Parties writer, current producer source or current SDK participates in capture.
JsonObject detail = JsonNode.Parse("""
    {"detail":{"id":"party-1","type":1,"displayName":"Historical synthetic","sortName":"historical synthetic","isActive":true,"createdAt":"1970-01-01T00:00:01+00:00","lastModifiedAt":"1970-01-01T00:00:01+00:00"},"lastSequenceNumber":1,"erasureSequenceNumber":null,"erasedAt":null,"projectedAt":"1970-01-01T00:00:01+00:00","projectionVersion":"1"}
    """)!.AsObject();
JsonObject processing = JsonNode.Parse("""
    {"records":[{"sequenceNumber":1,"partyId":"party-1","tenantId":"tenant-a","actorId":"unknown","correlationId":"correlation","operationCategory":"DomainEvent","outcome":"Succeeded","eventType":"PartyCreated","timestamp":"1970-01-01T00:00:01+00:00","summary":"Synthetic historical creation"}],"lastSequenceNumber":1,"erasureSequenceNumber":null,"erasedAt":null,"projectedAt":"1970-01-01T00:00:01+00:00","projectionVersion":"1"}
    """)!.AsObject();
var batch = new ReadModelBatch(scope,
[
    ReadModelBatchOperation.Write(detailKey, detail, ReadModelBatchConcurrency.CreateOnly),
    ReadModelBatchOperation.Write(processingKey, processing, ReadModelBatchConcurrency.CreateOnly),
]);
if (batch.RetainManifestForRecovery) throw new InvalidOperationException("Expected historical non-retained contract.");
var store = new InMemoryReadModelStore();
bool interrupted = false;
store.BatchFaultHook = (phase, _, _) =>
{
    if (phase == ReadModelBatchPhase.AfterCommit) interrupted = true;
    return phase is ReadModelBatchPhase.AfterCommit or ReadModelBatchPhase.Reconcile
        ? throw new IOException("Pinned old SDK interrupted after commit") : Task.CompletedTask;
};
ReadModelBatchResult result = await store.ExecuteAsync(batch);
if (!interrupted || result.Status != ReadModelBatchStatus.Indeterminate) throw new InvalidOperationException("Fault did not reach committed state.");
string[] keys = [scope.ComputeMarkerKey(), detailKey, processingKey];
var entries = keys.ToDictionary(key => key, key => Convert.ToBase64String(store.SnapshotRaw(storeName, key)
    ?? throw new InvalidOperationException("Missing old serialized row.")));
if (store.Count != entries.Count) throw new InvalidOperationException("Export omitted a physical protocol row.");
var fixture = new
{
    sdkVersion = "3.127.0", writer = "Published Hexalith.EventStore.Client/Testing; independent JSON inputs",
    clientAssembly = typeof(ReadModelBatch).Assembly.GetName().FullName,
    clientInformationalVersion = typeof(ReadModelBatch).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    markerKey = scope.ComputeMarkerKey(), stateCount = store.Count, entries,
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(fixture, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Captured {entries.Count} exact rows from {fixture.clientAssembly}; fault reached AfterCommit.");
