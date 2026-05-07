using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Server.Aggregates;

namespace Hexalith.Parties.Validation;

public sealed class UpdatePartyCompositeValidator : AbstractValidator<UpdatePartyComposite>
{
    public UpdatePartyCompositeValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x)
            .Must(x => CountSubOperations(x) <= PartyAggregate.GetEffectiveMaxSubOperations())
            .WithMessage(x => $"Total sub-operations ({CountSubOperations(x)}) exceeds maximum ({PartyAggregate.GetEffectiveMaxSubOperations()}).");

        RuleForEach(x => x.AddContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("PartyId must be a valid GUID.");

            channel.RuleFor(c => c.ContactChannelId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("ContactChannelId must be a valid GUID.");

            channel.RuleFor(c => c.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid ContactChannelType.");

            channel.RuleFor(c => c.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.UpdateContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("PartyId must be a valid GUID.");

            channel.RuleFor(c => c.ContactChannelId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("ContactChannelId must be a valid GUID.");
        });

        RuleForEach(x => x.RemoveContactChannelIds)
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("RemoveContactChannelId must be a valid GUID.");

        RuleForEach(x => x.AddIdentifiers).ChildRules(identifier =>
        {
            identifier.RuleFor(i => i.PartyId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("PartyId must be a valid GUID.");

            identifier.RuleFor(i => i.IdentifierId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("IdentifierId must be a valid GUID.");

            identifier.RuleFor(i => i.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid IdentifierType.");

            identifier.RuleFor(i => i.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.RemoveIdentifierIds)
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("RemoveIdentifierId must be a valid GUID.");
    }

    private static int CountSubOperations(UpdatePartyComposite command)
        => (command.PersonDetails is not null ? 1 : 0)
            + (command.OrganizationDetails is not null ? 1 : 0)
            + command.AddContactChannels.Count
            + command.UpdateContactChannels.Count
            + command.RemoveContactChannelIds.Count
            + command.AddIdentifiers.Count
            + command.RemoveIdentifierIds.Count;
}
