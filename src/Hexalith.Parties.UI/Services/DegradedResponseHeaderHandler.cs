using System.Globalization;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that captures the Parties host's degradation headers
/// (<c>X-Service-Degraded</c> / <c>X-Stale-Data-Age</c>, produced by
/// <c>Hexalith.Parties.Middleware.DegradedResponseMiddleware</c> on <strong>GET</strong> responses) into the
/// per-circuit <see cref="IDegradedStateAccessor"/> (Story 1.7, AC2/AC4 — NFR8).
/// </summary>
/// <remarks>
/// <para>
/// Only GET responses are inspected (the middleware sets the headers on GET only); a non-GET response never
/// clears a degraded flag a prior GET set.
/// </para>
/// <para>
/// <strong>Verify-live (relay).</strong> The headers are produced on the <em>Parties host</em> responses;
/// whether the EventStore gateway relays them through to this UI host's typed-client responses is a
/// runtime question. When they are not relayed, this handler simply never sets degraded and
/// <c>ProjectionFreshnessMetadata</c> remains the primary degraded signal — the building block + its test
/// are the story deliverable, not the relay.
/// </para>
/// </remarks>
internal sealed class DegradedResponseHeaderHandler(IDegradedStateAccessor accessor) : DelegatingHandler
{
    internal const string ServiceDegradedHeader = "X-Service-Degraded";
    internal const string StaleDataAgeHeader = "X-Stale-Data-Age";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Method == HttpMethod.Get)
        {
            Capture(response);
        }

        return response;
    }

    private void Capture(HttpResponseMessage response)
    {
        bool degraded =
            response.Headers.TryGetValues(ServiceDegradedHeader, out IEnumerable<string>? degradedValues)
            && degradedValues.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        long? staleDataAgeSeconds = null;
        if (degraded
            && response.Headers.TryGetValues(StaleDataAgeHeader, out IEnumerable<string>? ageValues)
            && long.TryParse(ageValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long age))
        {
            staleDataAgeSeconds = age;
        }

        accessor.Set(degraded, staleDataAgeSeconds);
    }
}
