using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Client.Abstractions;

public interface IPartiesCommandClient
{
    Task<string> CreatePartyAsync(CreateParty command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> CreatePartyWithResultAsync(CreateParty command, CancellationToken ct);

    Task<string> UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> UpdatePersonDetailsWithResultAsync(string partyId, UpdatePersonDetails command, CancellationToken ct);

    Task<string> UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> UpdateOrganizationDetailsWithResultAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct);

    Task<string> AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> AddContactChannelWithResultAsync(string partyId, AddContactChannel command, CancellationToken ct);

    Task<string> UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> UpdateContactChannelWithResultAsync(string partyId, UpdateContactChannel command, CancellationToken ct);

    Task<string> RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> RemoveContactChannelWithResultAsync(string partyId, RemoveContactChannel command, CancellationToken ct);

    Task<string> AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> AddIdentifierWithResultAsync(string partyId, AddIdentifier command, CancellationToken ct);

    Task<string> RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> RemoveIdentifierWithResultAsync(string partyId, RemoveIdentifier command, CancellationToken ct);

    Task<string> DeactivatePartyAsync(string partyId, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> DeactivatePartyWithResultAsync(string partyId, CancellationToken ct);

    Task<string> ReactivatePartyAsync(string partyId, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> ReactivatePartyWithResultAsync(string partyId, CancellationToken ct);

    Task<string> CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> CreatePartyCompositeWithResultAsync(CreatePartyComposite command, CancellationToken ct);

    Task<string> UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> UpdatePartyCompositeWithResultAsync(string partyId, UpdatePartyComposite command, CancellationToken ct);

    Task<string> SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct);

    Task<PartiesCommandResult<PartyDetail>> SetIsNaturalPersonWithResultAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct);
}
