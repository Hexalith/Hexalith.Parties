namespace Hexalith.Parties.Projections.Abstractions;

public interface IIndexPartitionStrategy
{
    string GetPartitionKey(string partyId);
}
