namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Reuses the real persistence assertions after a lifecycle hold without a second canonical observation.</summary>
/// <param name="fixture">The actual runner-owned dependency fixture.</param>
/// <param name="output">The native test output.</param>
public sealed class PartyPersistenceAssertions(PlatformEnvironmentFixture fixture, ITestOutputHelper output)
{
    /// <summary>Runs the complete original dependency, command, query and exact-stream assertions again.</summary>
    /// <returns>The bounded real-service recheck.</returns>
    public Task RecheckAsync() => new PartyEnvironmentTests(fixture, output).RecheckRealDependenciesAsync();
}
