namespace Hexalith.Parties.Queries;

/// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes"/>.</summary>
[Obsolete("Use PartyDetailQueryTypes; SDK queries do not use projection actors.")]
public static class PartyDetailProjectionQueryActor
{
    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.ActorTypeName"/>.</summary>
    public const string ActorTypeName = PartyDetailQueryTypes.ActorTypeName;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.ProjectionType"/>.</summary>
    public const string ProjectionType = PartyDetailQueryTypes.ProjectionType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.DataPortabilityProjectionType"/>.</summary>
    public const string DataPortabilityProjectionType = PartyDetailQueryTypes.DataPortabilityProjectionType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.GetPartyQueryType"/>.</summary>
    public const string GetPartyQueryType = PartyDetailQueryTypes.GetPartyQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.PartyDetailQueryType"/>.</summary>
    public const string PartyDetailQueryType = PartyDetailQueryTypes.PartyDetailQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.ExportPartyDataQueryType"/>.</summary>
    public const string ExportPartyDataQueryType = PartyDetailQueryTypes.ExportPartyDataQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.GetProcessingRecordsQueryType"/>.</summary>
    public const string GetProcessingRecordsQueryType = PartyDetailQueryTypes.GetProcessingRecordsQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.GetErasureStatusQueryType"/>.</summary>
    public const string GetErasureStatusQueryType = PartyDetailQueryTypes.GetErasureStatusQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.GetErasureCertificateQueryType"/>.</summary>
    public const string GetErasureCertificateQueryType = PartyDetailQueryTypes.GetErasureCertificateQueryType;

    /// <summary>Compatibility alias for <see cref="PartyDetailQueryTypes.PartyDomain"/>.</summary>
    public const string PartyDomain = PartyDetailQueryTypes.PartyDomain;
}
