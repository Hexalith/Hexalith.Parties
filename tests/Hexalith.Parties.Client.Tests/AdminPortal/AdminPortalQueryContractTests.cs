// ATDD red-phase scaffolds for Story 10.1 — Admin Portal Browse, Search & Inspect.
// These tests pin the wire-level contract that the FrontComposer-hosted Parties admin
// portal must rely on when it consumes IPartiesQueryClient. They are skipped until the
// portal composition lands the cancellation, page-size, degraded-header, and
// fail-closed behaviors required by AC1, AC2, AC3, AC4, and AC7.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.AdminPortal;

/// <summary>
/// Story 10.1 — wire-level expectations for the admin portal query path. Each test below
/// documents the contract the implementation must honor before activation. Once the
/// portal/composition exposes the matching seam, drop the Skip attribute and the test
/// should turn green without further edits.
/// </summary>
public sealed class AdminPortalQueryContractTests
{
    private const string SkipReason =
        "TDD red phase — Story 10.1 admin portal must wire cancellation, header surfacing, " +
        "page-size capping and degraded-search behavior into the query transport.";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact(Skip = SkipReason)]
    public async Task ListPartiesAsync_WhenTenantSwitchCancelsToken_PropagatesCancellationAsync()
    {
        // AC7: tenant context changes must abort in-flight party-list requests so the
        // new tenant view never sees rows from the previous tenant.
        using var cts = new CancellationTokenSource();
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler _) =
            CreateClient(HttpStatusCode.OK, JsonSerializer.Serialize(EmptyIndexPage(), _jsonOptions));

        Task<PagedResult<PartyIndexEntry>> pending = client.ListPartiesAsync(
            page: 1, pageSize: 20, type: null, active: null,
            createdAfter: null, createdBefore: null, modifiedAfter: null, modifiedBefore: null,
            ct: cts.Token);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(pending);
    }

    [Fact(Skip = SkipReason)]
    public async Task ListPartiesAsync_PageSizeAboveServerCap_ClientClampsTo100Async()
    {
        // AC4: portal must not allow callers to bypass the API page-size cap of 100.
        // Either the typed client clamps, or the new admin-portal query service does.
        // The contract: the outgoing request URI must contain pageSize=100 even when 200 is requested.
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) =
            CreateClient(HttpStatusCode.OK, JsonSerializer.Serialize(EmptyIndexPage(), _jsonOptions));

        await client.ListPartiesAsync(
            page: 1, pageSize: 200, type: null, active: null,
            createdAfter: null, createdBefore: null, modifiedAfter: null, modifiedBefore: null,
            ct: CancellationToken.None);

        string query = handler.LastRequest!.RequestUri!.Query;
        query.ShouldContain("pageSize=100");
        query.ShouldNotContain("pageSize=200");
    }

    [Fact(Skip = SkipReason)]
    public async Task ListPartiesAsync_WhenResponseHasDegradedHeaders_SurfacesThemToCallerAsync()
    {
        // AC3, AC4: portal must preserve X-Service-Degraded, X-Stale-Data-Age,
        // X-Parties-Search-Status, and X-Parties-Search-Degraded-Reason so the admin UI
        // can render a bounded warning. The current IPartiesQueryClient drops headers;
        // story 10.1 must add a typed envelope (e.g. PortalQueryResult<T>) that exposes them.
        var headers = new Dictionary<string, string>
        {
            ["X-Service-Degraded"] = "true",
            ["X-Stale-Data-Age"] = "PT12S",
            ["X-Parties-Search-Status"] = "local-only",
            ["X-Parties-Search-Degraded-Reason"] = "memories-search-unavailable",
        };

        // The compiling baseline below uses the existing client; the activated test must
        // assert the new envelope/seam carries every header through unchanged.
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) =
            CreateClient(HttpStatusCode.OK, JsonSerializer.Serialize(EmptyIndexPage(), _jsonOptions), headers);

        _ = await client.ListPartiesAsync(
            page: 1, pageSize: 20, type: null, active: null,
            createdAfter: null, createdBefore: null, modifiedAfter: null, modifiedBefore: null,
            ct: CancellationToken.None);

        // Activation guidance: replace this assertion with the new typed envelope check.
        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldStartWith("/api/v1/parties");
    }

    [Fact(Skip = SkipReason)]
    public async Task SearchPartiesAsync_EmptyQuery_FallsBackToBaselineListAsync()
    {
        // AC2: empty search text must behave like the baseline list endpoint
        // (matches PartiesController.SearchPartiesAsync handling).
        var emptySearch = new PagedResult<PartySearchResult>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalCount = 0,
            TotalPages = 0,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler handler) =
            CreateClient(HttpStatusCode.OK, JsonSerializer.Serialize(emptySearch, _jsonOptions));

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync(
            query: string.Empty, page: 1, pageSize: 20, ct: CancellationToken.None);

        result.ShouldNotBeNull();
        handler.LastRequest!.RequestUri!.Query.ShouldContain("q=");
    }

    [Fact(Skip = SkipReason)]
    public async Task SearchPartiesAsync_RichSearchUnavailable_KeepsDisplayNameOnlyAsync()
    {
        // AC3: when Story 9.6 capability is unavailable, the portal must NOT emulate
        // email/identifier search client-side. The wire contract: search responses tagged
        // X-Parties-Search-Status: local-only carry only display-name matches and the
        // portal must surface the degraded status without falling back to client-side scoring.
        var headers = new Dictionary<string, string>
        {
            ["X-Parties-Search-Status"] = "local-only",
            ["X-Parties-Search-Degraded-Reason"] = "rich-search-disabled",
        };

        var displayNameOnlyPage = new PagedResult<PartySearchResult>
        {
            Items =
            [
                new PartySearchResult
                {
                    Party = new PartyIndexEntry
                    {
                        Id = "p-1",
                        Type = PartyType.Person,
                        IsActive = true,
                        DisplayName = "Anna Smith",
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastModifiedAt = DateTimeOffset.UtcNow,
                    },
                    Matches = [],
                    RelevanceScore = 0.0,
                },
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 1,
            TotalPages = 1,
        };

        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler _) =
            CreateClient(HttpStatusCode.OK, JsonSerializer.Serialize(displayNameOnlyPage, _jsonOptions), headers);

        PagedResult<PartySearchResult> result = await client.SearchPartiesAsync(
            query: "anna@example.com", page: 1, pageSize: 20, ct: CancellationToken.None);

        // Activation guidance: assert the new envelope exposes "local-only" status and
        // that no client-side re-scoring of email/identifier candidates occurred.
        result.Items.Count.ShouldBe(1);
    }

    [Fact(Skip = SkipReason)]
    public async Task GetPartyAsync_OnGoneResponse_RaisesGoneOutcomeAsync()
    {
        // AC5, AC7: detail hydration on an erased party returns 410 Gone; the portal must
        // clear the selected detail and keep the browse context intact, surfacing a bounded
        // gone/erased state. The wire contract: the typed client raises a typed exception
        // (or outcome) the portal can branch on without leaking raw ProblemDetails text.
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler _) =
            CreateClient(HttpStatusCode.Gone, "{\"type\":\"about:blank\",\"title\":\"Gone\",\"status\":410}");

        PartiesClientException ex = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("p-erased", CancellationToken.None));

        ex.Status.ShouldBe((int)HttpStatusCode.Gone);
    }

    [Fact(Skip = SkipReason)]
    public async Task GetPartyAsync_OnForbidden_DoesNotLeakRawProblemDetailsToCallerAsync()
    {
        // AC4, AC7: cross-tenant scoped id must resolve to a forbidden state without
        // revealing existence, identifiers, or raw ProblemDetails body text. The portal
        // relies on the typed client to strip server-side detail from the surfaced exception.
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler _) =
            CreateClient(
                HttpStatusCode.Forbidden,
                "{\"type\":\"about:blank\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"Cross-tenant access denied for tenant=other-tenant party=p-99\"}");

        PartiesClientException ex = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetPartyAsync("other-tenant:party:p-99", CancellationToken.None));

        ex.Status.ShouldBe((int)HttpStatusCode.Forbidden);
        // Activation guidance: assert the surfaced message contains no tenant identifier
        // and no party identifier from the server-supplied detail.
    }

    [Fact(Skip = SkipReason)]
    public async Task ListPartiesAsync_OnUnauthorized_FailsClosedWithoutCachedRowsAsync()
    {
        // AC7: missing/invalid token must clear list state. The wire contract: the typed
        // client surfaces a 401 in a way the portal can branch on, and no item collection
        // is materialized from a partial/error body.
        (HttpPartiesQueryClient client, HttpPartiesCommandClientTests.MockHandler _) =
            CreateClient(HttpStatusCode.Unauthorized, "{\"title\":\"Unauthorized\",\"status\":401}");

        PartiesClientException ex = await Should.ThrowAsync<PartiesClientException>(
            () => client.ListPartiesAsync(
                page: 1, pageSize: 20, type: null, active: null,
                createdAfter: null, createdBefore: null, modifiedAfter: null, modifiedBefore: null,
                ct: CancellationToken.None));

        ex.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }

    private static PagedResult<PartyIndexEntry> EmptyIndexPage() => new()
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
        TotalPages = 0,
    };

    private static (HttpPartiesQueryClient Client, HttpPartiesCommandClientTests.MockHandler Handler) CreateClient(
        HttpStatusCode status,
        string body,
        IDictionary<string, string>? responseHeaders = null)
    {
        var handler = new HttpPartiesCommandClientTests.MockHandler(status, body, "application/json");
        if (responseHeaders is not null)
        {
            // The current handler does not expose response headers; activation must extend it.
            // Tracked via SkipReason; intentionally left as a forward-looking hook.
        }

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return (new HttpPartiesQueryClient(http), handler);
    }
}
