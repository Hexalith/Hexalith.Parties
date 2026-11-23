using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.PackageTests;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Runs the supported aggregate and shared backfill protocols in separately exiting workload processes.</summary>
public sealed class PartyLegacyBackfillProcessRuntimeTests
{
    private const string ManifestVariable = "PARTIES_BACKFILL_WORKLOAD_MANIFEST";
    private const string PhaseVariable = "PARTIES_BACKFILL_WORKLOAD_PHASE";
    private const string HashVariable = "PARTIES_BACKFILL_WORKLOAD_HASH";
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Requires durable canonical, erasure and survivor outcomes after independent seed/rebuild/stage/commit/verify processes.</summary>
    [Fact(Timeout = 300000)]
    public async Task ActualProviderBackfillSurvivesSeparateWorkloadProcesses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var configuration = await PartyLegacyBackfillRuntimeFixture.ConfigurationAsync(token).ConfigureAwait(true);
        string? selected = Environment.GetEnvironmentVariable(ManifestVariable);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            byte[] bytes = await File.ReadAllBytesAsync(selected, token).ConfigureAwait(true);
            string inputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            inputSha256.ShouldBe(Environment.GetEnvironmentVariable(HashVariable));
            var manifest = JsonSerializer.Deserialize<PartyLegacyBackfillWorkloadManifest>(bytes, s_json).ShouldNotBeNull();
            await RunPhaseAsync(configuration, manifest, selected, Environment.GetEnvironmentVariable(PhaseVariable).ShouldNotBeNull(), inputSha256, token).ConfigureAwait(true);
            return;
        }

        foreach (bool existing in new[] { false, true })
        {
            var fixture = new PartyLegacyBackfillRuntimeFixture(configuration);
            ProjectionRequest live = fixture.Histories.Single(history => history.AggregateId == "party-live");
            ProjectionRequest large = live with { AggregateId = "party-large", Events = [.. live.Events.Take(2),
                .. Enumerable.Range(3, 4095).Select(sequence => live.Events[^1] with
                { SequenceNumber = sequence, Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(sequence), MessageId = "message-" + sequence })] };
            var manifest = new PartyLegacyBackfillWorkloadManifest(fixture.Tenant, [.. fixture.Histories, large],
                "backfill-process-index-" + Guid.NewGuid().ToString("N"), existing, "backfill-process-large-" + Guid.NewGuid().ToString("N"));
            string directory = Path.Combine(Path.GetTempPath(), "parties-backfill-workloads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "input.json");
            byte[] immutable = JsonSerializer.SerializeToUtf8Bytes(manifest, s_json);
            string hash = Convert.ToHexStringLower(SHA256.HashData(immutable));
            Exception? primaryFailure = null;
            try
            {
                await File.WriteAllBytesAsync(path, immutable, token).ConfigureAwait(true);
                HashSet<int> workloads = [];
                foreach (string phase in new[] { "seed", "interrupt", "rebuild", "stage", "commit", "verify" })
                {
                    var start = new ProcessStartInfo("dotnet");
                    foreach (string argument in new[] { typeof(PartyLegacyBackfillProcessRuntimeTests).Assembly.Location,
                        "-method", typeof(PartyLegacyBackfillProcessRuntimeTests).FullName + "." + nameof(ActualProviderBackfillSurvivesSeparateWorkloadProcesses),
                        "-failSkips", "-result-xml", Path.Combine(directory, phase + ".xml") }) start.ArgumentList.Add(argument);
                    start.Environment[ManifestVariable] = path;
                    start.Environment[PhaseVariable] = phase;
                    start.Environment[HashVariable] = hash;
                    var result = PackageTestProcess.Run(start, directory, 120000);
                    await File.WriteAllTextAsync(Path.Combine(directory, phase + ".log"), result.Diagnostic, token).ConfigureAwait(true);
                    result.ExitCode.ShouldBe(0, result.Diagnostic);
                    var xml = System.Xml.Linq.XDocument.Load(Path.Combine(directory, phase + ".xml"));
                    var assembly = xml.Descendants("assembly").ShouldHaveSingleItem();
                    assembly.Attribute("total")!.Value.ShouldBe("1");
                    assembly.Attribute("passed")!.Value.ShouldBe("1");
                    assembly.Attribute("skipped")!.Value.ShouldBe("0");
                    using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, phase + ".json"), token).ConfigureAwait(true));
                    receipt.RootElement.GetProperty("tenant").GetString().ShouldBe(manifest.Tenant);
                    receipt.RootElement.GetProperty("operation").GetString().ShouldBe(manifest.IndexOperation);
                    receipt.RootElement.GetProperty("phase").GetString().ShouldBe(phase);
                    receipt.RootElement.GetProperty("inputSha256").GetString().ShouldBe(hash);
                    workloads.Add(receipt.RootElement.GetProperty("processId").GetInt32()).ShouldBeTrue();
                    (await File.ReadAllBytesAsync(path, token).ConfigureAwait(true)).ShouldBe(immutable);
                }
                workloads.Count.ShouldBe(6);
                workloads.ShouldNotContain(Environment.ProcessId);
                TestContext.Current.SendDiagnosticMessage($"separate workloads={workloads.Count}; existing={existing}; immutable input={hash}; 4097-record interrupted aggregate one-shot and shared stage/commit/verify passed; survivors=9");
            }
            catch (Exception failure) { primaryFailure = failure; throw; }
            finally
            {
                CompleteWorkloadCleanup(() =>
                {
                    string? evidence = Environment.GetEnvironmentVariable("PARTIES_BACKFILL_WORKLOAD_EVIDENCE");
                    if (!string.IsNullOrWhiteSpace(evidence))
                    {
                        string retained = Path.Combine(evidence, manifest.Tenant);
                        Directory.CreateDirectory(retained);
                        foreach (string file in Directory.EnumerateFiles(directory)) File.Copy(file, Path.Combine(retained, Path.GetFileName(file)), overwrite: false);
                    }
                }, () => Directory.Delete(directory, recursive: true), primaryFailure);
            }
        }
    }

    /// <summary>Always deletes scratch and retains qualification, evidence retention and deletion failures.</summary>
    internal static void CompleteWorkloadCleanup(Action retain, Action delete, Exception? primary)
    {
        List<Exception> failures = [];
        try { retain(); }
        catch (Exception failure) { failures.Add(failure); }
        finally
        {
            try { delete(); }
            catch (Exception failure) { failures.Add(failure); }
        }
        if (failures.Count == 0) { return; }
        if (primary is not null) { failures.Insert(0, primary); }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        throw new AggregateException("Backfill qualification or retained evidence cleanup failed.", failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvidenceRetentionFailureStillDeletesScratchAndPreservesPrimary(bool qualificationFailed)
    {
        string directory = Path.Combine(Path.GetTempPath(), "backfill-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "evidence.json");
        File.WriteAllText(source, "{}");
        var primary = qualificationFailed ? new InvalidOperationException("synthetic qualification failure") : null;
        Action operation = () => CompleteWorkloadCleanup(() => File.Copy(source, Path.Combine(directory, "absent", "evidence.json")),
            () => Directory.Delete(directory, recursive: true), primary);
        if (qualificationFailed)
        {
            var observed = Should.Throw<AggregateException>(operation);
            observed.InnerExceptions[0].ShouldBeSameAs(primary);
            observed.InnerExceptions[1].ShouldBeOfType<DirectoryNotFoundException>();
        }
        else { Should.Throw<DirectoryNotFoundException>(operation); }
        Directory.Exists(directory).ShouldBeFalse();
    }

    private static async Task RunPhaseAsync(PartyLegacyBackfillRuntimeConfiguration configuration, PartyLegacyBackfillWorkloadManifest manifest,
        string path, string phase, string inputSha256, CancellationToken token)
    {
        string directory = Path.GetDirectoryName(path)!;
        var fixture = new PartyLegacyBackfillRuntimeFixture(configuration, manifest.Tenant, manifest.Histories);
        string survivorsPath = Path.Combine(directory, "survivors.json");
        string indexKey = PartySdkReadModelAddresses.Index(manifest.Tenant);
        ProjectionRequest large = manifest.Histories.Single(history => history.AggregateId == "party-large");
        string detailKey = PartySdkReadModelAddresses.Detail(manifest.Tenant, large.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(manifest.Tenant, large.AggregateId);
        string recoveryPath = Path.Combine(directory, "generation.json");
        if (phase == "seed")
        {
            await fixture.SeedAsync(token).ConfigureAwait(true);
            fixture.Survivors.Count.ShouldBe(9);
            await File.WriteAllBytesAsync(survivorsPath, JsonSerializer.SerializeToUtf8Bytes(fixture.Survivors, s_json), token).ConfigureAwait(true);
            if (manifest.Existing)
            {
                foreach (var history in manifest.Histories)
                {
                    await fixture.SaveAsync(PartySdkReadModelAddresses.Detail(manifest.Tenant, history.AggregateId), new PartyDetailSdkReadModel { ProjectionVersion = "previous-canonical" }, token).ConfigureAwait(true);
                    await fixture.SaveAsync(PartySdkReadModelAddresses.Processing(manifest.Tenant, history.AggregateId), new PartyProcessingSdkReadModel { ProjectionVersion = "previous-canonical" }, token).ConfigureAwait(true);
                }
                await fixture.SaveAsync(indexKey, new PartyIndexSdkReadModel { ProjectionVersion = "previous-canonical" }, token).ConfigureAwait(true);
            }
            await fixture.SaveAsync(detailKey, PartyDetailSdkProjectionHandler.Fold(large with { Events = large.Events.Take(1).ToArray() }, null), token).ConfigureAwait(true);
            await fixture.SaveAsync(processingKey, PartyProcessingActivityFold.Fold(large, null), token).ConfigureAwait(true);
            await File.WriteAllBytesAsync(Path.Combine(directory, "canonical.json"), JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, byte[]>
            {
                [detailKey] = await fixture.RawAsync(detailKey, token).ConfigureAwait(true),
                [processingKey] = await fixture.RawAsync(processingKey, token).ConfigureAwait(true),
            }, s_json), token).ConfigureAwait(true);
        }
        else if (phase == "interrupt")
        {
            var batches = new List<ReadModelBatch>();
            using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(token);
            await Should.ThrowAsync<OperationCanceledException>(() => fixture.DetailAsync(large, manifest.LargeOperation, "rebuild", interrupted.Token,
                beforeBatch: (batch, cancellation) =>
                {
                    if (IsGeneration(batch))
                    {
                        batches.Add(batch);
                        if (batches.Count == 3) { interrupted.Cancel(); cancellation.ThrowIfCancellationRequested(); }
                    }
                    return Task.CompletedTask;
                })).ConfigureAwait(true);
            batches.Count.ShouldBe(3);
            batches.ShouldAllBe(batch => batch.Operations.Count <= 32);
            string firstKey = batches[0].Operations[0].Key;
            string generation = firstKey[(processingKey.Length + 1)..firstKey.LastIndexOf('.')];
            byte[] first = await fixture.RawAsync(firstKey, token).ConfigureAwait(true);
            var prior = new Dictionary<string, byte[]>();
            foreach (ReadModelBatchOperation operation in batches.Take(2).SelectMany(batch => batch.Operations))
                prior.Add(operation.Key, await fixture.RawAsync(operation.Key, token).ConfigureAwait(true));
            prior.Count.ShouldBe(64);
            (await fixture.ReadAsync<PartyProcessingBucket>(firstKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
            (await fixture.ReadAsync<PartyProcessingBucket>(PartySdkReadModelAddresses.ProcessingBucket(manifest.Tenant,
                large.AggregateId, generation, PartyProcessingBucket.Number(4097)), token).ConfigureAwait(true)).Value.ShouldBeNull();
            var before = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(await File.ReadAllBytesAsync(Path.Combine(directory, "canonical.json"), token).ConfigureAwait(true), s_json)!;
            foreach (var canonical in before) (await fixture.RawAsync(canonical.Key, token).ConfigureAwait(true)).ShouldBe(canonical.Value);
            var previous = (await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
            previous.LastSequenceNumber.ShouldBe(1);
            previous.Detail.ShouldNotBeNull().DisplayName.ShouldNotBe("Synthetic Updated");
            await File.WriteAllBytesAsync(recoveryPath, JsonSerializer.SerializeToUtf8Bytes(new
            { generation, firstKey, first, prior, operation = manifest.LargeOperation, batches = batches.Select(batch => batch.Scope.BatchId).ToArray(), interruptedProcess = Environment.ProcessId }, s_json), token).ConfigureAwait(true);
        }
        else if (phase == "rebuild")
        {
            foreach (var history in manifest.Histories)
            {
                var batches = new List<ReadModelBatch>();
                (await fixture.DetailAsync(history, history == large ? manifest.LargeOperation : "backfill-process-" + history.AggregateId, "rebuild", token,
                    beforeBatch: (batch, _) => { batches.Add(batch); return Task.CompletedTask; }).ConfigureAwait(true))
                    .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
                if (history == large)
                {
                    using JsonDocument recovery = JsonDocument.Parse(await File.ReadAllBytesAsync(recoveryPath, token).ConfigureAwait(true));
                    JsonElement retained = recovery.RootElement;
                    retained.GetProperty("operation").GetString().ShouldBe(manifest.LargeOperation);
                    retained.GetProperty("interruptedProcess").GetInt32().ShouldNotBe(Environment.ProcessId);
                    batches.Where(IsGeneration).Select(batch => batch.Scope.BatchId).Take(2)
                        .ShouldBe(retained.GetProperty("batches").EnumerateArray().Take(2).Select(item => item.GetString()));
                    batches.Where(IsGeneration).ShouldAllBe(batch => batch.Operations.Count <= 32);
                    batches.Last().Operations.Select(operation => operation.Key).Order(StringComparer.Ordinal).ShouldBe(new[] { detailKey, processingKey }.Order(StringComparer.Ordinal));
                    (await fixture.RawAsync(retained.GetProperty("firstKey").GetString()!, token).ConfigureAwait(true)).ShouldBe(retained.GetProperty("first").GetBytesFromBase64());
                    foreach (JsonProperty bucket in retained.GetProperty("prior").EnumerateObject())
                        (await fixture.RawAsync(bucket.Name, token).ConfigureAwait(true)).ShouldBe(bucket.Value.GetBytesFromBase64());
                    (await fixture.ReadAsync<PartyProcessingSdkReadModel>(processingKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull()
                        .BucketGeneration.ShouldBe(retained.GetProperty("generation").GetString());
                    var before = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(await File.ReadAllBytesAsync(Path.Combine(directory, "canonical.json"), token).ConfigureAwait(true), s_json)!;
                    (await fixture.RawAsync(detailKey, token).ConfigureAwait(true)).ShouldNotBe(before[detailKey]);
                    var completedDetail = (await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
                    JsonSerializer.Serialize(completedDetail, PartiesJsonOptions.Default)
                        .ShouldBe(JsonSerializer.Serialize(PartyDetailSdkProjectionHandler.Fold(large, null), PartiesJsonOptions.Default));
                }
            }
        }
        else if (phase == "stage")
        {
            (await fixture.PrepareIndexAsync(manifest.IndexOperation, token).ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await fixture.IndexAsync(manifest.IndexOperation, DomainSharedProjectionRebuildAction.Stage, token).ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            var staged = (await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token).ConfigureAwait(true)).Value;
            if (manifest.Existing) staged.ShouldNotBeNull().ProjectionVersion.ShouldBe("previous-canonical");
            else staged.ShouldBeNull();
        }
        else if (phase == "commit")
            (await fixture.IndexAsync(manifest.IndexOperation, DomainSharedProjectionRebuildAction.Commit, token).ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        else if (phase == "verify")
        {
            (await fixture.IndexAsync(manifest.IndexOperation, DomainSharedProjectionRebuildAction.Verify, token).ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            foreach (var history in manifest.Histories)
            {
                var detail = (await fixture.ReadAsync<PartyDetailSdkReadModel>(PartySdkReadModelAddresses.Detail(manifest.Tenant, history.AggregateId), token).ConfigureAwait(true)).Value.ShouldNotBeNull();
                var processing = (await fixture.ReadAsync<PartyProcessingSdkReadModel>(PartySdkReadModelAddresses.Processing(manifest.Tenant, history.AggregateId), token).ConfigureAwait(true)).Value.ShouldNotBeNull();
                detail.LastSequenceNumber.ShouldBe(history.Events[^1].SequenceNumber);
                processing.LastSequenceNumber.ShouldBe(history.Events[^1].SequenceNumber);
                if (history.AggregateId == "party-erased")
                {
                    detail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
                    detail.Detail.DisplayName.ShouldBeEmpty();
                    detail.Detail.PersonDetails.ShouldBeNull();
                    detail.ErasureSequenceNumber.ShouldBe(2);
                    processing.ErasureSequenceNumber.ShouldBe(2);
                    processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2]);
                }
                else
                {
                    detail.Detail.ShouldNotBeNull().DisplayName.ShouldBe("Synthetic Updated");
                    detail.Detail.IsActive.ShouldBeFalse();
                    if (history == large)
                    {
                        processing.RecordCount.ShouldBe(4097);
                        processing.Records.ShouldBeEmpty();
                        using JsonDocument recovery = JsonDocument.Parse(await File.ReadAllBytesAsync(recoveryPath, token).ConfigureAwait(true));
                        string generation = recovery.RootElement.GetProperty("generation").GetString()!;
                        processing.BucketGeneration.ShouldBe(generation);
                        var records = new List<ProcessingActivityRecord>();
                        for (long number = 0; number <= PartyProcessingBucket.Number(4097); number++)
                        {
                            var bucket = (await fixture.ReadAsync<PartyProcessingBucket>(PartySdkReadModelAddresses.ProcessingBucket(manifest.Tenant,
                                large.AggregateId, generation, number), token).ConfigureAwait(true)).Value.ShouldNotBeNull();
                            bucket.Records.Count.ShouldBeLessThanOrEqualTo(PartyProcessingBucket.Capacity);
                            records.AddRange(bucket.Records);
                        }
                        records.Select(record => record.SequenceNumber).ShouldBe(Enumerable.Range(1, 4097).Select(sequence => (long)sequence));
                        JsonSerializer.Serialize(records, PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(PartyProcessingActivityFold.Fold(large, null).Records, PartiesJsonOptions.Default));
                        JsonSerializer.Serialize(detail, PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(PartyDetailSdkProjectionHandler.Fold(large, null), PartiesJsonOptions.Default));
                    }
                    else processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2, 3]);
                }
            }
            var index = (await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
            index.Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-large", "party-live"]);
            index.Entries["party-live"].DisplayName.ShouldBe("Synthetic Updated");
            index.Entries["party-live"].IsActive.ShouldBeFalse();
            index.LastSequenceNumbers["party-live"].ShouldBe(3);
            index.LastSequenceNumbers["party-large"].ShouldBe(4097);
            index.ErasureSequenceNumbers["party-erased"].ShouldBe(2);
        }
        else throw new InvalidOperationException("Unknown backfill workload phase.");
        var survivors = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(await File.ReadAllBytesAsync(survivorsPath, token).ConfigureAwait(true), s_json).ShouldNotBeNull();
        survivors.Count.ShouldBe(9);
        foreach (var survivor in survivors) (await fixture.RawAsync(survivor.Key, token).ConfigureAwait(true)).ShouldBe(survivor.Value);
        await File.WriteAllBytesAsync(Path.Combine(directory, phase + ".json"), JsonSerializer.SerializeToUtf8Bytes(new
        { tenant = manifest.Tenant, operation = manifest.IndexOperation, phase, inputSha256, processId = Environment.ProcessId }, s_json), token).ConfigureAwait(true);
    }

    private static bool IsGeneration(ReadModelBatch batch) => batch.Scope.ProjectionType.EndsWith(".generation", StringComparison.Ordinal);
}
