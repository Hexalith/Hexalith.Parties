using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Checks the same registry assertion used by the real dependency flow without creating infrastructure.</summary>
public sealed class MemoriesTenantObservationTests
{
    /// <summary>Only an active current registry entry satisfies the dependency assertion.</summary>
    /// <param name="status">The currently observed lifecycle state.</param>
    [Theory]
    [InlineData("provisioning")]
    [InlineData("deleting")]
    [InlineData("failed")]
    public void InactiveCurrentTenantFails(string status)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { id = "run-tenant", status }));
        Should.Throw<ShouldAssertException>(() => PartyEnvironmentTests.AssertActiveMemoriesTenant(document.RootElement, "run-tenant"));
    }

    /// <summary>The actual active response shape passes the real dependency assertion.</summary>
    [Fact]
    public void ActiveCurrentTenantPasses()
    {
        using JsonDocument document = JsonDocument.Parse("""{"id":"run-tenant","status":"active"}""");
        PartyEnvironmentTests.AssertActiveMemoriesTenant(document.RootElement, "run-tenant");
    }
}
