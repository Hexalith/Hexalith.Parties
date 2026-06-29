namespace Hexalith.Parties.Contracts;

public static class PartyActorIds
{
    public static string Detail(string tenantId, string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        return $"{tenantId}:{PartyProjectionNames.Detail}:{partyId}";
    }

    public static string Index(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return $"{tenantId}:{PartyProjectionNames.Index}";
    }
}
