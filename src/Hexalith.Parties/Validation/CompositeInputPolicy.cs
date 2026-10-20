using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Validation;

/// <summary>Bounds every supplied string occurrence without changing caller values.</summary>
internal static class CompositeInputPolicy
{
    /// <summary>The maximum raw UTF-16 length of a single string.</summary>
    internal const int MaximumStringLength = 512;

    /// <summary>The maximum combined raw UTF-16 length of all supplied strings.</summary>
    internal const int MaximumTotalLength = 4096;

    /// <summary>Checks per-string bounds for a composite create.</summary>
    internal static bool HasBoundedFields(CreatePartyComposite command) => Strings(command).All(IsBounded);

    /// <summary>Checks per-string bounds for a composite update.</summary>
    internal static bool HasBoundedFields(UpdatePartyComposite command) => Strings(command).All(IsBounded);

    /// <summary>Checks the total string budget for a composite create.</summary>
    internal static bool HasBoundedTotal(CreatePartyComposite command) => HasBoundedTotal(Strings(command));

    /// <summary>Checks the total string budget for a composite update.</summary>
    internal static bool HasBoundedTotal(UpdatePartyComposite command) => HasBoundedTotal(Strings(command));

    private static bool IsBounded(string? value) => value is null || value.Length <= MaximumStringLength;

    private static bool HasBoundedTotal(IEnumerable<string?> values)
    {
        long total = 0;
        foreach (string? value in values)
        {
            total += value?.Length ?? 0;
            if (total > MaximumTotalLength)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string?> Strings(CreatePartyComposite command)
        => new[] { command.PartyId }
            .Concat(Details(command.PersonDetails, command.OrganizationDetails))
            .Concat((command.ContactChannels ?? []).Where(c => c is not null).SelectMany(c => new[] { c.PartyId, c.ContactChannelId, c.Value }))
            .Concat((command.Identifiers ?? []).Where(i => i is not null).SelectMany(i => new[] { i.PartyId, i.IdentifierId, i.Value }));

    private static IEnumerable<string?> Strings(UpdatePartyComposite command)
        => new[] { command.PartyId }
            .Concat(Details(command.PersonDetails, command.OrganizationDetails))
            .Concat((command.AddContactChannels ?? []).Where(c => c is not null).SelectMany(c => new[] { c.PartyId, c.ContactChannelId, c.Value }))
            .Concat((command.UpdateContactChannels ?? []).Where(c => c is not null).SelectMany(c => new[] { c.PartyId, c.ContactChannelId, c.Value }))
            .Concat(command.RemoveContactChannelIds ?? [])
            .Concat((command.AddIdentifiers ?? []).Where(i => i is not null).SelectMany(i => new[] { i.PartyId, i.IdentifierId, i.Value }))
            .Concat(command.RemoveIdentifierIds ?? []);

    private static IEnumerable<string?> Details(PersonDetails? person, OrganizationDetails? organization)
        => [person?.FirstName, person?.LastName, person?.Prefix, person?.Suffix,
            organization?.LegalName, organization?.TradingName, organization?.LegalForm, organization?.RegistrationNumber];
}
