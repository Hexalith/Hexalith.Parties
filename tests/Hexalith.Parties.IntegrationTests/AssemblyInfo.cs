namespace Hexalith.Parties.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollection
{
    public const string Name = "Non-parallel";
}
