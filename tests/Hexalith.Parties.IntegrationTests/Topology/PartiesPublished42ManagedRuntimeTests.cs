using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>The exact SDK workaround refuses runtime drift before any native resources start.</summary>
public sealed class PartiesPublished42ManagedRuntimeTests
{
    private static string SharedRoot => Path.Combine(Path.GetTempPath(), "runtime-profile-tests", "shared", "Microsoft.NETCore.App", "11.0.0");

    [Fact]
    public void AcceptsTrustedMatchingSharedCoreAndJson()
        => PartiesPublished42ManagedRuntime.ValidateHost(new Version(11, 0, 0), Path.Combine(SharedRoot, "System.Private.CoreLib.dll"),
            new Version(11, 0, 0, 0), Path.Combine(SharedRoot, "System.Text.Json.dll"));

    [Theory]
    [InlineData("10.0.15", "11.0.0.0", false)]
    [InlineData("11.0.1", "11.0.0.0", false)]
    [InlineData("11.0.0", "10.0.0.0", false)]
    [InlineData("11.0.0", "11.0.0.0", true)]
    public void RefusesUnqualifiedParentOrPackageLocalJson(string runtime, string json, bool packageLocal)
        => Should.Throw<InvalidOperationException>(() => PartiesPublished42ManagedRuntime.ValidateHost(Version.Parse(runtime),
            Path.Combine(SharedRoot, "System.Private.CoreLib.dll"), Version.Parse(json),
            packageLocal ? Path.Combine(Path.GetTempPath(), "nuget", "System.Text.Json.dll") : Path.Combine(SharedRoot, "System.Text.Json.dll")));

    [Fact]
    public void AcceptsOnlyTheMeasuredHighestRuntimePair()
        => PartiesPublished42ManagedRuntime.ValidateInventory("Microsoft.NETCore.App 10.0.15 [/runtime/shared/Microsoft.NETCore.App]\nMicrosoft.NETCore.App 11.0.0 [/runtime/shared/Microsoft.NETCore.App]\nMicrosoft.AspNetCore.App 11.0.0 [/runtime/shared/Microsoft.AspNetCore.App]\n");

    [Theory]
    [InlineData("")]
    [InlineData("Microsoft.NETCore.App 11.0.0 [/runtime/shared/Microsoft.NETCore.App]")]
    [InlineData("Microsoft.NETCore.App 11.0.0 [/runtime/shared/Microsoft.NETCore.App]\nMicrosoft.AspNetCore.App 11.0.1 [/runtime/shared/Microsoft.AspNetCore.App]")]
    [InlineData("Microsoft.NETCore.App 12.0.0-preview.1 [/runtime/shared/Microsoft.NETCore.App]\nMicrosoft.AspNetCore.App 11.0.0 [/runtime/shared/Microsoft.AspNetCore.App]")]
    [InlineData("Microsoft.NETCore.App 11.0.0 foreign\nMicrosoft.AspNetCore.App 11.0.0 [/runtime/shared/Microsoft.AspNetCore.App]")]
    public void RefusesMissingFuturePreviewOrMalformedRuntimeInventory(string inventory)
        => Should.Throw<InvalidOperationException>(() => PartiesPublished42ManagedRuntime.ValidateInventory(inventory));
}
