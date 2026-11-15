using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Hexalith.Memories.Contracts.V1;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Inspects exact producer-owned keys only in the current source's private disposable qualification session.</summary>
internal static class PartyMemoryUnitAuthorityRuntime
{
    /// <summary>Requires matching private session custody before any current-source qualification side effect.</summary>
    internal static async Task ValidateAsync(PartyMemoryRuntimeConfiguration config, CancellationToken token)
    {
        if (config.OwnershipGeneration is null) { return; }
        RequireCustody(Regex.IsMatch(config.OwnershipGeneration, "\\A[0-9a-f]{32}\\z")
            && !string.IsNullOrWhiteSpace(config.OwnershipSessionId)
            && config.OwnershipWriterExclusionEvidenceId == "owned-disposable-session:" + config.OwnershipSessionId + ":" + config.OwnershipGeneration);
        var selected = new Uri("redis://" + config.OwnershipRedisEndpoint);
        RequireCustody(selected.IsLoopback && selected.Port > 0 && selected.UserInfo.Length == 0
            && selected.Query.Length == 0 && selected.Fragment.Length == 0 && selected.AbsolutePath is "" or "/");
        using JsonDocument custody = JsonDocument.Parse(await File.ReadAllBytesAsync(config.OwnershipCustodyPath!, token).ConfigureAwait(false));
        RequireCustody(custody.RootElement.GetProperty("Session").GetString() == config.OwnershipSessionId
            && custody.RootElement.GetProperty("Generation").GetString() == config.OwnershipGeneration
            && custody.RootElement.GetProperty("WriterExclusionEvidenceId").GetString() == config.OwnershipWriterExclusionEvidenceId);
        JsonElement[] resources = custody.RootElement.GetProperty("Resources").EnumerateArray().ToArray();
        RequireCustody(resources.Select(resource => resource.GetProperty("Name").GetString()).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "fixture-memories-graph", "fixture-memories-search", "fixture-redis" }));
        foreach (JsonElement resource in resources)
        {
            var endpoint = new Uri(resource.GetProperty("Endpoint").GetString()!);
            RequireCustody(resource.GetProperty("State").GetString() == "Running"
                && endpoint.Scheme == "tcp" && endpoint.IsLoopback && endpoint.Port > 0);
        }
        var search = new Uri(resources.Single(resource => resource.GetProperty("Name").GetString() == "fixture-memories-search")
            .GetProperty("Endpoint").GetString()!);
        RequireCustody(string.Equals(selected.Host, search.Host, StringComparison.OrdinalIgnoreCase) && selected.Port == search.Port);
    }

    private static void RequireCustody(bool valid)
    {
        if (!valid) { throw new InvalidOperationException("Owned authority custody does not match the configured running resources."); }
    }

    /// <summary>Reads independently persisted issuance, generation reservation, provenance and terminal deletion receipts through a new connection.</summary>
    internal static async Task<string?> InspectAsync(PartyMemoryRuntimeConfiguration config, HttpClient http,
        string tenant, string caseId, string source, string workflow, string unit, bool deleted, List<object> observations, CancellationToken token)
    {
        if (config.OwnershipGeneration is null) { return null; }
        using IConnectionMultiplexer connection = await ConnectAsync(config, token).ConfigureAwait(false);
        IDatabase db = connection.GetDatabase();
        string authority = (await db.StringGetAsync(AuthorityKey(tenant, unit)).WaitAsync(token).ConfigureAwait(false)).ToString();
        authority.Length.ShouldBeInRange(1, 4096);
        using JsonDocument record = JsonDocument.Parse(authority);
        JsonElement owner = record.RootElement;
        owner.GetProperty("Kind").GetString().ShouldBe("issued-v2");
        owner.GetProperty("Tenant").GetString().ShouldBe(tenant);
        owner.GetProperty("Unit").GetString().ShouldBe(unit);
        owner.GetProperty("Generation").GetString().ShouldBe(config.OwnershipGeneration);
        owner.GetProperty("EvidenceId").GetString().ShouldBe("service-issuance:" + config.OwnershipGeneration);
        owner.GetProperty("WriterExclusionEvidenceId").GetString().ShouldBe(config.OwnershipWriterExclusionEvidenceId);
        owner.GetProperty("ProducerBinding").GetString().ShouldBe(Digest(JsonSerializer.Serialize(new[] { workflow, source, caseId })));
        unit.ShouldStartWith("owned-" + config.OwnershipGeneration + "-");
        (await db.StringGetAsync("ingestion-ownership-generation:" + config.OwnershipGeneration).WaitAsync(token).ConfigureAwait(false))
            .ToString().ShouldBe(config.OwnershipWriterExclusionEvidenceId);
        string provenance = (await db.StringGetAsync("publication-key-provenance:" + tenant + ":" + unit).WaitAsync(token).ConfigureAwait(false)).ToString();
        provenance.ShouldBe(authority);
        string receipt = (await db.StringGetAsync("source-inventory-deleted:" + Digest(JsonSerializer.Serialize(new[] { tenant, unit }))).WaitAsync(token).ConfigureAwait(false)).ToString();
        if (deleted) { receipt.ShouldBe("1"); }
        JsonElement inventory = await http.GetFromJsonAsync<JsonElement>("api/v1/tenants/" + Uri.EscapeDataString(tenant)
            + "/source-inventory?sourceUri=" + Uri.EscapeDataString(source), token).ConfigureAwait(false);
        inventory.GetProperty("coverageComplete").GetBoolean().ShouldBeTrue();
        JsonElement entry = inventory.GetProperty("entries").EnumerateArray().Single(item =>
            item.TryGetProperty("memoryUnitId", out JsonElement output) && output.GetString() == unit
            && item.GetProperty("workflowInstanceId").GetString() == workflow);
        if (deleted) { entry.GetProperty("state").GetString().ShouldBe("Deleted"); }
        observations.Add(new { Tenant = tenant, Unit = unit, Generation = config.OwnershipGeneration,
            WriterExclusionEvidenceId = config.OwnershipWriterExclusionEvidenceId, AuthoritySha256 = Digest(authority),
            ProvenanceSha256 = Digest(provenance), Deleted = deleted, DeletionReceipt = receipt, Inventory = inventory });
        return authority;
    }

    /// <summary>Missing and foreign exact-unit authority refuse refresh without admitting another producer.</summary>
    internal static async Task RefuseUnqualifiedRefreshAsync(PartyMemoryRuntimeConfiguration config, HttpClient http,
        string tenant, string caseId, string source, string unit, Func<Task> assertObligations, List<object> observations, CancellationToken token)
    {
        if (config.OwnershipGeneration is null) { return; }
        string route = MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, unit);
        byte[] before = await http.GetByteArrayAsync(route, token).ConfigureAwait(false);
        foreach (string control in Faults)
        {
            string[] producers = await ProducerIdentitiesAsync(http, tenant, source, token).ConfigureAwait(false);
            await WithAuthorityFaultAsync(config, tenant, unit, control, async () =>
            {
                using HttpResponseMessage refused = await http.PostAsJsonAsync("api/v1/ingest", new
                {
                    tenantId = tenant, caseId, sourceUri = source,
                    contentBytes = Convert.ToBase64String(Encoding.UTF8.GetBytes("unqualified refresh must not publish")),
                    contentType = "text/plain", sourceType = "file", ingestedBy = "owned-authority-negative",
                    idempotencyToken = "authority-negative-" + Guid.NewGuid().ToString("N"),
                    refreshExistingSource = true, sourceRevisionNumber = 2, expectedMemoryUnitId = unit,
                }, token).ConfigureAwait(false);
                refused.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
                (await ProducerIdentitiesAsync(http, tenant, source, token).ConfigureAwait(false)).ShouldBe(producers,
                    "A failed refresh must not add or replace any producer/admission identity.");
                (await http.GetByteArrayAsync(route, token).ConfigureAwait(false)).ShouldBe(before);
                await assertObligations().ConfigureAwait(false);
                observations.Add(new { Control = control, Operation = "refresh", Status = (int)refused.StatusCode,
                    ProducerIdentities = producers, UnitSha256 = Convert.ToHexStringLower(SHA256.HashData(before)), ObligationsRetained = true });
            }, token).ConfigureAwait(false);
        }
    }

    /// <summary>Fault variants include paired foreign records, independently exercising exact tenant/unit validation.</summary>
    internal static IReadOnlyList<string> Faults { get; } = ["missing-unit-authority", "foreign-unit-authority", "paired-foreign-tenant-authority-provenance", "paired-foreign-unit-authority-provenance"];

    /// <summary>Holds one actual deletion workflow at authority refusal, retaining physical contents and consumer obligations.</summary>
    internal static async Task RefuseUnqualifiedCleanupAsync(PartyMemoryRuntimeConfiguration config, HttpClient http,
        string tenant, string caseId, string source, string unit, string control, Func<Task<bool>> cleanup,
        Func<Task> assertObligations, List<object> observations, CancellationToken token)
    {
        string route = MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, unit);
        byte[] before = await http.GetByteArrayAsync(route, token).ConfigureAwait(false);
        string[] producers = await ProducerIdentitiesAsync(http, tenant, source, token).ConfigureAwait(false);
        await WithAuthorityFaultAsync(config, tenant, unit, control, async () =>
        {
            (await cleanup().ConfigureAwait(false)).ShouldBeFalse();
            using var sidecar = new HttpClient { BaseAddress = new Uri(config.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(5) };
            sidecar.DefaultRequestHeaders.Add("dapr-api-token", config.DaprApiToken);
            string workflow = "memory-unit-delete-" + unit;
            string? failure = null;
            for (int attempt = 0; attempt < 400; attempt++)
            {
                using HttpResponseMessage response = await sidecar.GetAsync("v1.0/workflows/dapr/" + Uri.EscapeDataString(workflow), token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string state = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    using JsonDocument document = JsonDocument.Parse(state);
                    if (string.Equals(document.RootElement.GetProperty("runtimeStatus").GetString(), "Failed", StringComparison.OrdinalIgnoreCase)) { failure = state; break; }
                    if (string.Equals(document.RootElement.GetProperty("runtimeStatus").GetString(), "Completed", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Unqualified deletion completed while authority was withheld.");
                }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            failure.ShouldNotBeNull().ShouldContain("source-publication-legacy-owner-reconciliation-required");
            (await cleanup().ConfigureAwait(false)).ShouldBeFalse();
            (await http.GetByteArrayAsync(route, token).ConfigureAwait(false)).ShouldBe(before);
            await assertObligations().ConfigureAwait(false);
            (await ProducerIdentitiesAsync(http, tenant, source, token).ConfigureAwait(false)).ShouldBe(producers);
            observations.Add(new { Control = control, Operation = "cleanup", Workflow = workflow, AuthorityRefusalObserved = true,
                UnitSha256 = Convert.ToHexStringLower(SHA256.HashData(before)), ObligationsRetained = true });
        }, token).ConfigureAwait(false);
    }

    private static async Task<string[]> ProducerIdentitiesAsync(HttpClient http, string tenant, string source, CancellationToken token)
    {
        JsonElement inventory = await http.GetFromJsonAsync<JsonElement>("api/v1/tenants/" + Uri.EscapeDataString(tenant)
            + "/source-inventory?sourceUri=" + Uri.EscapeDataString(source), token).ConfigureAwait(false);
        inventory.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null, "This owned negative scope must fit one complete inventory page.");
        return inventory.GetProperty("entries").EnumerateArray().Select(entry => JsonSerializer.Serialize(new
        {
            Case = entry.GetProperty("caseId").GetString(), Workflow = entry.GetProperty("workflowInstanceId").GetString(),
            Unit = entry.GetProperty("memoryUnitId").GetString(),
            Token = entry.TryGetProperty("idempotencyTokenHash", out JsonElement idempotency) ? idempotency.GetString() : null,
            Candidate = entry.GetProperty("candidateMemoryUnitId").GetString(),
            Revision = entry.GetProperty("sourceRevisionNumber").GetRawText(),
            RevisionBinding = entry.GetProperty("refreshIdentityDigest").GetString(),
            RefreshProtocol = entry.GetProperty("refreshProtocolVersion").GetString(),
        })).Order(StringComparer.Ordinal).ToArray();
    }

    private static async Task WithAuthorityFaultAsync(PartyMemoryRuntimeConfiguration config, string tenant, string unit,
        string control, Func<Task> check, CancellationToken token)
    {
        config.OwnershipGeneration.ShouldNotBeNullOrWhiteSpace("Fault injection requires the current-source custody profile.");
        await ValidateAsync(config, token).ConfigureAwait(false);
        using IConnectionMultiplexer connection = await ConnectAsync(config, token).ConfigureAwait(false);
        IDatabase db = connection.GetDatabase();
        string[] keys = [AuthorityKey(tenant, unit), "publication-key-provenance:" + tenant + ":" + unit];
        RedisValue[] original = await db.StringGetAsync(keys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false);
        original[0].ToString().ShouldNotBeNullOrWhiteSpace();
        original[1].ShouldBe(original[0]);
        JsonObject foreign = JsonNode.Parse(original[0].ToString())!.AsObject();
        foreign["Tenant"] = tenant + "-foreign-authority";
        JsonObject foreignUnit = JsonNode.Parse(original[0].ToString())!.AsObject();
        foreignUnit["Unit"] = unit + "-foreign-authority";
        RedisValue[] altered = control switch
        {
            "missing-unit-authority" => [RedisValue.EmptyString, original[1]],
            "foreign-unit-authority" => [foreign.ToJsonString(), original[1]],
            "paired-foreign-tenant-authority-provenance" => [foreign.ToJsonString(), foreign.ToJsonString()],
            "paired-foreign-unit-authority-provenance" => [foreignUnit.ToJsonString(), foreignUnit.ToJsonString()],
            _ => throw new ArgumentOutOfRangeException(nameof(control)),
        };
        await WithMutationAsync(() => ReplaceAsync(db, keys, original, altered, reconcile: false),
            _ => ReplaceAsync(db, keys, original, altered, reconcile: true), check, token).ConfigureAwait(false);
    }

    /// <summary>Never abandons a submitted mutation; restores with independent custody and preserves both primary and cleanup failures.</summary>
    internal static async Task WithMutationAsync(Func<Task> mutate, Func<CancellationToken, Task> reconcile, Func<Task> check, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Exception? primary = null;
        try
        {
            // Redis commands do not accept cancellation. Await their actual completion,
            // including an uncertain failure, before submitting conditional reconciliation.
            await mutate().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await check().ConfigureAwait(false);
        }
        catch (Exception failure) { primary = failure; }
        try
        {
            using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await reconcile(restore.Token).ConfigureAwait(false);
        }
        catch (Exception cleanup)
        {
            if (primary is not null) { throw new AggregateException("Authority qualification and restoration failed.", primary, cleanup); }
            throw;
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static async Task ReplaceAsync(IDatabase db, string[] keys, RedisValue[] original, RedisValue[] altered, bool reconcile)
    {
        const string compare = """
            for i,key in ipairs(KEYS) do
                local current=redis.call('GET',key) or ''
                if current~=ARGV[i] and (ARGV[5]~='1' or current~=ARGV[i+2]) then return 0 end
            end
            for i,key in ipairs(KEYS) do
                local value=ARGV[i]
                if ARGV[5]=='0' then value=ARGV[i+2] end
                if value=='' then redis.call('DEL',key) else redis.call('SET',key,value) end
            end
            return 1
            """;
        ((long)await db.ScriptEvaluateAsync(compare, keys.Select(key => (RedisKey)key).ToArray(),
            [.. original, .. altered, reconcile ? "1" : "0"]).ConfigureAwait(false)).ShouldBe(1,
                "Reconciliation accepts only the exact original or injected records and never overwrites competing state.");
        if (reconcile) { (await db.StringGetAsync(keys.Select(key => (RedisKey)key).ToArray()).ConfigureAwait(false)).ShouldBe(original); }
    }

    private static Task<ConnectionMultiplexer> ConnectAsync(PartyMemoryRuntimeConfiguration config, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions { EndPoints = { config.OwnershipRedisEndpoint! },
            ConnectTimeout = 5000, SyncTimeout = 5000, AsyncTimeout = 5000, AbortOnConnectFail = true, AllowAdmin = false });
    }

    private static string AuthorityKey(string tenant, string unit) => "ingestion-unit-authority:" + Digest(JsonSerializer.Serialize(new[] { tenant, unit }));

    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
