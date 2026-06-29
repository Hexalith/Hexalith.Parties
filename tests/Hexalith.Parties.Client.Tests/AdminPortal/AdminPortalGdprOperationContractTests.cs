// ATDD red-phase transport scaffolds for Story 12.7 — Admin Portal GDPR Operations.
// These tests pin the frontend-facing client/adapter contract for the EventStore
// command/query boundary. They are skipped until Story 12.5 exposes the typed
// client contract that replaces the retired admin controller surface.

using System.Reflection;
using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.AdminPortal;

/// <summary>
/// Story 10.2 — AC2 through AC6 and AC8. Reflection keeps these scaffolds compiling
/// before the GDPR portal adapter exists, while activated tests fail until the
/// expected methods, route constants, and bounded outcome names are implemented.
/// </summary>
public sealed class AdminPortalGdprOperationContractTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    private const string AdapterTypeName = "Hexalith.Parties.Client.AdminPortal.IAdminPortalGdprClient";
    private const string RouteMapTypeName = "Hexalith.Parties.Client.AdminPortal.AdminPortalGdprRoutes";
    private const string OutcomeTypeName = "Hexalith.Parties.Client.AdminPortal.AdminPortalGdprOutcome";

    [Fact]
    public void AdminGdprClient_DefinesErasureRequestStatusCertificateAndRetryMethods()
    {
        Type adapter = LoadClientType(AdapterTypeName);

        adapter.GetMethod("RequestErasureAsync", PublicInstance)
            .ShouldNotBeNull("AC4 requires Request Erasure to call the accepted EventStore command/client contract.");
        adapter.GetMethod("GetErasureStatusAsync", PublicInstance)
            .ShouldNotBeNull("AC2/AC3 require polling or refreshing authoritative erasure status.");
        adapter.GetMethod("GetErasureCertificateAsync", PublicInstance)
            .ShouldNotBeNull("AC2 requires rendering the verification report when available.");
        adapter.GetMethod("RetryErasureVerificationAsync", PublicInstance)
            .ShouldNotBeNull("AC3 requires retry only for supported partial or failed verification states.");
    }

    [Fact]
    public void AdminGdprClient_DefinesRestrictionConsentExportAndProcessingMethods()
    {
        Type adapter = LoadClientType(AdapterTypeName);

        string[] required =
        [
            "RestrictProcessingAsync",
            "LiftRestrictionAsync",
            "AddConsentAsync",
            "RevokeConsentAsync",
            "GetConsentAsync",
            "ExportPartyDataAsync",
            "GetProcessingRecordsAsync",
        ];

        foreach (string method in required)
        {
            adapter.GetMethod(method, PublicInstance)
                .ShouldNotBeNull($"Story 10.2 requires adapter method {method} for the existing admin GDPR endpoint.");
        }
    }

    [Fact]
    public void AdminGdprRoutes_MapToEventStoreCommandAndQueryContracts()
    {
        Type routes = LoadClientType(RouteMapTypeName);

        GetRoute(routes, "EraseParty").ShouldBe("eventstore:command:party:EraseParty");
        GetRoute(routes, "CancelErasure").ShouldBe("eventstore:command:party:CancelPartyErasure");
        GetRoute(routes, "ErasureStatus").ShouldBe("eventstore:query:party:GetErasureStatus");
        GetRoute(routes, "ErasureCertificate").ShouldBe("eventstore:query:party:GetErasureCertificate");
        GetRoute(routes, "RetryVerification").ShouldBe("eventstore:command:party:RetryErasureVerification");
        GetRoute(routes, "RestrictProcessing").ShouldBe("eventstore:command:party:RestrictProcessing");
        GetRoute(routes, "LiftRestriction").ShouldBe("eventstore:command:party:LiftRestriction");
        GetRoute(routes, "Consent").ShouldBe("eventstore:command:party:AddConsent");
        GetRoute(routes, "ConsentById").ShouldBe("eventstore:command:party:RevokeConsent");
        GetRoute(routes, "Export").ShouldBe("eventstore:query:party:ExportPartyData");
        GetRoute(routes, "ProcessingRecords").ShouldBe("eventstore:query:party:GetProcessingRecords");
    }

    [Fact]
    public void AddConsentAsync_PreservesChannelPurposeAndLawfulBasisShape()
    {
        Type adapter = LoadClientType(AdapterTypeName);
        MethodInfo method = adapter.GetMethod("AddConsentAsync", PublicInstance)
            ?? throw new InvalidOperationException("Missing AddConsentAsync.");

        Type[] parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

        parameterTypes.ShouldContain(typeof(string), "party id must remain a route id, not a payload tenant field.");
        parameterTypes.ShouldContain(typeof(LawfulBasis), "lawful basis choices must come from the contract enum.");
        method.GetParameters().Any(p => string.Equals(p.Name, "purpose", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue("Consent purpose must be explicit and treated as untrusted text by the UI.");
        method.GetParameters().Any(p => string.Equals(p.Name, "channelId", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue("Consent add must target an existing channel id.");
    }

    [Fact]
    public void GdprOutcomes_DistinguishForbiddenConflictGoneAndDomainRejection()
    {
        Type outcome = LoadClientType(OutcomeTypeName);
        outcome.IsEnum.ShouldBeTrue("The portal needs stable bounded outcome names for UI branching.");

        string[] required =
        [
            "Accepted",
            "Completed",
            "ValidationRejected",
            "Forbidden",
            "MissingTenant",
            "ErasureInProgress",
            "Erased",
            "TransientFailure",
        ];

        foreach (string name in required)
        {
            Enum.GetNames(outcome).ShouldContain(name,
                $"AdminPortalGdprOutcome must distinguish {name} without exposing raw ProblemDetails.");
        }
    }

    [Theory]
    [InlineData(401, null, AdminPortalGdprOutcome.AuthenticationRequired)]
    [InlineData(403, "Tenant context is unavailable for this request.", AdminPortalGdprOutcome.MissingTenant)]
    [InlineData(403, "Access denied.", AdminPortalGdprOutcome.Forbidden)]
    [InlineData(404, null, AdminPortalGdprOutcome.NotFound)]
    [InlineData(409, null, AdminPortalGdprOutcome.ErasureInProgress)]
    [InlineData(410, null, AdminPortalGdprOutcome.Erased)]
    [InlineData(400, null, AdminPortalGdprOutcome.ValidationRejected)]
    [InlineData(422, null, AdminPortalGdprOutcome.ValidationRejected)]
    [InlineData(501, null, AdminPortalGdprOutcome.ContractUnavailable)]
    [InlineData(408, null, AdminPortalGdprOutcome.TransientFailure)]
    [InlineData(429, null, AdminPortalGdprOutcome.TransientFailure)]
    [InlineData(503, null, AdminPortalGdprOutcome.TransientFailure)]
    [InlineData(418, null, AdminPortalGdprOutcome.Unknown)]
    public void MapGdprOutcome_ProvidesSingleClientOwnedMappingSurface(
        int status,
        string? detail,
        AdminPortalGdprOutcome expected)
    {
        HttpAdminPortalGdprClient.MapGdprOutcome(status, detail: detail).ShouldBe(expected);
    }

    [Fact]
    public void MapGdprOutcome_DetectsMissingTenantFromTitle()
    {
        // The title tenant-detection branch is a distinct source from detail; pin it.
        HttpAdminPortalGdprClient
            .MapGdprOutcome(403, title: "Tenant membership required")
            .ShouldBe(AdminPortalGdprOutcome.MissingTenant);
    }

    [Fact]
    public void MapGdprOutcome_DetectsMissingTenantFromGlobalErrors()
    {
        // The globalErrors tenant-detection branch must also resolve to MissingTenant.
        HttpAdminPortalGdprClient
            .MapGdprOutcome(403, globalErrors: ["Access denied.", "eventstore:tenant claim is required"])
            .ShouldBe(AdminPortalGdprOutcome.MissingTenant);
    }

    [Fact]
    public void MapGdprOutcome_WhenForbiddenWithoutTenantSignals_ReturnsForbidden()
    {
        HttpAdminPortalGdprClient
            .MapGdprOutcome(403, title: "Access denied.", detail: "Operation not permitted.", globalErrors: ["No access."])
            .ShouldBe(AdminPortalGdprOutcome.Forbidden);
    }

    [Fact]
    public void ExportPartyDataAsync_ReturnsDownloadEnvelopeWithoutPiiFilenameInputs()
    {
        Type adapter = LoadClientType(AdapterTypeName);
        MethodInfo method = adapter.GetMethod("ExportPartyDataAsync", PublicInstance)
            ?? throw new InvalidOperationException("Missing ExportPartyDataAsync.");

        Type returnType = UnwrapTask(method.ReturnType);
        returnType.Name.ShouldBe("AdminPortalExportDownload",
            "AC6 requires a JSON download envelope with safe filename, content type, and body.");

        IEnumerable<string> propertyNames = returnType.GetProperties(PublicInstance).Select(p => p.Name);
        propertyNames.ShouldContain("FileName");
        propertyNames.ShouldContain("ContentType");
        propertyNames.ShouldContain("Payload");
        propertyNames.ShouldNotContain("DisplayName",
            "Export filenames/storage keys must not be derived from personal display names.");
    }

    [Fact]
    public async Task AddConsentAsync_SubmitsRecordConsentContractCommandTypeAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(new { correlationId = "corr-consent" }),
            "application/json");
        var client = CreateHttpClient(handler);

        await client.AddConsentAsync("party-1", "channel-1", "billing", LawfulBasis.Consent, CancellationToken.None);

        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/commands");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("commandType").GetString().ShouldBe(typeof(RecordConsent).FullName);
        root.GetProperty("aggregateId").GetString().ShouldBe("party-1");
        root.GetProperty("payload").GetProperty("purpose").GetString().ShouldBe("billing");
    }

    [Fact]
    public async Task RetryVerificationAsync_SubmitsRetryVerificationCommandContractAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(new { correlationId = "corr-retry" }),
            "application/json");
        var client = CreateHttpClient(handler);

        AdminPortalGdprCommandResult result = await client.RetryErasureVerificationAsync("party-1", CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalGdprOutcome.Accepted);
        result.CorrelationId.ShouldBe("corr-retry");
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/commands");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("commandType").GetString().ShouldBe(typeof(RetryErasureVerification).FullName);
        root.GetProperty("aggregateId").GetString().ShouldBe("party-1");
        root.GetProperty("payload").GetProperty("partyId").GetString().ShouldBe("party-1");
        root.GetProperty("payload").GetProperty("tenantId").GetString().ShouldBe("tenant-a");
    }

    [Fact]
    public async Task CancelErasureAsync_SubmitsCancelPartyErasureCommandContractAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(new { correlationId = "corr-cancel" }),
            "application/json");
        var client = CreateHttpClient(handler);

        AdminPortalGdprCommandResult result = await client.CancelErasureAsync("party-1", CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalGdprOutcome.Accepted);
        result.CorrelationId.ShouldBe("corr-cancel");
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/commands");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("domain").GetString().ShouldBe("party");
        root.GetProperty("commandType").GetString().ShouldBe(typeof(CancelPartyErasure).FullName);
        root.GetProperty("aggregateId").GetString().ShouldBe("party-1");
        root.GetProperty("payload").GetProperty("partyId").GetString().ShouldBe("party-1");
        root.GetProperty("payload").GetProperty("tenantId").GetString().ShouldBe("tenant-a");
    }

    [Fact]
    public async Task GetConsentAsync_UsesPartyDetailProjectionQueryInsteadOfGdprRouteNameAsync()
    {
        var detail = new PartyDetail
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-1",
                    ChannelId = "channel-1",
                    Purpose = "billing",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "admin",
                },
            ],
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = detail }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        IReadOnlyList<ConsentRecord> result = await client.GetConsentAsync("party-1", CancellationToken.None);

        result.ShouldHaveSingleItem().Purpose.ShouldBe("billing");
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("GetParty");
        root.GetProperty("projectionType").GetString().ShouldBe(PartyProjectionNames.Detail);
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionActor");
    }

    [Fact]
    public async Task ExportPartyDataAsync_UsesAuthoritativeExportQueryAndSafeDownloadNameAsync()
    {
        var package = new PartyDataPortabilityPackage
        {
            PartyId = "party-export",
            TenantId = "tenant-a",
            Status = "Exported",
            ExportedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
            ExportedBy = "admin-user",
            CorrelationId = "corr-export",
            Party = new PartyDetail
            {
                Id = "party-export",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
            },
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = package }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        AdminPortalExportDownload export = await client.ExportPartyDataAsync("party-export", CancellationToken.None);

        export.ContentType.ShouldBe("application/json");
        export.FileName.ShouldBe("party-party-export-20260521T204500Z.json");
        export.FileName.ShouldNotContain("Ada", Case.Insensitive);
        export.FileName.ShouldNotContain("tenant-a", Case.Insensitive);
        JsonSerializer.Deserialize<PartyDataPortabilityPackage>(export.Payload, s_jsonOptions)!
            .Party!.DisplayName.ShouldBe("Ada Lovelace");

        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("ExportPartyData");
        root.GetProperty("projectionType").GetString().ShouldBe(PartyProjectionNames.Detail);
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionQueryActor");
    }

    [Fact]
    public async Task GetProcessingRecordsAsync_UsesAuthoritativeProcessingRecordsQueryAsync()
    {
        ProcessingActivityRecord[] records =
        [
            new()
            {
                SequenceNumber = 3,
                PartyId = "party-1",
                TenantId = "tenant-a",
                ActorId = "admin-user",
                CorrelationId = "corr-records",
                OperationCategory = "Restriction",
                Outcome = "Succeeded",
                EventType = "ProcessingRestricted",
                Timestamp = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
                Summary = "Processing restricted.",
            },
        ];
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = records }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        IReadOnlyList<ProcessingActivityRecord> result = await client.GetProcessingRecordsAsync("party-1", CancellationToken.None);

        result.Single().CorrelationId.ShouldBe("corr-records");
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("GetProcessingRecords");
        root.GetProperty("projectionType").GetString().ShouldBe(PartyProjectionNames.Detail);
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionQueryActor");
    }

    [Fact]
    public async Task GetErasureCertificateAsync_UsesAuthoritativeCertificateQueryAndAllowsNullPayloadAsync()
    {
        var certificate = new ErasureCertificate
        {
            PartyId = "party-erased",
            TenantId = "tenant-a",
            Timestamp = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
            KeyVersionsDestroyed = [1, 2],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = certificate }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        ErasureCertificate? result = await client.GetErasureCertificateAsync("party-erased", CancellationToken.None);

        result.ShouldNotBeNull();
        result.PartyId.ShouldBe("party-erased");
        result.TenantId.ShouldBe("tenant-a");
        result.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("GetErasureCertificate");
        root.GetProperty("projectionType").GetString().ShouldBe(PartyProjectionNames.Detail);
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionQueryActor");

        string serialized = JsonSerializer.Serialize(result, s_jsonOptions);
        serialized.ShouldNotContain("Ada", Case.Insensitive);
        serialized.ShouldNotContain("ada@example.test", Case.Insensitive);
        serialized.ShouldNotContain("stateKey", Case.Insensitive);
    }

    [Fact]
    public async Task GetErasureCertificateAsync_NullPayloadReturnsNullWithoutProblemDetailsLeakAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = (ErasureCertificate?)null }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        ErasureCertificate? result = await client.GetErasureCertificateAsync("party-missing", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RetryVerificationAsync_ForbiddenTenantDetailMapsToMissingTenantWithoutLeakAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Forbidden,
            JsonSerializer.Serialize(new { detail = "tenant=other party=p-99 ProblemDetails" }),
            "application/json");
        var client = CreateHttpClient(handler);

        AdminPortalGdprCommandResult result = await client.RetryErasureVerificationAsync("party-1", CancellationToken.None);

        result.Outcome.ShouldBe(AdminPortalGdprOutcome.MissingTenant);
        result.Detail.ShouldNotBeNull();
        result.Detail.ShouldNotContain("tenant=other", Case.Insensitive);
        result.Detail.ShouldNotContain("p-99", Case.Insensitive);
        result.Detail.ShouldNotContain("ProblemDetails", Case.Insensitive);
    }

    [Fact]
    public async Task GetErasureCertificateAsync_NotImplementedMapsToContractUnavailableWithoutDetailLeakAsync()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.NotImplemented,
            JsonSerializer.Serialize(new { detail = "No EventStore GDPR query contract is available for tenant=other party=p-99." }),
            "application/json");
        var client = CreateHttpClient(handler);

        PartiesClientException ex = await Should.ThrowAsync<PartiesClientException>(
            () => client.GetErasureCertificateAsync("party-1", CancellationToken.None));

        ex.Status.ShouldBe((int)HttpStatusCode.NotImplemented);
        ex.Title.ShouldBe(AdminPortalGdprOutcome.ContractUnavailable.ToString());
        ex.Detail.ShouldNotBeNull();
        ex.Detail.ShouldNotContain("tenant=other", Case.Insensitive);
        ex.Detail.ShouldNotContain("p-99", Case.Insensitive);
    }

    [Fact]
    public async Task GetErasureStatusAsync_UsesAuthoritativeStatusQueryAsync()
    {
        var expected = new PartyErasureStatusRecord
        {
            PartyId = "party-pending",
            TenantId = "tenant-a",
            Status = "ErasurePending",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { payload = expected }, s_jsonOptions),
            "application/json");
        var client = CreateHttpClient(handler);

        PartyErasureStatusRecord? status = await client.GetErasureStatusAsync("party-pending", CancellationToken.None);

        status.ShouldNotBeNull();
        status.Status.ShouldBe("ErasurePending");
        status.PartyId.ShouldBe("party-pending");
        status.TenantId.ShouldBe("tenant-a");
        JsonSerializer.Serialize(status, s_jsonOptions).ShouldNotContain("decrypt", Case.Insensitive);
        handler.LastRequest.ShouldNotBeNull().RequestUri!.PathAndQuery.ShouldBe("/api/v1/queries");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        JsonElement root = body.RootElement;
        root.GetProperty("queryType").GetString().ShouldBe("GetErasureStatus");
        root.GetProperty("projectionType").GetString().ShouldBe(PartyProjectionNames.Detail);
        root.GetProperty("projectionActorType").GetString().ShouldBe("PartyDetailProjectionQueryActor");
    }

    private static BindingFlags PublicInstance => BindingFlags.Public | BindingFlags.Instance;

    private static Type LoadClientType(string fullName)
    {
        Assembly clientAssembly = typeof(HttpPartiesQueryClient).Assembly;
        return clientAssembly.GetType(fullName, throwOnError: true)
            ?? throw new InvalidOperationException($"Unable to load {fullName}.");
    }

    private static string GetRoute(Type routes, string memberName)
    {
        MemberInfo member = routes.GetMember(memberName, BindingFlags.Public | BindingFlags.Static).SingleOrDefault()
            ?? throw new InvalidOperationException($"Missing route member {memberName}.");

        object? value = member switch
        {
            FieldInfo field => field.GetValue(null),
            PropertyInfo property => property.GetValue(null),
            _ => null,
        };

        return value as string
            ?? throw new InvalidOperationException($"Route member {memberName} must be a string.");
    }

    private static Type UnwrapTask(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static HttpAdminPortalGdprClient CreateHttpClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return new HttpAdminPortalGdprClient(
            httpClient,
            Options.Create(new PartiesClientOptions { BaseUrl = "https://localhost", Tenant = "tenant-a" }));
    }

    private sealed class RecordingHandler(
        HttpStatusCode statusCode,
        string responseBody,
        string contentType) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, contentType),
            };
        }
    }
}
