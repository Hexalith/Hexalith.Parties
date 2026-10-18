using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Search;

/// <summary>
/// Classifies cleanup responses without treating an unknown route or missing scope as unit absence.
/// </summary>
internal static class PartyMemoryCleanupResponse
{
    /// <summary>
    /// Accepts successful requests or a structured missing-unit response within the requested case.
    /// This does not establish terminal deletion of asynchronously processed memory units.
    /// </summary>
    /// <param name="response">The response from the versioned per-unit DELETE route.</param>
    /// <param name="cancellationToken">Cancellation for reading the error envelope.</param>
    /// <returns>Whether the request succeeded or the unit was already absent in the requested case.</returns>
    internal static async Task<bool> IsAcceptedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            return false;
        }

        try
        {
            ErrorResponse? error = await response.Content
                .ReadFromJsonAsync<ErrorResponse>(cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(error?.Code, "MEMORY_UNIT_NOT_FOUND", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // Invalid charset metadata is an unqualified response, not unit absence.
            return false;
        }
    }
}
