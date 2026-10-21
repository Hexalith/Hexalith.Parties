namespace Hexalith.Parties.Projections.Serialization;

/// <summary>The allowlisted event type and whether its short name is ambiguous.</summary>
/// <param name="Type">The resolved type, or null when unavailable.</param>
/// <param name="IsAmbiguous">Whether multiple contract types share the short name.</param>
internal readonly record struct ResolveOutcome(Type? Type, bool IsAmbiguous)
{
    /// <summary>An unresolved event name.</summary>
    public static ResolveOutcome Unknown { get; } = new(null, IsAmbiguous: false);

    /// <summary>A short name shared by multiple contract types.</summary>
    public static ResolveOutcome Ambiguous { get; } = new(null, IsAmbiguous: true);
}
