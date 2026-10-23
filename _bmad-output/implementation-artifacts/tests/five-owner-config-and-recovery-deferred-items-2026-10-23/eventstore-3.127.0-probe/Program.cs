using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;
var store = new InMemoryReadModelStore();
var scope = new ReadModelBatchScope("statestore", "synthetic-tenant", "counter", "synthetic-aggregate", "detail", "synthetic-dispatch");
var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("model", new { Version = 1 }, ReadModelBatchConcurrency.CreateOnly)]);
store.BatchFaultHook = (phase, _, _) => phase is ReadModelBatchPhase.BeforeReceipt or ReadModelBatchPhase.Reconcile
    ? throw new IOException("synthetic interrupted receipt") : Task.CompletedTask;
var first = await store.ExecuteAsync(batch);
store.BatchFaultHook = null;
await store.SaveAsync("statestore", "model", new { Version = 99, Erased = true });
byte[] before = store.SnapshotRaw("statestore", "model")!;
var retry = await store.ExecuteAsync(batch);
byte[] after = store.SnapshotRaw("statestore", "model")!;
Console.WriteLine(JsonSerializer.Serialize(new { PackageVersion = "3.127.0", InitialStatus = first.Status.ToString(), RetryStatus = retry.Status.ToString(), SupersedingStatePreserved = before.SequenceEqual(after), LegacyFailureAliasesOnly = typeof(Hexalith.EventStore.Contracts.Queries.QueryAdapterFailureReason).GetField("QueryExecutionFailure") is null }));
