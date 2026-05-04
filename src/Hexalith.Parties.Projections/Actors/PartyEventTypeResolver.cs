using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;

namespace Hexalith.Parties.Projections.Actors;

/// <summary>
/// Resolves event type names to <see cref="IEventPayload"/> types from the Parties event
/// assembly. Builds a static lookup the first time it is queried so that the projection actors
/// don't pay <c>assembly.GetTypes()</c> on every event delivery (which compounded with the
/// replay-from-zero pattern in PartyProjectionUpdateOrchestrator).
/// <para>
/// Resolution order: full-name match first, then short-name match. Short-name collisions across
/// namespaces are NOT silently resolved — when more than one type shares a short name, the
/// resolver returns null so the caller can log and skip rather than dispatch to a guessed type.
/// </para>
/// </summary>
internal static class PartyEventTypeResolver
{
    private static readonly Lazy<FrozenDictionary<string, Type>> s_byFullName = new(BuildFullNameLookup);
    private static readonly Lazy<FrozenDictionary<string, Type?>> s_byShortName = new(BuildShortNameLookup);
    private static readonly ConcurrentDictionary<string, ResolveOutcome> s_resolvedCache = new(StringComparer.Ordinal);

    public static Type? Resolve(string eventTypeName)
    {
        ResolveOutcome outcome = ResolveInternal(eventTypeName);
        return outcome.Type;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="eventTypeName"/> resolves to a short
    /// name that has more than one matching type in the Parties event assembly. Callers should
    /// log this case distinctly from "unknown event type" because a colliding event was emitted
    /// by the aggregate but cannot be dispatched safely.
    /// </summary>
    /// <param name="eventTypeName">The event type name (full or short) to test.</param>
    /// <returns><see langword="true"/> if the short name maps to multiple types.</returns>
    public static bool IsAmbiguousShortName(string eventTypeName)
    {
        ResolveOutcome outcome = ResolveInternal(eventTypeName);
        return outcome.IsAmbiguous;
    }

    private static ResolveOutcome ResolveInternal(string eventTypeName)
    {
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            return ResolveOutcome.Unknown;
        }

        return s_resolvedCache.GetOrAdd(eventTypeName, name =>
        {
            // Prefer assembly-qualified or full-namespaced names — exact match.
            if (s_byFullName.Value.TryGetValue(name, out Type? full))
            {
                return new ResolveOutcome(full, IsAmbiguous: false);
            }

            // Fall back to the short name lookup. Type.GetType is intentionally NOT consulted
            // here: it accepts assembly-qualified names like "System.Diagnostics.Process,
            // System.Diagnostics.Process" and triggers arbitrary assembly load on miss, which
            // an attacker controlling event-type-name strings could weaponize. The contract
            // assembly is the only authoritative source for valid event types.
            string shortName = name.Contains('.', StringComparison.Ordinal)
                ? name[(name.LastIndexOf('.') + 1)..]
                : name;

            if (!s_byShortName.Value.TryGetValue(shortName, out Type? shortMatch))
            {
                return ResolveOutcome.Unknown;
            }

            // s_byShortName stores null for ambiguous short names. Distinguish "ambiguous"
            // from "unknown" so callers (and IsAmbiguousShortName) can route to distinct
            // diagnostics without re-walking the lookup.
            return shortMatch is null
                ? ResolveOutcome.Ambiguous
                : new ResolveOutcome(shortMatch, IsAmbiguous: false);
        });
    }

    private readonly record struct ResolveOutcome(Type? Type, bool IsAmbiguous)
    {
        public static ResolveOutcome Unknown { get; } = new(null, IsAmbiguous: false);

        public static ResolveOutcome Ambiguous { get; } = new(null, IsAmbiguous: true);
    }

    private static FrozenDictionary<string, Type> BuildFullNameLookup()
    {
        Dictionary<string, Type> lookup = new(StringComparer.Ordinal);
        foreach (Type t in EnumerateEventTypes())
        {
            // FullName may be null for generic parameter types — those aren't events.
            if (t.FullName is { } fullName)
            {
                lookup[fullName] = t;
            }
        }

        return lookup.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, Type?> BuildShortNameLookup()
    {
        Dictionary<string, List<Type>> grouped = new(StringComparer.Ordinal);
        foreach (Type t in EnumerateEventTypes())
        {
            if (!grouped.TryGetValue(t.Name, out List<Type>? bucket))
            {
                bucket = [];
                grouped[t.Name] = bucket;
            }

            bucket.Add(t);
        }

        // Map short name to the unique type, or null when there's a collision so the caller
        // can decline to guess (preventing one tenant's renamed event from dispatching to
        // another tenant's same-short-named event).
        Dictionary<string, Type?> resolved = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<Type>> kvp in grouped)
        {
            resolved[kvp.Key] = kvp.Value.Count == 1 ? kvp.Value[0] : null;
        }

        return resolved.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IEnumerable<Type> EnumerateEventTypes()
    {
        Assembly partiesContracts = typeof(PartyCreated).Assembly;
        Type[] types;
        try
        {
            types = partiesContracts.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types failed to load (likely a missing reference). Fall back to the loaded ones.
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (Type t in types)
        {
            if (IsEventPayloadType(t))
            {
                yield return t;
            }
        }
    }

    private static bool IsEventPayloadType(Type t)
        => !t.IsAbstract
            && !t.IsInterface
            && typeof(IEventPayload).IsAssignableFrom(t);
}
