using System.Text.Json;

namespace Hexalith.Parties.Search;

internal sealed partial class PartyMemoryCleanupService
{
    /// <summary>Persists every discovered obligation before continuing pagination; unknown coverage never certifies cleanup.</summary>
    private async Task<(bool Complete, bool Terminal, string Version)> ReconcileSourceInventoryAsync(
        string tenantId, string partyId, bool permanent, bool fence, CancellationToken cancellationToken)
    {
        string sourceUri = PartyMemoryUrn.Build(tenantId, partyId);
        string route = $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/source-inventory";
        string query = "?sourceUri=" + Uri.EscapeDataString(sourceUri);
        if (fence)
        {
            using HttpResponseMessage closed = await httpClient.PostAsync(route + "/fence" + query + "&permanent="
                + permanent.ToString().ToLowerInvariant(), null, cancellationToken).ConfigureAwait(false);
            closed.EnsureSuccessStatusCode();
        }
        string? cursor = null;
        string? version = null;
        bool complete = true;
        bool terminal = true;
        HashSet<(string Kind, string? Case, string? Id)> seen = [];
        HashSet<string> cursors = new(StringComparer.Ordinal);
        for (int pageNumber = 0; pageNumber < PartyMemoryIngestionIntentStore.Capacity; pageNumber++)
        {
            string path = route + query + "&pageSize=100" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (httpClient.Timeout != Timeout.InfiniteTimeSpan) { deadline.CancelAfter(httpClient.Timeout); }
            using HttpResponseMessage response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = await ReadBoundedInventoryAsync(response.Content, deadline.Token).ConfigureAwait(false);
            JsonElement page = document.RootElement;
            string? observedVersion = page.GetProperty("version").GetString();
            if (page.GetProperty("tenantId").GetString() != tenantId || page.GetProperty("sourceUri").GetString() != sourceUri
                || !page.GetProperty("admissionClosed").GetBoolean() || (permanent && !page.GetProperty("permanent").GetBoolean())
                || !ValidHandle(observedVersion, 200) || (version is not null && version != observedVersion))
            {
                throw new InvalidOperationException("memory-source-inventory-scope-or-version-mismatch");
            }
            version = observedVersion;
            complete &= page.GetProperty("coverageComplete").GetBoolean();
            JsonElement entries = page.GetProperty("entries");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 100)
            {
                throw new InvalidOperationException("memory-source-inventory-page-bound");
            }
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string? workflow = entry.TryGetProperty("workflowInstanceId", out JsonElement handle) ? handle.GetString() : null;
                string? capturedCase = entry.GetProperty("caseId").GetString();
                string? canonical = entry.TryGetProperty("memoryUnitId", out JsonElement unit) ? unit.GetString() : null;
                string? retryHash = entry.TryGetProperty("idempotencyTokenHash", out JsonElement retry) ? retry.GetString() : null;
                string? state = entry.GetProperty("state").GetString();
                bool terminalVerified = entry.TryGetProperty("terminalVerified", out JsonElement proof) && proof.GetBoolean();
                var identity = workflow is not null ? ("workflow", (string?)null, workflow) : ("unit", capturedCase, canonical);
                if ((workflow is null && canonical is null) || (workflow is not null && !ValidHandle(workflow, 512))
                    || (canonical is not null && !ValidHandle(canonical, 512)) || !ValidHandle(capturedCase, 512)
                    || (retryHash is not null && (retryHash.Length != 64 || retryHash.Any(character => !char.IsAsciiHexDigit(character)))) || !seen.Add(identity)
                    || seen.Count > PartyMemoryIngestionIntentStore.Capacity || state is not ("Admitted" or "Completed" or "Deleted")
                    || (state != "Admitted" && string.IsNullOrWhiteSpace(canonical)))
                {
                    throw new InvalidOperationException("memory-source-inventory-entry-invalid");
                }
                // Even a producer deletion marker must pass the consumer's exact scoped GET
                // absence check. Retaining canonical provenance does not settle the obligation.
                await intentStore.RetainDiscoveredAsync(tenantId, partyId, capturedCase, sourceUri, workflow,
                    state == "Admitted" ? null : canonical, retryHash, terminalVerified, cancellationToken, BuildProducerAuthority(httpClient.BaseAddress)).ConfigureAwait(false);
                terminal &= state == "Deleted" && (workflow is null || terminalVerified);
            }
            cursor = page.TryGetProperty("nextCursor", out JsonElement next) ? next.GetString() : null;
            if (cursor is null) { return (complete, terminal, version!); }
            if (!ValidHandle(cursor, 8192) || entries.GetArrayLength() == 0 || !cursors.Add(cursor))
            {
                throw new InvalidOperationException("memory-source-inventory-continuation-invalid");
            }
        }
        throw new InvalidOperationException("memory-source-inventory-page-limit");
    }
    internal static string? BuildProducerAuthority(Uri? endpoint)
    {
        if (endpoint is null) { return null; }
        var normalized = new UriBuilder(new Uri(endpoint, "./")) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty };
        string authority = normalized.Uri.AbsoluteUri.TrimEnd('/') + "/";
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(authority)));
    }

    private static bool ValidHandle([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && !value.Any(char.IsControl);

    private static async Task<JsonDocument> ReadBoundedInventoryAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int maximumBytes = 1024 * 1024;
        if (content.Headers.ContentLength > maximumBytes) { throw new InvalidOperationException("memory-source-inventory-body-bound"); }
        using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream bounded = new();
        byte[] buffer = new byte[8192];
        int bytes;
        while ((bytes = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (bounded.Length + bytes > maximumBytes) { throw new InvalidOperationException("memory-source-inventory-body-bound"); }
            await bounded.WriteAsync(buffer.AsMemory(0, bytes), cancellationToken).ConfigureAwait(false);
        }
        bounded.Position = 0;
        return await JsonDocument.ParseAsync(bounded, new JsonDocumentOptions { MaxDepth = 16 }, cancellationToken).ConfigureAwait(false);
    }

}
