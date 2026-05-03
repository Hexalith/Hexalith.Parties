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
    private static readonly ConcurrentDictionary<string, Type?> s_resolvedCache = new(StringComparer.Ordinal);

    public static Type? Resolve(string eventTypeName)
    {
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            return null;
        }

        return s_resolvedCache.GetOrAdd(eventTypeName, name =>
        {
            // Prefer assembly-qualified or full-namespaced names — exact match.
            if (s_byFullName.Value.TryGetValue(name, out Type? full))
            {
                return full;
            }

            // CLR Type.GetType handles assembly-qualified names like
            // "Hexalith.Parties.Contracts.Events.PartyCreated, Hexalith.Parties.Contracts".
            Type? clrResolved = Type.GetType(name, throwOnError: false);
            if (clrResolved is not null && IsEventPayloadType(clrResolved))
            {
                return clrResolved;
            }

            // Fall back to the short name lookup. The lookup stores null for ambiguous short
            // names so we can return null instead of guessing.
            string shortName = name.Contains('.', StringComparison.Ordinal)
                ? name[(name.LastIndexOf('.') + 1)..]
                : name;

            return s_byShortName.Value.TryGetValue(shortName, out Type? shortMatch) ? shortMatch : null;
        });
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
