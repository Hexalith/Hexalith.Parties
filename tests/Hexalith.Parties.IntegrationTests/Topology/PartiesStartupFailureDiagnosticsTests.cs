using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Startup diagnostics must preserve the primary refusal while revealing no captured resource payload.</summary>
public sealed class PartiesStartupFailureDiagnosticsTests
{
    [Fact]
    public void ReportsOnlyExceptionTypeIdentifiers()
    {
        string[] lines = ["System.InvalidOperationException: password=private-value token=private-token",
            "at Microsoft.Extensions.Hosting.BackgroundServiceExceptionBehavior", "System.InvalidOperationException: repeat",
            "secret Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException payload=private-payload"];
        PartiesStartupFailureDiagnostics.ExceptionTypes(lines).ShouldBe([
            "Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException", "System.InvalidOperationException"]);
    }

    [Fact]
    public void RetainsMethodIdentifiersWithoutArgumentsOrSourcePaths()
    {
        PartiesStartupFailureDiagnostics.FrameMethods([
            "   at Hexalith.Example.StartAsync(String secret=private-token) in /private/source.cs:line 1"]).ShouldBe(["Hexalith.Example.StartAsync"]);
    }

    [Fact]
    public void ReportsStaticRefusalCodesWithoutMessagesOrValues()
    {
        string[] lines = ["Installed operation digests differ; credential=private-token",
            "Selected child dependency secret-path is absent from its immutable output snapshot.",
            "Could not load file: manifest definition does not match; password=private-value"];
        PartiesStartupFailureDiagnostics.ErrorCodes(lines).ShouldBe([
            "assembly-binding-failure", "operation-inventory-mismatch", "selected-child-dependency-unavailable"]);
    }

    [Fact]
    public void UnknownSensitiveTextProducesNoDiagnosticValues()
    {
        string[] lines = ["Bearer private-token", "password=private-value", "{payload:private-payload}"];
        PartiesStartupFailureDiagnostics.ExceptionTypes(lines).ShouldBeEmpty();
        PartiesStartupFailureDiagnostics.ErrorCodes(lines).ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingApplicationPreservesTheOriginalFailure()
        => (await PartiesStartupFailureDiagnostics.DescribeAsync(null)).ShouldBeEmpty();
}
