using System.Net;

using Hexalith.Parties.UI.Services;
using Hexalith.Parties.UI.Status;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC2/AC4 (NFR8) — the degraded-header reader: a GET response carrying
/// <c>X-Service-Degraded</c>/<c>X-Stale-Data-Age</c> is captured into the per-circuit
/// <see cref="IDegradedStateAccessor"/>; an absent header leaves it not-degraded.
/// </summary>
public sealed class DegradedResponseHeaderHandlerTests
{
    [Fact]
    public async Task GetWithDegradedHeaders_SetsAccessorDegradedWithStaleAge()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "true");
        response.Headers.Add(DegradedResponseHeaderHandler.StaleDataAgeHeader, "45");

        var accessor = new DegradedStateAccessor();
        await SendGetAsync(accessor, response);

        accessor.IsDegraded.ShouldBeTrue();
        accessor.StaleDataAgeSeconds.ShouldBe(45);
        accessor.StatusKind.ShouldBe(StatusKind.Degraded);
    }

    [Fact]
    public async Task GetWithoutDegradedHeaders_LeavesAccessorNotDegraded()
    {
        var accessor = new DegradedStateAccessor();
        await SendGetAsync(accessor, new HttpResponseMessage(HttpStatusCode.OK));

        accessor.IsDegraded.ShouldBeFalse();
        accessor.StaleDataAgeSeconds.ShouldBeNull();
        accessor.StatusKind.ShouldBeNull();
    }

    [Fact]
    public async Task PostWithDegradedHeaders_DoesNotCapture_GetOnly()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "true");
        response.Headers.Add(DegradedResponseHeaderHandler.StaleDataAgeHeader, "45");

        var accessor = new DegradedStateAccessor();
        await SendAsync(accessor, response, HttpMethod.Post).ConfigureAwait(true);

        // The middleware sets the headers on GET responses only; a non-GET response never flips the flag.
        accessor.IsDegraded.ShouldBeFalse();
        accessor.StaleDataAgeSeconds.ShouldBeNull();
        accessor.StatusKind.ShouldBeNull();
    }

    [Fact]
    public async Task GetDegradedWithoutStaleAgeHeader_IsDegradedWithNullAge()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "true");

        var accessor = new DegradedStateAccessor();
        await SendGetAsync(accessor, response).ConfigureAwait(true);

        accessor.IsDegraded.ShouldBeTrue();
        accessor.StaleDataAgeSeconds.ShouldBeNull(); // degraded, but no age was reported
        accessor.StatusKind.ShouldBe(StatusKind.Degraded);
    }

    [Fact]
    public async Task GetDegradedWithMalformedStaleAgeHeader_IsDegradedWithNullAge()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "true");
        response.Headers.Add(DegradedResponseHeaderHandler.StaleDataAgeHeader, "not-a-number");

        var accessor = new DegradedStateAccessor();
        await SendGetAsync(accessor, response).ConfigureAwait(true);

        accessor.IsDegraded.ShouldBeTrue();
        accessor.StaleDataAgeSeconds.ShouldBeNull(); // un-parseable age is dropped, degraded still holds
    }

    [Fact]
    public async Task GetWithServiceDegradedFalse_LeavesAccessorNotDegraded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "false");

        var accessor = new DegradedStateAccessor();
        await SendGetAsync(accessor, response).ConfigureAwait(true);

        accessor.IsDegraded.ShouldBeFalse();
        accessor.StatusKind.ShouldBeNull();
    }

    [Fact]
    public async Task SecondHealthyGet_ClearsPriorDegradedState()
    {
        var accessor = new DegradedStateAccessor();

        var degraded = new HttpResponseMessage(HttpStatusCode.OK);
        degraded.Headers.Add(DegradedResponseHeaderHandler.ServiceDegradedHeader, "true");
        degraded.Headers.Add(DegradedResponseHeaderHandler.StaleDataAgeHeader, "12");
        await SendGetAsync(accessor, degraded).ConfigureAwait(true);
        accessor.IsDegraded.ShouldBeTrue();

        // A later healthy GET (no degraded header) clears the prior degraded snapshot, including the age.
        await SendGetAsync(accessor, new HttpResponseMessage(HttpStatusCode.OK)).ConfigureAwait(true);

        accessor.IsDegraded.ShouldBeFalse();
        accessor.StaleDataAgeSeconds.ShouldBeNull();
        accessor.StatusKind.ShouldBeNull();
    }

    private static Task SendGetAsync(IDegradedStateAccessor accessor, HttpResponseMessage response)
        => SendAsync(accessor, response, HttpMethod.Get);

    private static async Task SendAsync(IDegradedStateAccessor accessor, HttpResponseMessage response, HttpMethod method)
    {
        var handler = new DegradedResponseHeaderHandler(accessor)
        {
            InnerHandler = new StubHandler(response),
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(method, "http://localhost/api/v1/queries");
        _ = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
