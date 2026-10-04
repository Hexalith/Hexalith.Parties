using Hexalith.Parties.Contracts.ValueObjects;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Safe source identity and current eligibility observation, without profile details.</summary>
/// <param name="ContractVersion">The identity contract version.</param>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="PartyId">The exact Party.</param>
/// <param name="Classification">The positive classification.</param>
/// <param name="IsActive">The recorded current active state.</param>
/// <param name="IsRestricted">The recorded processing restriction.</param>
/// <param name="IsErasingOrErased">The recorded erasure ineligibility.</param>
/// <param name="SourcePosition">The complete stable source head.</param>
/// <param name="ObservedAt">The observation instant.</param>
/// <param name="ObservationId">The coherent opaque source observation.</param>
/// <param name="HumanBinding">Verified opaque human attribution, if available.</param>
public sealed record PartyIdentityEvidence(
    int ContractVersion,
    string TenantId,
    string PartyId,
    PartyIdentityClassification Classification,
    bool IsActive,
    bool IsRestricted,
    bool IsErasingOrErased,
    long SourcePosition,
    DateTimeOffset ObservedAt,
    string ObservationId,
    HumanActorBindingEvidence? HumanBinding);
