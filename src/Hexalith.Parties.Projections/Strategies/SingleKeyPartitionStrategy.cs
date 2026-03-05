using Hexalith.Parties.Projections.Abstractions;

namespace Hexalith.Parties.Projections.Strategies;

public sealed class SingleKeyPartitionStrategy : IIndexPartitionStrategy
{
    public string GetPartitionKey(string partyId) => "default";
}
