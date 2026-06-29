using System.Security.Claims;

namespace Hexalith.Parties.Contracts.Authorization;

public static class PartiesClaimExtraction
{
    public static PartiesClaimExtractionResult TryGetTenantId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ExtractSingle(principal.FindAll(PartiesClaimTypes.EventStoreTenant));
    }

    public static PartiesClaimExtractionResult TryGetTenantId(this ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return ExtractSingle(identity.FindAll(PartiesClaimTypes.EventStoreTenant));
    }

    public static PartiesClaimExtractionResult TryGetPartyId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ExtractSingle(principal.FindAll(PartiesClaimTypes.PartyId));
    }

    public static PartiesClaimExtractionResult TryGetPartyId(this ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return ExtractSingle(identity.FindAll(PartiesClaimTypes.PartyId));
    }

    public static PartiesClaimExtractionResult TryGetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ExtractUserId(principal.FindAll);
    }

    public static PartiesClaimExtractionResult TryGetUserId(this ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return ExtractUserId(identity.FindAll);
    }

    private static PartiesClaimExtractionResult ExtractUserId(Func<string, IEnumerable<Claim>> findAll)
    {
        PartiesClaimExtractionResult subject = ExtractSingle(findAll(PartiesClaimTypes.Subject));
        if (subject.Succeeded || subject.Failure == PartiesClaimExtractionFailure.Ambiguous)
        {
            return subject;
        }

        return ExtractSingle(findAll(PartiesClaimTypes.ObjectId));
    }

    private static PartiesClaimExtractionResult ExtractSingle(IEnumerable<Claim> claims)
    {
        Claim[] matchingClaims = claims.ToArray();
        if (matchingClaims.Length == 0)
        {
            return PartiesClaimExtractionResult.Missing;
        }

        string[] values = matchingClaims
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (values.Length == 0)
        {
            return PartiesClaimExtractionResult.Empty;
        }

        return values.Length == 1
            ? PartiesClaimExtractionResult.Success(values[0])
            : PartiesClaimExtractionResult.Ambiguous;
    }
}
