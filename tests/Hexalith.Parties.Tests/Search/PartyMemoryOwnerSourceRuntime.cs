using System.Globalization;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Queries;
using Hexalith.Memories.Contracts.V1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Calls existing owner seams from the exact running source-host assembly against its unchanged owned backing store.</summary>
internal static class PartyMemoryOwnerSourceRuntime
{
    /// <summary>Rejects actual same-unit source reuse, then independently refuses unknown historical discovery under complete source B.</summary>
    internal static async Task CrossSourceAsync(PartyMemoryRuntimeConfiguration config, string tenant, string caseId,
        string sourceA, string unitA, string sourceB, string unitB, List<object> observations, CancellationToken token)
    {
        await WithServerAsync(config, async owner =>
        {
            using IConnectionMultiplexer connection = await ConnectAsync(config).ConfigureAwait(false);
            IDatabase db = connection.GetDatabase();
            object store = Store(owner, connection, config);
            string[] authorityKeys = [AuthorityKey(tenant, unitA), "publication-key-provenance:" + tenant + ":" + unitA];
            RedisValue[] originalAuthority = await db.StringGetAsync(authorityKeys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false);
            RedisValue sourceBBefore = await db.StringGetAsync(SourceKey(tenant, sourceB)).ConfigureAwait(false);
            using JsonDocument sourceBState = JsonDocument.Parse(sourceBBefore.ToString());
            sourceBState.RootElement.GetProperty("coverageComplete").GetBoolean().ShouldBeTrue();
            string[] originalKeys = await ExactKeysAsync(db, tenant, unitA).ConfigureAwait(false);
            string[] sourceBKeys = await ExactKeysAsync(db, tenant, unitB).ConfigureAwait(false);
            HashEntry[][] originalHashes = await Task.WhenAll(originalKeys.Select(key => db.HashGetAllAsync(key))).ConfigureAwait(false);
            HashEntry[][] sourceBHashes = await Task.WhenAll(sourceBKeys.Select(key => db.HashGetAllAsync(key))).ConfigureAwait(false);
            using JsonDocument baseline = JsonDocument.Parse(await File.ReadAllBytesAsync(BaselinePath(), token).ConfigureAwait(false));
            baseline.RootElement.GetProperty("Tenant").GetString().ShouldBe(tenant);
            baseline.RootElement.GetProperty("Case").GetString().ShouldBe(caseId);
            JsonSerializer.Serialize(HashDigests(originalKeys, originalHashes)).ShouldBe(baseline.RootElement.GetProperty("SourceAHashes").GetRawText());
            JsonSerializer.Serialize(HashDigests(sourceBKeys, sourceBHashes)).ShouldBe(baseline.RootElement.GetProperty("SourceBHashes").GetRawText());
            Digest(originalAuthority[0].ToString()).ShouldBe(baseline.RootElement.GetProperty("SourceAAuthoritySha256").GetString());
            string[] metadataKeys = MetadataKeys(tenant, sourceA, unitA, sourceB, unitB);
            JsonElement[] metadataBaseline = baseline.RootElement.GetProperty("Metadata").EnumerateArray().ToArray();
            metadataKeys.ShouldBe(metadataBaseline.Select(item => item.GetProperty("Key").GetString()));
            RedisValue[] metadata = await db.StringGetAsync(metadataKeys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false);
            metadata.Select(value => value.ToString()).ShouldBe(metadataBaseline.Select(item => item.GetProperty("Value").GetString()));
            JsonElement historicalBaseline = baseline.RootElement.GetProperty("Historical");
            string historical = historicalBaseline.GetProperty("Unit").GetString()!;
            string historicalSource = historicalBaseline.GetProperty("Source").GetString()!;
            string[] historicalKeys = historicalBaseline.GetProperty("Keys").EnumerateArray().Select(item => item.GetString()!).ToArray();
            HashEntry[][] historicalHashes = await Task.WhenAll(historicalKeys.Select(key => db.HashGetAllAsync(key))).ConfigureAwait(false);
            JsonSerializer.Serialize(HashDigests(historicalKeys, historicalHashes)).ShouldBe(historicalBaseline.GetProperty("Hashes").GetRawText());
            (await db.StringGetAsync("publication-key-provenance:" + tenant + ":" + historical).ConfigureAwait(false)).ToString()
                .ShouldBe(historicalBaseline.GetProperty("Provenance").GetString());
            (await db.KeyExistsAsync(AuthorityKey(tenant, historical)).ConfigureAwait(false)).ShouldBeFalse();
            using var sidecar = new HttpClient { BaseAddress = new Uri(config.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(5) };
            sidecar.DefaultRequestHeaders.Add("dapr-api-token", config.DaprApiToken);
            JsonElement workflow = await sidecar.GetFromJsonAsync<JsonElement>("v1.0/workflows/dapr/" + Uri.EscapeDataString(unitA), token).ConfigureAwait(false);
            workflow.GetProperty("runtimeStatus").GetString().ShouldBe("Completed", StringCompareShould.IgnoreCase);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter());
            IngestionInput originalInput = JsonSerializer.Deserialize<IngestionInput>(workflow.GetProperty("properties").GetProperty("dapr.workflow.input").GetString()!, json)!;
            originalInput.TenantId.ShouldBe(tenant);
            originalInput.CaseId.ShouldBe(caseId);
            originalInput.SourceUri.ShouldBe(sourceA);
            IngestionInput reused = originalInput with { SourceUri = sourceB };
            RedisServerException conflict = await Should.ThrowAsync<RedisServerException>(() => InvokeAsync(store, "AdmitAsync", [unitA, reused, token, false])).ConfigureAwait(false);
            conflict.Message.ShouldContain("unit-ownership-producer-conflict");
            (await db.StringGetAsync(SourceKey(tenant, sourceB)).ConfigureAwait(false)).ShouldBe(sourceBBefore);
            (await db.StringGetAsync(authorityKeys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false)).ShouldBe(originalAuthority);
            (await db.StringGetAsync(metadataKeys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false)).ShouldBe(metadata);
            await AssertHashesAsync(db, originalKeys, originalHashes).ConfigureAwait(false);
            await AssertHashesAsync(db, sourceBKeys, sourceBHashes).ConfigureAwait(false);
            observations.Add(new { Boundary = "cross-source-authority", Proof = "direct-owner-admission", Tenant = tenant,
                Unit = unitA, SourceA = sourceA, SourceB = sourceB, SameTenantUnitAndCase = true,
                OriginalProducerBinding = Digest(JsonSerializer.Serialize(new[] { unitA, sourceA, caseId })),
                ReusedProducerBinding = Digest(JsonSerializer.Serialize(new[] { unitA, sourceB, caseId })),
                AuthoritySha256 = Digest(originalAuthority[0].ToString()), AdmissionRefused = true, OriginalMetadataUnchangedAcrossRestart = true,
                BeforeRestartBaseline = baseline.RootElement.Clone(),
                SourceAHashes = HashDigests(originalKeys, originalHashes), SourceBHashes = HashDigests(sourceBKeys, sourceBHashes) });

            await InvokeAsync(store, "AdmitAsync", [historical, Input(tenant, caseId, sourceB), token, false]).ConfigureAwait(false);
            await InvokeAsync(store, "RecordCandidateAsync", [tenant, sourceB, historical, historical, token]).ConfigureAwait(false);
            Type discovery = owner.GetType("Hexalith.Memories.Server.Ingestion.SemanticKeyDiscovery", throwOnError: true)!;
            InvalidOperationException refused = await Should.ThrowAsync<InvalidOperationException>(() => InvokeAsync(discovery, "FindAsync", [db, tenant, historical, token])).ConfigureAwait(false);
            refused.Message.ShouldBe("source-publication-legacy-owner-reconciliation-required");
            (await db.KeyExistsAsync(AuthorityKey(tenant, historical)).ConfigureAwait(false)).ShouldBeFalse();
            await AssertHashesAsync(db, historicalKeys, historicalHashes).ConfigureAwait(false);
            (await db.StringGetAsync(metadataKeys.Take(5).Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false)).ShouldBe(metadata.Take(5));
            await AssertHashesAsync(db, originalKeys, originalHashes).ConfigureAwait(false);
            await AssertHashesAsync(db, sourceBKeys, sourceBHashes).ConfigureAwait(false);
            observations.Add(new { Boundary = "cross-source-authority", Proof = "direct-owner-cleanup-discovery", Unit = historical,
                SourceA = historicalSource, SourceB = sourceB, CompleteSourceBCannotCertifyUnit = true, CleanupDiscoveryRefused = true,
                HistoricalBaseline = historicalBaseline.Clone(), HistoricalHashes = HashDigests(historicalKeys, historicalHashes), SourceBHashes = HashDigests(sourceBKeys, sourceBHashes),
                TerminalCompletenessUnclaimed = true, CleanupScope = "owned backing-resource disposal; unknown fixture obligation retained" });
        }).ConfigureAwait(false);
    }

    /// <summary>Seeds historical artifacts and records both settled source metadata and bytes before replacement.</summary>
    internal static async Task CaptureBaselineAsync(PartyMemoryRuntimeConfiguration config, HttpClient http, string tenant, string caseId,
        string sourceA, string unitA, string sourceB, string unitB, CancellationToken token)
    {
        foreach ((string source, string unit) in new[] { (sourceA, unitA), (sourceB, unitB) })
        {
            JsonElement inventory = await http.GetFromJsonAsync<JsonElement>("api/v1/tenants/" + Uri.EscapeDataString(tenant)
                + "/source-inventory?sourceUri=" + Uri.EscapeDataString(source), token).ConfigureAwait(false);
            inventory.GetProperty("coverageComplete").GetBoolean().ShouldBeTrue();
            JsonElement entry = inventory.GetProperty("entries").EnumerateArray().Single(item => item.GetProperty("memoryUnitId").GetString() == unit);
            entry.GetProperty("state").GetString().ShouldBe("Completed");
            entry.GetProperty("terminalVerified").GetBoolean().ShouldBeTrue();
        }
        using IConnectionMultiplexer connection = await ConnectAsync(config).ConfigureAwait(false);
        IDatabase db = connection.GetDatabase();
        string[] first = await ExactKeysAsync(db, tenant, unitA).ConfigureAwait(false);
        string[] second = await ExactKeysAsync(db, tenant, unitB).ConfigureAwait(false);
        HashEntry[][] firstHashes = await Task.WhenAll(first.Select(key => db.HashGetAllAsync(key))).ConfigureAwait(false);
        HashEntry[][] secondHashes = await Task.WhenAll(second.Select(key => db.HashGetAllAsync(key))).ConfigureAwait(false);
        string[] metadataKeys = MetadataKeys(tenant, sourceA, unitA, sourceB, unitB);
        RedisValue[] metadata = await db.StringGetAsync(metadataKeys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false);
        foreach (RedisValue value in metadata) { value.ToString().ShouldNotBeNullOrWhiteSpace(); }
        string historical = "unknown-historical-" + Guid.NewGuid().ToString("N");
        string historicalSource = "https://synthetic.invalid/source-a/" + historical;
        string[] historicalKeys = [tenant + ":vec:" + historical + ":7", tenant + ":vec:nl:" + historical];
        HashEntry[] bytes = [new("tenantId", tenant), new("memoryUnitId", historical), new("caseId", caseId),
            new("sourceUri", historicalSource), new("text", new byte[] { 0, 255, 128, 13, 10 }), new("vector", new byte[768 * sizeof(float)])];
        foreach (string key in historicalKeys) { (await db.KeyExistsAsync(key).ConfigureAwait(false)).ShouldBeFalse(); await db.HashSetAsync(key, bytes).ConfigureAwait(false); }
        string historicalProvenance = "publication-key-provenance:" + tenant + ":" + historical;
        (await db.KeyExistsAsync(historicalProvenance).ConfigureAwait(false)).ShouldBeFalse();
        (await db.KeyExistsAsync(AuthorityKey(tenant, historical)).ConfigureAwait(false)).ShouldBeFalse();
        await db.StringSetAsync(historicalProvenance, "modern").ConfigureAwait(false);
        await File.WriteAllTextAsync(BaselinePath(), JsonSerializer.Serialize(new { Tenant = tenant, Case = caseId, UnitA = unitA, UnitB = unitB,
            CapturedAtUtc = DateTimeOffset.UtcNow, SourceAAuthoritySha256 = Digest(metadata[0].ToString()),
            SourceAHashes = HashDigests(first, firstHashes), SourceBHashes = HashDigests(second, secondHashes),
            Metadata = metadataKeys.Select((key, index) => new { Key = key, Value = metadata[index].ToString(), Sha256 = Digest(metadata[index].ToString()) }),
            Historical = new { Unit = historical, Source = historicalSource, Keys = historicalKeys, Hashes = HashDigests(historicalKeys, [bytes, bytes]),
                Provenance = "modern", AuthorityAbsent = true },
            Scope = "isolated owner-contract component; unknown fixture obligation does not affect main-party or survivor API teardown" }), token).ConfigureAwait(false);
    }

    private static string[] MetadataKeys(string tenant, string sourceA, string unitA, string sourceB, string unitB)
        => [AuthorityKey(tenant, unitA), "publication-key-provenance:" + tenant + ":" + unitA,
            AuthorityKey(tenant, unitB), "publication-key-provenance:" + tenant + ":" + unitB, SourceKey(tenant, sourceA), SourceKey(tenant, sourceB)];

    /// <summary>Uses the existing owner reconciliation seam with exact fixture keys and conditional original authority.</summary>
    internal static Task ReconcileUnitAsync(PartyMemoryRuntimeConfiguration config, string tenant, string unit, string[] exact,
        string evidence, string? expected, CancellationToken token)
        => WithServerAsync(config, async owner =>
        {
            using IConnectionMultiplexer connection = await ConnectAsync(config).ConfigureAwait(false);
            await InvokeAsync(Store(owner, connection, config), "ReconcileUnitAsync", [tenant, unit, exact, evidence, expected, token]).ConfigureAwait(false);
        });

    private static async Task WithServerAsync(PartyMemoryRuntimeConfiguration config, Func<Assembly, Task> action)
    {
        string path = config.OwnershipServerAssemblyPath ?? throw new InvalidOperationException("The exact running owner assembly is required.");
        Path.GetFileName(path).ShouldBe("Hexalith.Memories.Server.dll");
        foreach (Assembly shared in new[] { typeof(IngestionInput).Assembly, typeof(QueryCursorCodec).Assembly, typeof(IConnectionMultiplexer).Assembly })
        {
            string peer = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(shared.Location));
            SHA256.HashData(File.ReadAllBytes(peer)).ShouldBe(SHA256.HashData(File.ReadAllBytes(shared.Location)), "Direct owner calls require the exact same shared dependency bytes.");
        }
        var resolver = new AssemblyDependencyResolver(path);
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            string? dependency = resolver.ResolveAssemblyToPath(name);
            return dependency is null ? null : context.LoadFromAssemblyPath(dependency);
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try { await action(AssemblyLoadContext.Default.LoadFromAssemblyPath(path)).ConfigureAwait(false); }
        finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
    }

    private static object Store(Assembly owner, IConnectionMultiplexer connection, PartyMemoryRuntimeConfiguration config)
    {
        Type settingsType = owner.GetType("Hexalith.Memories.Server.Ingestion.IngestionSettings", throwOnError: true)!;
        object settings = Activator.CreateInstance(settingsType, nonPublic: true)!;
        settingsType.GetProperty("OwnershipGeneration")!.SetValue(settings, config.OwnershipGeneration);
        settingsType.GetProperty("OwnershipWriterExclusionEvidenceId")!.SetValue(settings, config.OwnershipWriterExclusionEvidenceId);
        object options = Activator.CreateInstance(typeof(OptionsWrapper<>).MakeGenericType(settingsType), new object[] { settings })!;
        Type store = owner.GetType("Hexalith.Memories.Server.Ingestion.SourceInventoryStore", throwOnError: true)!;
        return Activator.CreateInstance(store, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [connection, new QueryCursorCodec(new EphemeralDataProtectionProvider(), "OwnedProducerRestart"), options], CultureInfo.InvariantCulture)!;
    }

    private static async Task InvokeAsync(object target, string method, object?[] arguments)
    {
        Type type = target as Type ?? target.GetType();
        try
        {
            var task = (Task)type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
                .Invoke(target is Type ? null : target, arguments)!;
            await task.ConfigureAwait(false);
        }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        { ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); }
    }

    private static IngestionInput Input(string tenant, string caseId, string source) => new()
    { TenantId = tenant, CaseId = caseId, SourceUri = source, SourceType = SourceType.Annotation, ContentType = "text/plain", IngestedBy = "owned-cross-source-contract" };

    private static async Task<string[]> ExactKeysAsync(IDatabase db, string tenant, string unit)
    {
        RedisValue[] keys = await db.SetMembersAsync("publication-keys:" + tenant + ":" + unit).ConfigureAwait(false);
        keys.Length.ShouldBeInRange(1, 4096);
        return keys.Select(key => key.ToString()).Order(StringComparer.Ordinal).ToArray();
    }

    private static async Task AssertHashesAsync(IDatabase db, string[] keys, HashEntry[][] expected)
    {
        for (int index = 0; index < keys.Length; index++)
        { (await db.HashGetAllAsync(keys[index]).ConfigureAwait(false)).OrderBy(item => item.Name.ToString(), StringComparer.Ordinal).ShouldBe(expected[index].OrderBy(item => item.Name.ToString(), StringComparer.Ordinal)); }
    }

    private static object[] HashDigests(string[] keys, HashEntry[][] hashes) => keys.Select((key, index) => (object)new
    { Key = key, Sha256 = Digest(JsonSerializer.Serialize(hashes[index].OrderBy(item => item.Name.ToString(), StringComparer.Ordinal)
        .Select(item => new { Name = item.Name.ToString(), Value = Convert.ToBase64String((byte[])item.Value!) }))) }).ToArray();

    private static Task<ConnectionMultiplexer> ConnectAsync(PartyMemoryRuntimeConfiguration config) => ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
    { EndPoints = { config.OwnershipRedisEndpoint! }, ConnectTimeout = 5000, SyncTimeout = 5000, AsyncTimeout = 5000, AbortOnConnectFail = true });

    private static string AuthorityKey(string tenant, string unit) => "ingestion-unit-authority:" + Digest(JsonSerializer.Serialize(new[] { tenant, unit }));
    private static string SourceKey(string tenant, string source) => "source-inventory:" + Digest(JsonSerializer.Serialize(new[] { tenant, source }));
    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private static string BaselinePath() => Path.ChangeExtension(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG")!, ".owner-component.json");
}
