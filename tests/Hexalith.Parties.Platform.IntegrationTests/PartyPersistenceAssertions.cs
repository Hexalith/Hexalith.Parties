namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Reuses the real persistence assertions after a lifecycle hold without a second canonical observation.</summary>
/// <param name="fixture">The actual runner-owned dependency fixture.</param>
/// <param name="output">The native test output.</param>
public sealed class PartyPersistenceAssertions(PlatformEnvironmentFixture fixture, ITestOutputHelper output)
{
    /// <summary>Runs initial persistence within the caller's remaining qualification lifetime.</summary>
    /// <param name="cancellationToken">The shared lifecycle qualification deadline.</param>
    /// <returns>The bounded real-service assertions and canonical observation.</returns>
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        new PartyEnvironmentTests(fixture, output).ExerciseRealDependenciesAsync(cancellationToken);

    /// <summary>Runs the complete original dependency, command, query and exact-stream assertions again.</summary>
    /// <returns>The bounded real-service recheck.</returns>
    public Task RecheckAsync() => RecheckAsync(TestContext.Current.CancellationToken);

    /// <summary>Rechecks persistence within the caller's remaining qualification lifetime.</summary>
    /// <param name="cancellationToken">The shared lifecycle qualification deadline.</param>
    /// <returns>The bounded real-service recheck without another canonical observation.</returns>
    public Task RecheckAsync(CancellationToken cancellationToken) =>
        new PartyEnvironmentTests(fixture, output).RecheckRealDependenciesAsync(cancellationToken);
}
