namespace Hexalith.Parties.Queries;

/// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes"/>.</summary>
[Obsolete("Use PartyIndexQueryTypes; SDK queries do not use projection actors.")]
public static class PartyIndexProjectionQueryActor
{
    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.ActorTypeName"/>.</summary>
    public const string ActorTypeName = PartyIndexQueryTypes.ActorTypeName;

    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.ListAggregateId"/>.</summary>
    public const string ListAggregateId = PartyIndexQueryTypes.ListAggregateId;

    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.PartyDomain"/>.</summary>
    public const string PartyDomain = PartyIndexQueryTypes.PartyDomain;

    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.PartyIndexQueryType"/>.</summary>
    public const string PartyIndexQueryType = PartyIndexQueryTypes.PartyIndexQueryType;

    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.PartySearchQueryType"/>.</summary>
    public const string PartySearchQueryType = PartyIndexQueryTypes.PartySearchQueryType;

    /// <summary>Compatibility alias for <see cref="PartyIndexQueryTypes.ProjectionType"/>.</summary>
    public const string ProjectionType = PartyIndexQueryTypes.ProjectionType;
}
