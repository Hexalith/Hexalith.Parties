// ATDD red-phase transport scaffolds for Story 10.2 — Admin Portal GDPR Operations.
// These tests pin the frontend-facing client/adapter contract for existing admin
// GDPR endpoints. They are skipped until the green phase adds the portal GDPR
// operation seam on top of the current HttpClient-based Parties client stack.

using System.Reflection;

using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Security;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.AdminPortal;

/// <summary>
/// Story 10.2 — AC2 through AC6 and AC8. Reflection keeps these scaffolds compiling
/// before the GDPR portal adapter exists, while activated tests fail until the
/// expected methods, route constants, and bounded outcome names are implemented.
/// </summary>
public sealed class AdminPortalGdprOperationContractTests
{
    private const string SkipReason =
        "TDD red phase — Story 10.2 must add an admin GDPR portal client/adapter over existing admin endpoints.";

    private const string AdapterTypeName = "Hexalith.Parties.Client.AdminPortal.IAdminPortalGdprClient";
    private const string RouteMapTypeName = "Hexalith.Parties.Client.AdminPortal.AdminPortalGdprRoutes";
    private const string OutcomeTypeName = "Hexalith.Parties.Client.AdminPortal.AdminPortalGdprOutcome";

    [Fact(Skip = SkipReason)]
    public void AdminGdprClient_DefinesErasureRequestStatusCertificateAndRetryMethods()
    {
        Type adapter = LoadClientType(AdapterTypeName);

        adapter.GetMethod("RequestErasureAsync", PublicInstance)
            .ShouldNotBeNull("AC2 requires Request Erasure to call POST /api/v1/admin/parties/{partyId}/erase.");
        adapter.GetMethod("GetErasureStatusAsync", PublicInstance)
            .ShouldNotBeNull("AC2/AC3 require polling or refreshing authoritative erasure status.");
        adapter.GetMethod("GetErasureCertificateAsync", PublicInstance)
            .ShouldNotBeNull("AC2 requires rendering the verification report when available.");
        adapter.GetMethod("RetryErasureVerificationAsync", PublicInstance)
            .ShouldNotBeNull("AC3 requires retry only for supported partial or failed verification states.");
    }

    [Fact(Skip = SkipReason)]
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

    [Fact(Skip = SkipReason)]
    public void AdminGdprRoutes_MapToExistingAdminEndpointContracts()
    {
        Type routes = LoadClientType(RouteMapTypeName);

        GetRoute(routes, "EraseParty").ShouldBe("api/v1/admin/parties/{partyId}/erase");
        GetRoute(routes, "ErasureStatus").ShouldBe("api/v1/admin/parties/{partyId}/erasure-status");
        GetRoute(routes, "ErasureCertificate").ShouldBe("api/v1/admin/parties/{partyId}/erasure-certificate");
        GetRoute(routes, "RetryVerification").ShouldBe("api/v1/admin/parties/{partyId}/retry-verification");
        GetRoute(routes, "RestrictProcessing").ShouldBe("api/v1/admin/parties/{partyId}/restrict");
        GetRoute(routes, "LiftRestriction").ShouldBe("api/v1/admin/parties/{partyId}/lift-restriction");
        GetRoute(routes, "Consent").ShouldBe("api/v1/admin/parties/{partyId}/consent");
        GetRoute(routes, "ConsentById").ShouldBe("api/v1/admin/parties/{partyId}/consent/{consentId}");
        GetRoute(routes, "Export").ShouldBe("api/v1/admin/parties/{partyId}/export");
        GetRoute(routes, "ProcessingRecords").ShouldBe("api/v1/admin/parties/{partyId}/processing-records");
    }

    [Fact(Skip = SkipReason)]
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

    [Fact(Skip = SkipReason)]
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

    [Fact(Skip = SkipReason)]
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
}
