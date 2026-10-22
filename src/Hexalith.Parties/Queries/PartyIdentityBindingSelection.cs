using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Queries;

/// <summary>Selects recorded intervals without changing their source order or exclusive end boundary.</summary>
internal static class PartyIdentityBindingSelection
{
    /// <summary>Scans inside the query work lease and cooperatively stops abandoned selection.</summary>
    internal static T[] SelectAt<T>(IEnumerable<T> source, Func<T, HumanActorBindingEvidence> evidence,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        List<T> matches = [];
        foreach (T item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HumanActorBindingEvidence binding = evidence(item);
            if (binding.ValidFrom <= at && (binding.ValidUntil is null || at < binding.ValidUntil))
            {
                matches.Add(item);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return [.. matches];
    }
}
