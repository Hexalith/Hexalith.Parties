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
