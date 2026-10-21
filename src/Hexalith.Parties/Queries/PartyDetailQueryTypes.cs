using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.Queries;

/// <summary>Stable SDK Party detail query discriminators.</summary>
public static class PartyDetailQueryTypes
{
    /// <summary>The historical wire routing identity retained for gateway compatibility.</summary>
    public const string ActorTypeName = "PartyDetailProjectionQueryActor";
    /// <summary>The stable ProjectionType discriminator.</summary>
    public const string ProjectionType = PartyProjectionNames.Detail;
    /// <summary>The stable DataPortabilityProjectionType discriminator.</summary>
    public const string DataPortabilityProjectionType = "party-data-portability";
    /// <summary>The stable GetPartyQueryType discriminator.</summary>
    public const string GetPartyQueryType = "GetParty";
    /// <summary>The stable PartyDetailQueryType discriminator.</summary>
    public const string PartyDetailQueryType = "PartyDetail";
    /// <summary>The stable ExportPartyDataQueryType discriminator.</summary>
    public const string ExportPartyDataQueryType = "ExportPartyData";
    /// <summary>The stable GetProcessingRecordsQueryType discriminator.</summary>
    public const string GetProcessingRecordsQueryType = "GetProcessingRecords";
    /// <summary>The stable GetErasureStatusQueryType discriminator.</summary>
    public const string GetErasureStatusQueryType = "GetErasureStatus";
    /// <summary>The stable GetErasureCertificateQueryType discriminator.</summary>
    public const string GetErasureCertificateQueryType = "GetErasureCertificate";
    /// <summary>The stable PartyDomain discriminator.</summary>
    public const string PartyDomain = "party";
}
