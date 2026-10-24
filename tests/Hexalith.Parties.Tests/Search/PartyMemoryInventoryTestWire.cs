using System.Net;
using System.Net.Http.Json;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Explicit complete empty inventory for controlled legacy transport fixtures; real runtime coverage is independent.</summary>
internal static class PartyMemoryInventoryTestWire
{
    internal static bool TryResponse(HttpRequestMessage request, out HttpResponseMessage response)
    {
        string path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/memory-units/by-source-uri", StringComparison.Ordinal))
        {
            response = new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }) };
            return true;
        }
        if (!path.Contains("/source-inventory", StringComparison.Ordinal)) { response = null!; return false; }
        if (request.Method == HttpMethod.Post) { response = new(HttpStatusCode.NoContent); return true; }
        string tenant = Uri.UnescapeDataString(path.Split("/tenants/", StringSplitOptions.None)[1].Split('/')[0]);
        string source = Uri.UnescapeDataString(request.RequestUri.Query.Split("sourceUri=", StringSplitOptions.None)[1].Split('&')[0]);
        response = new(HttpStatusCode.OK) { Content = JsonContent.Create(new
        {
            tenantId = tenant, sourceUri = source, version = "synthetic-complete-inventory", coverageComplete = true,
            admissionClosed = true, permanent = true, entries = Array.Empty<object>(), nextCursor = (string?)null,
        }) };
        return true;
    }
}
