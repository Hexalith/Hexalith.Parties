using Hexalith.Parties.TestInfrastructure;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Verifies cleanup fails closed on Docker errors without modifying foreign resources.</summary>
public sealed class AspireMtlsCleanupFailureTests
{
    [Theory]
    [InlineData("unavailable")]
    [InlineData("container-ls")]
    [InlineData("network-ls")]
    [InlineData("container-inspect")]
    [InlineData("network-inspect")]
    public async Task Cleanup_DockerFailure_DoesNotReportSuccessOrTouchForeignResources(string failure)
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The mTLS bootstrap requires Linux bash.");
        }

        using var commands = new MtlsBootstrapCommandDouble();
        string certificates = Path.Combine(commands.Root, "certificates");
        (int prepared, string prepareError) = await commands.RunAsync(
            "--prepare-only", certificates, TestContext.Current.CancellationToken).ConfigureAwait(true);
        prepared.ShouldBe(0, prepareError);
        commands.OwnedRoles.Length.ShouldBe(3);
        commands.SetFailure(failure);
        int firstCleanupRequest = commands.Requests.Length;

        (int exitCode, string error) = await commands.RunAsync(
            "--cleanup-only", certificates, TestContext.Current.CancellationToken).ConfigureAwait(true);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("Docker unavailable");
        commands.ForeignResourcesPreserved.ShouldBeTrue();
        commands.Requests.Skip(firstCleanupRequest).ShouldNotContain(request =>
            request.Contains("unrelated-service", StringComparison.Ordinal)
            || request.Contains("unrelated-network", StringComparison.Ordinal));
        File.Exists(Path.Combine(certificates, ".owned-control-plane-network")).ShouldBeTrue();
        File.Exists(Path.Combine(certificates, "issuer.key")).ShouldBeTrue();
    }

    [Fact]
    public async Task SessionBootstrap_SeparatesNamesPortsAndCleanupOwnership()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The mTLS bootstrap requires Linux bash."); }
        using var commands = new MtlsBootstrapCommandDouble();
        static Dictionary<string, string?> Settings(string id, int port) => new()
        {
            ["DAPR_MTLS_SESSION_ID"] = id, ["DAPR_MTLS_NAMESPACE"] = "parties-" + id,
            ["DAPR_MTLS_SENTRY_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DAPR_MTLS_SENTRY_HEALTH_PORT"] = (port + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DAPR_MTLS_PLACEMENT_PORT"] = (port + 2).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DAPR_MTLS_PLACEMENT_HEALTH_PORT"] = (port + 3).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DAPR_MTLS_SCHEDULER_PORT"] = (port + 4).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DAPR_MTLS_SCHEDULER_HEALTH_PORT"] = (port + 5).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        Dictionary<string, string?> first = Settings("first", 42001);
        Dictionary<string, string?> second = Settings("second", 43001);
        string firstCertificates = Path.Combine(commands.Root, "first-certificates");
        string secondCertificates = Path.Combine(commands.Root, "second-certificates");
        foreach ((string certificates, Dictionary<string, string?> settings) in new[] { (firstCertificates, first), (secondCertificates, second) })
        {
            (int prepared, string error) = await commands.RunAsync("--prepare-only", certificates, TestContext.Current.CancellationToken, settings).ConfigureAwait(true);
            prepared.ShouldBe(0, error);
        }
        commands.Requests.ShouldContain(request => request.Contains("--name hexalith-parties-dapr-scheduler-first", StringComparison.Ordinal)
            && request.Contains("127.0.0.1:42005:50006", StringComparison.Ordinal)
            && request.Contains("DAPR_CONTROLPLANE_NAMESPACE=parties-first", StringComparison.Ordinal));
        commands.Requests.ShouldContain(request => request.Contains("--name hexalith-parties-dapr-scheduler-second", StringComparison.Ordinal)
            && request.Contains("127.0.0.1:43005:50006", StringComparison.Ordinal));
        (int cleaned, string cleanupError) = await commands.RunAsync("--cleanup-only", firstCertificates, TestContext.Current.CancellationToken, first).ConfigureAwait(true);
        cleaned.ShouldBe(0, cleanupError);
        foreach (string role in new[] { "sentry", "placement", "scheduler" })
        {
            File.Exists(Path.Combine(commands.Root, $"hexalith-parties-dapr-{role}-first")).ShouldBeFalse();
            File.Exists(Path.Combine(commands.Root, $"hexalith-parties-dapr-{role}-second")).ShouldBeTrue();
        }
        (int secondCleanup, string secondError) = await commands.RunAsync("--cleanup-only", secondCertificates, TestContext.Current.CancellationToken, second).ConfigureAwait(true);
        secondCleanup.ShouldBe(0, secondError);
        commands.ForeignResourcesPreserved.ShouldBeTrue();
    }

    [Fact]
    public async Task SessionCleanup_ConflictingNetworkRetainsOwnershipUntilSafeRetry()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The mTLS bootstrap requires Linux bash."); }
        using var commands = new MtlsBootstrapCommandDouble();
        string certificates = Path.Combine(commands.Root, "certificates");
        var settings = new Dictionary<string, string?> { ["DAPR_MTLS_SESSION_ID"] = "network-owner" };
        (int prepared, string preparationError) = await commands.RunAsync("--prepare-only", certificates,
            TestContext.Current.CancellationToken, settings).ConfigureAwait(true);
        prepared.ShouldBe(0, preparationError);
        settings["FAKE_MTLS_NETWORK_LABEL"] = "foreign-owner";
        (int rejected, string rejection) = await commands.RunAsync("--cleanup-only", certificates,
            TestContext.Current.CancellationToken, settings).ConfigureAwait(true);
        rejected.ShouldNotBe(0);
        rejection.ShouldContain("retain session evidence");
        File.Exists(Path.Combine(certificates, ".owned-control-plane-network")).ShouldBeTrue();
        File.Exists(Path.Combine(certificates, "issuer.key")).ShouldBeTrue();
        commands.OwnedNetworkExists.ShouldBeTrue();
        settings.Remove("FAKE_MTLS_NETWORK_LABEL");
        (int cleaned, string error) = await commands.RunAsync("--cleanup-only", certificates,
            TestContext.Current.CancellationToken, settings).ConfigureAwait(true);
        cleaned.ShouldBe(0, error);
        commands.OwnedNetworkExists.ShouldBeFalse();
        commands.ForeignResourcesPreserved.ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid/name", "42001", "42002")]
    [InlineData("conflict", "42001", "42001")]
    [InlineData("privileged", "80", "42002")]
    public async Task SessionBootstrap_RejectsConflictingConfigurationBeforeDocker(string id, string firstPort, string secondPort)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The mTLS bootstrap requires Linux bash."); }
        using var commands = new MtlsBootstrapCommandDouble();
        (int exitCode, _) = await commands.RunAsync("--prepare-only", Path.Combine(commands.Root, "certificates"),
            TestContext.Current.CancellationToken, new Dictionary<string, string?>
            {
                ["DAPR_MTLS_SESSION_ID"] = id, ["DAPR_MTLS_SENTRY_PORT"] = firstPort,
                ["DAPR_MTLS_SENTRY_HEALTH_PORT"] = secondPort,
            }).ConfigureAwait(true);
        exitCode.ShouldNotBe(0);
        commands.Requests.ShouldBeEmpty();
        commands.ForeignResourcesPreserved.ShouldBeTrue();
    }

    [Fact]
    public async Task Cleanup_ConfirmedAbsentResources_SucceedsWithoutTouchingForeignResources()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The mTLS bootstrap requires Linux bash.");
        }

        using var commands = new MtlsBootstrapCommandDouble();
        (int exitCode, string error) = await commands.RunAsync(
            "--cleanup-only", Path.Combine(commands.Root, "absent-certificates"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        exitCode.ShouldBe(0, error);
        commands.OwnedRoles.ShouldBeEmpty();
        commands.ForeignResourcesPreserved.ShouldBeTrue();
        commands.Requests.ShouldNotContain(request => request.Contains(" inspect ", StringComparison.Ordinal)
            || request.StartsWith("docker rm ", StringComparison.Ordinal)
            || request.StartsWith("docker network rm ", StringComparison.Ordinal));
    }
}
