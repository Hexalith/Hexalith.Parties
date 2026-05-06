namespace Hexalith.Parties.CommandApi.Tests.Controllers;

internal static class TenantActorIds
{
    public static string PartyDetail(string tenantId, string partyId) => $"{tenantId}:party-detail:{partyId}";

    public static string PartyDetailPrefix(string tenantId) => $"{tenantId}:party-detail:";

    public static string PartyIndex(string tenantId) => $"{tenantId}:party-index";
}
