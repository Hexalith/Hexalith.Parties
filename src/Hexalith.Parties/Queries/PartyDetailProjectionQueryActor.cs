using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.Queries;

/// <summary>Stable Party detail query discriminators retained for wire compatibility.</summary>
public static class PartyDetailProjectionQueryActor
{
    public const string ActorTypeName = nameof(PartyDetailProjectionQueryActor);
    public const string ProjectionType = PartyProjectionNames.Detail;
    public const string DataPortabilityProjectionType = "party-data-portability";
    public const string GetPartyQueryType = "GetParty";
    public const string PartyDetailQueryType = "PartyDetail";
    public const string ExportPartyDataQueryType = "ExportPartyData";
    public const string GetProcessingRecordsQueryType = "GetProcessingRecords";
    public const string GetErasureStatusQueryType = "GetErasureStatus";
    public const string GetErasureCertificateQueryType = "GetErasureCertificate";
    public const string PartyDomain = "party";
}
