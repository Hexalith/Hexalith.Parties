using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Xml.Linq;

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
    private const string ReceiptPrefix = "Backfill workload receipt (base64): ";
    private const string CanonicalSnapshot = "canonical.json";
    private const string GenerationSnapshot = "generation.json";
    private const string SurvivorSnapshot = "survivors.json";
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private static string WorkloadMethod => typeof(PartyLegacyBackfillProcessRuntimeTests).FullName + "." + nameof(ActualProviderBackfillSurvivesSeparateWorkloadProcesses);

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
                int interruptedProcess = 0;
                Dictionary<string, string> retainedDigests = new(StringComparer.Ordinal);
                foreach (string phase in new[] { "seed", "interrupt", "rebuild", "stage", "commit", "verify" })
                {
                    var start = new ProcessStartInfo("dotnet");
                    foreach (string argument in new[] { typeof(PartyLegacyBackfillProcessRuntimeTests).Assembly.Location,
                        "-method", WorkloadMethod,
                        "-failSkips", "-result-xml", Path.Combine(directory, phase + ".xml") }) start.ArgumentList.Add(argument);
                    start.Environment[ManifestVariable] = path;
                    start.Environment[PhaseVariable] = phase;
                    start.Environment[HashVariable] = hash;
                    var result = PackageTestProcess.Run(start, directory, 120000);
                    await File.WriteAllTextAsync(Path.Combine(directory, phase + ".log"), result.Diagnostic, token).ConfigureAwait(true);
                    result.ExitCode.ShouldBe(0, result.Diagnostic);
                    byte[] receiptBytes = await File.ReadAllBytesAsync(Path.Combine(directory, phase + ".json"), token).ConfigureAwait(true);
                    VerifyWorkloadReport(XDocument.Load(Path.Combine(directory, phase + ".xml")), receiptBytes);
                    using var receipt = JsonDocument.Parse(receiptBytes);
                    receipt.RootElement.GetProperty("tenant").GetString().ShouldBe(manifest.Tenant);
                    receipt.RootElement.GetProperty("operation").GetString().ShouldBe(manifest.IndexOperation);
                    receipt.RootElement.GetProperty("phase").GetString().ShouldBe(phase);
                    receipt.RootElement.GetProperty("inputSha256").GetString().ShouldBe(hash);
                    int processId = receipt.RootElement.GetProperty("processId").GetInt32();
                    workloads.Add(processId).ShouldBeTrue();
                    if (phase == "interrupt")
                    {
                        interruptedProcess = processId;
                    }
                    JsonProperty[] observations = receipt.RootElement.GetProperty("snapshotSha256").EnumerateObject().ToArray();
                    string[] expectedSnapshots = phase switch
                    {
                        "seed" => [SurvivorSnapshot, CanonicalSnapshot],
                        "interrupt" or "rebuild" => [SurvivorSnapshot, CanonicalSnapshot, GenerationSnapshot],
                        "verify" => [SurvivorSnapshot, GenerationSnapshot],
                        _ => [SurvivorSnapshot],
                    };
                    observations.Select(item => item.Name).ShouldBe(expectedSnapshots, ignoreOrder: true);
                    foreach (JsonProperty observation in observations)
                    {
                        string digest = observation.Value.GetString().ShouldNotBeNull();
                        if (!retainedDigests.TryAdd(observation.Name, digest))
                        {
                            digest.ShouldBe(retainedDigests[observation.Name], $"Snapshot changed between workload observations: {observation.Name}");
                        }
                    }
                    (await File.ReadAllBytesAsync(path, token).ConfigureAwait(true)).ShouldBe(immutable);
                }
                foreach ((string name, string digest) in retainedDigests)
                {
                    byte[] snapshot = await File.ReadAllBytesAsync(Path.Combine(directory, name), token).ConfigureAwait(true);
                    SnapshotDigest(snapshot).ShouldBe(digest, $"Retained snapshot differs from workload observations: {name}");
                    if (name == GenerationSnapshot)
                    {
                        using JsonDocument generation = JsonDocument.Parse(snapshot);
                        generation.RootElement.GetProperty("interruptedProcess").GetInt32().ShouldBe(interruptedProcess);
                    }
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

    [Theory]
    [InlineData("phase")]
    [InlineData("target")]
    [InlineData("nested output")]
    [InlineData("identity")]
    [InlineData("counts")]
    public void WorkloadReportRefusesSubstitutedOrMalformedEvidence(string defect)
    {
        byte[] receipt = """{"tenant":"test-tenant","phase":"stage"}"""u8.ToArray();
        XDocument xml = XDocument.Parse($"""
            <assemblies>
              <assembly name="{typeof(PartyLegacyBackfillProcessRuntimeTests).Assembly.GetName().Name}.dll" errors="0" total="1" passed="1" failed="0" skipped="0" not-run="0">
                <collection total="1" passed="1" failed="0" skipped="0" not-run="0">
                  <test type="{typeof(PartyLegacyBackfillProcessRuntimeTests).FullName}" method="{nameof(ActualProviderBackfillSurvivesSeparateWorkloadProcesses)}" name="{WorkloadMethod}" result="Pass">
                    <output>{ReceiptPrefix}{Convert.ToBase64String(receipt)}</output>
                  </test>
                </collection>
              </assembly>
            </assemblies>
            """);
        VerifyWorkloadReport(xml, receipt);

        XElement test = xml.Descendants("test").ShouldHaveSingleItem();
        XElement output = test.Element("output").ShouldNotBeNull();
        switch (defect)
        {
            case "phase":
                output.Value = ReceiptPrefix + Convert.ToBase64String("""{"tenant":"test-tenant","phase":"seed"}"""u8);
                break;
            case "target":
                output.Value = ReceiptPrefix + Convert.ToBase64String("""{"tenant":"other-tenant","phase":"stage"}"""u8);
                break;
            case "nested output":
                output.ReplaceNodes(new XElement("nested", output.Value));
                break;
            case "identity":
                test.SetAttributeValue("method", "OtherMethod");
                break;
            case "counts":
                test.Parent.ShouldNotBeNull().SetAttributeValue("failed", "1");
                break;
        }

        Should.Throw<ShouldAssertException>(() => VerifyWorkloadReport(xml, receipt));
    }

    private static void VerifyWorkloadReport(XDocument report, byte[] receipt)
    {
        XElement root = report.Root.ShouldNotBeNull();
        root.Name.ShouldBe(XName.Get("assemblies"));
        XElement assembly = root.Elements("assembly").ShouldHaveSingleItem();
        Path.GetFileName((string?)assembly.Attribute("name")).ShouldBe(typeof(PartyLegacyBackfillProcessRuntimeTests).Assembly.GetName().Name + ".dll");
        ((string?)assembly.Attribute("errors")).ShouldBe("0");
        assembly.Elements("errors").Elements("error").ShouldBeEmpty();
        VerifyPassCounts(assembly, 1);

        XElement test = assembly.Elements("collection").Elements("test").ShouldHaveSingleItem();
        root.Descendants("test").ShouldHaveSingleItem();
        foreach (XElement collection in assembly.Elements("collection"))
        {
            VerifyPassCounts(collection, collection.Elements("test").Count());
        }

        ((string?)test.Attribute("type")).ShouldBe(typeof(PartyLegacyBackfillProcessRuntimeTests).FullName);
        ((string?)test.Attribute("method")).ShouldBe(nameof(ActualProviderBackfillSurvivesSeparateWorkloadProcesses));
        ((string?)test.Attribute("name")).ShouldBe(WorkloadMethod);
        ((string?)test.Attribute("result")).ShouldBe("Pass");
        XElement[] outputs = test.Elements("output").ToArray();
        outputs.ShouldAllBe(output => !output.HasElements);
        outputs.SelectMany(output => output.Value.ReplaceLineEndings("\n").Split('\n'))
            .Where(line => line.StartsWith(ReceiptPrefix, StringComparison.Ordinal))
            .ShouldHaveSingleItem().ShouldBe(ReceiptPrefix + Convert.ToBase64String(receipt));

        static void VerifyPassCounts(XElement node, int count)
        {
            string passed = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ((string?)node.Attribute("total")).ShouldBe(passed);
            ((string?)node.Attribute("passed")).ShouldBe(passed);
            foreach (string field in new[] { "failed", "skipped", "not-run" })
            {
                ((string?)node.Attribute(field)).ShouldBe("0");
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
        Dictionary<string, string> snapshotSha256 = new(StringComparer.Ordinal);
        string survivorsPath = Path.Combine(directory, SurvivorSnapshot);
        string canonicalPath = Path.Combine(directory, CanonicalSnapshot);
        string indexKey = PartySdkReadModelAddresses.Index(manifest.Tenant);
        ProjectionRequest large = manifest.Histories.Single(history => history.AggregateId == "party-large");
        string detailKey = PartySdkReadModelAddresses.Detail(manifest.Tenant, large.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(manifest.Tenant, large.AggregateId);
        string recoveryPath = Path.Combine(directory, GenerationSnapshot);
        byte[]? seededSurvivors = null;
        if (phase == "seed")
        {
            await fixture.SeedAsync(token).ConfigureAwait(true);
            fixture.Survivors.Count.ShouldBe(9);
            seededSurvivors = JsonSerializer.SerializeToUtf8Bytes(fixture.Survivors, s_json);
            await File.WriteAllBytesAsync(survivorsPath, seededSurvivors, token).ConfigureAwait(true);
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
            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, byte[]>
            {
                [detailKey] = await fixture.RawAsync(detailKey, token).ConfigureAwait(true),
                [processingKey] = await fixture.RawAsync(processingKey, token).ConfigureAwait(true),
            }, s_json);
            await File.WriteAllBytesAsync(canonicalPath, canonical, token).ConfigureAwait(true);
            snapshotSha256.Add(CanonicalSnapshot, SnapshotDigest(canonical));
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
            byte[] canonical = await File.ReadAllBytesAsync(canonicalPath, token).ConfigureAwait(true);
            var before = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(canonical, s_json).ShouldNotBeNull();
            before.Keys.ShouldBe([detailKey, processingKey], ignoreOrder: true);
            foreach (var entry in before) (await fixture.RawAsync(entry.Key, token).ConfigureAwait(true)).ShouldBe(entry.Value);
            snapshotSha256.Add(CanonicalSnapshot, SnapshotDigest(canonical));
            var previous = (await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
            previous.LastSequenceNumber.ShouldBe(1);
            previous.Detail.ShouldNotBeNull().DisplayName.ShouldNotBe("Synthetic Updated");
            byte[] interruptedGeneration = JsonSerializer.SerializeToUtf8Bytes(new
            { generation, firstKey, first, prior, operation = manifest.LargeOperation, batches = batches.Select(batch => batch.Scope.BatchId).ToArray(), interruptedProcess = Environment.ProcessId }, s_json);
            await File.WriteAllBytesAsync(recoveryPath, interruptedGeneration, token).ConfigureAwait(true);
            snapshotSha256.Add(GenerationSnapshot, SnapshotDigest(interruptedGeneration));
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
                    JsonElement retained = await ObserveGenerationAsync().ConfigureAwait(true);
                    batches.Where(IsGeneration).Select(batch => batch.Scope.BatchId).Take(2)
                        .ShouldBe(retained.GetProperty("batches").EnumerateArray().Take(2).Select(item => item.GetString()));
                    batches.Where(IsGeneration).ShouldAllBe(batch => batch.Operations.Count <= 32);
                    batches.Last().Operations.Select(operation => operation.Key).Order(StringComparer.Ordinal).ShouldBe(new[] { detailKey, processingKey }.Order(StringComparer.Ordinal));
                    (await fixture.ReadAsync<PartyProcessingSdkReadModel>(processingKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull()
                        .BucketGeneration.ShouldBe(retained.GetProperty("generation").GetString());
                    byte[] canonical = await File.ReadAllBytesAsync(canonicalPath, token).ConfigureAwait(true);
                    var before = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(canonical, s_json).ShouldNotBeNull();
                    (await fixture.RawAsync(detailKey, token).ConfigureAwait(true)).ShouldNotBe(before[detailKey]);
                    snapshotSha256.Add(CanonicalSnapshot, SnapshotDigest(canonical));
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
                        JsonElement retained = await ObserveGenerationAsync().ConfigureAwait(true);
                        string generation = retained.GetProperty("generation").GetString()!;
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
        byte[] survivorBytes = await File.ReadAllBytesAsync(survivorsPath, token).ConfigureAwait(true);
        if (seededSurvivors is not null)
        {
            survivorBytes.ShouldBe(seededSurvivors);
        }

        var survivors = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(survivorBytes, s_json).ShouldNotBeNull();
        survivors.Count.ShouldBe(9);
        foreach (var survivor in survivors) (await fixture.RawAsync(survivor.Key, token).ConfigureAwait(true)).ShouldBe(survivor.Value);
        snapshotSha256.Add(SurvivorSnapshot, SnapshotDigest(survivorBytes));
        byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
        { tenant = manifest.Tenant, operation = manifest.IndexOperation, phase, inputSha256, processId = Environment.ProcessId, snapshotSha256 }, s_json);
        await File.WriteAllBytesAsync(Path.Combine(directory, phase + ".json"), receipt, token).ConfigureAwait(true);
        // Base64 preserves the exact JSON bytes through xUnit's XML text escaping.
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().WriteLine(ReceiptPrefix + Convert.ToBase64String(receipt));

        async Task<JsonElement> ObserveGenerationAsync()
        {
            byte[] snapshot = await File.ReadAllBytesAsync(recoveryPath, token).ConfigureAwait(true);
            JsonElement retained = JsonSerializer.Deserialize<JsonElement>(snapshot, s_json);
            retained.GetProperty("operation").GetString().ShouldBe(manifest.LargeOperation);
            retained.GetProperty("interruptedProcess").GetInt32().ShouldNotBe(Environment.ProcessId);
            var prior = retained.GetProperty("prior");
            prior.EnumerateObject().Count().ShouldBe(64);
            prior.GetProperty(retained.GetProperty("firstKey").GetString().ShouldNotBeNull()).GetBytesFromBase64()
                .ShouldBe(retained.GetProperty("first").GetBytesFromBase64());
            foreach (JsonProperty bucket in prior.EnumerateObject())
            {
                (await fixture.RawAsync(bucket.Name, token).ConfigureAwait(true)).ShouldBe(bucket.Value.GetBytesFromBase64());
            }

            snapshotSha256.Add(GenerationSnapshot, SnapshotDigest(snapshot));
            return retained;
        }
    }

    private static string SnapshotDigest(byte[] snapshot) => Convert.ToHexStringLower(SHA256.HashData(snapshot));

    private static bool IsGeneration(ReadModelBatch batch) => batch.Scope.ProjectionType.EndsWith(".generation", StringComparison.Ordinal);
}
