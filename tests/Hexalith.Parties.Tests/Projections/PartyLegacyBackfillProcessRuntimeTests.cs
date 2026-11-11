using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.PackageTests;
using Hexalith.Parties.Projections.Models;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Runs the supported aggregate and shared backfill protocols in separately exiting workload processes.</summary>
public sealed class PartyLegacyBackfillProcessRuntimeTests
{
    private const string ManifestVariable = "PARTIES_BACKFILL_WORKLOAD_MANIFEST";
    private const string PhaseVariable = "PARTIES_BACKFILL_WORKLOAD_PHASE";
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
            Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(Environment.GetEnvironmentVariable("PARTIES_BACKFILL_WORKLOAD_HASH"));
            var manifest = JsonSerializer.Deserialize<PartyLegacyBackfillWorkloadManifest>(bytes, s_json).ShouldNotBeNull();
            await RunPhaseAsync(configuration, manifest, selected, Environment.GetEnvironmentVariable(PhaseVariable).ShouldNotBeNull(), token).ConfigureAwait(true);
            return;
        }

        foreach (bool existing in new[] { false, true })
        {
            var fixture = new PartyLegacyBackfillRuntimeFixture(configuration);
            var manifest = new PartyLegacyBackfillWorkloadManifest(fixture.Tenant, fixture.Histories, "backfill-process-index-" + Guid.NewGuid().ToString("N"), existing);
            string directory = Path.Combine(Path.GetTempPath(), "parties-backfill-workloads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "input.json");
            byte[] immutable = JsonSerializer.SerializeToUtf8Bytes(manifest, s_json);
            await File.WriteAllBytesAsync(path, immutable, token).ConfigureAwait(true);
            string hash = Convert.ToHexStringLower(SHA256.HashData(immutable));
            try
            {
                HashSet<int> workloads = [];
                foreach (string phase in new[] { "seed", "rebuild", "stage", "commit", "verify" })
                {
                    var start = new ProcessStartInfo("dotnet");
                    foreach (string argument in new[] { typeof(PartyLegacyBackfillProcessRuntimeTests).Assembly.Location,
                        "-method", typeof(PartyLegacyBackfillProcessRuntimeTests).FullName + "." + nameof(ActualProviderBackfillSurvivesSeparateWorkloadProcesses),
                        "-failSkips", "-result-xml", Path.Combine(directory, phase + ".xml") }) start.ArgumentList.Add(argument);
                    start.Environment[ManifestVariable] = path;
                    start.Environment[PhaseVariable] = phase;
                    start.Environment["PARTIES_BACKFILL_WORKLOAD_HASH"] = hash;
                    var result = PackageTestProcess.Run(start, directory, 60000);
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
                    workloads.Add(receipt.RootElement.GetProperty("processId").GetInt32()).ShouldBeTrue();
                    (await File.ReadAllBytesAsync(path, token).ConfigureAwait(true)).ShouldBe(immutable);
                }
                workloads.Count.ShouldBe(5);
                workloads.ShouldNotContain(Environment.ProcessId);
                TestContext.Current.SendDiagnosticMessage($"separate workloads={workloads.Count}; existing={existing}; immutable input={hash}; aggregate one-shot and shared stage/commit/verify passed; survivors=9");
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    private static async Task RunPhaseAsync(PartyLegacyBackfillRuntimeConfiguration configuration, PartyLegacyBackfillWorkloadManifest manifest,
        string path, string phase, CancellationToken token)
    {
        string directory = Path.GetDirectoryName(path)!;
        var fixture = new PartyLegacyBackfillRuntimeFixture(configuration, manifest.Tenant, manifest.Histories);
        string survivorsPath = Path.Combine(directory, "survivors.json");
        string indexKey = PartySdkReadModelAddresses.Index(manifest.Tenant);
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
        }
        else if (phase == "rebuild")
        {
            foreach (var history in manifest.Histories)
                (await fixture.DetailAsync(history, "backfill-process-" + history.AggregateId, "rebuild", token).ConfigureAwait(true))
                    .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
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
                detail.LastSequenceNumber.ShouldBe(3);
                processing.LastSequenceNumber.ShouldBe(3);
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
                    processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2, 3]);
                }
            }
            var index = (await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
            index.Entries.Keys.ShouldBe(["party-live"]);
            index.Entries["party-live"].DisplayName.ShouldBe("Synthetic Updated");
            index.Entries["party-live"].IsActive.ShouldBeFalse();
            index.LastSequenceNumbers["party-live"].ShouldBe(3);
            index.ErasureSequenceNumbers["party-erased"].ShouldBe(2);
        }
        else throw new InvalidOperationException("Unknown backfill workload phase.");
        var survivors = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(await File.ReadAllBytesAsync(survivorsPath, token).ConfigureAwait(true), s_json).ShouldNotBeNull();
        survivors.Count.ShouldBe(9);
        foreach (var survivor in survivors) (await fixture.RawAsync(survivor.Key, token).ConfigureAwait(true)).ShouldBe(survivor.Value);
        await File.WriteAllBytesAsync(Path.Combine(directory, phase + ".json"), JsonSerializer.SerializeToUtf8Bytes(new
        { tenant = manifest.Tenant, operation = manifest.IndexOperation, phase, processId = Environment.ProcessId }, s_json), token).ConfigureAwait(true);
    }
}
