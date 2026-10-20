#pragma warning disable CS8620 // DaprClient nullable generic arguments in substitutes.

using System.Reflection;
using System.Text.Json;

using Dapr.Client;

using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;
using Hexalith.Parties.Tests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Security;

public sealed class SecurityRegistrationTests
{
    [Fact]
    public async Task ProductionRegistrationsBindSecurityStoreForErasureAuditAndRotationRoundTrips()
    {
        const string component = "configured-security";
        using HealthEndpointIntegrationTests.HealthTestFactory factory = new(new Dictionary<string, string>
        {
            ["Parties:Security:StateStoreName"] = component,
            ["EventStore:DataProtection:PersistToStateStore"] = "false",
        });
        Dictionary<string, object> state = new(StringComparer.Ordinal);
        ConfigureState<PartyErasureStatusRecord>(factory.DaprClient, component, state);
        ConfigureState<ErasureCertificate>(factory.DaprClient, component, state);
        ConfigureState<ErasureVerificationReport>(factory.DaprClient, component, state);
        ConfigureState<List<KeyOperationAuditEntry>>(factory.DaprClient, component, state);
        // The persisted rotation record is internal; configure the same generic Dapr seam
        // without changing the production assembly's visibility for this test.
        Type progressType = typeof(TenantKeyRotationService).Assembly.GetType(
            "Hexalith.Parties.Security.TenantKeyRotationProgress", throwOnError: true)!;
        typeof(SecurityRegistrationTests).GetMethod(nameof(ConfigureState), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(progressType).Invoke(null, [factory.DaprClient, component, state]);

        using HttpClient client = factory.CreateClient();
        factory.Services.GetRequiredService<IOptions<PartiesSecurityStoreOptions>>().Value.StateStoreName.ShouldBe(component);
        IPartyErasureRecordStore erasure = factory.Services.GetRequiredService<IPartyErasureRecordStore>();
        PartyErasureStatusRecord status = new() { TenantId = "tenant", PartyId = "party", Status = "erased", UpdatedAt = DateTimeOffset.UtcNow };
        ErasureCertificate certificate = new() { TenantId = "tenant", PartyId = "party", Timestamp = DateTimeOffset.UtcNow, KeyVersionsDestroyed = [1], VerificationStatus = default };
        ErasureVerificationReport report = new() { TenantId = "tenant", PartyId = "party", Timestamp = DateTimeOffset.UtcNow, StoreResults = [], OverallStatus = default };
        await erasure.SaveStatusAsync(status);
        await erasure.SaveCertificateAsync(certificate);
        await erasure.SaveVerificationReportAsync(report);
        (await erasure.GetStatusAsync("tenant", "party")).ShouldBe(status);
        (await erasure.GetCertificateAsync("tenant", "party")).ShouldBe(certificate);
        (await erasure.GetVerificationReportAsync("tenant", "party")).ShouldBe(report);
        state["tenant:erasure-status:party"].ShouldBe(status);
        state["tenant:erasure:party"].ShouldBe(certificate);
        state["tenant:erasure-report:party"].ShouldBe(report);

        IKeyOperationAuditService audit = factory.Services.GetRequiredService<IKeyOperationAuditService>();
        KeyOperationAuditEntry entry = new()
        {
            OperationType = KeyOperationType.Create, TenantId = "tenant", PartyId = "party",
            KeyVersion = 1, Timestamp = DateTimeOffset.UtcNow, CorrelationId = "correlation",
        };
        await audit.RecordOperationAsync(entry);
        (await audit.GetAuditTrailAsync("tenant", "party")).ShouldBe([entry]);
        ((List<KeyOperationAuditEntry>)state["tenant:party-key-audit:party"]).ShouldBe([entry]);

        await factory.Services.GetRequiredService<IKeyStorageBackend>().CreateSecretAsync("tenant/parties/party/v1", [1, 2, 3, 4]);
        ITenantKeyRotationService rotation = factory.Services.GetRequiredService<ITenantKeyRotationService>();
        TenantKeyRotationRequest request = new() { TenantId = "tenant", OperationId = "rotation" };
        TenantKeyRotationStatus rotated = await rotation.RotateAsync(request);
        rotated.Phase.ShouldBe(TenantKeyRotationPhase.Completed);
        rotated.ProcessedCount.ShouldBe(1);
        (await rotation.GetStatusAsync("tenant", "rotation")).ShouldBe(rotated);
        (await rotation.RotateAsync(request)).ShouldBe(rotated);
        JsonSerializer.SerializeToElement(state["tenant:tenant-key-rotation:rotation"])
            .GetProperty("Status").GetProperty("OperationId").GetString().ShouldBe("rotation");
        (await audit.GetAuditTrailAsync("tenant", "tenant-key-rotation")).Single().OperationId.ShouldBe("rotation");
        ((List<KeyOperationAuditEntry>)state["tenant:party-key-audit:tenant-key-rotation"]).Single().OperationType.ShouldBe(KeyOperationType.TenantRotate);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void InvalidSecurityStoreFailsAtStartupBeforeDaprOperations(string name)
    {
        using HealthEndpointIntegrationTests.HealthTestFactory factory = new(new Dictionary<string, string>
        {
            ["Parties:Security:StateStoreName"] = name,
            ["EventStore:DataProtection:PersistToStateStore"] = "false",
        });
        OptionsValidationException failure = Should.Throw<OptionsValidationException>(() => factory.CreateClient());
        failure.Message.ShouldContain("Parties:Security:StateStoreName must not be empty.");
        factory.DaprClient.ReceivedCalls().ShouldBeEmpty();
    }

    private static void ConfigureState<T>(DaprClient client, string component, Dictionary<string, object> state)
        where T : class
    {
        client.SaveStateAsync(component, Arg.Any<string>(), Arg.Any<T>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => { state[call.ArgAt<string>(1)] = call.ArgAt<T>(2); return Task.CompletedTask; });
        client.GetStateAsync<T>(component, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => state.GetValueOrDefault(call.ArgAt<string>(1)) as T);
        client.GetStateAndETagAsync<T>(component, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => (state.GetValueOrDefault(call.ArgAt<string>(1)) as T, "etag"));
        client.TrySaveStateAsync(component, Arg.Any<string>(), Arg.Any<T>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => { state[call.ArgAt<string>(1)] = call.ArgAt<T>(2); return true; });
    }
}
